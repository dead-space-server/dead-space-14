// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared.DeadSpace.Shuttles.Components;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.Server.DeadSpace.Shuttles.Systems;

public sealed partial class RadarBlipSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private TagSystem _tags = default!;

    private const float BlipRadius = 0.5f;
    private const float BucketSize = 32f;
    private const LookupFlags BlipLookupFlags = LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sensors;

    private readonly Dictionary<RadarConsoleComponent, CachedBlipConfig> _configCache = new();
    private readonly HashSet<EntityUid> _candidates = new();
    private readonly HashSet<EntityUid> _seenIndexed = new();
    private readonly HashSet<Type> _watchedAllowedComponents = new();
    private readonly Dictionary<EntityUid, IndexedBlip> _indexedBlips = new();
    private readonly Dictionary<MapId, Dictionary<Vector2i, HashSet<EntityUid>>> _buckets = new();

    public override void Initialize()
    {
        base.Initialize();

        _xform.OnGlobalMoveEvent += OnGlobalMove;
        EntityManager.ComponentAdded += OnComponentAdded;
        EntityManager.ComponentRemoved += OnComponentRemoved;

        SubscribeLocalEvent<CollisionChangeEvent>(OnCollisionChange);
        SubscribeLocalEvent<MetaDataComponent, EntityTerminatingEvent>(OnEntityTerminating);
    }

    public override void Shutdown()
    {
        _xform.OnGlobalMoveEvent -= OnGlobalMove;
        EntityManager.ComponentAdded -= OnComponentAdded;
        EntityManager.ComponentRemoved -= OnComponentRemoved;

        _configCache.Clear();
        _candidates.Clear();
        _seenIndexed.Clear();
        _watchedAllowedComponents.Clear();
        _indexedBlips.Clear();
        _buckets.Clear();

        base.Shutdown();
    }

    public List<BlipState> CollectSpaceBlips(EntityUid consoleUid, RadarConsoleComponent component, float range)
    {
        if (!component.Advanced)
            return new List<BlipState>();

        if (!TryComp(consoleUid, out TransformComponent? consoleXform) ||
            consoleXform.MapUid == null)
        {
            return new List<BlipState>();
        }

        var config = GetConfig(component);
        var worldPos = _xform.GetWorldPosition(consoleXform);
        var mapId = consoleXform.MapID;
        var consoleParent = consoleXform.ParentUid;

        if (CanUseIndexedPath(config))
            return CollectIndexedBlips(mapId, worldPos, range, config, consoleUid, consoleParent);

        return CollectFallbackBlips(mapId, worldPos, range, config, consoleUid, consoleParent);
    }

    private static bool CanUseIndexedPath(CachedBlipConfig config)
    {
        return config.AllowedTags.Length == 0 && config.AllowedComponents.Length > 0;
    }

    private List<BlipState> CollectIndexedBlips(
        MapId mapId,
        Vector2 worldPos,
        float range,
        CachedBlipConfig config,
        EntityUid consoleUid,
        EntityUid consoleParent)
    {
        EnsureIndexedAllowedComponents(config);

        if (!_buckets.TryGetValue(mapId, out var mapBuckets))
            return new List<BlipState>();

        var min = GetBucket(worldPos - new Vector2(range, range));
        var max = GetBucket(worldPos + new Vector2(range, range));
        var rangeSquared = range * range;
        var blips = new List<BlipState>();

        for (var x = min.X; x <= max.X; x++)
        {
            for (var y = min.Y; y <= max.Y; y++)
            {
                var bucket = new Vector2i(x, y);
                if (!mapBuckets.TryGetValue(bucket, out var entities))
                    continue;

                foreach (var ent in entities)
                {
                    if (!_seenIndexed.Add(ent))
                        continue;

                    if (!_indexedBlips.TryGetValue(ent, out var indexed) ||
                        indexed.MapId != mapId ||
                        Vector2.DistanceSquared(indexed.WorldPosition, worldPos) > rangeSquared)
                    {
                        continue;
                    }

                    if (!TryGetBlipTransform(ent, consoleUid, consoleParent, config, out var entXform))
                        continue;

                    if (!TryPickColor(ent, config, out var color))
                        continue;

                    AddBlip(entXform, color, blips);
                }
            }
        }

        _seenIndexed.Clear();
        return blips;
    }

    private List<BlipState> CollectFallbackBlips(
        MapId mapId,
        Vector2 worldPos,
        float range,
        CachedBlipConfig config,
        EntityUid consoleUid,
        EntityUid consoleParent)
    {
        _candidates.Clear();
        CollectCandidates(mapId, worldPos, range);

        var blips = new List<BlipState>(_candidates.Count);
        foreach (var ent in _candidates)
        {
            if (!TryGetBlipTransform(ent, consoleUid, consoleParent, config, out var entXform))
                continue;

            if (!TryPickColor(ent, config, out var color))
                continue;

            AddBlip(entXform, color, blips);
        }

        _candidates.Clear();
        return blips;
    }

    private void EnsureIndexedAllowedComponents(CachedBlipConfig config)
    {
        foreach (var (type, _) in config.AllowedComponents)
        {
            if (!_watchedAllowedComponents.Add(type))
                continue;

            var query = EntityManager.AllEntityQueryEnumerator(type);
            while (query.MoveNext(out var uid, out _))
            {
                UpdateIndexedEntity(uid);
            }
        }
    }

    private void OnGlobalMove(ref MoveEvent ev)
    {
        if (_watchedAllowedComponents.Count == 0)
            return;

        var uid = ev.Sender;
        if (_indexedBlips.ContainsKey(uid) || HasAnyWatchedAllowedComponent(uid))
            UpdateIndexedEntity(uid);
    }

    private void OnCollisionChange(ref CollisionChangeEvent ev)
    {
        if (_watchedAllowedComponents.Count == 0)
            return;

        var uid = ev.BodyUid;
        if (_indexedBlips.ContainsKey(uid) || HasAnyWatchedAllowedComponent(uid))
            UpdateIndexedEntity(uid);
    }

    private void OnEntityTerminating(Entity<MetaDataComponent> ent, ref EntityTerminatingEvent args)
    {
        RemoveIndexedEntity(ent.Owner);
    }

    private void OnComponentAdded(AddedComponentEventArgs args)
    {
        if (_watchedAllowedComponents.Count == 0)
            return;

        var uid = args.BaseArgs.Owner;
        var type = args.ComponentType.Type;

        if (!ShouldRecheckIndexedEntity(uid, type))
            return;

        UpdateIndexedEntity(uid);
    }

    private void OnComponentRemoved(RemovedComponentEventArgs args)
    {
        if (_watchedAllowedComponents.Count == 0)
            return;

        var uid = args.BaseArgs.Owner;
        var type = args.BaseArgs.Component.GetType();

        if (!_indexedBlips.ContainsKey(uid) &&
            !_watchedAllowedComponents.Contains(type) &&
            !IsIndexStructuralComponent(type))
        {
            return;
        }

        if (IsIndexStructuralComponent(type) ||
            _watchedAllowedComponents.Contains(type) && !HasAnyWatchedAllowedComponent(uid, type))
        {
            RemoveIndexedEntity(uid);
            return;
        }

        UpdateIndexedEntity(uid);
    }

    private bool ShouldRecheckIndexedEntity(EntityUid uid, Type changedType)
    {
        return _indexedBlips.ContainsKey(uid) ||
               _watchedAllowedComponents.Contains(changedType) ||
               IsIndexStructuralComponent(changedType) && HasAnyWatchedAllowedComponent(uid);
    }

    private static bool IsIndexStructuralComponent(Type type)
    {
        return type == typeof(TransformComponent) ||
               type == typeof(PhysicsComponent) ||
               type == typeof(MapComponent) ||
               type == typeof(MapGridComponent);
    }

    private bool HasAnyWatchedAllowedComponent(EntityUid uid, Type? ignoredType = null)
    {
        foreach (var type in _watchedAllowedComponents)
        {
            if (type == ignoredType)
                continue;

            if (HasComp(uid, type))
                return true;
        }

        return false;
    }

    private void UpdateIndexedEntity(EntityUid uid)
    {
        if (!HasAnyWatchedAllowedComponent(uid) ||
            !TryGetIndexCandidate(uid, out var mapId, out var worldPosition))
        {
            RemoveIndexedEntity(uid);
            return;
        }

        var bucket = GetBucket(worldPosition);
        if (_indexedBlips.TryGetValue(uid, out var previous))
        {
            if (previous.MapId != mapId || previous.Bucket != bucket)
            {
                RemoveFromBucket(uid, previous);
                AddToBucket(uid, mapId, bucket);
            }

            _indexedBlips[uid] = new IndexedBlip(mapId, worldPosition, bucket);
            return;
        }

        AddToBucket(uid, mapId, bucket);
        _indexedBlips.Add(uid, new IndexedBlip(mapId, worldPosition, bucket));
    }

    private bool TryGetIndexCandidate(EntityUid uid, out MapId mapId, out Vector2 worldPosition)
    {
        mapId = default;
        worldPosition = default;

        if (HasComp<MapComponent>(uid) ||
            HasComp<MapGridComponent>(uid) ||
            !TryComp(uid, out TransformComponent? xform) ||
            xform.MapUid == null ||
            xform.GridUid != null ||
            !TryComp<PhysicsComponent>(uid, out var physics) ||
            !physics.CanCollide)
        {
            return false;
        }

        mapId = xform.MapID;
        worldPosition = _xform.GetWorldPosition(xform);
        return true;
    }

    private void AddToBucket(EntityUid uid, MapId mapId, Vector2i bucket)
    {
        if (!_buckets.TryGetValue(mapId, out var mapBuckets))
        {
            mapBuckets = new Dictionary<Vector2i, HashSet<EntityUid>>();
            _buckets.Add(mapId, mapBuckets);
        }

        if (!mapBuckets.TryGetValue(bucket, out var entities))
        {
            entities = new HashSet<EntityUid>();
            mapBuckets.Add(bucket, entities);
        }

        entities.Add(uid);
    }

    private void RemoveIndexedEntity(EntityUid uid)
    {
        if (!_indexedBlips.Remove(uid, out var indexed))
            return;

        RemoveFromBucket(uid, indexed);
    }

    private void RemoveFromBucket(EntityUid uid, IndexedBlip indexed)
    {
        if (!_buckets.TryGetValue(indexed.MapId, out var mapBuckets) ||
            !mapBuckets.TryGetValue(indexed.Bucket, out var entities))
        {
            return;
        }

        entities.Remove(uid);

        if (entities.Count != 0)
            return;

        mapBuckets.Remove(indexed.Bucket);

        if (mapBuckets.Count == 0)
            _buckets.Remove(indexed.MapId);
    }

    private static Vector2i GetBucket(Vector2 worldPosition)
    {
        return new Vector2i(
            (int) Math.Floor(worldPosition.X / BucketSize),
            (int) Math.Floor(worldPosition.Y / BucketSize));
    }

    private void CollectCandidates(MapId mapId, Vector2 worldPos, float range)
    {
        _lookup.GetEntitiesInRange(mapId, worldPos, range, _candidates, BlipLookupFlags);
    }

    private bool TryGetBlipTransform(
        EntityUid ent,
        EntityUid consoleUid,
        EntityUid consoleParent,
        CachedBlipConfig config,
        [NotNullWhen(true)] out TransformComponent? entXform)
    {
        entXform = null;

        if (ent == consoleUid ||
            ent == consoleParent ||
            HasComp<MapComponent>(ent) ||
            HasComp<MapGridComponent>(ent))
        {
            return false;
        }

        if (!TryComp(ent, out entXform) ||
            entXform.GridUid != null)
        {
            return false;
        }

        if (!TryComp<PhysicsComponent>(ent, out var phys) || !phys.CanCollide)
            return false;

        if (HasBlacklistedComponent(ent, config) || HasBlacklistedTag(ent, config))
            return false;

        return true;
    }

    private void AddBlip(TransformComponent entXform, Color color, List<BlipState> blips)
    {
        var entWorldPos = _xform.GetWorldPosition(entXform);
        blips.Add(new BlipState(entWorldPos, color, BlipRadius));
    }

    private CachedBlipConfig GetConfig(RadarConsoleComponent component)
    {
        var fingerprint = GetConfigFingerprint(component);
        if (_configCache.TryGetValue(component, out var cached) && cached.Fingerprint == fingerprint)
            return cached;

        cached = new CachedBlipConfig(
            fingerprint,
            ResolveAllowedEntries(component.AllowedComponents),
            ResolveComponentTypes(component.BlacklistComponents),
            component.AllowedTags.ToArray(),
            component.BlacklistTags.ToArray());

        _configCache[component] = cached;
        return cached;
    }

    private int GetConfigFingerprint(RadarConsoleComponent component)
    {
        var hash = new HashCode();

        foreach (var entry in component.AllowedComponents)
        {
            hash.Add(entry.Component);
            hash.Add(entry.Color);
        }

        foreach (var entry in component.BlacklistComponents)
        {
            hash.Add(entry);
        }

        foreach (var entry in component.AllowedTags)
        {
            hash.Add(entry.Tag);
            hash.Add(entry.Color);
        }

        foreach (var entry in component.BlacklistTags)
        {
            hash.Add(entry);
        }

        return hash.ToHashCode();
    }

    private (Type Type, Color Color)[] ResolveAllowedEntries(List<RadarBlipEntry> entries)
    {
        var result = new List<(Type, Color)>(entries.Count);
        foreach (var entry in entries)
        {
            if (_componentFactory.TryGetRegistration(entry.Component, out var reg))
                result.Add((reg.Type, entry.Color));
            else
                Log.Warning($"[RadarConsole] AllowedComponents: component '{entry.Component}' not found.");
        }

        return result.ToArray();
    }

    private Type[] ResolveComponentTypes(List<string> names)
    {
        var result = new List<Type>(names.Count);
        foreach (var name in names)
        {
            if (_componentFactory.TryGetRegistration(name, out var reg))
                result.Add(reg.Type);
            else
                Log.Warning($"[RadarConsole] Blacklist: component '{name}' not found.");
        }

        return result.ToArray();
    }

    private bool HasBlacklistedComponent(EntityUid ent, CachedBlipConfig config)
    {
        foreach (var type in config.BlacklistComponents)
        {
            if (HasComp(ent, type))
                return true;
        }

        return false;
    }

    private bool HasBlacklistedTag(EntityUid ent, CachedBlipConfig config)
    {
        foreach (var tag in config.BlacklistTags)
        {
            if (_tags.HasTag(ent, tag))
                return true;
        }

        return false;
    }

    private bool TryPickColor(EntityUid ent, CachedBlipConfig config, out Color color)
    {
        foreach (var (type, entryColor) in config.AllowedComponents)
        {
            if (!HasComp(ent, type))
                continue;

            color = entryColor;
            return true;
        }

        foreach (var entry in config.AllowedTags)
        {
            if (!_tags.HasTag(ent, entry.Tag))
                continue;

            color = entry.Color;
            return true;
        }

        if (config.AllowedComponents.Length == 0 && config.AllowedTags.Length == 0)
        {
            color = Color.Yellow;
            return true;
        }

        color = default;
        return false;
    }

    public void ClearCache(RadarConsoleComponent component)
    {
        _configCache.Remove(component);
    }

    private sealed record CachedBlipConfig(
        int Fingerprint,
        (Type Type, Color Color)[] AllowedComponents,
        Type[] BlacklistComponents,
        RadarBlipTagEntry[] AllowedTags,
        string[] BlacklistTags);

    private readonly record struct IndexedBlip(MapId MapId, Vector2 WorldPosition, Vector2i Bucket);
}
