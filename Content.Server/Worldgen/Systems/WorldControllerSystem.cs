using System.Linq;
using Content.Server.Worldgen.Components;
using Content.Shared.Ghost;
using Content.Shared.Mind.Components;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Worldgen.Systems;

/// <summary>
///     This handles putting together chunk entities and notifying them about important changes.
/// </summary>
public sealed partial class WorldControllerSystem : EntitySystem
{
    [Dependency] private TransformSystem _xformSys = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private MetaDataSystem _metaData = default!;

    private const int PlayerLoadRadius = 2;
    private static readonly TimeSpan ChunkEvictionDelay = TimeSpan.FromMinutes(5); // DS14 Edit: avoid immediate world chunk delete/recreate churn.

    private ISawmill _sawmill = default!;
    // DS14-Start: cache worldgen queries and reusable buffers.
    private EntityQuery<GhostComponent> _ghostQuery;
    private EntityQuery<LoadedChunkComponent> _loadedQuery;
    private EntityQuery<WorldControllerComponent> _controllerQuery;
    private EntityQuery<ChunkEvictionComponent> _evictQuery;

    private readonly HashSet<EntityUid> _controllerMaps = new();
    private readonly List<EntityUid> _loadedChunksBuffer = new();
    // DS14-End

    /// <inheritdoc />
    public override void Initialize()
    {
        _sawmill = _logManager.GetSawmill("world");
        // DS14-Start: cache worldgen queries used in Update.
        _ghostQuery = GetEntityQuery<GhostComponent>();
        _loadedQuery = GetEntityQuery<LoadedChunkComponent>();
        _controllerQuery = GetEntityQuery<WorldControllerComponent>();
        _evictQuery = GetEntityQuery<ChunkEvictionComponent>();
        // DS14-End
        SubscribeLocalEvent<LoadedChunkComponent, ComponentStartup>(OnChunkLoadedCore);
        SubscribeLocalEvent<LoadedChunkComponent, ComponentShutdown>(OnChunkUnloadedCore);
        SubscribeLocalEvent<WorldChunkComponent, ComponentShutdown>(OnChunkShutdown);
    }

    /// <summary>
    ///     Handles deleting chunks properly.
    /// </summary>
    private void OnChunkShutdown(EntityUid uid, WorldChunkComponent component, ComponentShutdown args)
    {
        if (!TryComp<WorldControllerComponent>(component.Map, out var controller))
            return;

        if (HasComp<LoadedChunkComponent>(uid))
        {
            var ev = new WorldChunkUnloadedEvent(uid, component.Coordinates);
            RaiseLocalEvent(component.Map, ref ev);
            RaiseLocalEvent(uid, ref ev, broadcast: true);
        }

        controller.Chunks.Remove(component.Coordinates);
    }

    /// <summary>
    ///     Handles the inner logic of loading a chunk, i.e. events.
    /// </summary>
    private void OnChunkLoadedCore(EntityUid uid, LoadedChunkComponent component, ComponentStartup args)
    {
        // DS14-Start: cancel pending chunk eviction when a chunk is loaded again.
        if (_evictQuery.HasComponent(uid))
            RemCompDeferred<ChunkEvictionComponent>(uid);
        // DS14-End

        if (!TryComp<WorldChunkComponent>(uid, out var chunk))
            return;

        var ev = new WorldChunkLoadedEvent(uid, chunk.Coordinates);
        RaiseLocalEvent(chunk.Map, ref ev);
        RaiseLocalEvent(uid, ref ev, broadcast: true);
        //_sawmill.Debug($"Loaded chunk {ToPrettyString(uid)} at {chunk.Coordinates}");
    }

    /// <summary>
    ///     Handles the inner logic of unloading a chunk, i.e. events.
    /// </summary>
    private void OnChunkUnloadedCore(EntityUid uid, LoadedChunkComponent component, ComponentShutdown args)
    {
        if (!TryComp<WorldChunkComponent>(uid, out var chunk))
            return;

        if (Terminating(uid))
            return; // SAFETY: This is in case a loaded chunk gets deleted, to avoid double unload.

        var ev = new WorldChunkUnloadedEvent(uid, chunk.Coordinates);
        RaiseLocalEvent(chunk.Map, ref ev);
        RaiseLocalEvent(uid, ref ev);
        //_sawmill.Debug($"Unloaded chunk {ToPrettyString(uid)} at {coords}");

        // DS14-Start: keep unloaded chunks briefly so repeated loader movement does not recreate entities every tick.
        var evict = EnsureComp<ChunkEvictionComponent>(uid);
        evict.EvictAt = _gameTiming.RealTime + ChunkEvictionDelay;
        // DS14-End
    }

    /// <inheritdoc />
    public override void Update(float frameTime)
    {
        // DS14-Start: build worldgen load state without per-tick nested dictionaries.
        _controllerMaps.Clear();
        var controllerEnum = EntityQueryEnumerator<WorldControllerComponent>();
        while (controllerEnum.MoveNext(out var uid, out _))
        {
            _controllerMaps.Add(uid);
        }

        if (_controllerMaps.Count == 0)
            return;

        _loadedChunksBuffer.Clear();
        var loadedEnum = EntityQueryEnumerator<LoadedChunkComponent, WorldChunkComponent>();
        while (loadedEnum.MoveNext(out var loadedUid, out var loadedChunk, out var chunk))
        {
            if (!_controllerMaps.Contains(chunk.Map))
                continue;

            loadedChunk.Loaders ??= new List<EntityUid>(4);
            loadedChunk.Loaders.Clear();
            _loadedChunksBuffer.Add(loadedUid);
        }

        var loaderEnum = EntityQueryEnumerator<WorldLoaderComponent, TransformComponent>();
        var anyChunksRequested = false;
        var startTime = _gameTiming.RealTime;
        var loadedCount = 0;

        while (loaderEnum.MoveNext(out var uid, out var worldLoader, out var xform))
        {
            var mapOrNull = xform.MapUid;
            if (mapOrNull is null)
                continue;
            var map = mapOrNull.Value;
            if (!_controllerMaps.Contains(map))
                continue;

            var wc = _xformSys.GetWorldPosition(xform);
            var coords = WorldGen.WorldToChunkCoords(wc);
            var chunks = new GridPointsNearEnumerator(coords.Floored(),
                (int) Math.Ceiling(worldLoader.Radius / (float) WorldGen.ChunkSize) + 1);
            var controller = _controllerQuery.GetComponent(map);

            while (chunks.MoveNext(out var chunkCoord))
            {
                anyChunksRequested = true;
                LoadRequestedChunk(chunkCoord.Value, map, controller, uid, ref loadedCount);
            }
        }

        var mindEnum = EntityQueryEnumerator<MindContainerComponent, TransformComponent>();

        // Mindful entities get special privilege as they're always a player and we don't want the illusion being broken around them.
        while (mindEnum.MoveNext(out var uid, out var mind, out var xform))
        {
            if (!mind.HasMind)
                continue;
            if (_ghostQuery.HasComponent(uid))
                continue;
            var mapOrNull = xform.MapUid;
            if (mapOrNull is null)
                continue;
            var map = mapOrNull.Value;
            if (!_controllerMaps.Contains(map))
                continue;

            var wc = _xformSys.GetWorldPosition(xform);
            var coords = WorldGen.WorldToChunkCoords(wc);
            var chunks = new GridPointsNearEnumerator(coords.Floored(), PlayerLoadRadius);
            var controller = _controllerQuery.GetComponent(map);

            while (chunks.MoveNext(out var chunkCoord))
            {
                anyChunksRequested = true;
                LoadRequestedChunk(chunkCoord.Value, map, controller, uid, ref loadedCount);
            }
        }

        var chunksUnloaded = 0;

        // Make sure these chunks get unloaded at the end of the tick.
        foreach (var loadedUid in _loadedChunksBuffer)
        {
            if (!_loadedQuery.TryGetComponent(loadedUid, out var loadedChunk) ||
                !TryComp<WorldChunkComponent>(loadedUid, out _))
            {
                continue;
            }

            if (loadedChunk.Loaders is null || loadedChunk.Loaders.Count == 0)
            {
                RemCompDeferred<LoadedChunkComponent>(loadedUid);
                chunksUnloaded++;
            }
        }

        if (chunksUnloaded > 0)
            _sawmill.Debug($"Queued {chunksUnloaded} chunks for unload.");

        if (!anyChunksRequested)
        {
            ProcessChunkEvictions();
            return;
        }

        if (loadedCount > 0)
        {
            var timeSpan = _gameTiming.RealTime - startTime;
            _sawmill.Debug($"Loaded {loadedCount} chunks in {timeSpan.TotalMilliseconds:N2}ms.");
        }

        ProcessChunkEvictions();
        // DS14-End
    }

    // DS14-Start: helper for allocation-light chunk loading.
    private void LoadRequestedChunk(Vector2i chunk, EntityUid map, WorldControllerComponent controller, EntityUid loader, ref int loadedCount)
    {
        var ent = GetOrCreateChunk(chunk, map, controller);
        if (ent is null)
            return;

        if (!_loadedQuery.TryGetComponent(ent.Value, out var loaded))
        {
            loaded = AddComp<LoadedChunkComponent>(ent.Value);
            loadedCount++;
        }

        loaded.Loaders ??= new List<EntityUid>(4);
        loaded.Loaders.Add(loader);
    }

    private void ProcessChunkEvictions()
    {
        var now = _gameTiming.RealTime;
        var query = EntityQueryEnumerator<ChunkEvictionComponent, WorldChunkComponent>();
        while (query.MoveNext(out var uid, out var evict, out _))
        {
            if (now < evict.EvictAt)
                continue;

            if (_loadedQuery.HasComponent(uid))
            {
                RemCompDeferred<ChunkEvictionComponent>(uid);
                continue;
            }

            QueueDel(uid);
        }
    }
    // DS14-End

    /// <summary>
    ///     Attempts to get a chunk, creating it if it doesn't exist.
    /// </summary>
    /// <param name="chunk">Chunk coordinates to get the chunk entity for.</param>
    /// <param name="map">Map the chunk is in.</param>
    /// <param name="controller">The controller this chunk belongs to.</param>
    /// <returns>A chunk, if available.</returns>
    [Pure]
    public EntityUid? GetOrCreateChunk(Vector2i chunk, EntityUid map, WorldControllerComponent? controller = null)
    {
        if (!Resolve(map, ref controller))
            throw new Exception($"Tried to use {ToPrettyString(map)} as a world map, without actually being one.");

        if (controller.Chunks.TryGetValue(chunk, out var ent))
            return ent;
        return CreateChunkEntity(chunk, map, controller);
    }

    /// <summary>
    ///     Constructs a new chunk entity, attaching it to the map.
    /// </summary>
    /// <param name="chunkCoords">The coordinates the new chunk should be initialized for.</param>
    /// <param name="map"></param>
    /// <param name="controller"></param>
    /// <returns></returns>
    private EntityUid CreateChunkEntity(Vector2i chunkCoords, EntityUid map, WorldControllerComponent controller)
    {
        var chunk = Spawn(controller.ChunkProto, MapCoordinates.Nullspace);
        StartupChunkEntity(chunk, chunkCoords, map, controller);
        _metaData.SetEntityName(chunk, $"Chunk {chunkCoords.X}/{chunkCoords.Y}");
        return chunk;
    }

    private void StartupChunkEntity(EntityUid chunk, Vector2i coords, EntityUid map,
        WorldControllerComponent controller)
    {
        if (!TryComp<WorldChunkComponent>(chunk, out var chunkComponent))
        {
            _sawmill.Error($"Chunk {ToPrettyString(chunk)} is missing WorldChunkComponent.");
            return;
        }

        ref var chunks = ref controller.Chunks;

        chunks[coords] = chunk; // Add this entity to chunk index.
        chunkComponent.Coordinates = coords;
        chunkComponent.Map = map;
        var ev = new WorldChunkAddedEvent(chunk, coords);
        RaiseLocalEvent(map, ref ev, broadcast: true);
    }
}

/// <summary>
///     A directed event fired when a chunk is initially set up in the world. The chunk is not loaded at this point.
/// </summary>
[ByRefEvent]
[PublicAPI]
public readonly record struct WorldChunkAddedEvent(EntityUid Chunk, Vector2i Coords);

/// <summary>
///     A directed event fired when a chunk is loaded into the world, i.e. a player or other world loader has entered vicinity.
/// </summary>
[ByRefEvent]
[PublicAPI]
public readonly record struct WorldChunkLoadedEvent(EntityUid Chunk, Vector2i Coords);

/// <summary>
///     A directed event fired when a chunk is unloaded from the world, i.e. no world loaders remain nearby.
/// </summary>
[ByRefEvent]
[PublicAPI]
public readonly record struct WorldChunkUnloadedEvent(EntityUid Chunk, Vector2i Coords);
