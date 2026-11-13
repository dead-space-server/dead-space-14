using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Content.Server.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Decals;
using Content.Server.DeadSpace.Lavaland.Components; //DS-14
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves; //DS-14
using Content.Server.Ghost.Roles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Shared.Atmos;
using Content.Shared.Decals;
using Content.Shared.Ghost;
using Content.Shared.Gravity;
using Content.Shared.Light.Components;
using Content.Shared.Mobs.Components; // DS14
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Layers;
using Content.Shared.Parallax.Biomes.Markers;
using Content.Shared.Tag;
using Microsoft.Extensions.ObjectPool;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Collections;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using ChunkIndicesEnumerator = Robust.Shared.Map.Enumerators.ChunkIndicesEnumerator;

namespace Content.Server.Parallax;

public sealed partial class BiomeSystem : SharedBiomeSystem
{
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly IConsoleHost _console = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!; //DS-14
    [Dependency] private readonly Robust.Shared.Timing.IGameTiming _timing = default!; //DS-14
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ShuttleSystem _shuttles = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    private EntityQuery<BiomeComponent> _biomeQuery;
    private EntityQuery<FixturesComponent> _fixturesQuery;
    private EntityQuery<GhostComponent> _ghostQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private readonly HashSet<EntityUid> _handledEntities = new();
    private const float DefaultLoadRange = 16f;
    private float _loadRange = DefaultLoadRange;
    private static readonly ProtoId<TagPrototype> AllowBiomeLoadingTag = "AllowBiomeLoading";

    private List<(Vector2i, Tile)> _tiles = new();

    private ObjectPool<HashSet<Vector2i>> _tilePool =
        new DefaultObjectPool<HashSet<Vector2i>>(new SetPolicy<Vector2i>(), 256);

    /// <summary>
    /// Load area for chunks containing tiles, decals etc.
    /// </summary>
    private Box2 _loadArea = new(-DefaultLoadRange, -DefaultLoadRange, DefaultLoadRange, DefaultLoadRange);

    /// <summary>
    /// Stores the chunks active for this tick temporarily.
    /// </summary>
    private readonly Dictionary<BiomeComponent, HashSet<Vector2i>> _activeChunks = new();

    private readonly Dictionary<BiomeComponent,
        Dictionary<string, HashSet<Vector2i>>> _markerChunks = new();

    // DS14-Start: cache marker layer prototype lookups for one biome update pass.
    private readonly Dictionary<ProtoId<BiomeMarkerLayerPrototype>, BiomeMarkerLayerPrototype> _markerLayerPrototypeCache = new();
    // DS14-End

    // DS14-End

    private readonly List<(Vector2i, Tile)> _unloadTilesBuffer = new(); //DS-14

    // DS-14 start
    private const int MaxBiomeChunksPerTick = 2;
    private const int MaxBiomeUnloadChunksPerTick = 3;
    private const int MaxBiomeMarkerChunksPerTick = 1;
    private const long ChunkBudgetMs = 6;
    private readonly Dictionary<EntityUid, Robust.Shared.Timing.GameTick> _spawnStamps = new();
    private const int HideDepth = 2;
    private const int SolidGridSize = ChunkSize + HideDepth * 2;
    private static readonly TimeSpan MobHiddenCheckInterval = TimeSpan.FromSeconds(0.5);
    private readonly Dictionary<EntityUid, Dictionary<Vector2i, string>> _hiddenWalls = new();
    private readonly Dictionary<string, bool> _solidPrototypes = new();
    private readonly bool[] _solidGrid = new bool[SolidGridSize * SolidGridSize];
    private readonly HashSet<(EntityUid Grid, Vector2i Tile)> _pendingReveals = new();
    private readonly List<(EntityUid Grid, Vector2i Tile)> _revealBuffer = new();
    private readonly List<Vector2i> _revealTiles = new();
    private readonly List<EntityUid> _deadHiddenGrids = new();
    private TimeSpan _nextMobHiddenCheck;
    private bool _suppressReveal;
    private const float UnloadMargin = 16f;
    private readonly Dictionary<BiomeComponent, HashSet<Vector2i>> _keepChunks = new();
    private long _chunkBudgetEnd;
    private bool _chunkWorkDone;
    private bool _unloadFirst;
    private readonly List<int> _loadOrder = new();
    private int _loadCursor;
    private readonly Dictionary<BiomeComponent, int> _viewerOffsets = new();
    private readonly Dictionary<int, int> _viewerBest = new();
    private readonly List<int> _viewerOrder = new();
    private readonly HashSet<int> _takenWork = new();
    private readonly HashSet<Entity<MobStateComponent>> _chunkMobs = new();
    private readonly HashSet<Vector2i> _mobTiles = new();
    private readonly Dictionary<Vector2i, string?> _scanEntityCache = new();
    private BiomeComponent? _scanCacheBiome;
    private Vector2i _scanCacheChunk;
    private int _markerChunksThisTick;
    private readonly Dictionary<BiomeComponent, List<Vector2>> _viewerPositions = new();
    private readonly Stack<List<Vector2>> _viewerListPool = new();
    private readonly List<BiomeWork> _biomeWork = new();
    private readonly List<ChunkWork> _chunkWork = new();
    private readonly List<ProtoId<BiomeMarkerLayerPrototype>> _forcedRelease = new();
    private readonly MarkerScan _markerScan = new();
    private const long MarkerScanBudgetMs = 4;

    private struct BiomeWork
    {
        public BiomeComponent Biome;
        public EntityUid GridUid;
        public MapGridComponent Grid;
    }

    private struct ChunkWork
    {
        public BiomeComponent Biome;
        public EntityUid GridUid;
        public MapGridComponent Grid;
        public Vector2i Chunk;
        public float Dist;
        public int Viewer;
        public bool MarkersOnly;
    }
    // DS-14 end

    public override void Initialize()
    {
        base.Initialize();
        Log.Level = LogLevel.Debug;
        _biomeQuery = GetEntityQuery<BiomeComponent>();
        _fixturesQuery = GetEntityQuery<FixturesComponent>();
        _ghostQuery = GetEntityQuery<GhostComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        SubscribeLocalEvent<BiomeComponent, MapInitEvent>(OnBiomeMapInit);
        SubscribeLocalEvent<FTLStartedEvent>(OnFTLStarted);
        SubscribeLocalEvent<ShuttleFlattenEvent>(OnShuttleFlatten);
        SubscribeLocalEvent<AnchorStateChangedEvent>(OnAnchorChanged); //DS-14
        Subs.CVar(_configManager, CVars.NetMaxUpdateRange, SetLoadRange, true);
        InitializeCommands();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(ProtoReload);
    }

    private void ProtoReload(PrototypesReloadedEventArgs obj)
    {
        if (!obj.ByType.TryGetValue(typeof(BiomeTemplatePrototype), out var reloads))
            return;

        var query = AllEntityQuery<BiomeComponent>();

        while (query.MoveNext(out var uid, out var biome))
        {
            if (biome.Template == null || !reloads.Modified.TryGetValue(biome.Template, out var proto))
                continue;

            SetTemplate(uid, biome, (BiomeTemplatePrototype)proto);
        }
    }

    private void SetLoadRange(float obj)
    {
        // Round it up
        _loadRange = MathF.Ceiling(obj / ChunkSize) * ChunkSize;
        _loadArea = new Box2(-_loadRange, -_loadRange, _loadRange, _loadRange);
    }

    private void OnBiomeMapInit(EntityUid uid, BiomeComponent component, MapInitEvent args)
    {
        if (component.Seed == -1)
        {
            SetSeed(uid, component, _random.Next());
        }

        if (_proto.Resolve(component.Template, out var biome))
            SetTemplate(uid, component, biome);

        var xform = Transform(uid);
        var mapId = xform.MapID;

        if (mapId != MapId.Nullspace && HasComp<MapGridComponent>(uid))
        {
            var setTiles = new List<(Vector2i Index, Tile tile)>();

            foreach (var grid in _mapSystem.GetAllGrids(mapId))
            {
                if (!_fixturesQuery.TryGetComponent(grid.Owner, out var fixtures))
                    continue;

                // Don't want shuttles flying around now do we.
                _shuttles.Disable(grid.Owner);
                var pTransform = _physics.GetPhysicsTransform(grid.Owner);

                foreach (var fixture in fixtures.Fixtures.Values)
                {
                    for (var i = 0; i < fixture.Shape.ChildCount; i++)
                    {
                        var aabb = fixture.Shape.ComputeAABB(pTransform, i);

                        setTiles.Clear();
                        ReserveTiles(uid, aabb, setTiles);
                    }
                }
            }
        }
    }

    public void SetEnabled(Entity<BiomeComponent?> ent, bool enabled = true)
    {
        if (!Resolve(ent, ref ent.Comp) || ent.Comp.Enabled == enabled)
            return;

        ent.Comp.Enabled = enabled;
        Dirty(ent, ent.Comp);
    }

    public void SetSeed(EntityUid uid, BiomeComponent component, int seed, bool dirty = true)
    {
        component.Seed = seed;
        ClearBiomeNoiseCache(); //DS-14

        if (dirty)
            Dirty(uid, component);
    }

    public void ClearTemplate(EntityUid uid, BiomeComponent component, bool dirty = true)
    {
        component.Layers.Clear();
        component.Template = null;
        ClearBiomeNoiseCache(); //DS-14

        if (dirty)
            Dirty(uid, component);
    }

    /// <summary>
    /// Sets the <see cref="BiomeComponent.Template"/> and refreshes layers.
    /// </summary>
    public void SetTemplate(EntityUid uid, BiomeComponent component, BiomeTemplatePrototype template, bool dirty = true)
    {
        component.Layers.Clear();
        ClearBiomeNoiseCache(); //DS-14
        component.Template = template.ID;

        foreach (var layer in template.Layers)
        {
            component.Layers.Add(layer);
        }

        if (dirty)
            Dirty(uid, component);
    }

    /// <summary>
    /// Adds the specified layer at the specified marker if it exists.
    /// </summary>
    public void AddLayer(EntityUid uid, BiomeComponent component, string id, IBiomeLayer addedLayer, int seedOffset = 0)
    {
        for (var i = 0; i < component.Layers.Count; i++)
        {
            var layer = component.Layers[i];

            if (layer is not BiomeDummyLayer dummy || dummy.ID != id)
                continue;

            addedLayer.Noise.SetSeed(addedLayer.Noise.GetSeed() + seedOffset);
            ClearBiomeNoiseCache(); //DS-14
            component.Layers.Insert(i, addedLayer);
            break;
        }

        Dirty(uid, component);
    }

    public void AddMarkerLayer(EntityUid uid, BiomeComponent component, string marker)
    {
        component.MarkerLayers.Add(marker);
        Dirty(uid, component);
    }

    /// <summary>
    /// Adds the specified template at the specified marker if it exists, withour overriding every layer.
    /// </summary>
    public void AddTemplate(EntityUid uid, BiomeComponent component, string id, BiomeTemplatePrototype template, int seedOffset = 0)
    {
        for (var i = 0; i < component.Layers.Count; i++)
        {
            var layer = component.Layers[i];

            if (layer is not BiomeDummyLayer dummy || dummy.ID != id)
                continue;

            for (var j = template.Layers.Count - 1; j >= 0; j--)
            {
                var addedLayer = template.Layers[j];
                addedLayer.Noise.SetSeed(addedLayer.Noise.GetSeed() + seedOffset);
                ClearBiomeNoiseCache(); //DS-14
                component.Layers.Insert(i, addedLayer);
            }

            break;
        }

        Dirty(uid, component);
    }

    private void OnFTLStarted(ref FTLStartedEvent ev)
    {
        var targetMap = _transform.ToMapCoordinates(ev.TargetCoordinates);
        var targetMapUid = _mapSystem.GetMapOrInvalid(targetMap.MapId);

        if (!TryComp<BiomeComponent>(targetMapUid, out var biome))
            return;

        var preloadArea = new Vector2(32f, 32f);
        var targetArea = new Box2(targetMap.Position - preloadArea, targetMap.Position + preloadArea);
        Preload(targetMapUid, biome, targetArea);
    }

    private void OnShuttleFlatten(ref ShuttleFlattenEvent ev)
    {
        if (!TryComp<BiomeComponent>(ev.MapUid, out var biome) ||
            !TryComp<MapGridComponent>(ev.MapUid, out var grid))
        {
            return;
        }

        var tiles = new List<(Vector2i Index, Tile Tile)>();

        foreach (var aabb in ev.AABBs)
        {
            for (var x = Math.Floor(aabb.Left); x <= Math.Ceiling(aabb.Right); x++)
            {
                for (var y = Math.Floor(aabb.Bottom); y <= Math.Ceiling(aabb.Top); y++)
                {
                    var index = new Vector2i((int)x, (int)y);
                    var chunk = SharedMapSystem.GetChunkIndices(index, ChunkSize);

                    var mod = biome.ModifiedTiles.GetOrNew(chunk * ChunkSize);

                    if (!mod.Add(index) || !TryGetBiomeTile(index, biome.Layers, biome.Seed, (ev.MapUid, grid), out var tile))
                        continue;

                    // If we flag it as modified then the tile is never set so need to do it ourselves.
                    tiles.Add((index, tile.Value));
                }
            }
        }

        _mapSystem.SetTiles(ev.MapUid, grid, tiles);

        // DS-14 start
        foreach (var aabb in ev.AABBs)
        {
            OpenHiddenArea(ev.MapUid, TilesIn(new Box2(
                MathF.Floor(aabb.Left),
                MathF.Floor(aabb.Bottom),
                MathF.Ceiling(aabb.Right) + 1,
                MathF.Ceiling(aabb.Top) + 1)));
        }
        // DS-14 end
    }

    /// <summary>
    /// Preloads biome for the specified area.
    /// </summary>
    public void Preload(EntityUid uid, BiomeComponent component, Box2 area)
    {
        var markers = component.MarkerLayers;
        var goobers = _markerChunks.GetOrNew(component);

        foreach (var layer in markers)
        {
            var proto = ProtoManager.Index(layer);
            var enumerator = new ChunkIndicesEnumerator(area, proto.Size);

            while (enumerator.MoveNext(out var chunk))
            {
                var chunkOrigin = chunk * proto.Size;
                if (!AreaIntersectsBiomeBounds(chunkOrigin.Value, proto.Size, component.Bounds))
                    continue;

                var layerChunks = goobers.GetOrNew(proto.ID);
                layerChunks.Add(chunkOrigin.Value);
            }
        }
    }

    private bool CanLoad(EntityUid uid)
    {
        return !_ghostQuery.HasComp(uid) || _tags.HasTag(uid, AllowBiomeLoadingTag);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var biomes = AllEntityQuery<BiomeComponent>();

        while (biomes.MoveNext(out var biome))
        {
            if (biome.LifeStage < ComponentLifeStage.Running)
                continue;

            _activeChunks.Add(biome, _tilePool.Get());
            _keepChunks.Add(biome, _tilePool.Get()); //DS-14
            _markerChunks.GetOrNew(biome);
        }

        // Get chunks in range
        foreach (var pSession in Filter.GetAllPlayers(_playerManager))
        {
            if (_xformQuery.TryGetComponent(pSession.AttachedEntity, out var xform) &&
                _handledEntities.Add(pSession.AttachedEntity.Value) &&
                 _biomeQuery.TryGetComponent(xform.MapUid, out var biome) &&
                biome.Enabled &&
                CanLoad(pSession.AttachedEntity.Value))
            {
                var worldPos = _transform.GetWorldPosition(xform);
                AddChunksInRange(biome, worldPos);
                NoteViewer(biome, worldPos); //DS-14
                AddKeepChunksInRange(biome, worldPos); //DS-14

                foreach (var layer in biome.MarkerLayers)
                {
                    var layerProto = GetMarkerLayerPrototype(layer); // DS14 Edit
                    AddMarkerChunksInRange(biome, worldPos, layerProto);
                }
            }

            foreach (var viewer in pSession.ViewSubscriptions)
            {
                if (!_handledEntities.Add(viewer) ||
                    !_xformQuery.TryGetComponent(viewer, out xform) ||
                    !_biomeQuery.TryGetComponent(xform.MapUid, out biome) ||
                    !biome.Enabled ||
                    !CanLoad(viewer))
                {
                    continue;
                }

                var worldPos = _transform.GetWorldPosition(xform);
                AddChunksInRange(biome, worldPos);
                NoteViewer(biome, worldPos); //DS-14
                AddKeepChunksInRange(biome, worldPos); //DS-14

                foreach (var layer in biome.MarkerLayers)
                {
                    var layerProto = GetMarkerLayerPrototype(layer); // DS14 Edit
                    AddMarkerChunksInRange(biome, worldPos, layerProto);
                }
            }
        }

        // DS-14 start
        _biomeWork.Clear();
        _markerChunksThisTick = 0;
        var loadBiomes = AllEntityQuery<BiomeComponent, MapGridComponent>();

        while (loadBiomes.MoveNext(out var gridUid, out var biome, out var grid))
        {
            if (biome.LifeStage < ComponentLifeStage.Running)
                continue;

            if (!biome.Enabled)
                continue;

            _biomeWork.Add(new BiomeWork
            {
                Biome = biome,
                GridUid = gridUid,
                Grid = grid,
            });
        }

        BuildBudgetedMarkerChunk();
        _chunkBudgetEnd = Stopwatch.GetTimestamp() + Stopwatch.Frequency * ChunkBudgetMs / 1000;
        _chunkWorkDone = false;
        _unloadFirst = !_unloadFirst;
        if (_unloadFirst)
        {
            UnloadBudgeted();
            LoadBudgeted();
        }
        else
        {
            LoadBudgeted();
            UnloadBudgeted();
        }

        ProcessHiddenWalls();
        // DS-14 end

        _handledEntities.Clear();

        foreach (var tiles in _activeChunks.Values)
        {
            _tilePool.Return(tiles);
        }

        _activeChunks.Clear();
        // DS-14 start
        foreach (var tiles in _keepChunks.Values)
        {
            _tilePool.Return(tiles);
        }

        _keepChunks.Clear();
        // DS-14 end
        _markerChunks.Clear();
        _markerLayerPrototypeCache.Clear(); // DS14 Edit
        ReturnViewers(); //DS-14
    }

    private void AddChunksInRange(BiomeComponent biome, Vector2 worldPos)
    {
        var enumerator = new ChunkIndicesEnumerator(_loadArea.Translated(worldPos), ChunkSize);

        while (enumerator.MoveNext(out var chunkOrigin))
        {
            var origin = chunkOrigin.Value * ChunkSize;
            if (!AreaIntersectsBiomeBounds(origin, ChunkSize, biome.Bounds))
                continue;

            _activeChunks[biome].Add(origin);
        }
    }

    private void AddMarkerChunksInRange(BiomeComponent biome, Vector2 worldPos, IBiomeMarkerLayer layer)
    {
        // Offset the load area so it's centralised.
        var loadArea = new Box2(0, 0, layer.Size, layer.Size);
        var halfLayer = new Vector2(layer.Size / 2f);

        var enumerator = new ChunkIndicesEnumerator(loadArea.Translated(worldPos - halfLayer), layer.Size);
        var lay = _markerChunks[biome].GetOrNew(layer.ID); // DS14 Edit: avoid repeated dictionary lookup per marker chunk.

        while (enumerator.MoveNext(out var chunkOrigin))
        {
            var origin = chunkOrigin.Value * layer.Size;
            if (!AreaIntersectsBiomeBounds(origin, layer.Size, biome.Bounds))
                continue;

            lay.Add(origin);
        }
    }

    // DS14-Start: cache marker layer prototype lookups for one update pass.
    private BiomeMarkerLayerPrototype GetMarkerLayerPrototype(ProtoId<BiomeMarkerLayerPrototype> layer)
    {
        if (_markerLayerPrototypeCache.TryGetValue(layer, out var layerProto))
            return layerProto;

        layerProto = ProtoManager.Index(layer);
        _markerLayerPrototypeCache[layer] = layerProto;
        return layerProto;
    }
    // DS14-End

    private static bool AreaIntersectsBiomeBounds(Vector2i origin, int size, Box2i? bounds)
    {
        if (bounds == null)
            return true;

        var biomeBounds = bounds.Value;
        return origin.X < biomeBounds.Right &&
               origin.X + size > biomeBounds.Left &&
               origin.Y < biomeBounds.Top &&
               origin.Y + size > biomeBounds.Bottom;
    }

    private static bool TileInsideBiomeBounds(Vector2i index, Box2i? bounds)
    {
        if (bounds == null)
            return true;

        var biomeBounds = bounds.Value;
        return index.X >= biomeBounds.Left &&
               index.X < biomeBounds.Right &&
               index.Y >= biomeBounds.Bottom &&
               index.Y < biomeBounds.Top;
    }

    #region Load

    // DS-14 start
    private void NoteViewer(BiomeComponent biome, Vector2 worldPos)
    {
        if (!_viewerPositions.TryGetValue(biome, out var viewers))
        {
            viewers = _viewerListPool.Count > 0 ? _viewerListPool.Pop() : new List<Vector2>();
            _viewerPositions.Add(biome, viewers);
        }

        viewers.Add(worldPos);
    }

    private bool IsUntouched(EntityUid uid)
    {
        if (!_spawnStamps.Remove(uid, out var stamp) ||
            !TryComp(uid, out MetaDataComponent? meta) ||
            meta.EntityLastModifiedTick > stamp)
        {
            return false;
        }

        return true;
    }

    public bool IsHiddenWall(EntityUid gridUid, Vector2i indices)
    {
        return _hiddenWalls.TryGetValue(gridUid, out var hidden) && hidden.ContainsKey(indices);
    }

    private Dictionary<Vector2i, string>? GetHiddenWalls(EntityUid gridUid)
    {
        if (_hiddenWalls.TryGetValue(gridUid, out var hidden))
            return hidden;

        if (!HasComp<LavalandMapComponent>(gridUid) && !HasComp<LavalandChromiteCavesComponent>(gridUid))
            return null;

        hidden = new Dictionary<Vector2i, string>();
        _hiddenWalls[gridUid] = hidden;
        return hidden;
    }

    private bool IsSolidPrototype(string id)
    {
        if (_solidPrototypes.TryGetValue(id, out var solid))
            return solid;

        solid = ProtoManager.TryIndex<EntityPrototype>(id, out var proto) &&
                proto.Components.ContainsKey("Airtight") &&
                proto.Components.ContainsKey("Occluder") &&
                !proto.Components.ContainsKey("MobState");
        _solidPrototypes[id] = solid;
        return solid;
    }

    private bool HasAnchoredSolid(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, indices);
        while (anchored.MoveNext(out var uid))
        {
            if (MetaData(uid.Value).EntityPrototype is { } proto && IsSolidPrototype(proto.ID))
                return true;
        }

        return false;
    }

    private void BuildSolidGrid(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i chunk,
        HashSet<Vector2i> modified,
        Dictionary<Vector2i, string> hidden)
    {
        for (var x = -HideDepth; x < ChunkSize + HideDepth; x++)
        {
            for (var y = -HideDepth; y < ChunkSize + HideDepth; y++)
            {
                var indices = new Vector2i(chunk.X + x, chunk.Y + y);
                _solidGrid[(x + HideDepth) + (y + HideDepth) * SolidGridSize] = IsSolidAt(component, gridUid, grid, chunk, indices, modified, hidden);
            }
        }
    }

    private bool IsSolidAt(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i chunk,
        Vector2i indices,
        HashSet<Vector2i> modified,
        Dictionary<Vector2i, string> hidden)
    {
        if (!TileInsideBiomeBounds(indices, component.Bounds))
            return true;

        var origin = SharedMapSystem.GetChunkIndices(indices, ChunkSize) * ChunkSize;
        if (origin == chunk)
        {
            if (_mobTiles.Contains(indices))
                return false;

            if (modified.Contains(indices) || _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, indices).MoveNext(out _))
                return HasAnchoredSolid(gridUid, grid, indices);
        }
        else if (component.LoadedChunks.Contains(origin))
        {
            return hidden.ContainsKey(indices) || HasAnchoredSolid(gridUid, grid, indices);
        }
        else if (component.ModifiedTiles.TryGetValue(origin, out var otherModified) && otherModified.Contains(indices))
        {
            return false;
        }

        return TryGetEntity(indices, component, (gridUid, grid), out var proto) && IsSolidPrototype(proto);
    }

    private bool IsEnclosed(Vector2i indices, Vector2i chunk)
    {
        var localX = indices.X - chunk.X + HideDepth;
        var localY = indices.Y - chunk.Y + HideDepth;
        for (var x = -HideDepth; x <= HideDepth; x++)
        {
            for (var y = -HideDepth; y <= HideDepth; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                if (!_solidGrid[(localX + x) + (localY + y) * SolidGridSize])
                    return false;
            }
        }

        return true;
    }

    private void SpawnBiomeEntity(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i indices,
        string prototype,
        Dictionary<EntityUid, Vector2i> loadedEntities)
    {
        // TODO: Fix non-anchored ents spawning.
        // Just track loaded chunks for now.
        var ent = Spawn(prototype, _mapSystem.GridTileToLocal(gridUid, grid, indices));

        // At least for now unless we do lookups or smth, only work with anchoring.
        // DS14: never a creature - anchored it stands frozen on its tile forever (the chromite caves argocytes).
        if (_xformQuery.TryGetComponent(ent, out var xform) && !xform.Anchored && !HasComp<MobStateComponent>(ent))
        {
            _transform.AnchorEntity((ent, xform), (gridUid, grid), indices);
        }

        loadedEntities.Add(ent, indices);
        _spawnStamps[ent] = MetaData(ent).EntityLastModifiedTick;
    }

    private void OnAnchorChanged(ref AnchorStateChangedEvent args)
    {
        if (_suppressReveal || args.Anchored || args.Transform.GridUid is not { } gridUid ||
            !_hiddenWalls.TryGetValue(gridUid, out var hidden) || hidden.Count == 0 ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return;
        }

        _pendingReveals.Add((gridUid, _mapSystem.LocalToTile(gridUid, grid, args.Transform.Coordinates)));
    }

    private void OpenAround(EntityUid gridUid, Vector2i indices, bool openCenter)
    {
        if (!_hiddenWalls.TryGetValue(gridUid, out var hidden) || hidden.Count == 0 ||
            !TryComp<BiomeComponent>(gridUid, out var biome) ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return;
        }

        if (openCenter)
            hidden.Remove(indices);

        for (var x = -HideDepth; x <= HideDepth; x++)
        {
            for (var y = -HideDepth; y <= HideDepth; y++)
            {
                var tile = new Vector2i(indices.X + x, indices.Y + y);
                if (!hidden.Remove(tile, out var proto))
                    continue;

                var origin = SharedMapSystem.GetChunkIndices(tile, ChunkSize) * ChunkSize;
                if (!biome.LoadedEntities.TryGetValue(origin, out var loaded))
                    continue;

                SpawnBiomeEntity(gridUid, grid, tile, proto, loaded);
            }
        }
    }

    private void ProcessHiddenWalls()
    {
        if (_hiddenWalls.Count == 0)
            return;

        foreach (var gridUid in _hiddenWalls.Keys)
        {
            if (TerminatingOrDeleted(gridUid))
                _deadHiddenGrids.Add(gridUid);
        }

        foreach (var gridUid in _deadHiddenGrids)
        {
            _hiddenWalls.Remove(gridUid);
        }

        _deadHiddenGrids.Clear();

        if (_pendingReveals.Count > 0)
        {
            _revealBuffer.AddRange(_pendingReveals);
            _pendingReveals.Clear();
            foreach (var (gridUid, tile) in _revealBuffer)
            {
                if (TryComp<MapGridComponent>(gridUid, out var grid) && !HasAnchoredSolid(gridUid, grid, tile))
                    OpenAround(gridUid, tile, false);
            }

            _revealBuffer.Clear();
        }

        foreach (var work in _biomeWork)
        {
            if (!_hiddenWalls.TryGetValue(work.GridUid, out var hidden) || hidden.Count == 0 ||
                !_viewerPositions.TryGetValue(work.Biome, out var viewers))
            {
                continue;
            }

            foreach (var viewer in viewers)
            {
                var tile = _mapSystem.WorldToTile(work.GridUid, work.Grid, viewer);
                if (HiddenNear(hidden, tile))
                    OpenAround(work.GridUid, tile, true);
            }
        }

        var now = _timing.CurTime;
        if (now < _nextMobHiddenCheck)
            return;

        _nextMobHiddenCheck = now + MobHiddenCheckInterval;
        var mobs = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (mobs.MoveNext(out _, out var xform))
        {
            if (xform.GridUid is not { } gridUid ||
                !_hiddenWalls.TryGetValue(gridUid, out var hidden) || hidden.Count == 0 ||
                !TryComp<MapGridComponent>(gridUid, out var grid))
            {
                continue;
            }

            var tile = _mapSystem.LocalToTile(gridUid, grid, xform.Coordinates);
            if (HiddenNear(hidden, tile))
                OpenAround(gridUid, tile, true);
        }
    }

    private static bool HiddenNear(Dictionary<Vector2i, string> hidden, Vector2i tile)
    {
        for (var x = -HideDepth; x <= HideDepth; x++)
        {
            for (var y = -HideDepth; y <= HideDepth; y++)
            {
                if (hidden.ContainsKey(new Vector2i(tile.X + x, tile.Y + y)))
                    return true;
            }
        }

        return false;
    }

    private void OpenHiddenArea(EntityUid gridUid, IEnumerable<Vector2i> tiles)
    {
        if (!_hiddenWalls.TryGetValue(gridUid, out var hidden) || hidden.Count == 0)
            return;

        _revealTiles.Clear();
        foreach (var tile in tiles)
        {
            hidden.Remove(tile);
            _revealTiles.Add(tile);
        }

        foreach (var tile in _revealTiles)
        {
            OpenAround(gridUid, tile, false);
        }

        _revealTiles.Clear();
    }

    private void AddKeepChunksInRange(BiomeComponent biome, Vector2 worldPos)
    {
        var range = _loadRange + UnloadMargin;
        var area = new Box2(-range, -range, range, range).Translated(worldPos);
        var enumerator = new ChunkIndicesEnumerator(area, ChunkSize);
        var keep = _keepChunks[biome];

        while (enumerator.MoveNext(out var chunkOrigin))
        {
            keep.Add(chunkOrigin.Value * ChunkSize);
        }
    }

    private void CollectMobTiles(EntityUid gridUid, MapGridComponent grid, Vector2i chunk)
    {
        _mobTiles.Clear();
        _chunkMobs.Clear();
        var bounds = new Box2(chunk, chunk + new Vector2i(ChunkSize, ChunkSize));
        _lookup.GetLocalEntitiesIntersecting(gridUid, bounds, _chunkMobs, LookupFlags.Dynamic);

        foreach (var mob in _chunkMobs)
        {
            if (!_xformQuery.TryGetComponent(mob, out var xform) || xform.GridUid != gridUid)
                continue;

            var tile = _mapSystem.LocalToTile(gridUid, grid, xform.Coordinates);
            if (tile.X >= chunk.X && tile.X < chunk.X + ChunkSize && tile.Y >= chunk.Y && tile.Y < chunk.Y + ChunkSize)
                _mobTiles.Add(tile);
        }

        _chunkMobs.Clear();
    }

    private void ReturnViewers()
    {
        foreach (var viewers in _viewerPositions.Values)
        {
            viewers.Clear();
            _viewerListPool.Push(viewers);
        }

        _viewerPositions.Clear();
    }

    private float DistanceSq(BiomeComponent biome, Vector2 center)
    {
        return DistanceSq(biome, center, out _);
    }

    private float DistanceSq(BiomeComponent biome, Vector2 center, out int nearest)
    {
        nearest = -1;
        if (!_viewerPositions.TryGetValue(biome, out var viewers) || viewers.Count == 0)
            return float.MaxValue;

        var best = float.MaxValue;
        for (var i = 0; i < viewers.Count; i++)
        {
            var delta = viewers[i] - center;
            var dist = delta.X * delta.X + delta.Y * delta.Y;
            if (dist >= best)
                continue;

            best = dist;
            nearest = i;
        }

        return best;
    }

    private bool MarkersReady(BiomeComponent component, Vector2i chunk)
    {
        if (!_markerChunks.TryGetValue(component, out var markers))
            return true;

        foreach (var layer in component.MarkerLayers)
        {
            var proto = GetMarkerLayerPrototype(layer);
            var origin = SharedMapSystem.GetChunkIndices(chunk, proto.Size) * proto.Size;
            if (!markers.TryGetValue(proto.ID, out var queued) || !queued.Contains(origin))
                continue;

            if (!component.LoadedMarkers.TryGetValue(proto.ID, out var loaded) || !loaded.Contains(origin))
                return false;
        }

        return true;
    }

    private bool TryNearestUnfinished(BiomeComponent biome, out Vector2i chunk)
    {
        var found = false;
        chunk = default;
        var dist = float.MaxValue;
        foreach (var candidate in _activeChunks[biome])
        {
            if (biome.LoadedChunks.Contains(candidate))
                continue;

            if (MarkersReady(biome, candidate))
                continue;

            var candidateDist = DistanceSq(biome, candidate + new Vector2(ChunkSize / 2f, ChunkSize / 2f));
            if (found && candidateDist >= dist)
                continue;

            found = true;
            dist = candidateDist;
            chunk = candidate;
        }

        return found;
    }

    private void BuildBudgetedMarkerChunk()
    {
        if (_markerChunksThisTick >= MaxBiomeMarkerChunksPerTick)
            return;

        if (_markerScan.Active)
        {
            StepMarkerScan();
            _markerChunksThisTick++;
            return;
        }

        var found = false;
        var coversNearest = false;
        var bestWork = default(BiomeWork);
        var bestLayer = string.Empty;
        var bestChunk = default(Vector2i);
        var bestIdx = 0;
        var bestDist = float.MaxValue;

        foreach (var work in _biomeWork)
        {
            if (!_markerChunks.TryGetValue(work.Biome, out var markers))
                continue;

            var hasTerrain = TryNearestUnfinished(work.Biome, out var terrain);
            var idx = 0;
            foreach (var (layer, chunks) in markers)
            {
                idx++;
                if (chunks.Count == 0)
                    continue;

                var needed = default(Vector2i);
                var restrict = false;
                if (hasTerrain)
                {
                    var proto = GetMarkerLayerPrototype(layer);
                    needed = SharedMapSystem.GetChunkIndices(terrain, proto.Size) * proto.Size;
                    restrict = true;
                }

                var hasLoaded = work.Biome.LoadedMarkers.TryGetValue(layer, out var loaded);
                foreach (var chunk in chunks)
                {
                    if (hasLoaded && loaded!.Contains(chunk)) //DS-14
                        continue;

                    if (restrict && chunk != needed)
                        continue;

                    var proto = GetMarkerLayerPrototype(layer);
                    var center = chunk + new Vector2(proto.Size / 2f, proto.Size / 2f);
                    var dist = DistanceSq(work.Biome, center);
                    if (found && restrict == coversNearest && dist >= bestDist)
                        continue;

                    if (found && !restrict && coversNearest)
                        continue;

                    found = true;
                    coversNearest = restrict;
                    bestDist = dist;
                    bestWork = work;
                    bestLayer = layer;
                    bestChunk = chunk;
                    bestIdx = idx;
                }
            }
        }

        if (!found)
            return;

        var biome = bestWork.Biome!;
        var grid = bestWork.Grid!;
        var forced = biome.ForcedMarkerLayers.Contains(bestLayer);
        StartMarkerScan(biome, bestWork.GridUid, grid, biome.Seed, bestLayer, bestChunk, bestIdx, forced);
        if (!_markerScan.Active)
            return;

        StepMarkerScan();
        _markerChunksThisTick++;
    }

    private void StartMarkerScan(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        int seed,
        string layer,
        Vector2i chunk,
        int localIdx,
        bool forced)
    {
        if (component.LoadedMarkers.TryGetValue(layer, out var existingChunks) && existingChunks.Contains(chunk))
            return;

        var layerProto = ProtoManager.Index<BiomeMarkerLayerPrototype>(layer);
        var buffer = (int)(layerProto.Radius / 2f);
        var bounds = new Box2i(chunk + buffer, chunk + layerProto.Size - buffer);
        var count = (int)(bounds.Area / (layerProto.Radius * layerProto.Radius));
        count = Math.Min(count, layerProto.MaxCount);
        if (count <= 0)
            return;

        if (_scanCacheBiome != component || _scanCacheChunk != chunk)
        {
            _scanEntityCache.Clear();
            _scanCacheBiome = component;
            _scanCacheChunk = chunk;
        }

        var scan = _markerScan;
        scan.Active = true;
        scan.Biome = component;
        scan.GridUid = gridUid;
        scan.Grid = grid;
        scan.Layer = layer;
        scan.Chunk = chunk;
        scan.LocalIdx = localIdx;
        scan.Seed = seed;
        scan.Forced = forced;
        scan.Proto = layerProto;
        scan.Bounds = bounds;
        scan.Count = count;
        scan.Rand = new Random(seed + chunk.X * ChunkSize + chunk.Y + localIdx);
        scan.X = bounds.Left;
        scan.Y = bounds.Bottom;
        scan.Placing = false;
        scan.GroupIndex = 0;
        scan.GroupOpen = false;
        scan.Remaining.Clear();
        scan.Entities.Clear();
        scan.Masks.Clear();
        scan.Frontier.Clear();
        scan.SpawnSet.Clear();
    }

    private void StepMarkerScan()
    {
        var scan = _markerScan;
        if (!scan.Active)
            return;

        if (Deleted(scan.GridUid))
        {
            scan.Active = false;
            scan.Placing = false;
            _scanEntityCache.Clear();
            _scanCacheBiome = null;
            return;
        }

        var budget = Stopwatch.GetTimestamp() + Stopwatch.Frequency * MarkerScanBudgetMs / 1000;
        if (scan.Placing)
        {
            PlaceMarkerScan(budget);
            return;
        }

        var gridUid = scan.GridUid;
        var grid = scan.Grid;
        var biome = scan.Biome;
        var layerProto = scan.Proto;
        var forced = scan.Forced;

        while (scan.X < scan.Bounds.Right)
        {
            while (scan.Y < scan.Bounds.Top)
            {
                var node = new Vector2i(scan.X, scan.Y);
                scan.Y++;

                var enumerator = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, node);
                enumerator.MoveNext(out var existing);

                if (!forced && existing != null)
                    continue;

                if (!_scanEntityCache.TryGetValue(node, out var proto))
                {
                    TryGetEntity(node, biome, (gridUid, grid), out proto);
                    _scanEntityCache[node] = proto;
                }

                if (layerProto.EntityMask.Count > 0 &&
                    (proto == null ||
                     !layerProto.EntityMask.ContainsKey(proto)))
                {
                    continue;
                }

                if (proto != null && layerProto.Prototype != null)
                    continue;

                scan.Remaining.Add(node);
                scan.Entities.Add(node, existing);
                scan.Masks.Add(node, proto);

                if (Stopwatch.GetTimestamp() >= budget)
                    return;
            }

            scan.Y = scan.Bounds.Bottom;
            scan.X++;
            if (Stopwatch.GetTimestamp() >= budget)
                return;
        }

        scan.Placing = true;
        scan.GroupIndex = 0;
        scan.GroupOpen = false;
        scan.Frontier.Clear();
        scan.SpawnSet.Clear();
        PlaceMarkerScan(budget);
    }

    private void PlaceMarkerScan(long budget)
    {
        var scan = _markerScan;
        var rand = scan.Rand;
        var layerProto = scan.Proto;

        while (scan.GroupIndex < scan.Count)
        {
            if (!scan.GroupOpen)
            {
                scan.GroupSize = rand.Next(layerProto.MinGroupSize, layerProto.MaxGroupSize + 1);
                scan.GroupOpen = true;
                scan.Frontier.Clear();
            }

            while (scan.GroupSize > 0 && (scan.Remaining.Count > 0 || scan.Frontier.Count > 0))
            {
                if (scan.Frontier.Count == 0)
                {
                    if (scan.Remaining.Count == 0)
                        break;

                    scan.Frontier.Add(rand.PickAndTake(scan.Remaining));
                }

                var frontierIndex = rand.Next(scan.Frontier.Count);
                var node = scan.Frontier[frontierIndex];
                scan.Frontier.RemoveAt(frontierIndex);
                scan.Remaining.Remove(node);

                for (var x = -1; x <= 1; x++)
                {
                    for (var y = -1; y <= 1; y++)
                    {
                        var neighbor = new Vector2i(node.X + x, node.Y + y);
                        if (scan.Frontier.Contains(neighbor) || !scan.Remaining.Contains(neighbor))
                            continue;

                        scan.Frontier.Add(neighbor);
                    }
                }

                scan.SpawnSet.Add(node, scan.Masks[node]);
                scan.GroupSize--;

                if (scan.Entities.TryGetValue(node, out var existing))
                {
                    _suppressReveal = true;
                    Del(existing);
                    _suppressReveal = false;
                }

                if (Stopwatch.GetTimestamp() >= budget)
                    return;
            }

            scan.GroupIndex++;
            scan.GroupOpen = false;
        }

        RegisterMarkerScan();
    }

    private void RegisterMarkerScan()
    {
        var scan = _markerScan;
        var component = scan.Biome;
        var layer = scan.Layer;
        var pending = new Dictionary<Vector2i, Dictionary<string, List<Vector2i>>>();

        foreach (var node in scan.SpawnSet.Keys)
        {
            if (!TileInsideBiomeBounds(node, component.Bounds))
                continue;

            var chunkOrigin = SharedMapSystem.GetChunkIndices(node, ChunkSize) * ChunkSize;
            if (!pending.TryGetValue(chunkOrigin, out var pendingMarkers))
            {
                pendingMarkers = new Dictionary<string, List<Vector2i>>();
                pending[chunkOrigin] = pendingMarkers;
            }

            if (!pendingMarkers.TryGetValue(layer, out var layerMarkers))
            {
                layerMarkers = new List<Vector2i>();
                pendingMarkers[layer] = layerMarkers;
            }

            layerMarkers.Add(node);
        }

        var loadedMarkers = component.LoadedMarkers;
        if (!loadedMarkers.TryGetValue(layer, out var mobChunks))
        {
            mobChunks = new HashSet<Vector2i>();
            loadedMarkers[layer] = mobChunks;
        }

        mobChunks.Add(scan.Chunk);

        foreach (var (chunkOrigin, layers) in pending)
        {
            if (!component.PendingMarkers.TryGetValue(chunkOrigin, out var lockMarkers))
            {
                lockMarkers = new Dictionary<string, List<Vector2i>>();
                component.PendingMarkers[chunkOrigin] = lockMarkers;
            }

            foreach (var (lockLayer, nodes) in layers)
                lockMarkers[lockLayer] = nodes;
        }

        scan.Active = false;
        scan.Placing = false;
        scan.Remaining.Clear();
        scan.Entities.Clear();
        scan.Masks.Clear();
        scan.Frontier.Clear();
        scan.SpawnSet.Clear();
        ReleaseFinishedForced(component);
    }

    private sealed class MarkerScan
    {
        public bool Active;
        public BiomeComponent Biome = default!;
        public EntityUid GridUid;
        public MapGridComponent Grid = default!;
        public string Layer = "";
        public Vector2i Chunk;
        public int LocalIdx;
        public int Seed;
        public bool Forced;
        public BiomeMarkerLayerPrototype Proto = default!;
        public Box2i Bounds;
        public int Count;
        public Random Rand = default!;
        public int X;
        public int Y;
        public bool Placing;
        public int GroupIndex;
        public int GroupSize;
        public bool GroupOpen;
        public readonly List<Vector2i> Frontier = new();
        public readonly Dictionary<Vector2i, string?> SpawnSet = new();
        public readonly HashSet<Vector2i> Remaining = new();
        public readonly Dictionary<Vector2i, EntityUid?> Entities = new();
        public readonly Dictionary<Vector2i, string?> Masks = new();
    }

    private void ReleaseFinishedForced(BiomeComponent component)
    {
        if (component.ForcedMarkerLayers.Count == 0)
            return;

        _forcedRelease.Clear();
        foreach (var layer in component.ForcedMarkerLayers)
        {
            if (!_markerChunks.TryGetValue(component, out var markers) ||
                !markers.TryGetValue(layer, out var chunks))
            {
                _forcedRelease.Add(layer);
                continue;
            }

            if (!component.LoadedMarkers.TryGetValue(layer, out var loaded))
                continue;

            var done = true;
            foreach (var chunk in chunks)
            {
                if (loaded.Contains(chunk))
                    continue;

                done = false;
                break;
            }

            if (done)
                _forcedRelease.Add(layer);
        }

        foreach (var layer in _forcedRelease)
            component.ForcedMarkerLayers.Remove(layer);
    }

    private void LoadBudgeted()
    {
        _chunkWork.Clear();
        _viewerOffsets.Clear();
        var viewerOffset = 0;
        foreach (var work in _biomeWork)
        {
            if (!_activeChunks.TryGetValue(work.Biome, out var active))
                continue;

            if (!_viewerOffsets.ContainsKey(work.Biome))
            {
                _viewerOffsets[work.Biome] = viewerOffset;
                if (_viewerPositions.TryGetValue(work.Biome, out var viewers))
                    viewerOffset += viewers.Count;
            }

            var offset = _viewerOffsets[work.Biome];
            foreach (var chunk in active)
            {
                var loaded = work.Biome.LoadedChunks.Contains(chunk);
                if (loaded)
                {
                    if (!work.Biome.PendingMarkers.ContainsKey(chunk))
                        continue;
                }
                else if (!MarkersReady(work.Biome, chunk))
                {
                    continue;
                }

                var dist = DistanceSq(work.Biome, chunk + new Vector2(ChunkSize / 2f, ChunkSize / 2f), out var viewer);
                _chunkWork.Add(new ChunkWork
                {
                    Biome = work.Biome,
                    GridUid = work.GridUid,
                    Grid = work.Grid,
                    Chunk = chunk,
                    Dist = dist,
                    Viewer = viewer < 0 ? -1 : offset + viewer,
                    MarkersOnly = loaded,
                });
            }
        }

        if (_chunkWork.Count == 0)
            return;

        _chunkWork.Sort(static (a, b) => a.Dist.CompareTo(b.Dist));

        _viewerBest.Clear();
        _viewerOrder.Clear();
        for (var i = 0; i < _chunkWork.Count; i++)
        {
            var viewer = _chunkWork[i].Viewer;
            if (_viewerBest.TryAdd(viewer, i))
                _viewerOrder.Add(viewer);
        }

        _viewerOrder.Sort();
        _takenWork.Clear();
        _loadOrder.Clear();
        var start = _loadCursor % _viewerOrder.Count;
        var served = Math.Min(MaxBiomeChunksPerTick, _viewerOrder.Count);
        for (var i = 0; i < served; i++)
        {
            var pick = _viewerBest[_viewerOrder[(start + i) % _viewerOrder.Count]];
            if (_takenWork.Add(pick))
                _loadOrder.Add(pick);
        }

        for (var i = 0; i < _chunkWork.Count && _loadOrder.Count < MaxBiomeChunksPerTick; i++)
        {
            if (_takenWork.Add(i))
                _loadOrder.Add(i);
        }

        var rotated = 0;
        for (var i = 0; i < _loadOrder.Count; i++)
        {
            if (!ChunkBudgetLeft())
                break;

            var work = _chunkWork[_loadOrder[i]];
            if (i < served)
                rotated++;

            _chunkWorkDone = true;
            LoadChunkMarkers(work.Biome, work.GridUid, work.Grid, work.Chunk, work.Biome.Seed);
            if (work.MarkersOnly)
                continue;

            if (!work.Biome.LoadedChunks.Add(work.Chunk))
                continue;

            LoadChunk(work.Biome, work.GridUid, work.Grid, work.Chunk, work.Biome.Seed);
        }

        _loadCursor = (_loadCursor + rotated) % Math.Max(1, viewerOffset);
    }

    private bool ChunkBudgetLeft()
    {
        return !_chunkWorkDone || Stopwatch.GetTimestamp() < _chunkBudgetEnd;
    }

    private void UnloadBudgeted()
    {
        _chunkWork.Clear();
        foreach (var work in _biomeWork)
        {
            if (!_keepChunks.TryGetValue(work.Biome, out var keep))
                continue;

            foreach (var chunk in work.Biome.LoadedChunks)
            {
                if (keep.Contains(chunk))
                    continue;

                _chunkWork.Add(new ChunkWork
                {
                    Biome = work.Biome,
                    GridUid = work.GridUid,
                    Grid = work.Grid,
                    Chunk = chunk,
                    Dist = DistanceSq(work.Biome, chunk + new Vector2(ChunkSize / 2f, ChunkSize / 2f)),
                });
            }
        }

        _chunkWork.Sort(static (a, b) => b.Dist.CompareTo(a.Dist));
        var count = Math.Min(MaxBiomeUnloadChunksPerTick, _chunkWork.Count);
        for (var i = 0; i < count; i++)
        {
            if (!ChunkBudgetLeft())
                break;

            _chunkWorkDone = true;
            var work = _chunkWork[i];
            work.Biome.LoadedChunks.Remove(work.Chunk);
            UnloadChunk(work.Biome, work.GridUid, work.Grid, work.Chunk, work.Biome.Seed, _unloadTilesBuffer);
        }
    }
    // DS-14 end

    /// <summary>
    /// Gets the marker nodes for the specified area.
    /// </summary>
    /// <param name="emptyTiles">Should we include empty tiles when determine markers (e.g. if they are yet to be loaded)</param>
    public void GetMarkerNodes(
        EntityUid gridUid,
        BiomeComponent biome,
        MapGridComponent grid,
        BiomeMarkerLayerPrototype layerProto,
        bool forced,
        Box2i bounds,
        int count,
        Random rand,
        out Dictionary<Vector2i, string?> spawnSet,
        out HashSet<EntityUid> existingEnts,
        bool emptyTiles = true)
    {
        DebugTools.Assert(count > 0);
        var remainingTiles = _tilePool.Get();
        var nodeEntities = new Dictionary<Vector2i, EntityUid?>();
        var nodeMask = new Dictionary<Vector2i, string?>();

        // Okay so originally we picked a random tile and BFS outwards
        // the problem is if you somehow get a cooked frontier then it might drop entire veins
        // hence we'll grab all valid tiles up front and use that as possible seeds.
        // It's hella more expensive but stops issues.
        for (var x = bounds.Left; x < bounds.Right; x++)
        {
            for (var y = bounds.Bottom; y < bounds.Top; y++)
            {
                var node = new Vector2i(x, y);

                // Empty tile, skip if relevant.
                if (!emptyTiles && (!_mapSystem.TryGetTile(grid, node, out var tile) || tile.IsEmpty))
                    continue;

                // Check if it's a valid spawn, if so then use it.
                var enumerator = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, node);
                enumerator.MoveNext(out var existing);

                if (!forced && existing != null)
                    continue;

                // Check if mask matches // anything blocking.
                TryGetEntity(node, biome, (gridUid, grid), out var proto);

                // If there's an existing entity and it doesn't match the mask then skip.
                if (layerProto.EntityMask.Count > 0 &&
                    (proto == null ||
                     !layerProto.EntityMask.ContainsKey(proto)))
                {
                    continue;
                }

                // If it's just a flat spawn then just check for anything blocking.
                if (proto != null && layerProto.Prototype != null)
                {
                    continue;
                }

                DebugTools.Assert(layerProto.EntityMask.Count == 0 || !string.IsNullOrEmpty(proto));
                remainingTiles.Add(node);
                nodeEntities.Add(node, existing);
                nodeMask.Add(node, proto);
            }
        }

        var frontier = new ValueList<Vector2i>(32);
        // TODO: Need poisson but crashes whenever I use moony's due to inputs or smth idk
        // Get the total amount of groups to spawn across the entire chunk.
        // We treat a null entity mask as requiring nothing else on the tile

        spawnSet = new Dictionary<Vector2i, string?>();
        existingEnts = new HashSet<EntityUid>();

        // Iterate the group counts and pathfind out each group.
        for (var i = 0; i < count; i++)
        {
            var groupSize = rand.Next(layerProto.MinGroupSize, layerProto.MaxGroupSize + 1);

            // While we have remaining tiles keep iterating
            while (groupSize > 0 && remainingTiles.Count > 0)
            {
                var startNode = rand.PickAndTake(remainingTiles);
                frontier.Clear();
                frontier.Add(startNode);

                // This essentially may lead to a vein being split in multiple areas but the count matters more than position.
                while (frontier.Count > 0 && groupSize > 0)
                {
                    // Need to pick a random index so we don't just get straight lines of ores.
                    var frontierIndex = rand.Next(frontier.Count);
                    var node = frontier[frontierIndex];
                    frontier.RemoveSwap(frontierIndex);
                    remainingTiles.Remove(node);

                    // Add neighbors if they're valid, worst case we add no more and pick another random seed tile.
                    for (var x = -1; x <= 1; x++)
                    {
                        for (var y = -1; y <= 1; y++)
                        {
                            var neighbor = new Vector2i(node.X + x, node.Y + y);

                            if (frontier.Contains(neighbor) || !remainingTiles.Contains(neighbor))
                                continue;

                            frontier.Add(neighbor);
                        }
                    }

                    // Tile valid salad so add it.
                    var mask = nodeMask[node];
                    spawnSet.Add(node, mask);
                    groupSize--;

                    if (nodeEntities.TryGetValue(node, out var existing))
                    {
                        Del(existing);
                    }
                }
            }

            if (groupSize > 0)
            {
                Log.Warning($"Found remaining group size for ore veins!");
            }
        }

        _tilePool.Return(remainingTiles);
    }

    /// <summary>
    /// Loads the pre-deteremined marker nodes for a particular chunk.
    /// This is calculated in <see cref="BuildMarkerChunks"/>
    /// </summary>
    /// <remarks>
    /// Note that the marker chunks do not correspond to this chunk.
    /// </remarks>
    private void LoadChunkMarkers(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i chunk,
        int seed)
    {
        // Load any pending marker tiles first.
        if (!component.PendingMarkers.TryGetValue(chunk, out var layers))
            return;

        // This needs to be done separately in case we try to add a marker layer and want to force it on existing
        // loaded chunks.
        component.ModifiedTiles.TryGetValue(chunk, out var modified);
        modified ??= _tilePool.Get();

        foreach (var (layer, nodes) in layers)
        {
            var layerProto = ProtoManager.Index<BiomeMarkerLayerPrototype>(layer);

            foreach (var node in nodes)
            {
                if (!TileInsideBiomeBounds(node, component.Bounds))
                    continue;

                if (modified.Contains(node))
                    continue;

                // Need to ensure the tile under it has loaded for anchoring.
                if (TryGetBiomeTile(node, component.Layers, seed, (gridUid, grid), out var tile))
                {
                    _mapSystem.SetTile(gridUid, grid, node, tile.Value);
                }

                string? prototype;

                if (TryGetEntity(node, component, (gridUid, grid), out var proto) &&
                    layerProto.EntityMask.TryGetValue(proto, out var maskedProto))
                {
                    prototype = maskedProto;
                }
                else
                {
                    prototype = layerProto.Prototype;
                }

                // If it is a ghost role then purge it
                // TODO: This is *kind* of a bandaid but natural mobs spawns needs a lot more work.
                // Ideally we'd just have ghost role and non-ghost role variants for some stuff.
                GetHiddenWalls(gridUid)?.Remove(node); //DS-14
                var uid = EntityManager.CreateEntityUninitialized(prototype, _mapSystem.GridTileToLocal(gridUid, grid, node));
                RemComp<GhostTakeoverAvailableComponent>(uid);
                RemComp<GhostRoleComponent>(uid);
                EntityManager.InitializeAndStartEntity(uid);
                modified.Add(node);
            }
        }

        if (modified.Count == 0)
        {
            component.ModifiedTiles.Remove(chunk);
            _tilePool.Return(modified);
        }

        component.PendingMarkers.Remove(chunk);
    }

    /// <summary>
    /// Loads a particular queued chunk for a biome.
    /// </summary>
    private void LoadChunk(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i chunk,
        int seed)
    {
        component.ModifiedTiles.TryGetValue(chunk, out var modified);
        modified ??= _tilePool.Get();
        _tiles.Clear();

        // Set tiles first
        for (var x = 0; x < ChunkSize; x++)
        {
            for (var y = 0; y < ChunkSize; y++)
            {
                var indices = new Vector2i(x + chunk.X, y + chunk.Y);

                if (!TileInsideBiomeBounds(indices, component.Bounds))
                    continue;

                // Pass in null so we don't try to get the tileref.
                if (modified.Contains(indices))
                    continue;

                // If there's existing data then don't overwrite it.
                if (_mapSystem.TryGetTileRef(gridUid, grid, indices, out var tileRef) && !tileRef.Tile.IsEmpty)
                    continue;

                if (!TryGetBiomeTile(indices, component.Layers, seed, (gridUid, grid), out var biomeTile))
                    continue;

                _tiles.Add((indices, biomeTile.Value));
            }
        }

        _mapSystem.SetTiles(gridUid, grid, _tiles);
        _tiles.Clear();

        // Now do entities
        var loadedEntities = new Dictionary<EntityUid, Vector2i>();
        component.LoadedEntities.Add(chunk, loadedEntities);
        CollectMobTiles(gridUid, grid, chunk); //DS-14
        // DS-14 start
        var hidden = GetHiddenWalls(gridUid);
        if (hidden != null)
            BuildSolidGrid(component, gridUid, grid, chunk, modified, hidden);
        // DS-14 end

        for (var x = 0; x < ChunkSize; x++)
        {
            for (var y = 0; y < ChunkSize; y++)
            {
                var indices = new Vector2i(x + chunk.X, y + chunk.Y);

                if (!TileInsideBiomeBounds(indices, component.Bounds))
                    continue;

                if (modified.Contains(indices))
                    continue;

                if (_mobTiles.Contains(indices)) //DS-14
                    continue;

                // Don't mess with anything that's potentially anchored.
                var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, indices);

                if (anchored.MoveNext(out _) || !TryGetEntity(indices, component, (gridUid, grid), out var entPrototype))
                    continue;

                // DS-14 start
                if (hidden != null && IsSolidPrototype(entPrototype) && IsEnclosed(indices, chunk))
                {
                    hidden[indices] = entPrototype;
                    continue;
                }

                SpawnBiomeEntity(gridUid, grid, indices, entPrototype, loadedEntities);
                // DS-14 end
            }
        }

        // Decals
        var loadedDecals = new Dictionary<uint, Vector2i>();
        component.LoadedDecals.Add(chunk, loadedDecals);

        for (var x = 0; x < ChunkSize; x++)
        {
            for (var y = 0; y < ChunkSize; y++)
            {
                var indices = new Vector2i(x + chunk.X, y + chunk.Y);

                if (!TileInsideBiomeBounds(indices, component.Bounds))
                    continue;

                if (modified.Contains(indices))
                    continue;

                // Don't mess with anything that's potentially anchored.
                var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, indices);

                if (anchored.MoveNext(out _) || !TryGetDecals(indices, component.Layers, seed, (gridUid, grid), out var decals))
                    continue;

                foreach (var decal in decals)
                {
                    if (!_decals.TryAddDecal(decal.ID, new EntityCoordinates(gridUid, decal.Position), out var dec, color: decal.Color)) // DS14
                        continue;

                    loadedDecals.Add(dec, indices);
                }
            }
        }

        if (modified.Count == 0)
        {
            _tilePool.Return(modified);
            component.ModifiedTiles.Remove(chunk);
        }
        else
        {
            component.ModifiedTiles[chunk] = modified;
        }
    }

    #endregion

    #region Unload

    /// <summary>
    /// Unloads a specific biome chunk.
    /// </summary>
    private void UnloadChunk(BiomeComponent component, EntityUid gridUid, MapGridComponent grid, Vector2i chunk, int seed, List<(Vector2i, Tile)> tiles)
    {
        // Reverse order to loading
        component.ModifiedTiles.TryGetValue(chunk, out var modified);
        modified ??= _tilePool.Get(); // DS14 Edit: use biome tile pool for temporary modified set.

        // Delete decals
        foreach (var (dec, indices) in component.LoadedDecals[chunk])
        {
            // If we couldn't remove it then flag the tile to never be touched.
            if (!_decals.RemoveDecal(gridUid, dec))
            {
                modified.Add(indices);
            }
        }

        component.LoadedDecals.Remove(chunk);

        // Delete entities
        // Ideally any entities that aren't modified just get deleted and re-generated later
        // This is because if we want to save the map (e.g. persistent server) it makes the file much smaller
        // and also if the map is enormous will make stuff like physics broadphase much faster

        // DS-14 start
        _suppressReveal = true;
        if (GetHiddenWalls(gridUid) is { } hiddenWalls)
        {
            for (var x = 0; x < ChunkSize; x++)
            {
                for (var y = 0; y < ChunkSize; y++)
                {
                    hiddenWalls.Remove(new Vector2i(chunk.X + x, chunk.Y + y));
                }
            }
        }
        // DS-14 end

        foreach (var (ent, tile) in component.LoadedEntities[chunk])
        {
            var untouched = IsUntouched(ent); //DS-14
            if (Deleted(ent) || !_xformQuery.TryGetComponent(ent, out var xform)) // DS14 Edit: reuse cached transform query.
            {
                modified.Add(tile);
                continue;
            }

            // It's moved
            var entTile = _mapSystem.LocalToTile(gridUid, grid, xform.Coordinates);

            if (!xform.Anchored || entTile != tile)
            {
                modified.Add(tile);
                continue;
            }

            if (!untouched) //DS-14
            {
                modified.Add(tile);
                continue;
            }

            Del(ent);
        }

        _suppressReveal = false; //DS-14
        component.LoadedEntities.Remove(chunk);

        // DS-14 start
        CollectMobTiles(gridUid, grid, chunk);
        modified.UnionWith(_mobTiles);
        // DS-14 end

        // Unset tiles (if the data is custom)

        for (var x = 0; x < ChunkSize; x++)
        {
            for (var y = 0; y < ChunkSize; y++)
            {
                var indices = new Vector2i(x + chunk.X, y + chunk.Y);

                if (!TileInsideBiomeBounds(indices, component.Bounds))
                    continue;

                if (modified.Contains(indices))
                    continue;

                // Don't mess with anything that's potentially anchored.
                var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, indices);

                if (anchored.MoveNext(out _))
                {
                    modified.Add(indices);
                    continue;
                }

                // If it's default data unload the tile.
                if (!TryGetBiomeTile(indices, component.Layers, seed, null, out var biomeTile) ||
                    _mapSystem.TryGetTileRef(gridUid, grid, indices, out var tileRef) && tileRef.Tile != biomeTile.Value)
                {
                    modified.Add(indices);
                    continue;
                }

                tiles.Add((indices, Tile.Empty));
            }
        }

        _mapSystem.SetTiles(gridUid, grid, tiles);
        tiles.Clear();

        if (modified.Count == 0)
        {
            // DS14-Start: return temporary or removed modified tile sets to the pool.
            if (component.ModifiedTiles.Remove(chunk, out var toReturn))
            {
                toReturn.Clear();
                _tilePool.Return(toReturn);
            }
            else
            {
                modified.Clear();
                _tilePool.Return(modified);
            }
            // DS14-End
        }
        else
        {
            component.ModifiedTiles[chunk] = modified;
        }
    }

    #endregion

    /// <summary>
    /// Creates a simple planet setup for a map.
    /// </summary>
    public void EnsurePlanet(EntityUid mapUid, BiomeTemplatePrototype biomeTemplate, int? seed = null, MetaDataComponent? metadata = null, Color? mapLight = null)
    {
        if (!Resolve(mapUid, ref metadata))
            return;

        EnsureComp<MapGridComponent>(mapUid);
        var biome = EntityManager.ComponentFactory.GetComponent<BiomeComponent>();
        seed ??= _random.Next();
        SetSeed(mapUid, biome, seed.Value, false);
        SetTemplate(mapUid, biome, biomeTemplate, false);
        AddComp(mapUid, biome, true);
        Dirty(mapUid, biome, metadata);

        var gravity = EnsureComp<GravityComponent>(mapUid);
        gravity.Enabled = true;
        gravity.Inherent = true;
        Dirty(mapUid, gravity, metadata);

        // Day lighting
        // Daylight: #D8B059
        // Midday: #E6CB8B
        // Moonlight: #2b3143
        // Lava: #A34931
        var light = EnsureComp<MapLightComponent>(mapUid);
        light.AmbientLightColor = mapLight ?? Color.FromHex("#D8B059");
        Dirty(mapUid, light, metadata);

        EnsureComp<RoofComponent>(mapUid);

        EnsureComp<LightCycleComponent>(mapUid);

        EnsureComp<SunShadowComponent>(mapUid);
        EnsureComp<SunShadowCycleComponent>(mapUid);

        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int)Gas.Oxygen] = 21.824779f;
        moles[(int)Gas.Nitrogen] = 82.10312f;

        var mixture = new GasMixture(moles, Atmospherics.T20C);

        _atmos.SetMapAtmosphere(mapUid, false, mixture);
    }

    /// <summary>
    /// Sets the specified tiles as relevant and marks them as modified.
    /// </summary>
    public void ReserveTiles(EntityUid mapUid, Box2 bounds, List<(Vector2i Index, Tile Tile)> tiles, BiomeComponent? biome = null, MapGridComponent? mapGrid = null)
    {
        if (!Resolve(mapUid, ref biome, ref mapGrid, false))
            return;

        foreach (var tileSet in _mapSystem.GetLocalTilesIntersecting(mapUid, mapGrid, bounds, false))
        {
            Vector2i chunkOrigin;
            HashSet<Vector2i> modified;

            // Existing, ignore
            if (_mapSystem.TryGetTileRef(mapUid, mapGrid, tileSet.GridIndices, out var existingRef) && !existingRef.Tile.IsEmpty)
            {
                chunkOrigin = SharedMapSystem.GetChunkIndices(tileSet.GridIndices, ChunkSize) * ChunkSize;
                modified = biome.ModifiedTiles.GetOrNew(chunkOrigin);
                modified.Add(tileSet.GridIndices);
                continue;
            }

            if (!TryGetBiomeTile(tileSet.GridIndices, biome.Layers, biome.Seed, (mapUid, mapGrid), out var tile))
            {
                continue;
            }

            chunkOrigin = SharedMapSystem.GetChunkIndices(tileSet.GridIndices, ChunkSize) * ChunkSize;
            modified = biome.ModifiedTiles.GetOrNew(chunkOrigin);
            modified.Add(tileSet.GridIndices);
            tiles.Add((tileSet.GridIndices, tile.Value));
        }

        _mapSystem.SetTiles(mapUid, mapGrid, tiles);
        OpenHiddenArea(mapUid, TilesIn(bounds)); //DS-14
    }

    // DS-14 start
    private static IEnumerable<Vector2i> TilesIn(Box2 bounds)
    {
        for (var x = (int) Math.Floor(bounds.Left); x < (int) Math.Ceiling(bounds.Right); x++)
        {
            for (var y = (int) Math.Floor(bounds.Bottom); y < (int) Math.Ceiling(bounds.Top); y++)
            {
                yield return new Vector2i(x, y);
            }
        }
    }
    // DS-14 end
}
