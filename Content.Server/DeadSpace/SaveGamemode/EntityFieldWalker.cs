using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Markdown.Mapping;

namespace Content.Server.DeadSpace.SaveGamemode;

/// <summary>
///     Сопоставляет YAML-имя компонента (ключ <c>type:</c> в дампе) с его C#-типом.
///     Нужен пост-процессору, чтобы отличать настоящие ссылки на сущности от похожих на них чисел
///     (урон в <c>DamageSpecifier</c>, id декалей, индексы плиток): угадывание по значению числа
///     сносило обычные данные.
/// </summary>
public interface IComponentFieldProbe
{
    Type? GetComponentType(string componentName);

    /// <summary>
    ///     Даёт ли прототип сущности этот компонент. Если да, при загрузке движок подмешивает к узлу
    ///     его данные, и обязательные поля восстановятся сами; если нет — узел читается целиком,
    ///     и отсутствующего обязательного поля быть не может.
    /// </summary>
    bool PrototypeProvidesComponent(string? protoId, string componentName);

    /// <summary>
    ///     YAML-маппинг компонента в прототипе (эффективный, с учётом наследования) или <c>null</c>.
    /// </summary>
    /// <remarks>
    ///     Самого присутствия в прототипе мало: читатель сливает узел файла с маппингом прототипа,
    ///     и <c>[DataField(required: true)]</c> поле должно найтись в объединении обоих наборов ключей.
    ///     Слияние же происходит только для того прототипа, по которому сущность реально создаётся,
    ///     а имя прототипа движок по дороге подменяет по <c>Resources/migration.yml</c>. Поэтому маппинг
    ///     отдаётся наружу — чинящий код дописывает недостающие обязательные поля прямо в узел файла.
    /// </remarks>
    MappingDataNode? PrototypeComponentMapping(string? protoId, string componentName);
}

/// <summary>
///     Реализация проба поверх <see cref="IComponentFactory"/>. Результати кэшируются:
///     прогон по сохранению касается десятков тысяч компонентов.
/// </summary>
public sealed class ComponentFieldProbe : IComponentFieldProbe
{
    private readonly IComponentFactory _factory;
    private readonly IPrototypeManager? _prototypes;
    private readonly Dictionary<string, Type?> _cache = new(StringComparer.Ordinal);
    private readonly Dictionary<(string? Proto, string Comp), bool> _protoCache = new();
    private readonly Dictionary<(string? Proto, string Comp), MappingDataNode?> _protoFieldsCache = new();

    public ComponentFieldProbe(IComponentFactory factory, IPrototypeManager? prototypes = null)
    {
        _factory = factory;
        _prototypes = prototypes;
    }

    public Type? GetComponentType(string componentName)
    {
        if (_cache.TryGetValue(componentName, out var cached))
            return cached;

        Type? type = null;
        if (_factory.TryGetRegistration(componentName, out var registration, true))
            type = registration.Type;

        _cache[componentName] = type;
        return type;
    }

    public bool PrototypeProvidesComponent(string? protoId, string componentName)
    {
        // Сущность без прототипа (группа с пустым `proto`) — все её компоненты читаются из файла целиком.
        if (_prototypes == null || string.IsNullOrEmpty(protoId))
            return false;

        var key = (protoId, componentName);
        if (_protoCache.TryGetValue(key, out var cached))
            return cached;

        var provides = Proto(protoId) is { } proto && proto.Components.ContainsKey(RegisteredName(componentName));

        _protoCache[key] = provides;
        return provides;
    }

    public MappingDataNode? PrototypeComponentMapping(string? protoId, string componentName)
    {
        if (_prototypes == null || string.IsNullOrEmpty(protoId))
            return null;

        var key = (protoId, componentName);
        if (_protoFieldsCache.TryGetValue(key, out var cached))
            return cached;

        MappingDataNode? mapping = null;

        if (Proto(protoId) is { } proto
            && proto.Components.TryGetValue(RegisteredName(componentName), out var entry))
        {
            // Маппинг после разбора наследования: именно его движок подмешивает к узлу файла.
            // Наружу отдаём как есть, но менять его нельзя — он принадлежит прототипу.
            mapping = entry.Mapping;
        }

        _protoFieldsCache[key] = mapping;
        return mapping;
    }

    private EntityPrototype? Proto(string protoId) =>
        _prototypes!.TryIndex<EntityPrototype>(protoId, out var proto) ? proto : null;

    /// <summary>Имя компонента в YAML может быть алиасом — нормализуем к имени регистрации.</summary>
    private string RegisteredName(string componentName) =>
        _factory.TryGetRegistration(componentName, out var registration, true) ? registration.Name : componentName;
}

/// <summary>
///     Тип поля компонента в том виде, как его видит пост-процессор: либо известный тип,
///     либо «неизвестно» (тогда в поле ничего не меняется).
/// </summary>
public readonly struct FieldKind
{
    public static readonly FieldKind Opaque = default;

    public readonly Type? Type;

    /// <summary>Поле помечено <c>[DataField(required: true)]</c>: удалять его нельзя.</summary>
    public readonly bool Required;

    public FieldKind(Type? type, bool required = false)
    {
        Type = type;
        Required = required;
    }

    public bool Known => Type != null;
}

/// <summary>
///     Обход C#-типов компонента по YAML-дереву. Статический и без IoC — чтобы остались юнит-тесты.
///     Правила сознательно консервативные: лучше оставить в файле висячую ссылку (движок на ней только
///     пишет ошибку), чем испортить валидные данные, поэтому неизвестный тип поля — «не трогать».
/// </summary>
public static class EntityFieldWalker
{
    private const BindingFlags MemberFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static readonly Dictionary<(Type Owner, string Name), FieldKind> FieldCache = new();
    private static readonly Dictionary<Type, IReadOnlyList<string>> RequiredCache = new();

    /// <summary>
    ///     Поля, которые пишет ручной сериализатор: имя в YAML есть, а в C#-типе узла поля нет,
    ///     и рефлексия их пропускала. <c>ContainerSerializer</c> пишет содержимое контейнера ключами
    ///     <c>ents</c>/<c>ent</c>, объявленными в <c>Container</c>/<c>ContainerSlot&lt;T&gt;</c>,
    ///     тогда как тип значения — базовый <see cref="BaseContainer"/>.
    /// </summary>
    private static readonly Dictionary<(Type Owner, string Name), FieldKind> HandwrittenSerializerFields = new()
    {
        [(typeof(BaseContainer), "ents")] = new FieldKind(typeof(List<EntityUid>)),
        [(typeof(BaseContainer), "ent")] = new FieldKind(typeof(EntityUid?)),
    };

    /// <summary>
    ///     Ссылается ли значение этого типа на сущность (то есть записано ли оно в YAML как yaml uid).
    /// </summary>
    /// <remarks>
    ///     <see cref="Robust.Shared.GameObjects.NetEntity"/> сюда намеренно не входит: это упакованный
    ///     сетевой id, а не yaml uid, и числа из него совпадают с uid лишь случайно.
    /// </remarks>
    public static bool IsEntityReference(Type? type)
    {
        type = Unwrap(type);
        if (type == null)
            return false;

        if (type == typeof(EntityUid))
            return true;

        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Entity<>);
    }

    /// <summary>
    ///     Можно ли вместо ссылки записать <c>null</c>, то есть допускает ли поле отсутствие сущности.
    /// </summary>
    /// <remarks>
    ///     Для полей вида <c>EntityUid</c> (не nullable) это не так: там «нет сущности» выражено
    ///     значением <c>EntityUid.Invalid</c>, и читатель возвращает её наружу как несуществующую сущность.
    /// </remarks>
    public static bool AcceptsNull(Type? type)
    {
        if (type == null)
            return false;

        if (Nullable.GetUnderlyingType(type) != null)
            return true;

        // Entity<T> с reference-типом компонента: null читается как «сущности нет».
        return type.IsGenericType
               && type.GetGenericTypeDefinition() == typeof(Entity<>)
               && !type.GetGenericArguments()[0].IsValueType;
    }

    /// <summary>
    ///     Тип поля компонента по его YAML-имени. <see cref="FieldKind.Opaque"/>, если поле
    ///     не найдено, сериализуется ручным сериализатором или его тип нам неизвестен.
    /// </summary>
    public static FieldKind LookupField(Type? owner, string yamlName)
    {
        if (owner == null)
            return FieldKind.Opaque;

        var key = (owner, yamlName);
        if (FieldCache.TryGetValue(key, out var cached))
            return cached;

        // Ручные сериализаторы: имя поля в YAML есть, в C#-типе узла его нет.
        if (!HandwrittenSerializerFields.TryGetValue(key, out var resolved))
            resolved = ResolveField(owner, yamlName);

        FieldCache[key] = resolved;
        return resolved;
    }

    /// <summary>
    ///     YAML-имена полей типа, помеченных <c>[DataField(required: true)]</c>, включая развёрнутые
    ///     через <c>[IncludeDataField]</c>. Без такого поля читатель отказывается читать узел компонента,
    ///     а писатель опускает поля, совпадающие с прототипом или значением по умолчанию.
    /// </summary>
    public static IReadOnlyList<string> RequiredFields(Type? owner)
    {
        if (owner == null)
            return Array.Empty<string>();

        if (RequiredCache.TryGetValue(owner, out var cached))
            return cached;

        var names = new List<string>();
        CollectRequiredFields(owner, names, new HashSet<Type>());
        RequiredCache[owner] = names;
        return names;
    }

    private static void CollectRequiredFields(Type owner, List<string> names, HashSet<Type> visited)
    {
        if (!visited.Add(owner))
            return;

        for (var type = owner; type != null && type != typeof(object); type = type.BaseType)
        {
            foreach (var member in Members(type))
            {
                if (member.GetCustomAttribute<DataFieldBaseAttribute>() is not { } attribute)
                    continue;

                if (attribute is DataFieldAttribute { Required: true })
                    names.Add(YamlName(attribute, member.Name));
                else if (attribute is IncludeDataFieldAttribute)
                {
                    // Поля вложенного типа лежат в том же YAML-маппинге, и required-ность у них своя.
                    var inner = Unwrap(MemberType(member));
                    if (inner != null)
                        CollectRequiredFields(inner, names, new HashSet<Type>(visited));
                }
            }
        }
    }

    /// <summary>Тип элемента последовательности (null, если тип последовательностью не является).</summary>
    public static Type? ElementType(Type? type)    {
        type = Unwrap(type);
        if (type == null || type == typeof(string))
            return null;

        if (type.IsArray)
            return type.GetElementType();

        foreach (var @interface in GetSelfAndInterfaces(type))
        {
            if (!@interface.IsGenericType)
                continue;

            if (@interface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return @interface.GetGenericArguments()[0];
        }

        return null;
    }

    /// <summary>
    ///     Аргументы типа словаря: (тип ключа, тип значения) либо (null, null) — тип не словарь.
    /// </summary>
    public static (Type? Key, Type? Value) DictionaryArguments(Type? type)
    {
        type = Unwrap(type);
        if (type == null)
            return (null, null);

        foreach (var @interface in GetSelfAndInterfaces(type))
        {
            if (!@interface.IsGenericType)
                continue;

            var definition = @interface.GetGenericTypeDefinition();
            if (definition != typeof(IDictionary<,>) && definition != typeof(IReadOnlyDictionary<,>))
                continue;

            var args = @interface.GetGenericArguments();
            return (args[0], args[1]);
        }

        return (null, null);
    }

    private static Type? Unwrap(Type? type)
    {
        if (type == null)
            return null;

        return Nullable.GetUnderlyingType(type) ?? type;
    }

    private static IEnumerable<Type> GetSelfAndInterfaces(Type type)
    {
        yield return type;
        foreach (var @interface in type.GetInterfaces())
            yield return @interface;
    }

    private static FieldKind ResolveField(Type owner, string yamlName) =>
        ResolveField(owner, yamlName, new HashSet<Type>());

    private static FieldKind ResolveField(Type owner, string yamlName, HashSet<Type> visited)
    {
        if (!visited.Add(owner))
            return FieldKind.Opaque;

        FieldKind loose = FieldKind.Opaque;
        FieldKind included = FieldKind.Opaque;

        for (var type = owner; type != null && type != typeof(object); type = type.BaseType)
        {
            foreach (var member in Members(type))
            {
                if (member.GetCustomAttribute<DataFieldBaseAttribute>() is not { } attribute)
                    continue;

                var name = YamlName(attribute, member.Name);
                var exact = string.Equals(name, yamlName, StringComparison.Ordinal);
                if (!exact && !string.Equals(name, yamlName, StringComparison.OrdinalIgnoreCase))
                {
                    // [IncludeDataField] разворачивает поля вложенного типа в те же ключи YAML,
                    // поэтому имя поля надо искать и внутри него.
                    if (attribute is IncludeDataFieldAttribute && !included.Known)
                    {
                        var inner = Unwrap(MemberType(member));
                        if (inner != null)
                            included = ResolveField(inner, yamlName, visited);
                    }

                    continue;
                }

                var kind = ToKind(member, attribute);
                if (exact)
                    return kind;

                if (!loose.Known)
                    loose = kind;
            }
        }

        return loose.Known ? loose : included;
    }

    private static IEnumerable<MemberInfo> Members(Type type)
    {
        foreach (var field in type.GetFields(MemberFlags))
            yield return field;

        foreach (var property in type.GetProperties(MemberFlags))
            yield return property;
    }

    private static Type? MemberType(MemberInfo member) => member switch
    {
        FieldInfo field => field.FieldType,
        PropertyInfo property => property.PropertyType,
        _ => null,
    };

    private static FieldKind ToKind(MemberInfo member, DataFieldBaseAttribute attribute)
    {
        // Ручной сериализатор пишет структуру, которую мы не можем надёжно разобрать:
        // формат узла его автору известен лучше, чем нам.
        if (attribute.CustomTypeSerializer != null)
            return FieldKind.Opaque;

        var type = Unwrap(MemberType(member));
        if (type == null)
            return FieldKind.Opaque;

        // Тип целиком сериализуется своим сериализатором (DamageSpecifier и подобные).
        if (type.GetCustomAttribute<TypeSerializerAttribute>() != null)
            return FieldKind.Opaque;

        return new FieldKind(type, attribute is DataFieldAttribute { Required: true });
    }

    /// <summary>
    ///     Имя поля в YAML: явный тег <c>[DataField("tag")]</c>, иначе имя члена с малой первой буквой.
    /// </summary>
    private static string YamlName(DataFieldBaseAttribute attribute, string memberName)
    {
        // Тег есть только у [DataField("tag")]; [IncludeDataField] тега не имеет.
        if (attribute is DataFieldAttribute { Tag: { Length: > 0 } tag })
            return tag;

        if (memberName.Length == 0)
            return memberName;

        return char.ToLowerInvariant(memberName[0]) + memberName.Substring(1);
    }
}
