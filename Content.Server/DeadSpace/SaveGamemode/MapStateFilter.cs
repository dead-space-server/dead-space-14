using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server.DeadSpace.SaveGamemode;

/// <summary>
///     Итог одного прогона пост-процессора по дереву карты.
/// </summary>
public sealed class MapFilterReport
{
    /// <summary>Сколько сущностей удалено целиком.</summary>
    public int RemovedEntities;

    /// <summary>Сколько компонентов снято с сохранённых сущностей.</summary>
    public int StrippedComponents;

    /// <summary>Сколько полей компонентов удалено как runtime-мусор.</summary>
    public int DroppedFields;

    /// <summary>Сколько ссылок на удалённые сущности вычищено.</summary>
    public int CleanedReferences;

    /// <summary>Сколько записей удалено из списков <c>maps</c>/<c>grids</c>/<c>orphans</c>/<c>nullspace</c>.</summary>
    public int CleanedRootLists;

    /// <summary>
    ///     Сколько сущностей получили флаг <c>mapInit</c>: без него движок прогоняет MapInit заново
    ///     и набивает контейнеры вторым комплектом из прототипа.
    /// </summary>
    public int MarkedInitFlags;

    /// <summary>
    ///     Сколько флагов инициализации снято с самой карты.
    /// </summary>
    /// <remarks>
    ///     <c>GameTicker.StartRound</c> инициализирует <c>DefaultMap</c> сам, а
    ///     <c>SharedMapSystem.InitializeMap</c> на уже инициализированной карте бросает исключение.
    /// </remarks>
    public int ClearedMapInitFlags;

    /// <summary>
    ///     Сколько различных компонентов в типизированном режиме не нашлось в регистрации: их поля
    ///     чистка не трогает. Нетипично большое число означает, что проб полей не работает.
    /// </summary>
    public int UnresolvedComponents;

    /// <summary>Сколько отложенных состояний извлечено в <see cref="DeferredStates"/>.</summary>
    public int ExtractedDeferred;

    /// <summary>
    ///     Сколько компонентов убрано, потому что их обязательные поля нечем заполнить
    ///     (ни в узле, ни в прототипе). Без этого сохранение вообще не загрузилось бы.
    /// </summary>
    public int SanitizedComponents;

    /// <summary>
    ///     Сколько обязательных полей дописано прямо в узлы компонентов значением из прототипа.
    ///     Без этого узел читается только если движок подмешает данные прототипа при загрузке.
    /// </summary>
    public int RepairedRequiredFields;

    /// <summary>
    ///     Сколько компонентов снято потому, что их поле-ссылка, не допускающее null, ведёт в никуда
    ///     (например, <c>StationMember</c> сетки без станции). При загрузке такой компонент роняет карту.
    /// </summary>
    public int DroppedDeadLinkComponents;

    /// <summary>Сколько подробностей о санитизации сохраняем, чтобы не раздувать лог.</summary>
    public const int MaxDetails = 25;

    /// <summary>Первые <see cref="MaxDetails"/> случаев санитизации: компонент, uid, прототип, поля.</summary>
    public readonly List<string> SanitizedDetails = new();

    /// <summary>Первые <see cref="MaxDetails"/> случаев починки обязательных полей.</summary>
    public readonly List<string> RepairedDetails = new();

    /// <summary>
    ///     Сколько сущностей удалено потому, что их родителя в файле нет, а корневыми они не значатся.
    ///     Это содержимое вырезанных сущностей; без удаления загрузка карты падает на assert'е
    ///     <c>EntityDeserializer.GetRootNodes</c>.
    /// </summary>
    public int RemovedParentlessEntities;

    /// <summary>Первые <see cref="MaxDetails"/> сущностей, удалённых без родителя.</summary>
    public readonly List<string> ParentlessDetails = new();

    /// <summary>
    ///     Сколько сущностей оставлены, хотя на них есть компонент из
    ///     <see cref="MapFilterConfig.RemoveEntitiesWithComponents"/>: такой компонент стоит и на
    ///     механизмах. Турель управляется тем же <c>HTN</c>, что и моб, но телом не обладает.
    /// </summary>
    public int KeptMachines;

    /// <summary>
    ///     Удалённые сущности по прототипам. Без этой статистики непонятно, что именно исчезло из
    ///     раунда: общий счётчик удалений ни о чём не говорит.
    /// </summary>
    public readonly Dictionary<string, int> RemovedByPrototype = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Записывает удаление сущности с указанным прототипом.</summary>
    public void NoteRemoved(string? proto)
    {
        var key = proto ?? "<без прототипа>";
        RemovedByPrototype.TryGetValue(key, out var count);
        RemovedByPrototype[key] = count + 1;
    }

    /// <summary>
    ///     Топ удалённых прототипов для лога: <c>Proto×N, …</c>. Пустая строка, если удалять было нечего.
    /// </summary>
    public string RemovedTop(int count = 12)
    {
        if (RemovedByPrototype.Count == 0)
            return string.Empty;

        var ordered = RemovedByPrototype
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var top = ordered.Take(count).Select(pair => $"{pair.Key}×{pair.Value}");
        var rest = ordered.Count - count;
        return string.Join(", ", top) + (rest > 0 ? $" … ещё {rest}" : string.Empty);
    }

    /// <summary>Число сущностей в дереве после фильтрации.</summary>
    public int RemainingEntities;

    /// <summary>
    ///     True, если в дереве вообще не было секции <c>entities</c> и фильтрация не выполнялась.
    ///     Такое дерево сохранением карты быть не может, и запись он не пройдёт.
    /// </summary>
    public bool MissingEntitiesSection;

    /// <summary>Отложенные (whitelisted) состояния, извлечённые из дерева. Ключ — yaml uid.</summary>
    public readonly Dictionary<int, MappingDataNode> DeferredStates = new();

    public override string ToString()
    {
        var prefix = MissingEntitiesSection ? "нет секции 'entities', фильтрация не выполнялась; " : string.Empty;

        return prefix +
               $"удалено сущностей: {RemovedEntities} (механизмов оставлено: {KeptMachines}), снято компонентов: {StrippedComponents}, " +
               $"удалено полей: {DroppedFields}, ссылок вычищено: {CleanedReferences}, " +
               $"без обязательных полей снято: {SanitizedComponents}, " +
               $"починено обязательных полей: {RepairedRequiredFields}, " +
               $"снято из-за ссылки в никуда: {DroppedDeadLinkComponents}, " +
               $"удалено без родителя: {RemovedParentlessEntities}, " +
               $"неизвестных компонентов: {UnresolvedComponents}, " +
               $"списков почищено: {CleanedRootLists}, mapInit проставлен: {MarkedInitFlags}, у карты снят: {ClearedMapInitFlags}, " +
               $"осталось сущностей: {RemainingEntities}";
    }
}

/// <summary>
///     Пост-процессор дерева карты формата 7: выкидывает нежелательные сущности и поля,
///     вычищает висячие ссылки и приводит служебные списки в соответствие новому составу.
/// </summary>
/// <remarks>
///     Класс намеренно не зависит от IoC и работает только с <see cref="MappingDataNode"/>,
///     чтобы его можно было тестировать на маленьком YAML без запуска игры.
///
///     Порядок прогонов важен: сначала фильтрация сущностей, затем снятие компонентов
///     (нужен список выживших), затем удаление полей и чистка ссылок (по уже известному множеству удалённых uid).
/// </remarks>
public static class MapStateFilter
{
    /// <summary>
    ///     Ключи маппингов, в которых заведомо не бывает ссылок на сущности — ни значениями, ни ключами.
    /// </summary>
    private static readonly HashSet<string> NonReferenceFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "type",
        // TileMapSerializer: словарь yaml-id тайла -> имя тайла.
        "tiles",
        // AtmosPipeNet / AtmosMonitoring: идентификаторы труб и сети, не EntityUid.
        "net",
        "network",
        "pipe",
        // DecalGridChunkCollectionTypeSerializer: ключи в decals — uid декали, id в node — chunk id.
        // Всё это числа из того же диапазона, что и yaml uid сущностей, но сущностями не являются.
        "chunkCollection",
        "nodes",
        "decals",
        "id",
        "version",
    };

    /// <summary>
    ///     Поля маппинга сущности, которые никогда не являются состоянием компонента.
    /// </summary>
    private static readonly HashSet<string> EntityMetaFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "uid",
        "mapInit",
        "paused",
        "components",
        "missingComponents",
        "proto",
        "type",
    };

    /// <summary>
    ///     Ключ секции со списком сущностей внутри группы прототипа в формате 7.
    /// </summary>
    public const string EntitiesKey = "entities";

    /// <summary>
    ///     Прогоняет фильтр по дереву карты «на месте» (дерево мутируется).
    /// </summary>
    public static MapFilterReport Run(MappingDataNode data, MapFilterConfig config, IComponentFieldProbe? probe = null)
    {
        var report = new MapFilterReport();
        var removed = new HashSet<int>();

        if (!data.TryGet(EntitiesKey, out SequenceDataNode? entities))
        {
            // Лога здесь намеренно нет: класс не зависит от IoC, о ситуации сообщает вызывающему коду.
            report.MissingEntitiesSection = true;
            return report;
        }

        FilterEntities(data, entities, config, removed, report);
        CleanRootLists(data, removed, report);

        // Сироты — сразу после фильтрации и до чистки ссылок: ссылки на удалённых сирот должны
        // вычищаться проходом CleanReferences, а не доезжать до загрузчика.
        FixEntitiesWithoutParent(data, entities, config, removed, report);

        // Порядок проходов значим: сначала удаляем сущности, затем вычищаем blacklist-поля и ссылки
        // на удалённые сущности, и только в конце снимаем компоненты целиком (StripAllComponents).
        // Иначе компонент из StripAllComponents уходит из файла вместе со своим состоянием раньше,
        // чем DropFields/CleanReferences успеют его посмотреть.
        DropFields(data, entities, config, report);

        // Типизированный режим: мёртвой считается ссылка на любой uid, которого в итоговом файле
        // нет. Часть таких сущностей исчезает не по нашему фильтру, а молча пропускается
        // сериализатором (отсутствующий прототип и т.п.), поэтому одних «removed» мало.
        var alive = new HashSet<int>();
        ForEachEntity(entities, (_, ent) =>
        {
            if (TryGetYamlUid(ent, out var uid))
                alive.Add(uid);
        });

        if (probe != null)
        {
            CleanReferencesTyped(entities, alive, config, probe, report);
        }
        else
        {
            CleanReferences(entities, removed, config, report);
        }

        // После чистки ссылок: компоненты, которым нечем починить ссылку, снимаются целиком.
        DropComponentsWithoutLiveLink(entities, alive, config, report);

        StripComponents(data, entities, config, report);
        ExtractDeferred(entities, config, report);

        // Санитизация обязательных полей — после всех удалений: именно проходы выше могут
        // оставить компонент без поля, без которого читатель откажется его читать.
        if (probe != null && config.RepairRequiredComponents)
            RepairRequiredComponents(entities, probe, report);

        // Флаги mapInit выравниваем в самом конце: к этому моменту состав сущностей окончательный.
        if (config.NormalizeMapInitFlags)
            NormalizeInitFlags(data, entities, report);

        UpdateMetadata(data, report);

        return report;
    }

    /// <summary>Имя флага «сущность уже прошла MapInit» в YAML.</summary>
    private const string MapInitKey = "mapInit";

    /// <summary>Ключ секции карт — по ней определяем сущность самой карты.</summary>
    private const string MapsKey = "maps";

    /// <summary>Имя компонента карты в YAML.</summary>
    private const string MapComponentName = "Map";

    /// <summary>Поля <c>MapComponent</c>, говорящие читателю об уже инициализированной карте.</summary>
    private static readonly string[] MapInitComponentFields = { "MapInitialized", "MapPaused" };

    /// <summary>
    ///     Выравнивает флаг <c>mapInit</c>: ставит его всем сущностям и снимает у самой карты.
    /// </summary>
    /// <remarks>
    ///     Без флага читатель формата 7 сочтёт сущности предыннициализированными и прогонит MapInit
    ///     заново, а прототип вернёт компоненты наполнения, которых в файле нет: <c>ContainerFill</c> и
    ///     <c>StorageFill</c> сыплют вторым комплектом медикаменты и гильзы, <c>GridFill</c> докидывает
    ///     спаскапсулы, <c>RandomFillSolution</c> долирует реагенты.
    ///     Карта — единственное исключение: <c>GameTicker.StartRound</c> инициализирует её сам, поэтому
    ///     у карты снимаем и флаг, и поля компонента <c>Map</c>. Поддерево карты не трогаем:
    ///     <c>RecursiveMapInit</c> останавливается на уже инициализированной сущности.
    /// </remarks>
    private static void NormalizeInitFlags(MappingDataNode data, SequenceDataNode entities, MapFilterReport report)
    {
        var mapUids = ReadRootList(data, MapsKey);
        if (mapUids.Count == 0)
        {
            // Не зная uid карты, нельзя решить, у кого флаг обязан оставаться снятым, — загрузка тогда
            // падает на «Map is already initialized». Дерево оставляем как есть.
            return;
        }

        ForEachEntity(entities, (_, ent) =>
        {
            TryGetYamlUid(ent, out var uid);

            if (mapUids.Contains(uid))
            {
                if (TryFindFoldKey(ent, MapInitKey, out var mapFlag) && ent.Remove(mapFlag))
                    report.ClearedMapInitFlags++;

                ClearMapInitFields(ent, report);
                return;
            }

            if (TryFindFoldKey(ent, MapInitKey, out var existing) &&
                ent[existing] is ValueDataNode value &&
                string.Equals(value.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase))
                return;

            ent[MapInitKey] = new ValueDataNode("true");
            report.MarkedInitFlags++;
        });
    }

    /// <summary>
    ///     Снимает с компонента <c>Map</c> поля, сообщающие об уже пройденной инициализации карты.
    /// </summary>
    /// <remarks>
    ///     Это <c>MapComponent.MapInitialized</c> и <c>MapPaused</c>: попав в файл, они делают карту
    ///     «готовой» независимо от флага сущности, и <c>SharedMapSystem.InitializeMap</c> отказывается
    ///     её инициализировать.
    /// </remarks>
    private static void ClearMapInitFields(MappingDataNode ent, MapFilterReport report)
    {
        if (GetComponentsNode(ent) is not { } components)
            return;

        foreach (var item in components)
        {
            if (item is not MappingDataNode comp || !TryGetComponentName(comp, out var name))
                continue;

            if (!name.Equals(MapComponentName, StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var field in MapInitComponentFields)
            {
                if (TryFindFoldKey(comp, field, out var key) && comp.Remove(key))
                    report.ClearedMapInitFlags++;
            }
        }
    }

    /// <summary>
    ///     Чинит компоненты, которые читатель не смог бы прочитать: обязательные
    ///     <c>[DataField(required: true)]</c>-поля, отсутствующие в узле файла.
    /// </summary>
    /// <remarks>
    ///     Писатель вычитает данные прототипа, поэтому обязательное поле, равное прототипному, в файл
    ///     не попадает (например <c>damage:</c> у <c>MeleeWeapon</c>). Читатель добирает его из маппинга
    ///     прототипа, но только если компонент есть у фактического прототипа сущности, а имя прототипа
    ///     из файла движок по дороге подменяет по <c>Resources/migration.yml</c>. Книга <c>BookSpaceLaw</c>
    ///     переименована в <c>HyperLinkBookCorporateLaw</c>, где <c>MeleeWeapon</c> нет вовсе, — и
    ///     <c>RequiredFieldNotMappedException</c> роняет загрузку всей карты.
    ///     Поэтому недостающее поле дописываем в узел значением из маппинга (копией: узлы прототипов
    ///     менять нельзя), а брать негде — снимаем компонент: полусломанный компонент лучше мёртвого раунда.
    /// </remarks>
    private static void RepairRequiredComponents(
        SequenceDataNode entities,
        IComponentFieldProbe probe,
        MapFilterReport report)
    {
        ForEachEntity(entities, (proto, ent) =>
        {
            if (GetComponentsNode(ent) is not { } comps)
                return;

            var uid = TryGetYamlUid(ent, out var yamlUid) ? yamlUid : -1;

            for (var i = comps.Count - 1; i >= 0; i--)
            {
                if (comps[i] is not MappingDataNode comp)
                    continue;

                if (!TryGetComponentName(comp, out var name))
                    continue;

                if (probe.GetComponentType(name) is not { } compType)
                    continue;

                var required = EntityFieldWalker.RequiredFields(compType);
                if (required.Count == 0)
                    continue;

                // Маппинг компонента в прототипе — ровно то, что движок подмешал бы при слиянии.
                var protoMapping = probe.PrototypeComponentMapping(proto, name);

                List<string>? repaired = null;
                List<string>? hopeless = null;

                foreach (var field in required)
                {
                    // Читатель ищет обязательное поле строго по регистру (MappingDataNode.TryGet).
                    if (comp.ContainsKey(field))
                        continue;

                    // Ключ есть в другом регистре — для читателя его нет, поэтому чиним имя ключа.
                    if (TryFindFoldKey(comp, field, out var ownKey))
                    {
                        var value = comp[ownKey];
                        comp.Remove(ownKey);
                        comp[field] = value;
                        (repaired ??= new List<string>()).Add($"{field} (регистр ключа)");
                        continue;
                    }

                    if (protoMapping != null && TryFindFoldKey(protoMapping, field, out var protoKey))
                    {
                        comp[field] = protoMapping[protoKey].Copy();
                        (repaired ??= new List<string>()).Add(field);
                        continue;
                    }

                    (hopeless ??= new List<string>()).Add(field);
                }

                if (hopeless != null)
                {
                    comps.RemoveAt(i);
                    report.SanitizedComponents++;

                    if (report.SanitizedDetails.Count < MapFilterReport.MaxDetails)
                    {
                        var why = protoMapping != null
                            ? $"нет {string.Join(", ", hopeless)} (ни в узле, ни у прототипа)"
                            : $"нет {string.Join(", ", hopeless)}";

                        report.SanitizedDetails.Add($"{name} (uid {uid}, proto {proto ?? "нет"}): {why}");
                    }

                    continue;
                }

                if (repaired == null)
                    continue;

                report.RepairedRequiredFields += repaired.Count;

                if (report.RepairedDetails.Count < MapFilterReport.MaxDetails)
                    report.RepairedDetails.Add(
                        $"{name} (uid {uid}, proto {proto ?? "нет"}): дописано {string.Join(", ", repaired)}");
            }
        });
    }

    /// <summary>
    ///     Ключ узла, совпадающий с именем поля с точностью до регистра.
    /// </summary>
    /// <remarks>
    ///     Сам читатель к регистру не терпим (<c>MappingDataNode._children</c> — словарь без
    ///     компаратора), но найти поле с опечаткой в регистре лучше, чем его потерять.
    /// </remarks>
    private static bool TryFindFoldKey(MappingDataNode node, string field,
        [NotNullWhen(true)] out string? foundKey)
    {
        foreach (var key in node.Keys)
        {
            if (string.Equals(key, field, StringComparison.OrdinalIgnoreCase))
            {
                foundKey = key;
                return true;
            }
        }

        foundKey = null;
        return false;
    }

    #region Фильтрация сущностей
    private static void FilterEntities(
        MappingDataNode data,
        SequenceDataNode entities,
        MapFilterConfig config,
        HashSet<int> removed,
        MapFilterReport report)
    {
        for (var i = entities.Count - 1; i >= 0; i--)
        {
            if (entities[i] is not MappingDataNode group)
                continue;

            var proto = GetPrototype(group);
            var list = GetEntityList(group);
            if (list == null)
                continue;

            for (var j = list.Count - 1; j >= 0; j--)
            {
                if (list[j] is not MappingDataNode ent)
                    continue;

                if (!TryGetYamlUid(ent, out var uid))
                    continue;

                if (ShouldRemove(uid, proto, GetComponents(ent), config, report))
                {
                    list.RemoveAt(j);
                    removed.Add(uid);
                    report.RemovedEntities++;
                }
            }

            // Пустая группа прототипа десериализатором не запрещена, но в файле ей не место.
            if (list.Count == 0)
                entities.RemoveAt(i);
        }

        DropRemovedFromComponents(data, removed, report);
    }

    /// <summary>
    ///     Решает, удаляется ли сущность целиком.
    /// </summary>
    private static bool ShouldRemove(
        int uid,
        string? proto,
        HashSet<string>? components,
        MapFilterConfig config,
        MapFilterReport? report = null)
    {
        if (config.RemoveUids.Contains(uid))
        {
            report?.NoteRemoved(proto);
            return true;
        }

        var predicate = config.EntityPredicate;
        if (predicate != null)
        {
            var action = predicate(proto, components ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            switch (action)
            {
                case EntityFilterAction.Remove:
                    report?.NoteRemoved(proto);
                    return true;
                // StripComponents — не удаление сущности, решение принималось предикатом осознанно.
                case EntityFilterAction.StripComponents:
                    return false;
            }
        }

        if (proto != null)
        {
            if (config.RemovePrototypes.Contains(proto))
            {
                report?.NoteRemoved(proto);
                return true;
            }

            foreach (var mask in config.RemovePrototypeMasks)
            {
                if (MatchesMask(proto, mask))
                {
                    report?.NoteRemoved(proto);
                    return true;
                }
            }
        }

        if (components == null || components.Count == 0)
            return false;

        var creature = false;
        foreach (var marker in config.CreatureMarkers)
        {
            if (components.Contains(marker))
            {
                creature = true;
                break;
            }
        }

        foreach (var marker in config.RemoveEntitiesWithComponents)
        {
            if (!components.Contains(marker))
                continue;

            // «Живое» поведение стоит и на технике: HTN есть и у моба, и у турели (BaseWeaponTurret).
            // Без признака существа автономные механизмы вычищались из сохранения вместе с мобами.
            if (!creature && config.DriverComponents.Contains(marker))
            {
                if (report != null)
                    report.KeptMachines++;
                continue;
            }

            report?.NoteRemoved(proto);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Простейшая маска с <c>*</c> и <c>?</c>, сравнение без учёта регистра.
    /// </summary>
    public static bool MatchesMask(string value, string mask)
    {
        var v = 0;
        var m = 0;
        var star = -1;
        var mark = 0;

        while (v < value.Length)
        {
            if (m < mask.Length && (mask[m] == '?' || char.ToUpperInvariant(mask[m]) == char.ToUpperInvariant(value[v])))
            {
                v++;
                m++;
            }
            else if (m < mask.Length && mask[m] == '*')
            {
                star = m++;
                mark = v;
            }
            else if (star != -1)
            {
                m = star + 1;
                v = ++mark;
            }
            else
            {
                return false;
            }
        }

        while (m < mask.Length && mask[m] == '*')
        {
            m++;
        }

        return m == mask.Length;
    }

    #endregion

    #region Служебные списки и meta

    private static void CleanRootLists(MappingDataNode data, HashSet<int> removed, MapFilterReport report)
    {
        if (removed.Count == 0)
            return;

        foreach (var key in new[] { "maps", "grids", "orphans", "nullspace" })
        {
            if (!data.TryGet(key, out SequenceDataNode? list))
                continue;

            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] is not ValueDataNode value || !int.TryParse(value.Value, out var uid))
                    continue;

                if (!removed.Contains(uid))
                    continue;

                list.RemoveAt(i);
                report.CleanedRootLists++;
            }
        }
    }

    /// <summary>
    ///     Ключи списков корневых сущностей формата 7. Каждая сущность без родителя обязана стоять
    ///     в одном из них, иначе десериализатор считает дерево битым.
    /// </summary>
    private static readonly string[] RootListKeys = { "maps", "grids", "orphans", "nullspace" };

    /// <summary>Имя компонента, в котором лежит родитель сущности.</summary>
    private const string TransformComponentName = "Transform";

    /// <summary>
    ///     Сколько раз пересчитывать сирот: удаление одного сироты делает сиротой его детей.
    ///     Хватит с запасом — глубина вложенности контейнеров в карте единицы.
    /// </summary>
    private const int MaxOrphanPasses = 32;

    /// <summary>
    ///     Убирает сущности, у которых не осталось родителя, и приводит списки
    ///     <c>maps</c>/<c>grids</c>/<c>orphans</c>/<c>nullspace</c> в соответствие дереву.
    /// </summary>
    /// <remarks>
    ///     Проходы выше вырезают сущностей, и их дети остаются без родителя: <c>Transform.parent</c>
    ///     в никуда чистка ссылок убирает. Десериализатор требует, чтобы каждая сущность без родителя
    ///     числилась в <c>maps</c>, <c>grids</c>, <c>orphans</c> или <c>nullspace</c>, иначе
    ///     <c>EntityDeserializer.GetRootNodes</c> падает assert'ом и карта не грузится вовсе.
    ///     Осиротевшая сущность — содержимое вырезанного (слитки из toolstrip'а моба, сущности его
    ///     экшенов), поэтому удаляем её вместе с её детьми. Карты и сетки не удаляем: они законные
    ///     корни, их лишь дописываем в нужный список.
    /// </remarks>
    private static void FixEntitiesWithoutParent(
        MappingDataNode data,
        SequenceDataNode entities,
        MapFilterConfig config,
        HashSet<int> removed,
        MapFilterReport report)
    {
        for (var pass = 0; pass < MaxOrphanPasses; pass++)
        {
            var roots = ReadRootLists(data);
            var alive = new HashSet<int>();

            ForEachEntity(entities, (proto, ent) =>
            {
                if (TryReadEntityLink(ent, out var uid, out _))
                    alive.Add(uid);
            });

            var changed = false;

            for (var i = entities.Count - 1; i >= 0; i--)
            {
                if (entities[i] is not MappingDataNode group)
                    continue;

                var proto = GetPrototype(group);
                var list = GetEntityList(group);
                if (list == null)
                    continue;

                for (var j = list.Count - 1; j >= 0; j--)
                {
                    if (list[j] is not MappingDataNode ent)
                        continue;

                    if (!TryReadEntityLink(ent, out var uid, out var parent))
                        continue;

                    // Корневые списки нам не противоречат: сущность в них — осознанный корень.
                    if (roots.Contains(uid))
                        continue;

                    if (parent != 0 && alive.Contains(parent))
                        continue;

                    var names = GetComponents(ent);
                    var isMap = names != null && names.Contains("Map");
                    var isGrid = names != null && names.Contains("MapGrid");

                    if (isMap || isGrid || !config.RemoveEntitiesWithoutParent)
                    {
                        // Карта без родителя — обычное дело, сетка без родителя тоже грузится:
                        // движок сам заведёт для неё карту (AdoptGrids). Оба варианта обязаны
                        // стоять в соответствующих списках.
                        if (isMap)
                            EnsureInRootList(data, "maps", uid, report);
                        else
                            EnsureInRootList(data, "orphans", uid, report);

                        if (isGrid)
                            EnsureInRootList(data, "grids", uid, report);

                        continue;
                    }

                    list.RemoveAt(j);
                    removed.Add(uid);
                    alive.Remove(uid);
                    changed = true;
                    report.RemovedParentlessEntities++;
                    report.NoteRemoved(proto);

                    if (report.ParentlessDetails.Count < MapFilterReport.MaxDetails)
                        report.ParentlessDetails.Add($"{proto ?? "без прототипа"} (uid {uid})");
                }

                if (list.Count == 0)
                    entities.RemoveAt(i);
            }

            if (!changed)
                break;
        }

        PruneStaleRootEntries(data, entities, report);
    }

    /// <summary>
    ///     Удаляет из списков корневых сущностей uid, которых в дереве больше нет, и тех, кто
    ///     в <c>orphans</c>/<c>nullspace</c> числится ошибочно (родитель у него на месте).
    /// </summary>
    /// <remarks>
    ///     Иначе десериализатор пишет «Missing orphan entity» / «was incorrectly labelled as an
    ///     orphan?» и считает дерево подозрительным. Карты и сетки из <c>maps</c>/<c>grids</c>
    ///     не вычищаем: исчезновение карты — поломка сохранения, о которой должен сказать движок.
    /// </remarks>
    private static void PruneStaleRootEntries(MappingDataNode data, SequenceDataNode entities, MapFilterReport report)
    {
        var parents = new Dictionary<int, int>();
        var alive = new HashSet<int>();

        ForEachEntity(entities, (_, ent) =>
        {
            if (!TryReadEntityLink(ent, out var uid, out var parent))
                return;

            alive.Add(uid);

            if (parent != 0)
                parents[uid] = parent;
        });

        foreach (var key in new[] { "orphans", "nullspace" })
        {
            if (!data.TryGet(key, out SequenceDataNode? list))
                continue;

            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] is not ValueDataNode value || !int.TryParse(value.Value, out var uid))
                    continue;

                var stale = !alive.Contains(uid)
                            || (parents.TryGetValue(uid, out var parent) && alive.Contains(parent));

                if (!stale)
                    continue;

                list.RemoveAt(i);
                report.CleanedRootLists++;
            }
        }
    }

    /// <summary>
    ///     Читает uid и родителя сущности. Родителя нет — возвращает 0: поля <c>parent</c> нет,
    ///     оно не число или в нём <c>invalid</c>.
    /// </summary>
    private static bool TryReadEntityLink(MappingDataNode ent, out int uid, out int parent)
    {
        uid = 0;
        parent = 0;

        if (!TryGetYamlUid(ent, out uid))
            return false;

        if (GetComponentsNode(ent) is not { } components)
            return true;

        foreach (var item in components)
        {
            if (item is not MappingDataNode comp)
                continue;

            if (!TryGetComponentName(comp, out var name) ||
                !name.Equals(TransformComponentName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (TryFindFoldKey(comp, "parent", out var key) &&
                comp[key] is ValueDataNode value &&
                int.TryParse(value.Value, out var parsed))
                parent = parsed;

            break;
        }

        return true;
    }

    /// <summary>Все uid из списков <c>maps</c>/<c>grids</c>/<c>orphans</c>/<c>nullspace</c>.</summary>
    private static HashSet<int> ReadRootLists(MappingDataNode data)
    {
        var roots = new HashSet<int>();

        foreach (var key in RootListKeys)
            roots.UnionWith(ReadRootList(data, key));

        return roots;
    }

    /// <summary>Читает один список корневых сущностей (<c>maps</c>, <c>grids</c> и так далее).</summary>
    private static HashSet<int> ReadRootList(MappingDataNode data, string key)
    {
        var uids = new HashSet<int>();

        if (!data.TryGet(key, out SequenceDataNode? list))
            return uids;

        foreach (var item in list)
        {
            if (item is ValueDataNode value && int.TryParse(value.Value, out var uid))
                uids.Add(uid);
        }

        return uids;
    }

    /// <summary>Дописывает uid в список корневых сущностей, если его там ещё нет.</summary>
    private static void EnsureInRootList(MappingDataNode data, string key, int uid, MapFilterReport report)
    {
        if (!data.TryGet(key, out SequenceDataNode? list))
        {
            list = new SequenceDataNode();
            data[key] = list;
        }

        foreach (var item in list)
        {
            if (item is ValueDataNode value && int.TryParse(value.Value, out var existing) && existing == uid)
                return;
        }

        list.Add(new ValueDataNode(uid.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        report.CleanedRootLists++;
    }

    /// <summary>
    ///     Удаляет записи об удалённых сущностях из поля <c>components</c> группы прототипа (формат 3) и
    ///     приводит <c>meta.entityCount</c> в соответствие новому составу.
    /// </summary>
    private static void DropRemovedFromComponents(MappingDataNode data, HashSet<int> removed, MapFilterReport report)
    {
        if (removed.Count == 0)
            return;

        // Формат 3: components — плоская последовательность сущностей.
        if (!data.TryGet("components", out SequenceDataNode? components))
            return;

        for (var i = components.Count - 1; i >= 0; i--)
        {
            if (components[i] is not MappingDataNode ent || !TryGetYamlUid(ent, out var uid))
                continue;

            if (!removed.Contains(uid))
                continue;

            components.RemoveAt(i);
            report.RemovedEntities++;
        }
    }

    private static void UpdateMetadata(MappingDataNode data, MapFilterReport report)
    {
        if (!data.TryGet("meta", out MappingDataNode? meta))
            return;

        var count = CountEntities(data);
        report.RemainingEntities = count;

        // entityCount десериализатором не читается, но файл должен быть внутренне консистентным.
        if (meta.Has("entityCount"))
            meta["entityCount"] = new ValueDataNode(count.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static int CountEntities(MappingDataNode data)
    {
        if (!data.TryGet(EntitiesKey, out SequenceDataNode? entities))
            return 0;

        var count = 0;
        foreach (var node in entities)
        {
            if (node is MappingDataNode group && GetEntityList(group) is { } list)
                count += list.Count;
        }

        return count;
    }

    #endregion

    #region Компоненты и поля

    /// <summary>
    ///     Снимает компоненты, у которых поле-ссылка ведёт в никуда, а «пустого» значения поле не имеет.
    /// </summary>
    /// <remarks>
    ///     Отсутствующее поле такого типа — не «нет сущности», а <c>EntityUid.Invalid</c>: читатель
    ///     отдаёт его наружу как несуществующую сущность, и обработчик инициализации падает на её имени.
    ///     Пример: сетка с <c>StationMember</c> без <c>station</c> — станция к моменту сохранения уже
    ///     удалена, и на <c>ComponentInit</c> <c>PdaSystem.UpdateStationName</c> зовёт <c>Name(0)</c>,
    ///     что обрывает загрузку всей карты. Снимать безопасно: сетка остаётся ровно в том состоянии,
    ///     в каком попала в сохранение, — без станции. Починить ссылку нечем, ставит её только рантайм.
    /// </remarks>
    private static void DropComponentsWithoutLiveLink(
        SequenceDataNode entities,
        HashSet<int> alive,
        MapFilterConfig config,
        MapFilterReport report)
    {
        if (config.ComponentsNeedingLiveLink.Count == 0)
            return;

        ForEachEntity(entities, (_, ent) =>
        {
            var components = GetComponentsNode(ent);
            if (components == null)
                return;

            List<int>? toDrop = null;
            HashSet<string>? droppedNames = null;

            for (var i = 0; i < components.Count; i++)
            {
                if (components[i] is not MappingDataNode comp || !TryGetComponentName(comp, out var name))
                    continue;

                if (!config.ComponentsNeedingLiveLink.TryGetValue(name, out var field))
                    continue;

                if (HasLiveLink(comp, field, alive))
                    continue;

                (toDrop ??= new List<int>()).Add(i);
                (droppedNames ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase)).Add(name);
                report.DroppedDeadLinkComponents++;
            }

            if (toDrop == null)
                return;

            // Удаляем с конца, чтобы предыдущие индексы не съезжали.
            for (var i = toDrop.Count - 1; i >= 0; i--)
                components.RemoveAt(toDrop[i]);

            if (components.Count == 0)
                ent.Remove("components");

            // Как и в StripComponents: снятого компонента не должно быть в missingComponents,
            // иначе движок не восстановил бы его из прототипа (если тот его даёт).
            SyncMissingComponents(ent, components, droppedNames);
        });
    }

    /// <summary>
    ///     Есть ли в узле компонента ссылка на сущность, которая жива в итоговом файле.
    /// </summary>
    private static bool HasLiveLink(MappingDataNode comp, string field, HashSet<int> alive)
    {
        // Поля нет: читатель оставит в нём значение по умолчанию, а для EntityUid это Invalid.
        if (!TryFindFoldKey(comp, field, out var key))
            return false;

        return comp[key] is ValueDataNode value
               && int.TryParse(value.Value, out var uid)
               && uid != 0
               && alive.Contains(uid);
    }

    private static void StripComponents(
        MappingDataNode data,
        SequenceDataNode entities,
        MapFilterConfig config,
        MapFilterReport report)
    {
        if (config.StripAllComponents.Count == 0 && config.StripComponentsByPrototype.Count == 0)
            return;

        ForEachEntity(entities, (proto, ent) =>
        {
            var components = GetComponentsNode(ent);
            if (components == null)
                return;

            // Blacklist-поля вычищаются отдельным проходом (DropFields) до снятия компонентов.
            HashSet<string>? perProto = null;
            if (proto != null && config.StripComponentsByPrototype.Count > 0)
            {
                foreach (var (mask, names) in config.StripComponentsByPrototype)
                {
                    if (MatchesMask(proto, mask))
                        perProto ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var name in names)
                    {
                        perProto!.Add(name);
                    }
                }
            }

            // Снимать компоненты будем отдельным списком: менять children SequenceDataNode
            // во время его же обхода нельзя.
            List<int>? toStrip = null;
            HashSet<string>? strippedNames = null;

            for (var i = 0; i < components.Count; i++)
            {
                if (components[i] is not MappingDataNode comp || !TryGetComponentName(comp, out var name))
                    continue;

                // Снимаем только те компоненты, которые действительно есть в списке снятия.
                if (!config.StripAllComponents.Contains(name) && perProto?.Contains(name) != true)
                    continue;

                (toStrip ??= new List<int>()).Add(i);
                (strippedNames ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase)).Add(name);
            }

            if (toStrip == null)
                return;

            // Удаляем с конца, чтобы предыдущие индексы не съезжали.
            for (var i = toStrip.Count - 1; i >= 0; i--)
            {
                components.RemoveAt(toStrip[i]);
                report.StrippedComponents++;
            }

            if (components.Count == 0)
                ent.Remove("components");

            // Снятый компонент больше не «отсутствующий относительно прототипа»: иначе при загрузке
            // движок не добавил бы его обратно из прототипа, и «сброс к дефолту» не случился бы.
            // Синхронизируем список: в нём не должно быть ни снятых, ни присутствующих компонентов.
            SyncMissingComponents(ent, components, strippedNames);
        });
    }

    /// <summary>
    ///     Чистит <c>missingComponents</c>: из него убираются компоненты, которые в файле присутствуют,
    ///     и которые мы сами сняли (<paramref name="stripped"/>).
    /// </summary>
    /// <remarks>
    ///     Присутствующий компонент в списке отсутствующих — рассинхрон из редактора или отживший запись
    ///     раунда. Снятый нами компонент обязан вернуться из прототипа при загрузке, а запись в
    ///     <c>missingComponents</c> как раз запрещает движку его добавлять.
    /// </remarks>
    private static void SyncMissingComponents(
        MappingDataNode ent,
        SequenceDataNode? components,
        HashSet<string>? stripped)
    {
        if (!ent.TryGet("missingComponents", out SequenceDataNode? missing) || missing.Count == 0)
            return;

        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (components != null)
        {
            foreach (var node in components)
            {
                if (node is MappingDataNode comp && TryGetComponentName(comp, out var name))
                    present.Add(name);
            }
        }

        for (var i = missing.Count - 1; i >= 0; i--)
        {
            if (missing[i] is not ValueDataNode value)
                continue;

            if (present.Contains(value.Value) || stripped?.Contains(value.Value) == true)
                missing.RemoveAt(i);
        }

        if (missing.Count == 0)
            ent.Remove("missingComponents");
    }

    /// <summary>
    ///     Извлекает whitelisted-состояния (<see cref="MapFilterConfig.DeferredComponents"/>) из дерева
    ///     в <see cref="MapFilterReport.DeferredStates"/> и убирает их оттуда, чтобы состояние применилось
    ///     отдельным шагом после загрузки карты.
    /// </summary>
    private static void ExtractDeferred(SequenceDataNode entities, MapFilterConfig config, MapFilterReport report)
    {
        if (config.DeferredComponents.Count == 0)
            return;

        ForEachEntity(entities, (_, ent) =>
        {
            if (!TryGetYamlUid(ent, out var uid))
                return;

            var components = GetComponentsNode(ent);
            if (components == null)
                return;

            for (var i = components.Count - 1; i >= 0; i--)
            {
                if (components[i] is not MappingDataNode comp || !TryGetComponentName(comp, out var name))
                    continue;

                if (!config.DeferredComponents.Contains(name))
                    continue;

                // Одно отложенное состояние на сущность — при коллизии оставляем первое.
                report.DeferredStates.TryAdd(uid, comp.Copy());
                components.RemoveAt(i);
                report.ExtractedDeferred++;
            }

            if (components.Count == 0)
                ent.Remove("components");

            // stripped == null: отложенные состояния применяются после загрузки,
            // в прототип они не возвращаются, так что missingComponents остаётся как есть.
            SyncMissingComponents(ent, components, null);
        });
    }

    private static void DropFields(
        MappingDataNode data,
        SequenceDataNode entities,
        MapFilterConfig config,
        MapFilterReport report)
    {
        if (config.DropComponentFields.Count == 0 && config.DropFieldsWhenEntityHas.Count == 0)
            return;

        ForEachEntity(entities, (proto, ent) =>
        {
            var node = GetComponentsNode(ent);
            if (node == null)
                return;

            DropRuntimeFields(node, config, report);

            // Список компонентов считаем один раз на сущность: он нужен и для условных правил.
            var names = GetComponents(ent);
            if (names == null || config.DropFieldsWhenEntityHas.Count == 0)
                return;

            foreach (var item in node)
            {
                if (item is not MappingDataNode comp || !TryGetComponentName(comp, out var name))
                    continue;

                // Защищённые состояния не чистим никогда.
                if (config.ProtectedComponents.Contains(name))
                    continue;

                foreach (var (marker, byComponent) in config.DropFieldsWhenEntityHas)
                {
                    if (!names.Contains(marker))
                        continue;

                    if (byComponent.TryGetValue(name, out var conditional))
                        Drop(comp, conditional, ref report.DroppedFields);
                }
            }
        });
    }

    /// <summary>
    ///     Удаляет blacklist-поля (<see cref="MapFilterConfig.DropComponentFields"/>) у компонентов сущности.
    ///     Применяется и в обычном проходе, и внутри снятия компонентов целиком: удалённый вместе с
    ///     компонентом блок всё равно не должен унести в файл runtime-состояние других правил.
    /// </summary>
    private static void DropRuntimeFields(SequenceDataNode components, MapFilterConfig config, MapFilterReport report)
    {
        foreach (var item in components)
        {
            if (item is not MappingDataNode comp || !TryGetComponentName(comp, out var name))
                continue;

            // Защищённые состояния не чистим никогда.
            if (config.ProtectedComponents.Contains(name))
                continue;

            if (config.DropComponentFields.TryGetValue(name, out var fields))
                Drop(comp, fields, ref report.DroppedFields);
        }
    }

    private static void Drop(MappingDataNode comp, HashSet<string> fields, ref int counter)
    {
        if (fields.Count == 0)
            return;

        // Keys — снимок имён (_list.Select(...).ToArray()), поэтому удалять прямо во время обхода можно.
        foreach (var key in comp.Keys)
        {
            if (!ContainsField(fields, key))
                continue;

            comp.Remove(key);
            counter++;
        }
    }

    /// <summary>
    ///     Считает ли blacklist-правило поле <paramref name="key"/> своим — без учёта регистра.
    ///     Сериализатор пишет имя поля с первой строчной буквой (<c>NextSound</c> → <c>nextSound</c>),
    ///     а правило пишут именем C#-поля; точное сравнение превращало такое правило в тихую пустую
    ///     операцию. Имена полей в одном компоненте регистром не различаются.
    /// </summary>
    private static bool ContainsField(HashSet<string> fields, string key)
    {
        if (fields.Contains(key))
            return true;

        foreach (var field in fields)
        {
            if (string.Equals(field, key, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    #endregion

    #region Ссылки на удалённые сущности

    private static void CleanReferences(
        SequenceDataNode entities,
        HashSet<int> removed,
        MapFilterConfig config,
        MapFilterReport report)
    {
        if (removed.Count == 0)
            return;

        var excluded = BuildExcludedFields(config);

        ForEachEntity(entities, (_, ent) =>
        {
            var components = GetComponentsNode(ent);
            if (components == null)
                return;

            foreach (var item in components)
            {
                if (item is not MappingDataNode comp || !TryGetComponentName(comp, out var name))
                    continue;

                // Компоненты с ручными сериализаторами (декали, атмосфера, плитки): числа внутри них
                // — не ссылки на сущности, хоть и попадают в диапазон yaml uid.
                if (config.ReferenceCleanExcludedComponents.Contains(name))
                    continue;

                // Компонент-обёртка всегда содержит поля type/uid сущности — их не трогаем,
                // спускаемся только в поля компонента.
                var actions = new List<(string Key, NodeAction Action)>();
                foreach (var (key, child) in comp)
                {
                    if (EntityMetaFields.Contains(key) || excluded.Contains(key))
                        continue;

                    var action = CleanNode(child, removed, config, report, name);
                    if (action != NodeAction.Keep)
                        actions.Add((key, action));
                }

                ApplyActions(comp, actions);
            }
        });
    }

    /// <summary>
    ///     Поля, которые никогда не считаются ссылками на сущности: встроенный список плюс configured.
    /// </summary>
    private static HashSet<string> BuildExcludedFields(MapFilterConfig config)
    {
        if (config.ReferenceCleanExcludedFields.Count == 0)
            return NonReferenceFields;

        var set = new HashSet<string>(NonReferenceFields, StringComparer.OrdinalIgnoreCase);
        set.UnionWith(config.ReferenceCleanExcludedFields);
        return set;
    }

    private enum NodeAction
    {
        Keep,

        /// <summary>Заменить скаляр на <c>invalid</c>: поле сохраняется, но читается как EntityUid.Invalid.</summary>
        Replace,

        /// <summary>
        ///     Заменить скаляр на <c>null</c>: поле сохраняется и читается как «сущности нет».
        ///     Применимо только полям, допускающим null — см. <see cref="EntityFieldWalker.AcceptsNull"/>.
        /// </summary>
        ReplaceNull,

        Remove,
    }

    /// <summary>
    ///     Рекурсивно вычищает из узла ссылки на удалённые yaml uid.
    /// </summary>
    /// <returns>Что надо сделать с этим узлом родителем.</returns>
    private static NodeAction CleanNode(
        DataNode node,
        HashSet<int> removed,
        MapFilterConfig config,
        MapFilterReport report,
        string component,
        HashSet<string>? excluded = null)
    {
        excluded ??= NonReferenceFields;

        switch (node)
        {
            case ValueDataNode value:
            {
                if (!int.TryParse(value.Value, out var uid) || !removed.Contains(uid))
                    return NodeAction.Keep;

                report.CleanedReferences++;
                return config.ReplaceDanglingReferencesWithInvalid ? NodeAction.Replace : NodeAction.Remove;
            }
            case SequenceDataNode sequence:
            {
                var wasEmpty = sequence.Count == 0;
                for (var i = sequence.Count - 1; i >= 0; i--)
                {
                    var action = CleanNode(sequence[i], removed, config, report, component, excluded);
                    switch (action)
                    {
                        case NodeAction.Remove:
                            sequence.RemoveAt(i);
                            break;
                        case NodeAction.Replace:
                            sequence[i] = new ValueDataNode("invalid");
                            break;
                    }
                }

                // Ключ удаляем только если опустошили его сами. Пустая последовательность в исходном
                // дампе — валидные данные, а не мусор: ручные сериализаторы (например, DecalGrid)
                // читают такой ключ безусловно и на его отсутствии роняют сервер при загрузке карты.
                return !wasEmpty && sequence.Count == 0 ? NodeAction.Remove : NodeAction.Keep;
            }
            case MappingDataNode mapping:
            {
                var wasEmpty = mapping.Count == 0;

                // Ключи словарей Dictionary<EntityUid, ...> (DeviceLinkSource.LinkedPorts и т.п.) —
                // это тоже ссылки на сущности, записанные в YAML как скалярные ключи. Удаляем пару целиком.
                List<string>? deadKeys = null;
                foreach (var (key, _) in mapping)
                {
                    if (int.TryParse(key, out var keyUid) && removed.Contains(keyUid))
                        (deadKeys ??= new List<string>()).Add(key);
                }

                if (deadKeys != null)
                {
                    foreach (var key in deadKeys)
                    {
                        mapping.Remove(key);
                        report.CleanedReferences++;
                    }
                }

                var actions = new List<(string Key, NodeAction Action)>();
                foreach (var (key, child) in mapping)
                {
                    // Поля с известной не-EntityUid структурой не проверяем — см. NonReferenceFields.
                    if (excluded.Contains(key))
                        continue;

                    var action = CleanNode(child, removed, config, report, component, excluded);
                    if (action != NodeAction.Keep)
                        actions.Add((key, action));
                }

                ApplyActions(mapping, actions);

                // Пустой маппинг родителю не нужен, но только если опустошили его мы (см. случай выше).
                return !wasEmpty && mapping.Count == 0 ? NodeAction.Remove : NodeAction.Keep;
            }
            default:
                return NodeAction.Keep;
        }
    }

    #endregion

    #region Ссылки на несуществующие сущности (типизированный режим)

    /// <summary>
    ///     Чистка ссылок, в которой ссылкой считается только то, чем оно является по типу поля.
    /// </summary>
    /// <remarks>
    ///     Прежняя эвристика «любое число, совпавшее с удалённым uid — ссылка» портила валидные данные:
    ///     так из сохранения пропали значения урона в <c>MeleeWeapon.damage</c> (читатель затем упал на
    ///     RequiredFieldNotMappedException) и числа внутри декалей. Здесь поле вообще не рассматривается
    ///     как ссылка, если его C#-тип не <see cref="EntityUid"/>.
    /// </remarks>
    private static void CleanReferencesTyped(
        SequenceDataNode entities,
        HashSet<int> alive,
        MapFilterConfig config,
        IComponentFieldProbe probe,
        MapFilterReport report)
    {
        HashSet<string>? unresolved = null;

        ForEachEntity(entities, (_, ent) =>
        {
            var components = GetComponentsNode(ent);
            if (components == null)
                return;

            foreach (var item in components)
            {
                if (item is not MappingDataNode comp || !TryGetComponentName(comp, out var name))
                    continue;

                // Компонент исчез из регистрации: угадывать структуру нечего, а её состояние
                // при загрузке всё равно не читается.
                var compType = probe.GetComponentType(name);
                if (compType == null)
                {
                    (unresolved ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase)).Add(name);
                    continue;
                }

                var actions = new List<(string Key, NodeAction Action)>();
                foreach (var (key, child) in comp)
                {
                    if (EntityMetaFields.Contains(key))
                        continue;

                    var field = EntityFieldWalker.LookupField(compType, key);
                    if (!field.Known)
                        continue;

                    var action = CleanNodeTyped(child, field, alive, config, report);
                    if (action != NodeAction.Keep)
                        actions.Add((key, action));
                }

                ApplyActions(comp, actions);
            }
        });

        if (unresolved != null)
            report.UnresolvedComponents += unresolved.Count;
    }

    /// <summary>
    ///     То же, что <see cref="CleanNode"/>, но спускается в узел только тогда, когда знает его тип.
    /// </summary>
    private static NodeAction CleanNodeTyped(
        DataNode node,
        FieldKind field,
        HashSet<int> alive,
        MapFilterConfig config,
        MapFilterReport report)
    {
        var type = field.Type!;

        switch (node)
        {
            case ValueDataNode value:
            {
                if (!EntityFieldWalker.IsEntityReference(type))
                    return NodeAction.Keep;

                // Ссылкой считаем и числовой uid, и слово "invalid": сериализатор пишет его, когда
                // сущности нет уже на стороне записи. Для движка это не «пусто», а EntityUid.Invalid,
                // на который системы после загрузки не рассчитывают.
                var dangling = int.TryParse(value.Value, out var uid)
                    ? !alive.Contains(uid)
                    : string.Equals(value.Value, InvalidEntityLiteral, StringComparison.OrdinalIgnoreCase);

                if (!dangling)
                    return NodeAction.Keep;

                report.CleanedReferences++;

                // Обязательное поле удалять нельзя: читатель упадёт на RequiredFieldNotMappedException.
                // То же для режима «гасить, а не удалять». Если поле допускает null, гасим именно им:
                // "invalid" читается как несуществующая сущность, и проверки «есть ли адресат»
                // на ней не срабатывают.
                if (field.Required || config.ReplaceDanglingReferencesWithInvalid)
                    return EntityFieldWalker.AcceptsNull(type) ? NodeAction.ReplaceNull : NodeAction.Replace;

                // Остальные ссылки убираем целиком: отсутствующее поле читатель трактует так же,
                // как null, а явный "invalid" к тому же пишется в лог как ошибка загрузки.
                return config.ReplaceDanglingReferencesWithInvalid ? NodeAction.Replace : NodeAction.Remove;
            }
            case SequenceDataNode sequence:
            {
                var element = EntityFieldWalker.ElementType(type);
                if (element == null)
                    return NodeAction.Keep;

                var elementField = new FieldKind(element);
                for (var i = sequence.Count - 1; i >= 0; i--)
                {
                    // Элемент списка-ссылки не «гасится» invalid'ом, а убирается: сущности больше нет.
                    if (CleanNodeTyped(sequence[i], elementField, alive, config, report) != NodeAction.Keep)
                        sequence.RemoveAt(i);
                }

                // Пустой список — валидные данные, ключ оставляем (см. комментарий в CleanNode).
                return NodeAction.Keep;
            }
            case MappingDataNode mapping:
            {
                var (keyType, valueType) = EntityFieldWalker.DictionaryArguments(type);
                if (keyType != null)
                {
                    if (EntityFieldWalker.IsEntityReference(keyType))
                    {
                        List<string>? deadKeys = null;
                        foreach (var (key, _) in mapping)
                        {
                            if (int.TryParse(key, out var keyUid) && !alive.Contains(keyUid))
                                (deadKeys ??= new List<string>()).Add(key);
                        }

                        if (deadKeys != null)
                        {
                            foreach (var key in deadKeys)
                            {
                                mapping.Remove(key);
                                report.CleanedReferences++;
                            }
                        }
                    }

                    if (valueType != null)
                        CleanMappingValues(mapping, new FieldKind(valueType), alive, config, report);

                    return NodeAction.Keep;
                }

                // Составной тип: спускаемся только в те ключи, чьи поля нам известны.
                var actions = new List<(string Key, NodeAction Action)>();
                foreach (var (key, child) in mapping)
                {
                    var sub = EntityFieldWalker.LookupField(type, key);
                    if (!sub.Known)
                        continue;

                    var action = CleanNodeTyped(child, sub, alive, config, report);
                    if (action != NodeAction.Keep)
                        actions.Add((key, action));
                }

                ApplyActions(mapping, actions);
                return NodeAction.Keep;
            }
            default:
                return NodeAction.Keep;
        }
    }

    /// <summary>
    ///     Так сериализатор движка записывает <see cref="Robust.Shared.GameObjects.EntityUid.Invalid"/>.
    /// </summary>
    /// <remarks>
    ///     Для поля типа <c>EntityUid</c> это не «пусто», а несуществующая сущность: читатель отдаёт
    ///     её наружу, и системы, которые не ждут null, падают на ней при инициализации сущностей.
    /// </remarks>
    private const string InvalidEntityLiteral = "invalid";

    /// <summary>
    ///     Применяет решения родителя-маппинга после обхода: менять маппинг во время перечисления нельзя.
    /// </summary>
    private static void ApplyActions(MappingDataNode mapping, List<(string Key, NodeAction Action)> actions)
    {
        foreach (var (key, action) in actions)
        {
            switch (action)
            {
                case NodeAction.Replace:
                    mapping[key] = new ValueDataNode(InvalidEntityLiteral);
                    break;
                case NodeAction.ReplaceNull:
                    mapping[key] = new ValueDataNode("null");
                    break;
                case NodeAction.Remove:
                    mapping.Remove(key);
                    break;
            }
        }
    }

    private static void CleanMappingValues(
        MappingDataNode mapping,
        FieldKind valueField,
        HashSet<int> alive,
        MapFilterConfig config,
        MapFilterReport report)
    {
        var actions = new List<(string Key, NodeAction Action)>();
        foreach (var (key, child) in mapping)
        {
            var action = CleanNodeTyped(child, valueField, alive, config, report);
            if (action != NodeAction.Keep)
                actions.Add((key, action));
        }

        ApplyActions(mapping, actions);
    }

    #endregion

    #region Извлечение отложенных состояний (Этап 4)

    /// <summary>
    ///     Собирает whitelisted-состояния (<see cref="MapFilterConfig.DeferredComponents"/>) в отдельное дерево,
    ///     пригодное для записи рядом с картой. Само дерево карты этим методом не изменяется.
    /// </summary>
    public static MappingDataNode BuildDeferredTree(MapFilterReport report)
    {
        var entities = new SequenceDataNode();

        foreach (var (uid, state) in report.DeferredStates)
        {
            var ent = new MappingDataNode
            {
                { "uid", uid.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                { "components", new SequenceDataNode { state.Copy() } },
            };

            entities.Add(ent);
        }

        return new MappingDataNode
        {
            { "meta", new MappingDataNode { { "format", "7" }, { "category", "Save" } } },
            { EntitiesKey, entities },
        };
    }

    /// <summary>
    ///     Читает ранее сохранённые отложенные состояния в маппинг «yaml uid → компонент».
    /// </summary>
    public static Dictionary<int, MappingDataNode> ReadDeferredStates(MappingDataNode tree)
    {
        var result = new Dictionary<int, MappingDataNode>();

        if (!tree.TryGet(EntitiesKey, out SequenceDataNode? entities))
            return result;

        foreach (var node in entities)
        {
            if (node is not MappingDataNode ent || !TryGetYamlUid(ent, out var uid))
                continue;

            if (ent.TryGet("components", out SequenceDataNode? components))
            {
                foreach (var item in components)
                {
                    if (item is MappingDataNode comp)
                        result[uid] = comp;
                }
            }
        }

        return result;
    }

    #endregion

    #region Хелперы разбора дерева

    private delegate void EntityVisitor(string? proto, MappingDataNode entity);

    /// <summary>
    ///     Обходит все сущности независимо от версии секции <c>entities</c>:
    ///     либо группы по прототипу (формат 4+), либо плоский список (формат 3).
    /// </summary>
    private static void ForEachEntity(SequenceDataNode entities, EntityVisitor visitor)
    {
        foreach (var node in entities)
        {
            if (node is not MappingDataNode group)
                continue;

            var proto = GetPrototype(group);
            var list = GetEntityList(group);

            // Плоская сущность формата 3: узел сам является сущностью.
            if (list == null)
            {
                if (TryGetYamlUid(group, out _))
                    visitor(proto, group);
                continue;
            }

            // Копия списка: обход во время изменения children у SequenceDataNode недопустим.
            foreach (var item in list.Sequence.ToArray())
            {
                if (item is MappingDataNode ent)
                    visitor(proto, ent);
            }
        }
    }

    private static string? GetPrototype(MappingDataNode group)
    {
        if (group.TryGet<ValueDataNode>("proto", out var proto) && !string.IsNullOrWhiteSpace(proto.Value))
            return proto.Value;

        if (group.TryGet<ValueDataNode>("type", out var type) && !string.IsNullOrWhiteSpace(type.Value))
            return type.Value;

        return null;
    }

    /// <summary>
    ///     Возвращает список сущностей группы прототипа, либо null если узел сам является сущностью.
    /// </summary>
    private static SequenceDataNode? GetEntityList(MappingDataNode group)
    {
        if (group.TryGet(EntitiesKey, out SequenceDataNode? list))
            return list;

        // Формат 3: "components" внутри плоской сущности — это компоненты, а не список сущностей.
        if (group.Has("uid") || group.Has("components"))
            return null;

        return null;
    }

    private static SequenceDataNode? GetComponentsNode(MappingDataNode ent)
    {
        return ent.TryGet("components", out SequenceDataNode? components) ? components : null;
    }

    /// <summary>
    ///     Имена компонентов сущности (значение поля <c>type</c> в каждом компоненте).
    /// </summary>
    private static HashSet<string>? GetComponents(MappingDataNode ent)
    {
        var components = GetComponentsNode(ent);
        if (components == null)
            return null;

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in components)
        {
            if (item is MappingDataNode comp && TryGetComponentName(comp, out var name))
                names.Add(name);
        }

        // Компоненты, удалённые у сущности относительно прототипа, тоже считаются «имеющимися»
        // для признаков? Нет: missingComponents — это как раз отсутствующие, их не учитываем.
        return names;
    }

    private static bool TryGetComponentName(MappingDataNode comp, out string name)
    {
        if (comp.TryGet<ValueDataNode>("type", out var node) && !string.IsNullOrWhiteSpace(node.Value))
        {
            name = node.Value;
            return true;
        }

        name = string.Empty;
        return false;
    }

    private static bool TryGetYamlUid(MappingDataNode ent, out int uid)
    {
        uid = 0;
        return ent.TryGet<ValueDataNode>("uid", out var node) && int.TryParse(node.Value, out uid);
    }

    #endregion
}
