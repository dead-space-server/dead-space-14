using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.SaveGamemode;

/// <summary>
///     SaveGamemode: сохранение инициализированной карты + полных прототипов сущностей с MobState.
/// </summary>
public sealed partial class SaveSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IResourceManager _res = default!;

    private static readonly ResPath SaveDir = new("/Saves/GameMode");

    private readonly HashSet<EntityUid> _mobSaveSet = new();

    private EntityQuery<MobStateComponent> _mobQuery;
    private EntityQuery<ActorComponent> _actorQuery;

    public override void Initialize()
    {
        base.Initialize();
        _mobQuery = GetEntityQuery<MobStateComponent>();
        _actorQuery = GetEntityQuery<ActorComponent>();
    }

    #region Save

    /// <summary>
    ///     Все сущности с моб-стейтом. Игроки исключаются, так как их нельзя сериализовать.
    /// </summary>
    private HashSet<EntityUid> CollectMobs(bool includeActors = false)
    {
        _mobSaveSet.Clear();
        var q = AllEntityQuery<MobStateComponent>();
        while (q.MoveNext(out var uid, out _))
        {
            if (!includeActors && _actorQuery.HasComp(uid))
                continue;
            _mobSaveSet.Add(uid);
        }
        return _mobSaveSet;
    }

    /// <summary>
    ///     Принудительное сохранение инициализированной карты вместе с моб-стейтом.
    /// </summary>
    public bool TrySaveMap(MapId mapId, string name)
    {
        if (!_map.TryGetMap(mapId, out _) || !_map.IsInitialized(mapId))
        {
            Log.Warning($"SaveSystem: карта {mapId} отсутствует или не инициализирована");
            return false;
        }

        var dir = SaveDir / name;
        var okMap = _mapLoader.TrySaveMap(mapId, dir / "map.yml");
        var okMobs = SaveMobEntities(mapId, dir / "mobs.yml");
        return okMap && okMobs;
    }

    /// <summary>
    ///     Полный прототип + координаты + MobState для сущностей на карте.
    /// </summary>
    private bool SaveMobEntities(MapId mapId, ResPath path)
    {
        var mobs = CollectMobs().Where(x => Exists(x) && Transform(x).MapID == mapId).ToHashSet();
        if (mobs.Count == 0)
            return true;

        var root = new MappingDataNode();
        var entities = new MappingDataNode();

        foreach (var uid in mobs)
        {
            var xform = Transform(uid);
            var meta = MetaData(uid);

            var entry = new MappingDataNode()
                .Add("proto", new ValueDataNode(meta.EntityPrototype?.ID ?? string.Empty))
                .Add("parent", new ValueDataNode(xform.ParentUid.ToString()))
                .Add("coords", new MappingDataNode()
                    .Add("map", new ValueDataNode(mapId.ToString()))
                    .Add("x", new ValueDataNode(xform.LocalPosition.X.ToString(CultureInfo.InvariantCulture)))
                    .Add("y", new ValueDataNode(xform.LocalPosition.Y.ToString(CultureInfo.InvariantCulture))));

            // весь прототип: все компоненты, все DataField, без вычитания значений прототипа
            entry.Add("components", SerializeAllComponents(uid));

            // моб-стейт: CurrentState не помечен [DataField], поэтому дописываем вручную
            if (_mobQuery.TryGetComponent(uid, out var mob))
            {
                // CurrentState/LastReceivedState под [Access(MobStateSystem, MobThresholdSystem)] и без публичного геттера.
#pragma warning disable RA0002
                var current = mob.CurrentState;
                var lastReceived = mob.LastReceivedState;
#pragma warning restore RA0002

                entry.Add("mobState", new MappingDataNode()
                    .Add("current", new ValueDataNode(current.ToString()))
                    .Add("lastReceived", new ValueDataNode(lastReceived.ToString())));
            }

            entities.Add(uid.ToString(), entry);
        }

        root.Add("entities", entities);
        return WriteNode(path, root);
    }

    private SequenceDataNode SerializeAllComponents(EntityUid uid)
    {
        var seq = new SequenceDataNode();

        foreach (var comp in EntityManager.GetComponents(uid))
        {
            var reg = _factory.GetRegistration(comp.GetType());
            if (reg.Unsaved)
                continue;

            MappingDataNode mapping;
            try
            {
                mapping = _serialization.WriteValueAs<MappingDataNode>(
                    comp.GetType(), comp, alwaysWrite: true);
            }
            catch (Exception e)
            {
                Log.Error($"SaveSystem: не удалось сериализовать {reg.Name} у {ToPrettyString(uid)}:\n{e}");
                continue;
            }

            mapping.InsertAt(0, "type", new ValueDataNode(reg.Name));
            seq.Add(mapping);
        }

        return seq;
    }

    private bool WriteNode(ResPath path, MappingDataNode node)
    {
        var rooted = path.ToRootedPath();
        _res.UserData.CreateDir(rooted.Directory);
        _res.UserData.WriteAllText(rooted, node.ToString());
        return true;
    }

    #endregion

    #region Load

    /// <summary>
    ///     Загрузка: карта сразу инициализируется, затем восстанавливаются полные компоненты и MobState.
    /// </summary>
    public bool TryLoad(string name, out MapId mapId, out LoadResult? result)
    {
        mapId = MapId.Nullspace;
        result = null;

        var dir = SaveDir / name;
        var opts = MapLoadOptions.Default with
        {
            ExpectedCategory = FileCategory.Save,
            DeserializationOptions = DeserializationOptions.Default with
            {
                InitializeMaps = true,
                PauseMaps = false,
            },
        };

        if (!_mapLoader.TryLoadGeneric(dir / "map.yml", out result, opts))
            return false;

        if (!_mapLoader.TryReadFile(dir / "mobs.yml", out var data)
            || !data.TryGet<MappingDataNode>("entities", out var entities))
            return true; // карта загружена, мобов нет

        var loaded = result.Entities;

        foreach (var (key, raw) in entities.Children)
        {
            if (raw is not MappingDataNode entry)
                continue;

            if (!EntityUid.TryParse(key, out var uid) || !loaded.Contains(uid))
                continue;

            // координаты и родитель
            if (entry.TryGet<MappingDataNode>("coords", out var coords)
                && coords.TryGet<ValueDataNode>("x", out var xs)
                && coords.TryGet<ValueDataNode>("y", out var ys)
                && float.TryParse(xs.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                && float.TryParse(ys.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                _xform.SetLocalPosition(uid, new Vector2(x, y));
            }

            // весь прототип
            if (entry.TryGet<SequenceDataNode>("components", out var components))
                RestoreComponents(uid, components);

            // MobState без UpdateMobState (иначе пороги перезапишут состояние)
            if (entry.TryGet<MappingDataNode>("mobState", out var mobNode)
                && mobNode.TryGet<ValueDataNode>("current", out var stateNode)
                && Enum.TryParse<MobState>(stateNode.Value, out var state)
                && _mobQuery.TryGetComponent(uid, out var mob))
            {
                _mobState.ChangeMobState(uid, state, mob);
            }
        }

        if (result.Maps.Count == 1)
            mapId = result.Maps.First().Comp.MapId;

        return true;
    }

    private void ReadInto(IComponent comp, DataNode data)
    {
        // ISerializationManager.Read<T> не выводит T из System.Type, поэтому маршрутизируем по фактическому типу.
        var method = typeof(SaveSystem)
            .GetMethod(nameof(ReadIntoTyped), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(comp.GetType());

        method.Invoke(null, new object?[] { _serialization, data, comp });
    }

    private static void ReadIntoTyped<T>(ISerializationManager ser, DataNode data, IComponent comp) where T : IComponent
    {
        ser.Read<T>(data, instanceProvider: () => (T) comp, notNullableOverride: true);
    }

    private void RestoreComponents(EntityUid uid, SequenceDataNode components)
    {
        foreach (var raw in components)
        {
            if (raw is not MappingDataNode compNode)
                continue;

            if (!compNode.TryGet<ValueDataNode>("type", out var typeNode))
                continue;

            if (!_factory.TryGetRegistration(typeNode.Value, out var reg))
                continue;

            if (!EntityManager.TryGetComponent(uid, reg.Type, out var comp))
                continue; // компонента нет на новой сущности - пропускаем

            var data = new MappingDataNode(compNode.Children
                .Where(kv => kv.Key != "type")
                .ToDictionary(kv => kv.Key, kv => kv.Value));

            try
            {
                ReadInto(comp, data);
            }
            catch (Exception e)
            {
                Log.Error($"SaveSystem: не удалось восстановить {reg.Name} у {ToPrettyString(uid)}:\n{e}");
            }
        }

        DirtyEntity(uid); // синхронизация с клиентом
    }

    #endregion
}
