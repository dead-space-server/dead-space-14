// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.EUI;
using Content.Server.Parallax;
using Content.Shared.Atmos;
using Content.Shared.Chasm;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Content.Shared.DeadSpace.Lavaland.SecretBoss;
using Content.Shared.Ghost;
using Content.Shared.Gravity;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.DeadSpace.Lavaland.ChromiteCaves;

/// <summary>
/// The chromite caves under lavaland (<see cref="LavalandChromiteCavesConfig"/>). Generated together with the
/// planet: a biome map with the Hierophant arena and a waiting elite, and a shimmering pit near the outpost.
/// Stepping into the pit: jump, fall, land in the flowers under a beam of light. The beam is the only way back up,
/// and only with something that has <see cref="GrapplingGunComponent"/>.
/// The caves' light follows the lavaland day: day - deep night, night - night, deep night - the caves glow.
/// </summary>
public sealed class LavalandChromiteCavesSystem : EntitySystem
{
    private const int BoundaryBatchSize = 256;

    /// <summary>Boundary walls go up a chunk of this many tiles at a time.</summary>
    private const int BoundaryChunkSize = 16;

    /// <summary>The walls of a boundary chunk go up once anyone is this close to it (tiles): well beyond sight.</summary>
    private const float BoundaryLoadRange = 48f;

    private static readonly TimeSpan BoundaryInterval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextBoundaryCheck;
    private const int HookSearchDepth = 3;

    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly EuiManager _eui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinition = default!;
    [Dependency] private readonly LavalandBossArenaSystem _bossArena = default!;
    [Dependency] private readonly LavalandPitDropSystem _pitDrop = default!;
    [Dependency] private readonly LavalandFaunaRaidSystem _raid = default!;
    [Dependency] private readonly LavalandPitLandingSystem _landing = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly TimeSpan ModeInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ExitCheckInterval = TimeSpan.FromSeconds(0.5);

    private readonly List<EntityUid> _toProcess = new();
    private TimeSpan _nextModeCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChasmFallingAttemptEvent>(OnChasmFallingAttempt);
        SubscribeLocalEvent<LavalandChromiteExitComponent, InteractHandEvent>(OnExitInteract);
        SubscribeLocalEvent<LavalandChromiteCavesLinkComponent, ComponentShutdown>(OnLinkShutdown);
    }

    #region Public helpers

    /// <summary>The caves under this lavaland map, if any.</summary>
    public EntityUid? GetCaves(EntityUid lavalandMap)
    {
        return TryComp<LavalandChromiteCavesLinkComponent>(lavalandMap, out var link) &&
               link.Caves is { } caves && !TerminatingOrDeleted(caves)
            ? caves
            : null;
    }

    /// <summary>The lavaland map above these caves.</summary>
    public bool TryGetLavaland(EntityUid? cavesMap, out EntityUid lavalandMap)
    {
        lavalandMap = default;
        if (!TryComp<LavalandChromiteCavesComponent>(cavesMap, out var caves) || !Exists(caves.LavalandMap))
            return false;

        lavalandMap = caves.LavalandMap;
        return true;
    }

    /// <summary>The jaunter works in the caves only while fauna raid the outpost above.</summary>
    public bool IsJaunterAllowed(EntityUid? cavesMap)
    {
        return TryGetLavaland(cavesMap, out var lavaland) && _raid.IsRaidRunning(lavaland);
    }

    /// <summary>Busy going down or up: no jaunters, no questions.</summary>
    public bool IsTravelling(EntityUid uid)
    {
        return HasComp<LavalandCaveTravelComponent>(uid) || HasComp<LavalandPitLandingComponent>(uid);
    }

    #endregion

    #region Generation

    /// <summary>Builds the caves under a freshly generated lavaland and digs the pit near its outpost.</summary>
    public async Task SetupCaves(
        EntityUid lavalandMap,
        MapGridComponent lavalandGrid,
        LavalandPlanetPrototype planet,
        Random random,
        CancellationToken cancellation)
    {
        var config = planet.ChromiteCaves;
        if (!config.Enabled || !_prototype.TryIndex(config.Biome, out var template))
            return;

        var mapUid = _map.CreateMap(out var mapId, runMapInit: false);
        var grid = EnsureComp<MapGridComponent>(mapUid);
        _metaData.SetEntityName(mapUid, config.MapName);

        var biome = EntityManager.ComponentFactory.GetComponent<BiomeComponent>();
        _biome.SetSeed(mapUid, biome, random.Next(), false);
        _biome.SetTemplate(mapUid, biome, template, false);
        var half = Math.Max(16, config.MapHalfSize);
        _biome.SetBounds(mapUid, biome, new Box2i(-half, -half, half, half), false);
        AddComp(mapUid, biome, true);
        foreach (var layer in config.MarkerLayers)
        {
            _biome.AddMarkerLayer(mapUid, biome, layer);
        }

        var gravity = EnsureComp<GravityComponent>(mapUid);
        gravity.Enabled = true;
        gravity.Inherent = true;
        Dirty(mapUid, gravity);

        var light = EnsureComp<MapLightComponent>(mapUid);
        light.AmbientLightColor = config.NightLight;
        Dirty(mapUid, light);

        _atmosphere.SetMapAtmosphere(mapUid, false, CopyAtmosphere(planet.Atmosphere));

        var caves = EnsureComp<LavalandChromiteCavesComponent>(mapUid);
        caves.LavalandMap = lavalandMap;
        EnsureComp<LavalandCaveExitPromptsComponent>(mapUid);

        var link = EnsureComp<LavalandChromiteCavesLinkComponent>(lavalandMap);
        link.Caves = mapUid;

        _map.InitializeMap(mapId);
        _map.SetPaused(mapUid, true);

        await BuildBoundary((mapUid, caves), grid, config, half, cancellation);
        BuildLanding((mapUid, caves), grid, biome, config, random);
        // Generate everything around the landing now, not in the moment the first player drops in.
        _biome.Preload(mapUid, biome, Box2.CenteredAround(Vector2.Zero, new Vector2(config.PreloadRadius * 2f)));
        BuildHolding((mapUid, caves), mapId, config, half);
        cancellation.ThrowIfCancellationRequested();

        // Arena placement only needs the size and the edges of the caves: no outpost, no FTL beacon there.
        var cavesArea = new LavalandArenaArea(
            half,
            BoundaryEnabled: true,
            BoundaryLavaWidth: 0,
            BoundaryWallWidth: Math.Max(1, config.BoundaryWidth),
            TerminalReservationEnabled: false,
            TerminalGridOffset: Vector2.Zero,
            FtlEnabled: false,
            FtlBeaconOffset: Vector2.Zero);

        foreach (var (arenaId, count) in config.BossArenas)
        {
            if (!_prototype.TryIndex(arenaId, out var arena))
            {
                Log.Error($"Chromite caves: missing boss arena {arenaId}.");
                continue;
            }

            for (var i = 0; i < count; i++)
            {
                await _bossArena.TrySpawnBossArena(mapUid, grid, biome, cavesArea, arena, random, cancellation);
                await Timer.Delay(1, cancellation);
            }
        }

        SpawnElite(mapUid, grid, biome, config, random, half);
        _map.SetPaused(mapUid, false);

        if (!TryPlacePit(lavalandMap, lavalandGrid, config, random, link))
            Log.Warning("Chromite caves: no room for the pit near the outpost, the caves are unreachable.");
    }

    private static GasMixture CopyAtmosphere(GasMixture? atmosphere)
    {
        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        if (atmosphere == null)
        {
            moles[(int) Gas.Oxygen] = 14f;
            moles[(int) Gas.Nitrogen] = 23f;
            return new GasMixture(moles, 300f);
        }

        foreach (var (gas, amount) in atmosphere)
        {
            moles[(int) gas] = amount;
        }

        return new GasMixture(moles, atmosphere.Temperature, atmosphere.Volume);
    }

    /// <summary>
    /// Lays the boundary's floor all round the caves right away, but only notes its chunks for the walls:
    /// those go up when somebody comes near (<see cref="UpdateBoundary"/>).
    /// </summary>
    private async Task BuildBoundary(
        Entity<LavalandChromiteCavesComponent> caves,
        MapGridComponent grid,
        LavalandChromiteCavesConfig config,
        int half,
        CancellationToken cancellation)
    {
        var width = Math.Clamp(config.BoundaryWidth, 1, half);
        if (!_tileDefinition.TryGetDefinition(config.BoundaryTile, out var tileDef))
            return;

        var tiles = new List<(Vector2i, Tile)>(BoundaryBatchSize);
        foreach (var index in BoundaryTiles(half, width, new Box2i(-half, -half, half, half)))
        {
            tiles.Add((index, new Tile(tileDef.TileId)));
            caves.Comp.PendingBoundaryChunks.Add(new Vector2i(
                (int) MathF.Floor(index.X / (float) BoundaryChunkSize),
                (int) MathF.Floor(index.Y / (float) BoundaryChunkSize)));

            if (tiles.Count < BoundaryBatchSize)
                continue;

            _map.SetTiles(caves.Owner, grid, tiles);
            tiles.Clear();
            await Timer.Delay(1, cancellation);
        }

        if (tiles.Count > 0)
            _map.SetTiles(caves.Owner, grid, tiles);
    }

    /// <summary>The boundary tiles (the outer <paramref name="width"/> rings of the map) inside an area.</summary>
    private static IEnumerable<Vector2i> BoundaryTiles(int half, int width, Box2i area)
    {
        var left = Math.Max(area.Left, -half);
        var bottom = Math.Max(area.Bottom, -half);
        var right = Math.Min(area.Right, half);
        var top = Math.Min(area.Top, half);
        for (var x = left; x < right; x++)
        {
            for (var y = bottom; y < top; y++)
            {
                var edge = Math.Min(Math.Min(x + half, half - 1 - x), Math.Min(y + half, half - 1 - y));
                if (edge < width)
                    yield return new Vector2i(x, y);
            }
        }
    }

    /// <summary>Puts up the walls of the boundary chunks anyone has come near.</summary>
    private void UpdateBoundary(TimeSpan now)
    {
        if (now < _nextBoundaryCheck)
            return;
        _nextBoundaryCheck = now + BoundaryInterval;

        var query = EntityQueryEnumerator<LavalandChromiteCavesComponent, MapGridComponent>();
        while (query.MoveNext(out var uid, out var caves, out var grid))
        {
            if (caves.PendingBoundaryChunks.Count == 0 || !TryGetCavesConfig(uid, out _, out var config))
                continue;

            var mapId = Transform(uid).MapID;
            var half = Math.Max(16, config.MapHalfSize);
            var width = Math.Clamp(config.BoundaryWidth, 1, half);
            foreach (var session in _players.Sessions)
            {
                if (session.AttachedEntity is not { Valid: true } player)
                    continue;

                var coords = _transform.GetMapCoordinates(player);
                if (coords.MapId != mapId)
                    continue;

                foreach (var chunk in caves.PendingBoundaryChunks.ToArray())
                {
                    var origin = chunk * BoundaryChunkSize;
                    var bounds = new Box2(origin, origin + new Vector2i(BoundaryChunkSize, BoundaryChunkSize));
                    var nearest = Vector2.Clamp(coords.Position, bounds.BottomLeft, bounds.TopRight);
                    if (Vector2.DistanceSquared(nearest, coords.Position) > BoundaryLoadRange * BoundaryLoadRange)
                        continue;

                    caves.PendingBoundaryChunks.Remove(chunk);
                    var area = new Box2i(origin.X, origin.Y, origin.X + BoundaryChunkSize, origin.Y + BoundaryChunkSize);
                    foreach (var index in BoundaryTiles(half, width, area))
                    {
                        Spawn(config.BoundaryWall, _map.GridTileToLocal(uid, grid, index));
                    }
                }
            }
        }
    }

    /// <summary>A clearing in glowing flowers with the beam of light in the middle.</summary>
    private void BuildLanding(
        Entity<LavalandChromiteCavesComponent> caves,
        MapGridComponent grid,
        BiomeComponent biome,
        LavalandChromiteCavesConfig config,
        Random random)
    {
        var mapUid = caves.Owner;
        var radius = Math.Max(2, config.LandingClearRadius);
        ClearCircle(mapUid, grid, biome, Vector2i.Zero, radius, config.LandingTile);

        var flowerRadius = Math.Min(radius, config.LandingFlowerRadius);
        if (config.LandingFlowers.Count > 0)
        {
            for (var x = -flowerRadius; x <= flowerRadius; x++)
            {
                for (var y = -flowerRadius; y <= flowerRadius; y++)
                {
                    // The tile right under the beam stays bare.
                    if (x == 0 && y == 0 || x * x + y * y > flowerRadius * flowerRadius ||
                        random.NextDouble() > config.LandingFlowerChance)
                    {
                        continue;
                    }

                    var flower = config.LandingFlowers[random.Next(config.LandingFlowers.Count)];
                    Spawn(flower, _map.GridTileToLocal(mapUid, grid, new Vector2i(x, y)));
                }
            }
        }

        var center = _map.GridTileToLocal(mapUid, grid, Vector2i.Zero);
        var exit = Spawn(config.ExitPrototype, center);
        EnsureComp<LavalandChromiteExitComponent>(exit).CavesMap = mapUid;
        caves.Comp.Exit = exit;
        caves.Comp.Landing = _transform.ToMapCoordinates(center);
    }

    private void BuildHolding(Entity<LavalandChromiteCavesComponent> caves, MapId mapId, LavalandChromiteCavesConfig config, int half)
    {
        if (!_tileDefinition.TryGetDefinition(config.LandingTile, out var tileDef))
            return;

        var holding = _mapManager.CreateGridEntity(mapId);
        _metaData.SetEntityName(holding.Owner, "Chromite caves shaft");
        _transform.SetMapCoordinates(holding.Owner, new MapCoordinates(new Vector2(half + 500, half + 500), mapId));
        var tiles = new List<(Vector2i, Tile)>();
        for (var x = -1; x <= 1; x++)
        for (var y = -1; y <= 1; y++)
            tiles.Add((new Vector2i(x, y), new Tile(tileDef.TileId)));
        _map.SetTiles(holding.Owner, holding.Comp, tiles);
        caves.Comp.Holding = _transform.ToMapCoordinates(_map.GridTileToLocal(holding.Owner, holding.Comp, Vector2i.Zero));
    }

    /// <summary>The elite sits somewhere far from the landing in its own clearing and waits.</summary>
    private void SpawnElite(EntityUid mapUid, MapGridComponent grid, BiomeComponent biome, LavalandChromiteCavesConfig config, Random random, int half)
    {
        if (config.Elite is not { } elite)
            return;

        var limit = half - config.BoundaryWidth - config.EliteClearRadius - 4;
        var min = Math.Min(config.EliteMinDistance, limit);
        var max = Math.Clamp(config.EliteMaxDistance, min, limit);
        if (max <= 0)
            return;

        var angle = random.NextDouble() * Math.PI * 2;
        var distance = min + random.NextDouble() * (max - min);
        var center = new Vector2i((int) Math.Round(Math.Cos(angle) * distance), (int) Math.Round(Math.Sin(angle) * distance));
        ClearCircle(mapUid, grid, biome, center, Math.Max(2, config.EliteClearRadius), config.LandingTile);
        Spawn(elite, _map.GridTileToLocal(mapUid, grid, center));
    }

    /// <summary>Takes the area from the biome and lays plain floor in a circle, the rest stays as generated.</summary>
    private void ClearCircle(EntityUid mapUid, MapGridComponent grid, BiomeComponent biome, Vector2i center, int radius, string tile)
    {
        var box = new Box2(center.X - radius, center.Y - radius, center.X + radius + 1, center.Y + radius + 1);
        var reserved = new List<(Vector2i Index, Tile Tile)>();
        _biome.ReserveTiles(mapUid, box, reserved, biome, grid);

        if (!_tileDefinition.TryGetDefinition(tile, out var tileDef))
            return;

        var tiles = new List<(Vector2i, Tile)>();
        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                if (x * x + y * y > radius * radius)
                    continue;

                var index = center + new Vector2i(x, y);
                tiles.Add((index, new Tile(tileDef.TileId)));
                DeleteAnchored(mapUid, grid, index);
            }
        }

        _map.SetTiles(mapUid, grid, tiles);
    }

    private void DeleteAnchored(EntityUid mapUid, MapGridComponent grid, Vector2i index)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(mapUid, grid, index);
        while (anchored.MoveNext(out var uid))
        {
            if (!HasComp<MobStateComponent>(uid))
                QueueDel(uid.Value);
        }
    }

    private bool TryPlacePit(
        EntityUid lavalandMap,
        MapGridComponent grid,
        LavalandChromiteCavesConfig config,
        Random random,
        LavalandChromiteCavesLinkComponent link)
    {
        if (!_tileDefinition.TryGetDefinition("FloorBasalt", out var basalt) ||
            !TryComp<BiomeComponent>(lavalandMap, out var biome))
        {
            return false;
        }

        var origin = GetOutpostCenter(lavalandMap);
        var mapId = Transform(lavalandMap).MapID;
        var radius = Math.Max(0, config.PitRadius);
        var clear = radius + 2;
        for (var attempt = 0; attempt < 48; attempt++)
        {
            var angle = random.NextDouble() * Math.PI * 2;
            var distance = config.PitMinDistance + random.NextDouble() * Math.Max(0f, config.PitMaxDistance - config.PitMinDistance);
            var position = origin + new Vector2((float) Math.Cos(angle), (float) Math.Sin(angle)) * (float) distance;
            var center = new Vector2i((int) MathF.Round(position.X), (int) MathF.Round(position.Y));

            // Not on the outpost or any other grid.
            var box = new Box2(center.X - clear - 2, center.Y - clear - 2, center.X + clear + 3, center.Y + clear + 3);
            var blocked = false;
            foreach (var other in _mapManager.FindGridsIntersecting(mapId, box))
            {
                if (other.Owner != lavalandMap)
                {
                    blocked = true;
                    break;
                }
            }

            if (blocked)
                continue;

            var reserved = new List<(Vector2i Index, Tile Tile)>();
            _biome.ReserveTiles(lavalandMap, new Box2(center.X - clear, center.Y - clear, center.X + clear + 1, center.Y + clear + 1),
                reserved, biome, grid);

            var tiles = new List<(Vector2i, Tile)>();
            for (var x = -clear; x <= clear; x++)
            {
                for (var y = -clear; y <= clear; y++)
                {
                    var index = center + new Vector2i(x, y);
                    tiles.Add((index, new Tile(basalt.TileId)));
                    DeleteAnchored(lavalandMap, grid, index);
                }
            }

            _map.SetTiles(lavalandMap, grid, tiles);

            for (var x = -radius; x <= radius; x++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    var pit = Spawn(config.PitPrototype, _map.GridTileToLocal(lavalandMap, grid, center + new Vector2i(x, y)));
                    EnsureComp<LavalandChromitePitComponent>(pit).LavalandMap = lavalandMap;
                    link.PitTiles.Add(pit);
                }
            }

            link.PitCenter = _transform.ToMapCoordinates(_map.GridTileToLocal(lavalandMap, grid, center));
            return true;
        }

        return false;
    }

    private Vector2 GetOutpostCenter(EntityUid map)
    {
        var outposts = EntityQueryEnumerator<LavalandOutpostComponent, MapGridComponent, TransformComponent>();
        while (outposts.MoveNext(out _, out var outpost, out var grid, out var xform))
        {
            if (outpost.Map == map || xform.MapUid == map)
                return Vector2.Transform(grid.LocalAABB.Center, _transform.GetWorldMatrix(xform));
        }

        return Vector2.Zero;
    }

    private void OnLinkShutdown(Entity<LavalandChromiteCavesLinkComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Caves is { } caves && !TerminatingOrDeleted(caves))
            QueueDel(caves);
        ent.Comp.Caves = null;
    }

    private bool TryGetConfig(EntityUid lavalandMap, out LavalandChromiteCavesConfig config)
    {
        config = default!;
        if (!TryComp<LavalandMapComponent>(lavalandMap, out var lavaland) ||
            lavaland.Planet.Id == null || !_prototype.TryIndex(lavaland.Planet, out var planet) ||
            !planet.ChromiteCaves.Enabled)
        {
            return false;
        }

        config = planet.ChromiteCaves;
        return true;
    }

    private bool TryGetCavesConfig(EntityUid cavesMap, out LavalandChromiteCavesComponent caves, out LavalandChromiteCavesConfig config)
    {
        config = default!;
        return TryComp(cavesMap, out caves!) && TryGetConfig(caves.LavalandMap, out config);
    }

    /// <summary>The deep night of the caves map, while it is day on lavaland.</summary>
    public bool TryGetDeepNight(EntityUid cavesMap, out LavalandDeepNightConfig deep)
    {
        deep = default!;
        if (!TryGetCavesConfig(cavesMap, out var caves, out var config) || caves.Mode != LavalandCavesMode.DeepDark)
            return false;

        deep = config.DeepNight;
        return true;
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now >= _nextModeCheck)
        {
            _nextModeCheck = now + ModeInterval;
            UpdateModes(now);
        }

        UpdateBoundary(now);
        UpdateLights(now);
        UpdateJumpPrompts(now);
        UpdateExitPrompts(now);
        UpdateTravels(now);
    }

    #region Light

    private void UpdateModes(TimeSpan now)
    {
        var query = EntityQueryEnumerator<LavalandChromiteCavesComponent>();
        while (query.MoveNext(out var uid, out var caves))
        {
            if (!TryGetConfig(caves.LavalandMap, out var config))
                continue;

            var mode = GetModeFor(caves.LavalandMap);
            if (caves.ModeInitialized && mode == caves.Mode)
                continue;

            var announce = caves.ModeInitialized;
            caves.ModeInitialized = true;
            caves.Mode = mode;
            Dirty(uid, caves);

            var light = EnsureComp<MapLightComponent>(uid);
            caves.FromColor = light.AmbientLightColor;
            caves.ToColor = mode switch
            {
                LavalandCavesMode.DeepDark => config.DeepDarkLight,
                LavalandCavesMode.Glow => config.GlowLight,
                _ => config.NightLight,
            };
            caves.FadeStart = announce ? now : now - config.LightFade;
            caves.Fading = true;
            caves.NextLightUpdate = now;

            if (!announce)
                continue;

            var message = mode switch
            {
                LavalandCavesMode.DeepDark => config.DeepDarkMessage,
                LavalandCavesMode.Glow => config.GlowMessage,
                _ => config.NightMessage,
            };

            if (message is { } text)
            {
                _chat.DispatchFilteredAnnouncement(
                    Filter.Empty().AddInMap(Transform(uid).MapID),
                    Loc.GetString(text),
                    sender: Loc.GetString("lavaland-caves-sender"),
                    playSound: false,
                    colorOverride: config.MessageColor);
            }
        }
    }

    /// <summary>Lavaland day - deep night down here, night - night, deep night - the glow.</summary>
    private LavalandCavesMode GetModeFor(EntityUid lavalandMap)
    {
        if (!TryComp<LavalandDayCycleComponent>(lavalandMap, out var cycle) ||
            !TryComp<LavalandMapComponent>(lavalandMap, out var lavaland) ||
            lavaland.Planet.Id == null || !_prototype.TryIndex(lavaland.Planet, out var planet) ||
            cycle.Phase < 0 || cycle.Phase >= planet.DayCycle.Phases.Count)
        {
            return LavalandCavesMode.Night;
        }

        if (cycle.DeepNight)
            return LavalandCavesMode.Glow;

        var phase = planet.DayCycle.Phases[cycle.Phase];
        if (phase.CavesMode is { } forced)
            return forced;

        // A phase that can turn into a deep night is a night.
        return phase.DeepNight != null ? LavalandCavesMode.Night : LavalandCavesMode.DeepDark;
    }

    private void UpdateLights(TimeSpan now)
    {
        var query = EntityQueryEnumerator<LavalandChromiteCavesComponent>();
        while (query.MoveNext(out var uid, out var caves))
        {
            if (!caves.Fading || now < caves.NextLightUpdate || !TryGetConfig(caves.LavalandMap, out var config))
                continue;

            caves.NextLightUpdate = now + TimeSpan.FromSeconds(1);
            var progress = config.LightFade <= TimeSpan.Zero
                ? 1f
                : (float) Math.Clamp((now - caves.FadeStart) / config.LightFade, 0, 1);

            var light = EnsureComp<MapLightComponent>(uid);
            light.AmbientLightColor = Color.InterpolateBetween(caves.FromColor, caves.ToColor, progress);
            Dirty(uid, light);
            if (progress >= 1f)
                caves.Fading = false;
        }
    }

    #endregion

    #region Going down

    private void OnChasmFallingAttempt(ChasmFallingAttemptEvent args)
    {
        if (args.Cancelled || !TryComp<LavalandChromitePitComponent>(args.Chasm, out var pit))
            return;

        // The pit is a way down, not a death.
        args.Cancel();
        if (GetCaves(pit.LavalandMap) is not { } cavesMap ||
            !TryGetCavesConfig(cavesMap, out var caves, out var config) ||
            HasComp<LavalandCaveTravelComponent>(args.Tripper) ||
            HasComp<LavalandPitLandingComponent>(args.Tripper))
        {
            return;
        }

        // Whoever steps right onto the pit falls in; the question is asked only at its edge.
        if (HasComp<MobStateComponent>(args.Tripper) && _mobState.IsAlive(args.Tripper))
        {
            if (_players.TryGetSessionByEntity(args.Tripper, out var session) &&
                TryComp<LavalandChromiteCavesLinkComponent>(pit.LavalandMap, out var link) &&
                link.OpenPrompts.Contains(session))
            {
                // The open question is answered by the fall.
                link.OpenPrompts.Remove(session);
                if (link.PromptWindows.Remove(session, out var window))
                    _eui.CloseEui(window);
            }

            StartJump(args.Tripper, cavesMap, config, Transform(args.Chasm).Coordinates);
            return;
        }

        // Things and bodies fall for a while and drop into the flowers below.
        StopPulling(args.Tripper);
        _pitDrop.Drop(args.Tripper, caves.Holding, RandomLandingSpot(caves), 0.5f);
    }

    /// <summary>Players walking up to the edge of the pit are asked whether to jump.</summary>
    private void UpdateJumpPrompts(TimeSpan now)
    {
        var query = EntityQueryEnumerator<LavalandChromiteCavesLinkComponent>();
        while (query.MoveNext(out var uid, out var link))
        {
            if (now < link.NextPromptCheck || link.PitCenter.MapId == MapId.Nullspace ||
                !TryGetConfig(uid, out var config))
            {
                continue;
            }

            link.NextPromptCheck = now + ExitCheckInterval;
            var range = config.PitRadius + 0.5f + config.JumpPromptRange;
            foreach (var session in _players.Sessions)
            {
                if (session.AttachedEntity is not { Valid: true } mob)
                    continue;

                var coords = _transform.GetMapCoordinates(mob);
                if (coords.MapId != link.PitCenter.MapId)
                    continue;

                // Square pit: the distance to its edge counts along the longer axis.
                var delta = coords.Position - link.PitCenter.Position;
                if (MathF.Max(MathF.Abs(delta.X), MathF.Abs(delta.Y)) > range)
                    continue;

                TryOpenJumpPrompt(uid, session, mob);
            }
        }
    }

    private void TryOpenJumpPrompt(EntityUid lavalandMap, ICommonSession session, EntityUid mob)
    {
        if (!TryComp<LavalandChromiteCavesLinkComponent>(lavalandMap, out var link) ||
            link.OpenPrompts.Contains(session) ||
            link.PromptCooldowns.TryGetValue(session, out var cooldown) && _timing.CurTime < cooldown ||
            HasComp<GhostComponent>(mob) || !_mobState.IsAlive(mob) || IsTravelling(mob))
        {
            return;
        }

        link.OpenPrompts.Add(session);
        var window = new LavalandCavePitEui(this, lavalandMap);
        link.PromptWindows[session] = window;
        _eui.OpenEui(window, session);
    }

    /// <summary>Answer to "jump into the pit?".</summary>
    public void HandleJumpResponse(ICommonSession session, EntityUid lavalandMap, bool accepted)
    {
        if (!TryComp<LavalandChromiteCavesLinkComponent>(lavalandMap, out var link))
            return;

        link.PromptWindows.Remove(session);
        if (!link.OpenPrompts.Remove(session) ||
            !TryGetConfig(lavalandMap, out var config))
        {
            return;
        }

        link.PromptCooldowns[session] = _timing.CurTime + config.JumpPromptCooldown;
        if (!accepted ||
            GetCaves(lavalandMap) is not { } cavesMap ||
            session.AttachedEntity is not { Valid: true } mob ||
            !_mobState.IsAlive(mob) || IsTravelling(mob))
        {
            return;
        }

        var coords = _transform.GetMapCoordinates(mob);
        var delta = coords.Position - link.PitCenter.Position;
        if (coords.MapId != link.PitCenter.MapId ||
            MathF.Max(MathF.Abs(delta.X), MathF.Abs(delta.Y)) > config.PitRadius + 0.5f + config.JumpPromptRange + 2f)
        {
            return;
        }

        StartJump(mob, cavesMap, config, _transform.ToCoordinates(link.PitCenter));
    }

    private void StartJump(EntityUid mob, EntityUid cavesMap, LavalandChromiteCavesConfig config, EntityCoordinates pitTile)
    {
        StopPulling(mob);

        var pitCenter = TryComp<LavalandChromiteCavesComponent>(cavesMap, out var caves) &&
                        TryComp<LavalandChromiteCavesLinkComponent>(caves.LavalandMap, out var link)
            ? link.PitCenter.Position
            : _transform.ToMapCoordinates(pitTile).Position;

        var jumping = EnsureComp<LavalandPitJumpingComponent>(mob);
        jumping.Style = _random.Pick(Enum.GetValues<LavalandPitJumpStyle>());
        jumping.Target = pitCenter - _transform.GetWorldPosition(mob);
        jumping.Duration = (float) config.JumpDuration.TotalSeconds;
        Dirty(mob, jumping);

        EnsureComp<LavalandPitFrozenComponent>(mob);
        var travel = EnsureComp<LavalandCaveTravelComponent>(mob);
        travel.Stage = LavalandCaveTravelStage.Jump;
        travel.Caves = cavesMap;
        travel.NextStep = _timing.CurTime + config.JumpDuration;

        if (config.JumpSound != null)
            _audio.PlayPvs(config.JumpSound, mob);
    }

    private MapCoordinates RandomLandingSpot(LavalandChromiteCavesComponent caves)
    {
        var offset = _random.NextAngle().ToVec() * _random.NextFloat(0.8f, 2.6f);
        return new MapCoordinates(caves.Landing.Position + offset, caves.Landing.MapId);
    }

    #endregion

    #region Getting out

    private void OnExitInteract(Entity<LavalandChromiteExitComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !_players.TryGetSessionByEntity(args.User, out var session))
            return;

        args.Handled = true;
        TryOpenExitPrompt(ent.Comp.CavesMap, session, args.User, true);
    }

    private void UpdateExitPrompts(TimeSpan now)
    {
        var query = EntityQueryEnumerator<LavalandChromiteCavesComponent, LavalandCaveExitPromptsComponent>();
        while (query.MoveNext(out var uid, out var caves, out var prompts))
        {
            if (now < prompts.NextCheck || caves.Exit is not { } exit || TerminatingOrDeleted(exit) ||
                !TryGetConfig(caves.LavalandMap, out var config))
            {
                continue;
            }

            prompts.NextCheck = now + ExitCheckInterval;
            var exitPos = _transform.GetWorldPosition(exit);
            foreach (var session in _players.Sessions)
            {
                if (session.AttachedEntity is not { Valid: true } mob || Transform(mob).MapUid != uid ||
                    Vector2.Distance(_transform.GetWorldPosition(mob), exitPos) > config.ExitRange)
                {
                    continue;
                }

                TryOpenExitPrompt(uid, session, mob, false);
            }
        }
    }

    private void TryOpenExitPrompt(EntityUid cavesMap, ICommonSession session, EntityUid mob, bool force)
    {
        if (!TryComp<LavalandCaveExitPromptsComponent>(cavesMap, out var prompts) ||
            prompts.Open.Contains(session) ||
            HasComp<GhostComponent>(mob) || !_mobState.IsAlive(mob) || IsTravelling(mob) ||
            HasComp<LavalandCaveHopComponent>(mob) || HasComp<LavalandCaveClimbComponent>(mob) ||
            !force && prompts.Cooldowns.TryGetValue(session, out var cooldown) && _timing.CurTime < cooldown)
        {
            return;
        }

        prompts.Open.Add(session);
        _eui.OpenEui(new LavalandCaveExitEui(this, cavesMap), session);
    }

    /// <summary>Answer to "climb out?".</summary>
    public void HandleExitResponse(ICommonSession session, EntityUid cavesMap, bool accepted)
    {
        if (!TryGetCavesConfig(cavesMap, out var caves, out var config) ||
            !TryComp<LavalandCaveExitPromptsComponent>(cavesMap, out var prompts) ||
            !prompts.Open.Remove(session))
        {
            return;
        }

        prompts.Cooldowns[session] = _timing.CurTime + config.ExitPromptCooldown;
        if (!accepted ||
            session.AttachedEntity is not { Valid: true } mob ||
            !_mobState.IsAlive(mob) || IsTravelling(mob) ||
            Transform(mob).MapUid != cavesMap ||
            caves.Exit is not { } exit ||
            Vector2.Distance(_transform.GetWorldPosition(mob), _transform.GetWorldPosition(exit)) > config.ExitRange + 1.5f)
        {
            return;
        }

        if (TryFindHook(mob, out var hook))
            StartClimb(mob, hook, cavesMap, config);
        else
            StartHop(mob, cavesMap, config);
    }

    /// <summary>
    /// Anything that pulls - a grappling gun, a sticky hand and the like - on the player, in bags included.
    /// What is in the hands goes first: that is what the player means to climb with.
    /// </summary>
    public bool TryFindHook(EntityUid user, out Entity<GrapplingGunComponent> hook)
    {
        foreach (var item in _inventory.GetHandOrInventoryEntities(user))
        {
            if (TryFindHook(item, HookSearchDepth, out hook))
                return true;
        }

        hook = default;
        return false;
    }

    private bool TryFindHook(EntityUid item, int depth, out Entity<GrapplingGunComponent> hook)
    {
        if (TryComp<GrapplingGunComponent>(item, out var grappling))
        {
            hook = (item, grappling);
            return true;
        }

        hook = default;
        if (depth <= 0 || !TryComp<ContainerManagerComponent>(item, out var manager))
            return false;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (TryFindHook(contained, depth - 1, out hook))
                    return true;
            }
        }

        return false;
    }

    private void StartHop(EntityUid mob, EntityUid cavesMap, LavalandChromiteCavesConfig config)
    {
        var hop = EnsureComp<LavalandCaveHopComponent>(mob);
        hop.Duration = (float) config.HopDuration.TotalSeconds;
        hop.Hops = 3;
        Dirty(mob, hop);

        var travel = EnsureComp<LavalandCaveTravelComponent>(mob);
        travel.Stage = LavalandCaveTravelStage.Hop;
        travel.Caves = cavesMap;
        travel.NextStep = _timing.CurTime + config.HopDuration;
    }

    private void StartClimb(EntityUid mob, Entity<GrapplingGunComponent> hook, EntityUid cavesMap, LavalandChromiteCavesConfig config)
    {
        StopPulling(mob);
        EnsureComp<LavalandPitFrozenComponent>(mob);

        // The rope and its tip are those of the thing he climbs with.
        var climb = EnsureComp<LavalandCaveClimbComponent>(mob);
        climb.RopeSprite = hook.Comp.RopeSprite;
        climb.TipPrototype = TryComp<BasicEntityAmmoProviderComponent>(hook, out var ammo) ? ammo.Proto : null;
        SetClimbStage(mob, LavalandCaveClimbStage.Rope, config.RopeDuration);

        var travel = EnsureComp<LavalandCaveTravelComponent>(mob);
        travel.Stage = LavalandCaveTravelStage.Rope;
        travel.Caves = cavesMap;
        travel.NextStep = _timing.CurTime + config.RopeDuration;

        var sound = TryComp<GunComponent>(hook, out var gun) && gun.SoundGunshot != null ? gun.SoundGunshot : config.RopeSound;
        if (sound != null)
            _audio.PlayPvs(sound, mob);
    }

    private void SetClimbStage(EntityUid mob, LavalandCaveClimbStage stage, TimeSpan duration)
    {
        var climb = EnsureComp<LavalandCaveClimbComponent>(mob);
        climb.Stage = stage;
        climb.Duration = (float) duration.TotalSeconds;
        Dirty(mob, climb);
    }

    #endregion

    #region Travel steps

    private void UpdateTravels(TimeSpan now)
    {
        _toProcess.Clear();
        var query = EntityQueryEnumerator<LavalandCaveTravelComponent>();
        while (query.MoveNext(out var uid, out var travel))
        {
            if (now >= travel.NextStep)
                _toProcess.Add(uid);
        }

        foreach (var uid in _toProcess)
        {
            if (!TryComp<LavalandCaveTravelComponent>(uid, out var travel))
                continue;

            if (!TryGetCavesConfig(travel.Caves, out var caves, out var config))
            {
                FinishTravel(uid);
                continue;
            }

            switch (travel.Stage)
            {
                case LavalandCaveTravelStage.Jump:
                    RemComp<LavalandPitJumpingComponent>(uid);
                    _transform.SetMapCoordinates(uid, caves.Holding);
                    var falling = EnsureComp<LavalandPitFallingComponent>(uid);
                    falling.Style = _random.Pick(Enum.GetValues<LavalandPitJumpStyle>());
                    falling.Duration = (float) config.FallDuration.TotalSeconds;
                    Dirty(uid, falling);
                    travel.Stage = LavalandCaveTravelStage.Fall;
                    travel.NextStep = now + config.FallDuration;
                    break;

                case LavalandCaveTravelStage.Fall:
                    RemComp<LavalandPitFallingComponent>(uid);
                    RemComp<LavalandCaveTravelComponent>(uid);
                    var spot = RandomLandingSpot(caves);
                    _transform.SetMapCoordinates(uid, spot);
                    _landing.StartLanding(uid, config.LandingDuration);
                    if (config.LandingEffect is { } effect)
                        Spawn(effect, spot);
                    if (config.LandSound != null)
                        _audio.PlayPvs(config.LandSound, uid);
                    // Climbing out is offered only after walking up to the beam again.
                    if (_players.TryGetSessionByEntity(uid, out var session) &&
                        TryComp<LavalandCaveExitPromptsComponent>(travel.Caves, out var prompts))
                    {
                        prompts.Cooldowns[session] = now + config.LandingDuration + config.ExitPromptCooldown;
                    }
                    break;

                case LavalandCaveTravelStage.Hop:
                    FinishTravel(uid);
                    _popup.PopupEntity(Loc.GetString("lavaland-caves-no-hook"), uid, uid, PopupType.Large);
                    break;

                case LavalandCaveTravelStage.Rope:
                    SetClimbStage(uid, LavalandCaveClimbStage.FlyUp, config.FlyUpDuration);
                    travel.Stage = LavalandCaveTravelStage.FlyUp;
                    travel.NextStep = now + config.FlyUpDuration;
                    break;

                case LavalandCaveTravelStage.FlyUp:
                    _transform.SetMapCoordinates(uid, caves.Holding);
                    SetClimbStage(uid, LavalandCaveClimbStage.Hanging, config.HangDuration);
                    travel.Stage = LavalandCaveTravelStage.Hanging;
                    travel.NextStep = now + config.HangDuration;
                    break;

                case LavalandCaveTravelStage.Hanging:
                    ClimbOut(uid, caves, config);
                    break;
            }
        }
    }

    /// <summary>Out over the edge of the pit on lavaland.</summary>
    private void ClimbOut(EntityUid uid, LavalandChromiteCavesComponent caves, LavalandChromiteCavesConfig config)
    {
        RemComp<LavalandCaveClimbComponent>(uid);
        RemComp<LavalandCaveTravelComponent>(uid);

        if (!TryComp<LavalandChromiteCavesLinkComponent>(caves.LavalandMap, out var link) ||
            link.PitCenter.MapId == MapId.Nullspace)
        {
            RemComp<LavalandPitFrozenComponent>(uid);
            _transform.SetMapCoordinates(uid, caves.Landing);
            return;
        }

        var direction = _random.NextAngle().ToVec();
        var distance = config.PitRadius + 1.6f;
        // Square pit: push the spot out to the edge along the longer axis.
        var scale = distance / MathF.Max(MathF.Abs(direction.X), MathF.Abs(direction.Y));
        var position = link.PitCenter.Position + direction * scale;
        _transform.SetMapCoordinates(uid, new MapCoordinates(position, link.PitCenter.MapId));
        _landing.StartLanding(uid, config.ClimbOutDuration, link.PitCenter.Position - position);
    }

    private void FinishTravel(EntityUid uid)
    {
        RemComp<LavalandPitJumpingComponent>(uid);
        RemComp<LavalandPitFallingComponent>(uid);
        RemComp<LavalandCaveHopComponent>(uid);
        RemComp<LavalandCaveClimbComponent>(uid);
        RemComp<LavalandCaveTravelComponent>(uid);
        if (!HasComp<LavalandPitLandingComponent>(uid))
            RemComp<LavalandPitFrozenComponent>(uid);
    }

    private void StopPulling(EntityUid user)
    {
        if (TryComp<PullableComponent>(user, out var pullable) && _pulling.IsPulled(user, pullable))
            _pulling.TryStopPull(user, pullable);

        if (TryComp<PullerComponent>(user, out var puller) &&
            puller.Pulling is { } pulled &&
            TryComp<PullableComponent>(pulled, out var pulledComp))
        {
            _pulling.TryStopPull(pulled, pulledComp);
        }
    }

    #endregion
}
