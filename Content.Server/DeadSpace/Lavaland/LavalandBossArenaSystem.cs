using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Guardian;
using Content.Server.NPC.HTN;
using Content.Server.Parallax;
using Content.Server.Tiles;
using Content.Shared.Chasm;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Lavaland.Bosses;
using Content.Shared.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Maps;
using Content.Shared.Mining.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Parallax.Biomes;
using Content.Shared.PAI;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandBossArenaSystem : EntitySystem
{
    private const int ArenaEntityBatchSize = 128;
    private const int DefaultArenaSize = 35;
    private const int MinArenaSize = 15;
    private const int MaxArenaSize = 65;
    private const int EntranceHalfWidth = 2;
    private const int EntranceDepth = 3;
    private const float MinFightStartDistance = 7f;
    private const float MaxFightStartDistance = 11f;
    private const float FightStartDistanceFraction = 0.22f;

    private static readonly TimeSpan ParticipantScanInterval = TimeSpan.FromSeconds(0.35);
    private static readonly TimeSpan HudUpdateInterval = TimeSpan.FromSeconds(0.25);
    private static readonly TimeSpan BossLeashCheckInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan BossLeashResetDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan EmptyCleanupDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan BossDeadCleanupDelay = TimeSpan.FromMinutes(3);
    private static readonly Vector2i[] RewardOffsets =
    {
        new(-1, 0),
        new(1, 0),
        new(0, 1),
        new(0, -1),
        new(-1, 1),
        new(1, 1),
        new(-1, -1),
        new(1, -1),
        Vector2i.Zero,
    };

    [Dependency] private readonly Content.Server.Decals.DecalSystem _decals = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!; // DS14
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinition = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TileSystem _tile = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private List<Entity<MapGridComponent>> _nearbyGrids = new();
    private readonly List<EntityUid> _anchoredToDelete = new();
    private readonly List<NetUserId> _leavingParticipants = new();
    private readonly List<(Vector2i Index, Tile Tile)> _reservedTiles = new();
    private readonly List<(Vector2i Index, Tile Tile)> _terrainTiles = new();
    private readonly HashSet<Vector2i> _decorationTiles = new();
    private readonly HashSet<Entity<MobStateComponent>> _sealMobs = new();
    private int _nextArenaId = 1;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandBossComponent, BeforeDamageChangedEvent>(OnBossBeforeDamageChanged);
        SubscribeLocalEvent<LavalandBossComponent, DamageChangedEvent>(OnBossDamageChanged);
        SubscribeLocalEvent<LavalandBossComponent, MobStateChangedEvent>(OnBossMobStateChanged);
        SubscribeLocalEvent<LavalandBossArenaComponent, ComponentShutdown>(OnArenaShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandBossArenaComponent>();
        while (query.MoveNext(out var uid, out var arena))
        {
            if (arena.CleanupAt is { } cleanupAt && cleanupAt <= now)
            {
                CleanupArena((uid, arena));
                continue;
            }

            if (!arena.Ended && (!Exists(arena.Boss) || !Exists(arena.Grid)))
            {
                EndArena((uid, arena));
                continue;
            }

            if (arena.NextParticipantScan <= now)
            {
                arena.NextParticipantScan = now + ParticipantScanInterval;
                ScanParticipants((uid, arena), now);
            }

            if (!arena.Ended && arena.FightStarted && arena.NextBossLeashCheck <= now)
            {
                arena.NextBossLeashCheck = now + BossLeashCheckInterval;
                UpdateBossLeash((uid, arena), now);
            }

            if (!arena.Ended && arena.FightStarted && arena.NextHudUpdate <= now)
            {
                arena.NextHudUpdate = now + HudUpdateInterval;
                TrySealArena((uid, arena));
                SendHudUpdateToParticipants((uid, arena));
            }
        }
    }

    public async Task SpawnConfiguredArenas(
        EntityUid mapUid,
        MapGridComponent terrainGrid,
        BiomeComponent biome,
        LavalandPlanetPrototype planet,
        Random random,
        CancellationToken cancellation)
    {
        foreach (var (arenaId, count) in planet.BossArenas)
        {
            if (!_prototype.TryIndex(arenaId, out var arenaPrototype))
            {
                Log.Error($"Lavaland boss arena cannot spawn: missing arena prototype {arenaId}.");
                continue;
            }

            for (var i = 0; i < Math.Max(0, count); i++)
            {
                cancellation.ThrowIfCancellationRequested();
                await TrySpawnBossArena(mapUid, terrainGrid, biome, planet, arenaPrototype, random, cancellation);
                await Timer.Delay(1, cancellation);
            }
        }
    }

    public async Task<bool> TrySpawnBossArena(
        EntityUid mapUid,
        MapGridComponent terrainGrid,
        BiomeComponent biome,
        LavalandPlanetPrototype planet,
        LavalandBossArenaPrototype arenaPrototype,
        Random random,
        CancellationToken cancellation)
    {
        if (!_prototype.HasIndex<EntityPrototype>(arenaPrototype.BossPrototype.Id))
        {
            Log.Error($"Lavaland boss arena {arenaPrototype.ID} cannot spawn: missing boss prototype {arenaPrototype.BossPrototype}.");
            return false;
        }

        if (!_tileDefinition.TryGetDefinition(arenaPrototype.FloorTile, out var floorTile))
        {
            Log.Error($"Lavaland boss arena {arenaPrototype.ID} cannot spawn: missing floor tile {arenaPrototype.FloorTile}.");
            return false;
        }

        var width = NormalizeArenaSize(arenaPrototype.Size <= 0
            ? DefaultArenaSize
            : arenaPrototype.Size);
        var height = width;

        var layout = arenaPrototype.Layout is { Count: > 0 } rows ? rows : null;
        if (layout != null)
        {
            width = layout.Max(row => row.Length) | 1;
            height = layout.Count | 1;
        }

        // A cave arena takes the fight square plus its rock ring.
        var cave = layout == null ? arenaPrototype.Cave : null;
        var footprint = cave != null ? width + Math.Max(3, cave.Thickness) * 2 : width;

        if (!TryFindArenaCenter(mapUid, terrainGrid, planet, arenaPrototype, random, footprint, footprint, out var center))
        {
            Log.Warning($"Lavaland boss arena {arenaPrototype.ID} failed to find a safe placement.");
            return false;
        }

        var naturalFloor = new Dictionary<Vector2i, Tile>();
        PrepareArenaTerrain(mapUid, terrainGrid, biome, floorTile, arenaPrototype.NaturalFloors, naturalFloor, center, footprint, footprint);
        await Timer.Delay(1, cancellation);

        var mapId = Transform(mapUid).MapID;
        var grid = _mapManager.CreateGridEntity(mapId);
        _metadata.SetEntityName(grid.Owner, arenaPrototype.ArenaName);
        _transform.SetMapCoordinates(grid.Owner, new MapCoordinates(new Vector2(center.X, center.Y), mapId));

        CaveLayout? caveLayout = null;
        var layoutSealTiles = new List<(Vector2i Tile, Vector2i Inward)>();
        if (layout != null)
        {
            var altFloorTile = arenaPrototype.AltFloorTile is { } altId && _tileDefinition.TryGetDefinition(altId, out var altDef)
                ? altDef
                : floorTile;
            await FillLayoutArena(
                grid.Owner,
                grid.Comp,
                floorTile,
                altFloorTile,
                arenaPrototype.FloorVisualPrototype,
                arenaPrototype.AltFloorVisualPrototype ?? arenaPrototype.FloorVisualPrototype,
                arenaPrototype.WallPrototype,
                arenaPrototype.LightPrototype,
                layout,
                layoutSealTiles,
                cancellation);
        }
        else if (cave != null)
        {
            caveLayout = BuildCaveLayout(width, cave, random);
            await FillCave(
                grid.Owner,
                grid.Comp,
                mapUid,
                terrainGrid,
                center,
                naturalFloor,
                floorTile,
                arenaPrototype.FloorVisualPrototype,
                cave,
                caveLayout,
                random,
                cancellation);
            DigCaveExits(mapUid, terrainGrid, biome, center, cave, caveLayout, random);
        }
        else
        {
            await FillArenaFloor(
                grid.Owner,
                grid.Comp,
                naturalFloor,
                center,
                floorTile,
                arenaPrototype.FloorVisualPrototype,
                width,
                height,
                cancellation);
            SpawnArenaWalls(grid.Owner, grid.Comp, arenaPrototype.WallPrototype, width, height);
        }

        SpawnArenaDecorations(grid.Owner, grid.Comp, arenaPrototype.Decorations, width, height, random);

        if (layout == null && arenaPrototype.LightPrototype is { } lightPrototype)
            SpawnArenaLights(grid.Owner, grid.Comp, lightPrototype, width, height);

        var boss = Spawn(arenaPrototype.BossPrototype.Id, _map.GridTileToLocal(grid.Owner, grid.Comp, Vector2i.Zero));
        var bossComponent = EnsureComp<LavalandBossComponent>(boss);
        bossComponent.Arena = grid.Owner;
        bossComponent.MaxHealth = Math.Max(1f, bossComponent.MaxHealth);
        bossComponent.SelectedMusic = bossComponent.MusicPool.Count > 0
            ? bossComponent.MusicPool[random.Next(bossComponent.MusicPool.Count)]
            : bossComponent.Music;
        SetBossAiEnabled(boss, false);

        var arena = AddComp<LavalandBossArenaComponent>(grid.Owner);
        arena.ArenaId = _nextArenaId++;
        arena.Map = mapUid;
        arena.Grid = grid.Owner;
        arena.Boss = boss;
        arena.BossSpawnTile = Vector2i.Zero;
        arena.Width = width;
        arena.Height = height;
        arena.FightStartDistance = arenaPrototype.FightStartDistance > 0f
            ? arenaPrototype.FightStartDistance
            : GetDefaultFightStartDistance(width, height);
        arena.DeleteOnEmpty = arenaPrototype.DeleteOnEmpty;
        arena.DeleteOnBossDeath = arenaPrototype.DeleteOnBossDeath;
        arena.ReturnParticipantsOnDelete = arenaPrototype.ReturnParticipantsOnDelete;
        arena.ResetOnEmpty = arenaPrototype.ResetOnEmpty;
        arena.EmptyResetDelay = arenaPrototype.EmptyResetDelay < TimeSpan.Zero
            ? TimeSpan.Zero
            : arenaPrototype.EmptyResetDelay;
        arena.EmptyResetHealFraction = Math.Clamp(arenaPrototype.EmptyResetHealFraction, 0f, 1f);
        arena.EmptyResetMinHeal = Math.Max(0f, arenaPrototype.EmptyResetMinHeal);
        arena.SealHealthFraction = Math.Clamp(arenaPrototype.SealHealthFraction, 0f, 1f);
        arena.TileMovement = arenaPrototype.TileMovement;
        arena.SealWallPrototype = arenaPrototype.SealWallPrototype ?? arenaPrototype.WallPrototype;
        arena.SealTiles.AddRange(layoutSealTiles);
        if (caveLayout != null)
        {
            arena.SealTiles.AddRange(caveLayout.SealTiles);
            arena.ParticipantExtent = width / 2 + Math.Max(3, cave!.Thickness) - 3;
        }
        arena.SealSound = arenaPrototype.SealSound;
        arena.SealMessage = arenaPrototype.SealMessage;
        arena.BossName = string.IsNullOrWhiteSpace(bossComponent.BossName)
            ? Name(boss)
            : bossComponent.BossName;
        arena.MaxHealth = bossComponent.MaxHealth;
        arena.ScaledMaxHealth = bossComponent.MaxHealth;
        arena.NextParticipantScan = _timing.CurTime;
        arena.NextHudUpdate = _timing.CurTime;
        arena.NextBossLeashCheck = _timing.CurTime + BossLeashCheckInterval;

        var exclusion = EnsureComp<LavalandFtlExclusionComponent>(grid.Owner);
        exclusion.Range = GetArenaRadius(footprint, footprint) + Math.Max(0f, arenaPrototype.GridSeparation);
        _shuttle.AddIFFFlag(grid.Owner, IFFFlags.Hide);

        Log.Info($"Lavaland boss arena {arenaPrototype.ID} spawned at {center}.");
        return true;
    }

    private bool TryFindArenaCenter(
        EntityUid mapUid,
        MapGridComponent terrainGrid,
        LavalandPlanetPrototype planet,
        LavalandBossArenaPrototype arenaPrototype,
        Random random,
        int width,
        int height,
        out Vector2i center)
    {
        center = default;

        var limit = GetPlacementLimit(planet, arenaPrototype, width, height);
        if (limit <= 0)
            return false;

        var arenaRadius = GetArenaRadius(width, height);
        var minDistance = Math.Min(Math.Max(0f, arenaPrototype.MinDistance), limit);
        var configuredMax = arenaPrototype.MaxDistance > 0f
            ? arenaPrototype.MaxDistance
            : limit;
        var maxDistance = Math.Clamp(configuredMax, minDistance, limit);
        var attempts = Math.Max(1, arenaPrototype.SpawnAttempts);

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var angle = random.NextDouble() * MathF.PI * 2f;
            var distance = minDistance + random.NextDouble() * (maxDistance - minDistance);
            var candidate = new Vector2i(
                (int) MathF.Round(MathF.Cos((float) angle) * (float) distance),
                (int) MathF.Round(MathF.Sin((float) angle) * (float) distance));

            if (IsInsideArenaExclusion(candidate, planet, arenaPrototype, arenaRadius) ||
                HasNearbyNonTerrainGrid(mapUid, candidate, arenaRadius + arenaPrototype.GridSeparation) ||
                HasPersistentAnchoredEntityInFootprint(mapUid, terrainGrid, candidate, width, height))
            {
                continue;
            }

            center = candidate;
            return true;
        }

        return false;
    }

    private void PrepareArenaTerrain(
        EntityUid mapUid,
        MapGridComponent terrainGrid,
        BiomeComponent biome,
        ITileDefinition floorTile,
        Dictionary<string, string> naturalFloors,
        Dictionary<Vector2i, Tile> naturalFloor,
        Vector2i center,
        int width,
        int height)
    {
        var bounds = GetReservationBounds(center, width, height);
        var reserveBounds = new Box2(bounds.Left, bounds.Bottom, bounds.Right, bounds.Top);

        _reservedTiles.Clear();
        _biome.ReserveTiles(mapUid, reserveBounds, _reservedTiles, biome, terrainGrid);
        _reservedTiles.Clear();

        // Keep the look of the land: each natural floor becomes its storm-safe arena variant.
        _terrainTiles.Clear();
        for (var x = bounds.Left; x < bounds.Right; x++)
        {
            for (var y = bounds.Bottom; y < bounds.Top; y++)
            {
                var index = new Vector2i(x, y);
                var tile = CreateTile(floorTile);
                if (_map.TryGetTileRef(mapUid, terrainGrid, index, out var existing) &&
                    !existing.Tile.IsEmpty &&
                    naturalFloors.TryGetValue(_tileDefinition[existing.Tile.TypeId].ID, out var arenaTileId) &&
                    _tileDefinition.TryGetDefinition(arenaTileId, out var arenaTile))
                {
                    // Same pattern as the land: keep the natural tile's variant and rotation.
                    tile = new Tile(arenaTile.TileId, existing.Tile.Flags, existing.Tile.Variant, existing.Tile.RotationMirroring);
                }

                naturalFloor[index] = tile;
                _terrainTiles.Add((index, tile));
            }
        }

        _map.SetTiles(mapUid, terrainGrid, _terrainTiles);
        _terrainTiles.Clear();

        ClearClearableTerrainEntities(mapUid, terrainGrid, bounds);
    }

    /// <summary>The arena floor at a tile of the arena grid: the land's look where known, else the arena floor.</summary>
    private Tile ArenaFloorAt(Dictionary<Vector2i, Tile>? naturalFloor, Vector2i center, Vector2i local, ITileDefinition floorTile)
    {
        return naturalFloor != null && naturalFloor.TryGetValue(center + local, out var tile)
            ? tile
            : CreateTile(floorTile);
    }

    private async Task FillArenaFloor(
        EntityUid gridUid,
        MapGridComponent grid,
        Dictionary<Vector2i, Tile> naturalFloor,
        Vector2i center,
        ITileDefinition floorTile,
        EntProtoId? floorVisualPrototype,
        int width,
        int height,
        CancellationToken cancellation)
    {
        var halfWidth = width / 2;
        var halfHeight = height / 2;
        var tiles = new List<(Vector2i Index, Tile Tile)>(width * height + EntranceDepth * EntranceHalfWidth * 8);

        AddFloorRect(tiles, floorTile, -halfWidth, -halfHeight, halfWidth, halfHeight);
        AddFloorRect(tiles, floorTile, -EntranceHalfWidth, halfHeight + 1, EntranceHalfWidth, halfHeight + EntranceDepth);
        AddFloorRect(tiles, floorTile, -EntranceHalfWidth, -halfHeight - EntranceDepth, EntranceHalfWidth, -halfHeight - 1);
        AddFloorRect(tiles, floorTile, halfWidth + 1, -EntranceHalfWidth, halfWidth + EntranceDepth, EntranceHalfWidth);
        AddFloorRect(tiles, floorTile, -halfWidth - EntranceDepth, -EntranceHalfWidth, -halfWidth - 1, EntranceHalfWidth);

        for (var i = 0; i < tiles.Count; i++)
            tiles[i] = (tiles[i].Index, ArenaFloorAt(naturalFloor, center, tiles[i].Index, floorTile));

        _map.SetTiles(gridUid, grid, tiles);

        if (floorVisualPrototype == null)
            return;

        for (var i = 0; i < tiles.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var (index, _) = tiles[i];
            SpawnAnchored(floorVisualPrototype.Value.Id, gridUid, grid, index);

            if ((i + 1) % ArenaEntityBatchSize == 0 && i + 1 < tiles.Count)
                await Timer.Delay(1, cancellation);
        }
    }

    private void SpawnArenaWalls(EntityUid gridUid, MapGridComponent grid, EntProtoId? wallPrototype, int width, int height)
    {
        if (wallPrototype == null)
            return;

        var halfWidth = width / 2;
        var halfHeight = height / 2;

        for (var x = -halfWidth; x <= halfWidth; x++)
        {
            if (Math.Abs(x) > EntranceHalfWidth)
            {
                SpawnAnchored(wallPrototype.Value.Id, gridUid, grid, new Vector2i(x, -halfHeight));
                SpawnAnchored(wallPrototype.Value.Id, gridUid, grid, new Vector2i(x, halfHeight));
            }
        }

        for (var y = -halfHeight + 1; y < halfHeight; y++)
        {
            if (Math.Abs(y) > EntranceHalfWidth)
            {
                SpawnAnchored(wallPrototype.Value.Id, gridUid, grid, new Vector2i(-halfWidth, y));
                SpawnAnchored(wallPrototype.Value.Id, gridUid, grid, new Vector2i(halfWidth, y));
            }
        }
    }

    /// <summary>Builds the arena from a character plan (see <see cref="LavalandBossArenaPrototype.Layout"/>).</summary>
    private async Task FillLayoutArena(
        EntityUid gridUid,
        MapGridComponent grid,
        ITileDefinition floorTile,
        ITileDefinition altFloorTile,
        EntProtoId? floorVisualPrototype,
        EntProtoId? altFloorVisualPrototype,
        EntProtoId? wallPrototype,
        EntProtoId? lightPrototype,
        List<string> layout,
        List<(Vector2i Tile, Vector2i Inward)> sealTiles,
        CancellationToken cancellation)
    {
        var rows = layout.Count;
        var columns = layout.Max(row => row.Length);
        var halfX = columns / 2;
        var halfY = rows / 2;

        var floor = new List<Vector2i>();
        var altFloor = new HashSet<Vector2i>();
        var walls = new List<Vector2i>();
        var lights = new List<Vector2i>();
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < layout[row].Length; column++)
            {
                var symbol = layout[row][column];
                if (symbol == ' ')
                    continue;

                var tile = new Vector2i(column - halfX, halfY - row);
                if (symbol == '#')
                {
                    walls.Add(tile);
                    continue;
                }

                floor.Add(tile);
                if (symbol is ',' or 'l')
                    altFloor.Add(tile);
                if (symbol is 'L' or 'l')
                    lights.Add(tile);

                // Floor on the outer edge is a way in: walled off when the arena seals.
                if (row == 0 || row == rows - 1 || column == 0 || column == columns - 1)
                {
                    var inward = column == 0 ? new Vector2i(1, 0)
                        : column == columns - 1 ? new Vector2i(-1, 0)
                        : row == 0 ? new Vector2i(0, -1)
                        : new Vector2i(0, 1);
                    sealTiles.Add((tile, inward));
                }
            }
        }

        var tiles = new List<(Vector2i Index, Tile Tile)>(floor.Count + walls.Count);
        foreach (var tile in floor)
            tiles.Add((tile, CreateTile(altFloor.Contains(tile) ? altFloorTile : floorTile)));
        foreach (var tile in walls)
            tiles.Add((tile, CreateTile(floorTile)));
        _map.SetTiles(gridUid, grid, tiles);

        var spawned = 0;
        if (wallPrototype is { } wall)
        {
            foreach (var tile in walls)
            {
                cancellation.ThrowIfCancellationRequested();
                SpawnAnchored(wall.Id, gridUid, grid, tile);
                if (++spawned % ArenaEntityBatchSize == 0)
                    await Timer.Delay(1, cancellation);
            }
        }

        if (lightPrototype is { } light)
        {
            foreach (var tile in lights)
                SpawnAnchored(light.Id, gridUid, grid, tile);
        }

        foreach (var tile in floor)
        {
            cancellation.ThrowIfCancellationRequested();
            if ((altFloor.Contains(tile) ? altFloorVisualPrototype : floorVisualPrototype) is not { } visual)
                continue;

            SpawnAnchored(visual.Id, gridUid, grid, tile);
            if (++spawned % ArenaEntityBatchSize == 0)
                await Timer.Delay(1, cancellation);
        }
    }

    #region Cave arenas

    private sealed class CaveLayout
    {
        public readonly HashSet<Vector2i> Floor = new();
        public readonly HashSet<Vector2i> Rock = new();
        public int Outer;
        public readonly List<(Vector2i Tile, Vector2i Inward)> SealTiles = new();

        /// <summary>Where each passage leaves the cave (grid coordinates) and its direction.</summary>
        public readonly List<(Vector2 Exit, Vector2 Direction)> Exits = new();
    }

    /// <summary>
    /// The fight square stays fully open (the bosses' attacks rely on it); around it a rock ring with uneven
    /// pockets, and winding passages out. The seal goes across each passage near its outer end.
    /// </summary>
    private static CaveLayout BuildCaveLayout(int width, LavalandBossArenaCaveConfig cave, Random random)
    {
        var layout = new CaveLayout();
        var half = width / 2;
        var thickness = Math.Max(3, cave.Thickness);
        var outer = half + thickness;
        layout.Outer = outer;
        var maxPocket = Math.Max(0, thickness - Math.Max(3, cave.Shell));

        // Smooth random outline: a few sine harmonics around the circle.
        var harmonics = new (float Frequency, float Phase, float Amplitude)[4];
        for (var i = 0; i < harmonics.Length; i++)
            harmonics[i] = (2 + i * 2 + random.Next(2), (float) (random.NextDouble() * MathF.Tau), 1f / (i + 1));
        var amplitudeSum = 0f;
        foreach (var h in harmonics)
            amplitudeSum += h.Amplitude;

        float Outline(float angle)
        {
            var value = 0f;
            foreach (var (frequency, phase, amplitude) in harmonics)
                value += MathF.Sin(angle * frequency + phase) * amplitude;
            return value / amplitudeSum;
        }

        for (var x = -outer; x <= outer; x++)
        {
            for (var y = -outer; y <= outer; y++)
            {
                var tile = new Vector2i(x, y);
                var chebyshev = Math.Max(Math.Abs(x), Math.Abs(y));
                if (chebyshev <= half)
                {
                    layout.Floor.Add(tile);
                    continue;
                }

                var pocket = (int) MathF.Round(maxPocket * Math.Clamp(cave.Roughness, 0f, 1f) *
                                               MathF.Max(0f, Outline(MathF.Atan2(y, x))));
                if (chebyshev <= half + pocket)
                    layout.Floor.Add(tile);
                else
                    layout.Rock.Add(tile);
            }
        }

        // Passages: evenly spread around, each wandering a little on its way out.
        var passages = Math.Max(1, cave.Passages);
        var halfWidth = Math.Max(0, cave.PassageHalfWidth);
        var baseAngle = (float) (random.NextDouble() * MathF.Tau);
        for (var i = 0; i < passages; i++)
        {
            var angle = baseAngle + MathF.Tau * i / passages + (float) (random.NextDouble() - 0.5) * 0.6f;
            var wobblePhase = (float) (random.NextDouble() * MathF.Tau);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var scale = half / MathF.Max(MathF.Abs(direction.X), MathF.Abs(direction.Y));
            var position = direction * scale;
            var passageTiles = new HashSet<Vector2i>();

            for (var step = 0; step < 400; step++)
            {
                var heading = angle + MathF.Sin(step * 0.12f + wobblePhase) * 0.35f;
                position += new Vector2(MathF.Cos(heading), MathF.Sin(heading)) * 0.5f;
                var center = new Vector2i((int) MathF.Round(position.X), (int) MathF.Round(position.Y));
                for (var dx = -halfWidth; dx <= halfWidth; dx++)
                {
                    for (var dy = -halfWidth; dy <= halfWidth; dy++)
                    {
                        var tile = center + new Vector2i(dx, dy);
                        if (Math.Max(Math.Abs(tile.X), Math.Abs(tile.Y)) > outer ||
                            Math.Max(Math.Abs(tile.X), Math.Abs(tile.Y)) <= half)
                        {
                            continue;
                        }

                        layout.Rock.Remove(tile);
                        layout.Floor.Add(tile);
                        passageTiles.Add(tile);
                    }
                }

                if (Math.Max(MathF.Abs(position.X), MathF.Abs(position.Y)) > outer + 0.5f)
                    break;
            }

            layout.Exits.Add((position, Vector2.Normalize(position)));

            // The seal: two tiles thick, across the passage where no pocket reaches.
            foreach (var tile in passageTiles)
            {
                var chebyshev = Math.Max(Math.Abs(tile.X), Math.Abs(tile.Y));
                if (chebyshev < outer - 2 || chebyshev > outer - 1)
                    continue;

                var inward = Math.Abs(tile.X) >= Math.Abs(tile.Y)
                    ? new Vector2i(-Math.Sign(tile.X), 0)
                    : new Vector2i(0, -Math.Sign(tile.Y));
                layout.SealTiles.Add((tile, inward));
            }
        }

        return layout;
    }

    /// <summary>
    /// The cave floor goes on the arena grid; the rock goes on the lavaland itself so it joins the surrounding
    /// rock without seams. Inner rock can be mined and holds ore veins, the outer shell cannot.
    /// </summary>
    private async Task FillCave(
        EntityUid gridUid,
        MapGridComponent grid,
        EntityUid terrainUid,
        MapGridComponent terrainGrid,
        Vector2i center,
        Dictionary<Vector2i, Tile> naturalFloor,
        ITileDefinition floorTile,
        EntProtoId? floorVisualPrototype,
        LavalandBossArenaCaveConfig cave,
        CaveLayout layout,
        Random random,
        CancellationToken cancellation)
    {
        var tiles = new List<(Vector2i Index, Tile Tile)>(layout.Floor.Count);
        foreach (var tile in layout.Floor)
            tiles.Add((tile, ArenaFloorAt(naturalFloor, center, tile, floorTile)));
        _map.SetTiles(gridUid, grid, tiles);

        var outer = layout.Outer;
        var shell = Math.Max(0, cave.Shell);
        var minable = new List<Vector2i>();
        foreach (var tile in layout.Rock)
        {
            if (Math.Max(Math.Abs(tile.X), Math.Abs(tile.Y)) <= outer - shell)
                minable.Add(tile);
        }

        // Ore veins: random seeds grown into small clumps.
        var ore = new Dictionary<Vector2i, EntProtoId>();
        var totalWeight = 0f;
        foreach (var weight in cave.Ores.Values)
            totalWeight += Math.Max(0f, weight);
        if (totalWeight > 0f && minable.Count > 0)
        {
            var minableSet = new HashSet<Vector2i>(minable);
            for (var vein = 0; vein < cave.OreVeins; vein++)
            {
                var roll = (float) random.NextDouble() * totalWeight;
                var veinOre = default(EntProtoId);
                foreach (var (oreId, weight) in cave.Ores)
                {
                    roll -= Math.Max(0f, weight);
                    if (roll > 0f)
                        continue;
                    veinOre = oreId;
                    break;
                }

                if (veinOre == default)
                    continue;

                var current = minable[random.Next(minable.Count)];
                var size = random.Next(Math.Max(1, cave.OreVeinMin), Math.Max(cave.OreVeinMin, cave.OreVeinMax) + 1);
                for (var i = 0; i < size * 4 && size > 0; i++)
                {
                    if (minableSet.Contains(current) && !ore.ContainsKey(current))
                    {
                        ore[current] = veinOre;
                        size--;
                    }

                    current += new Vector2i(random.Next(-1, 2), random.Next(-1, 2));
                }
            }
        }

        var spawned = 0;
        foreach (var tile in layout.Rock)
        {
            cancellation.ThrowIfCancellationRequested();
            var chebyshev = Math.Max(Math.Abs(tile.X), Math.Abs(tile.Y));
            var prototype = chebyshev > outer - shell
                ? cave.ShellRock
                : ore.TryGetValue(tile, out var oreRock) ? oreRock : cave.Rock;
            SpawnAnchored(prototype.Id, terrainUid, terrainGrid, center + tile);
            if (++spawned % ArenaEntityBatchSize == 0)
                await Timer.Delay(1, cancellation);
        }

        if (floorVisualPrototype is not { } visual)
            return;

        foreach (var tile in layout.Floor)
        {
            cancellation.ThrowIfCancellationRequested();
            SpawnAnchored(visual.Id, gridUid, grid, tile);
            if (++spawned % ArenaEntityBatchSize == 0)
                await Timer.Delay(1, cancellation);
        }
    }

    /// <summary>Continues each passage into the lavaland so the cave can actually be reached.</summary>
    private void DigCaveExits(
        EntityUid mapUid,
        MapGridComponent terrainGrid,
        BiomeComponent biome,
        Vector2i center,
        LavalandBossArenaCaveConfig cave,
        CaveLayout layout,
        Random random)
    {
        if (!_tileDefinition.TryGetDefinition(cave.ExitFloor, out var exitFloor))
            return;

        var halfWidth = Math.Max(0, cave.PassageHalfWidth);
        foreach (var (exit, direction) in layout.Exits)
        {
            // A winding corridor: wanders left and right but keeps heading away from the cave.
            var outward = MathF.Atan2(direction.Y, direction.X);
            var heading = outward;
            var position = new Vector2(center.X, center.Y) + exit;
            for (var step = 0; step <= Math.Max(0, cave.ExitLength); step++)
            {
                heading += ((float) random.NextDouble() - 0.5f) * 2f * cave.ExitWander;
                heading = outward + Math.Clamp(heading - outward, -1.1f, 1.1f);
                position += new Vector2(MathF.Cos(heading), MathF.Sin(heading));
                var tile = new Vector2i((int) MathF.Floor(position.X), (int) MathF.Floor(position.Y));
                var bounds = new Box2i(tile.X - halfWidth, tile.Y - halfWidth, tile.X + halfWidth + 1, tile.Y + halfWidth + 1);

                // The land keeps its own floor; only the rock in the way is cleared.
                _reservedTiles.Clear();
                _biome.ReserveTiles(mapUid, new Box2(bounds.Left, bounds.Bottom, bounds.Right, bounds.Top), _reservedTiles, biome, terrainGrid);
                _reservedTiles.Clear();

                _terrainTiles.Clear();
                for (var x = bounds.Left; x < bounds.Right; x++)
                {
                    for (var y = bounds.Bottom; y < bounds.Top; y++)
                    {
                        var index = new Vector2i(x, y);
                        if (!_map.TryGetTileRef(mapUid, terrainGrid, index, out var existing) || existing.Tile.IsEmpty)
                            _terrainTiles.Add((index, CreateTile(exitFloor)));
                    }
                }

                if (_terrainTiles.Count > 0)
                    _map.SetTiles(mapUid, terrainGrid, _terrainTiles);
                _terrainTiles.Clear();
                ClearClearableTerrainEntities(mapUid, terrainGrid, bounds);
            }
        }
    }

    #endregion

    private void SpawnArenaLights(EntityUid gridUid, MapGridComponent grid, EntProtoId lightPrototype, int width, int height)
    {
        var offsetX = Math.Max(EntranceHalfWidth + 2, width / 4);
        var offsetY = Math.Max(EntranceHalfWidth + 2, height / 4);

        SpawnAnchored(lightPrototype.Id, gridUid, grid, Vector2i.Zero);
        SpawnAnchored(lightPrototype.Id, gridUid, grid, new Vector2i(-offsetX, -offsetY));
        SpawnAnchored(lightPrototype.Id, gridUid, grid, new Vector2i(-offsetX, offsetY));
        SpawnAnchored(lightPrototype.Id, gridUid, grid, new Vector2i(offsetX, -offsetY));
        SpawnAnchored(lightPrototype.Id, gridUid, grid, new Vector2i(offsetX, offsetY));
    }

    private void SpawnArenaDecorations(
        EntityUid gridUid,
        MapGridComponent grid,
        IReadOnlyList<LavalandBossArenaDecorationEntry> decorations,
        int width,
        int height,
        Random random)
    {
        if (decorations.Count == 0)
            return;

        _decorationTiles.Clear();

        foreach (var decoration in decorations)
        {
            if (decoration.Count <= 0)
                continue;

            if (decoration.Decals.Count == 0 &&
                (decoration.Prototype is not { } decorationPrototype || !_prototype.HasIndex<EntityPrototype>(decorationPrototype.Id)))
            {
                Log.Error($"Lavaland boss arena decoration cannot spawn: missing entity prototype {decoration.Prototype}.");
                continue;
            }

            var spawned = 0;
            var clusterSize = Math.Max(1, decoration.ClusterSize);
            var attempts = Math.Max(1, decoration.SpawnAttempts) + decoration.Count * clusterSize;

            for (var attempt = 0; attempt < attempts && spawned < decoration.Count; attempt++)
            {
                if (!TryPickDecorationTile(gridUid, grid, decoration, width, height, random, out var tile))
                    continue;

                spawned += SpawnDecorationCluster(gridUid, grid, decoration, width, height, random, tile, decoration.Count - spawned);
            }

            if (spawned < decoration.Count)
                Log.Warning($"Lavaland boss arena decoration {decoration.Prototype} only spawned {spawned}/{decoration.Count} entities.");
        }

        _decorationTiles.Clear();
    }

    private int SpawnDecorationCluster(
        EntityUid gridUid,
        MapGridComponent grid,
        LavalandBossArenaDecorationEntry decoration,
        int width,
        int height,
        Random random,
        Vector2i seed,
        int remaining)
    {
        var spawned = 0;
        var current = seed;
        var clusterSize = Math.Min(Math.Max(1, decoration.ClusterSize), remaining);
        var clusterRadius = Math.Max(1, decoration.ClusterRadius);
        var clusterAttempts = Math.Max(clusterSize * 8, 8);

        for (var attempt = 0; attempt < clusterAttempts && spawned < clusterSize; attempt++)
        {
            var tile = spawned == 0
                ? seed
                : current + new Vector2i(
                    random.Next(-clusterRadius, clusterRadius + 1),
                    random.Next(-clusterRadius, clusterRadius + 1));

            if (!IsValidDecorationTile(gridUid, grid, decoration, width, height, tile))
                continue;

            if (decoration.Decals.Count > 0)
            {
                // Decals lie on the floor: they do not take the tile from other decorations.
                var decal = decoration.Decals[random.Next(decoration.Decals.Count)];
                _decals.TryAddDecal(decal.Id, new EntityCoordinates(gridUid, tile), out _, decoration.DecalColor,
                    Angle.FromDegrees(random.Next(4) * 90), cleanable: true);
                current = tile;
                spawned++;
                continue;
            }

            SpawnAnchored(decoration.Prototype!.Value.Id, gridUid, grid, tile);
            _decorationTiles.Add(tile);
            current = tile;
            spawned++;
        }

        return spawned;
    }

    private bool TryPickDecorationTile(
        EntityUid gridUid,
        MapGridComponent grid,
        LavalandBossArenaDecorationEntry decoration,
        int width,
        int height,
        Random random,
        out Vector2i tile)
    {
        tile = default;

        var halfWidth = width / 2;
        var halfHeight = height / 2;
        var padding = Math.Max(0, decoration.EdgePadding);
        var minX = -halfWidth + 1 + padding;
        var maxX = halfWidth - 1 - padding;
        var minY = -halfHeight + 1 + padding;
        var maxY = halfHeight - 1 - padding;

        if (minX > maxX || minY > maxY)
            return false;

        var attempts = Math.Max(1, decoration.SpawnAttempts);
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var candidate = new Vector2i(
                random.Next(minX, maxX + 1),
                random.Next(minY, maxY + 1));

            if (!IsValidDecorationTile(gridUid, grid, decoration, width, height, candidate))
                continue;

            tile = candidate;
            return true;
        }

        return false;
    }

    private bool IsValidDecorationTile(
        EntityUid gridUid,
        MapGridComponent grid,
        LavalandBossArenaDecorationEntry decoration,
        int width,
        int height,
        Vector2i tile)
    {
        if (_decorationTiles.Contains(tile))
            return false;

        var halfWidth = width / 2;
        var halfHeight = height / 2;
        var padding = Math.Max(0, decoration.EdgePadding);
        if (tile.X < -halfWidth + 1 + padding ||
            tile.X > halfWidth - 1 - padding ||
            tile.Y < -halfHeight + 1 + padding ||
            tile.Y > halfHeight - 1 - padding)
        {
            return false;
        }

        if (decoration.AvoidEntrances && IsNearEntranceApproach(tile, width, height))
            return false;

        var distanceSquared = new Vector2(tile.X, tile.Y).LengthSquared();
        if (decoration.MinDistance > 0f && distanceSquared < decoration.MinDistance * decoration.MinDistance)
            return false;

        if (decoration.MaxDistance > 0f && distanceSquared > decoration.MaxDistance * decoration.MaxDistance)
            return false;

        if (decoration.MinSeparation > 0)
        {
            foreach (var reserved in _decorationTiles)
            {
                if (Math.Abs(reserved.X - tile.X) <= decoration.MinSeparation &&
                    Math.Abs(reserved.Y - tile.Y) <= decoration.MinSeparation)
                {
                    return false;
                }
            }
        }

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (uid != null && !TerminatingOrDeleted(uid.Value))
                return false;
        }

        return true;
    }

    private static bool IsNearEntranceApproach(Vector2i tile, int width, int height)
    {
        var halfWidth = width / 2;
        var halfHeight = height / 2;
        var entranceApproachDepth = EntranceDepth + 2;

        if (Math.Abs(tile.X) <= EntranceHalfWidth &&
            halfHeight - Math.Abs(tile.Y) <= entranceApproachDepth)
        {
            return true;
        }

        return Math.Abs(tile.Y) <= EntranceHalfWidth &&
               halfWidth - Math.Abs(tile.X) <= entranceApproachDepth;
    }

    private void ScanParticipants(Entity<LavalandBossArenaComponent> arena, TimeSpan now)
    {
        var current = new Dictionary<NetUserId, ICommonSession>();
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } attached ||
                !IsCountedParticipant(attached, arena.Comp))
            {
                continue;
            }

            current[session.UserId] = session;
        }

        _leavingParticipants.Clear();
        foreach (var participant in arena.Comp.Participants)
        {
            if (!current.ContainsKey(participant))
                _leavingParticipants.Add(participant);
        }

        foreach (var participant in _leavingParticipants)
        {
            arena.Comp.Participants.Remove(participant);
            if (arena.Comp.FightStarted &&
                _players.TryGetSessionById(participant, out var session))
            {
                SendHideAndStop(session, arena.Comp.ArenaId);
            }
        }

        if (arena.Comp.Ended)
        {
            foreach (var (userId, session) in current)
            {
                if (arena.Comp.Participants.Add(userId))
                    SendHideAndStop(session, arena.Comp.ArenaId);
            }

            arena.Comp.EmptySince = null;
            return;
        }

        foreach (var (userId, session) in current)
        {
            if (arena.Comp.Participants.Add(userId))
            {
                if (arena.Comp.ReturnParticipantsOnDelete &&
                    session.AttachedEntity is { Valid: true } attached &&
                    !arena.Comp.ReturnCoordinates.ContainsKey(userId))
                {
                    arena.Comp.ReturnCoordinates[userId] = _transform.GetMapCoordinates(attached);
                }

                if (arena.Comp.FightStarted)
                {
                    SendHudUpdate(session, arena.Comp);
                    SendMusicStart(session, arena.Comp);
                    TryScaleBossHpForParticipants((arena.Owner, arena.Comp));
                }
            }
        }

        if (!arena.Comp.FightStarted && ShouldStartFightByProximity((arena.Owner, arena.Comp), current.Values))
            StartFight((arena.Owner, arena.Comp));

        if (arena.Comp.Participants.Count == 0)
        {
            arena.Comp.EmptySince ??= now;
            if (arena.Comp.FightStarted &&
                arena.Comp.ResetOnEmpty &&
                !arena.Comp.HasResetWhileEmpty &&
                now - arena.Comp.EmptySince >= arena.Comp.EmptyResetDelay)
            {
                ResetBoss((arena.Owner, arena.Comp), now);
                arena.Comp.HasResetWhileEmpty = true;
            }

            if (arena.Comp.DeleteOnEmpty && now - arena.Comp.EmptySince >= EmptyCleanupDelay)
                arena.Comp.CleanupAt = now;
        }
        else
        {
            arena.Comp.EmptySince = null;
            arena.Comp.HasResetWhileEmpty = false;
        }
    }

    private void SendHudUpdateToParticipants(Entity<LavalandBossArenaComponent> arena)
    {
        if (!arena.Comp.FightStarted)
            return;

        foreach (var participant in arena.Comp.Participants)
        {
            if (_players.TryGetSessionById(participant, out var session))
                SendHudUpdate(session, arena.Comp);
        }
    }

    private void SendHudUpdate(ICommonSession session, LavalandBossArenaComponent arena)
    {
        if (!arena.FightStarted || !Exists(arena.Boss))
        {
            SendHideAndStop(session, arena.ArenaId);
            return;
        }

        var healthEv = GetHudHealth(arena);
        var ev = new LavalandBossHudUpdateEvent(
            arena.ArenaId,
            arena.BossName,
            healthEv.Current,
            healthEv.Max,
            arena.Participants.Count);
        RaiseNetworkEvent(ev, session.Channel);
    }

    /// <summary>Bosses made of several bodies (Legion) report their combined health.</summary>
    private LavalandBossHudHealthEvent GetHudHealth(LavalandBossArenaComponent arena)
    {
        var healthEv = new LavalandBossHudHealthEvent(
            GetCurrentBossHealth(arena.Boss, arena.ScaledMaxHealth),
            arena.ScaledMaxHealth,
            arena.ScaledMaxHealth / Math.Max(1f, arena.MaxHealth));
        RaiseLocalEvent(arena.Boss, ref healthEv);
        return healthEv;
    }

    /// <summary>
    /// Players built something in the arena: the fight starts right away, the exits close
    /// and jaunters stop working until the fight is over.
    /// </summary>
    public void HandleConstructionViolation(EntityUid arenaGrid)
    {
        if (!TryComp<LavalandBossArenaComponent>(arenaGrid, out var arena) || arena.Ended || !Exists(arena.Boss))
            return;

        if (!arena.FightStarted)
            StartFight((arenaGrid, arena));

        arena.SealedByConstruction = true;
        TrySealArena((arenaGrid, arena), true);
    }

    /// <summary>True if the entity stands in an arena that was sealed because of player construction.</summary>
    /// <summary>
    /// The arena is sealed - the boss is at half health or below, or players built inside:
    /// jaunters do not work until the fight ends.
    /// </summary>
    public bool IsInSealedArena(EntityUid uid)
    {
        return Transform(uid).GridUid is { } grid &&
               TryComp<LavalandBossArenaComponent>(grid, out var arena) &&
               (arena.Sealed || arena.SealedByConstruction) && !arena.Ended;
    }

    private void TrySealArena(Entity<LavalandBossArenaComponent> arena, bool force = false)
    {
        var comp = arena.Comp;
        if (comp.Sealed || comp.Ended || !comp.FightStarted ||
            comp.SealWallPrototype is not { } wall || !Exists(comp.Boss) ||
            !TryComp<MapGridComponent>(comp.Grid, out var grid))
        {
            return;
        }

        if (!force)
        {
            if (comp.SealHealthFraction <= 0f)
                return;

            var health = GetHudHealth(comp);
            if (health.Max <= 0f || health.Current > health.Max * comp.SealHealthFraction)
                return;
        }

        comp.Sealed = true;
        if (comp.SealTiles.Count > 0)
        {
            // Cave: wall the passages off across their narrow part.
            foreach (var (tile, inward) in comp.SealTiles)
                SealTile(arena, grid, wall, tile, inward);
        }
        else
        {
            var halfWidth = comp.Width / 2;
            var halfHeight = comp.Height / 2;
            for (var i = -EntranceHalfWidth; i <= EntranceHalfWidth; i++)
            {
                SealTile(arena, grid, wall, new Vector2i(i, halfHeight), new Vector2i(0, -1));
                SealTile(arena, grid, wall, new Vector2i(i, -halfHeight), new Vector2i(0, 1));
                SealTile(arena, grid, wall, new Vector2i(halfWidth, i), new Vector2i(-1, 0));
                SealTile(arena, grid, wall, new Vector2i(-halfWidth, i), new Vector2i(1, 0));
            }
        }

        if (comp.SealSound != null)
            _audio.PlayPvs(comp.SealSound, comp.Boss);

        if (comp.SealMessage is not { } message)
            return;

        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is { Valid: true } attached && IsCombatParticipant(attached, comp))
                _popup.PopupEntity(Loc.GetString(message), attached, attached, PopupType.LargeCaution);
        }
    }

    /// <summary>Walls off one entrance tile, pushing anyone standing in it into the arena.</summary>
    private void SealTile(
        Entity<LavalandBossArenaComponent> arena,
        MapGridComponent grid,
        EntProtoId wall,
        Vector2i tile,
        Vector2i inward)
    {
        var coordinates = _map.GridTileToLocal(arena.Comp.Grid, grid, tile);
        _sealMobs.Clear();
        _lookup.GetEntitiesInRange(coordinates, 0.45f, _sealMobs, LookupFlags.Dynamic);
        foreach (var mob in _sealMobs)
        {
            _transform.SetCoordinates(mob, _map.GridTileToLocal(arena.Comp.Grid, grid, tile + inward));
        }

        _sealMobs.Clear();
        var uid = Spawn(wall.Id, coordinates);
        if (TryComp(uid, out TransformComponent? xform) && !xform.Anchored)
            _transform.AnchorEntity((uid, xform), (arena.Comp.Grid, grid), tile);
        arena.Comp.SealWalls.Add(uid);
    }

    private void UnsealArena(LavalandBossArenaComponent arena)
    {
        foreach (var wall in arena.SealWalls)
        {
            if (!TerminatingOrDeleted(wall))
                QueueDel(wall);
        }

        arena.SealWalls.Clear();
        arena.Sealed = false;
    }

    private void SendMusicStart(ICommonSession session, LavalandBossArenaComponent arena)
    {
        if (!arena.FightStarted ||
            !TryComp<LavalandBossComponent>(arena.Boss, out var boss) ||
            boss.SelectedMusic == null)
        {
            return;
        }

        var audioParams = boss.SelectedMusic.Params.WithLoop(true);
        RaiseNetworkEvent(new LavalandBossMusicStartEvent(arena.ArenaId, _audio.ResolveSound(boss.SelectedMusic), audioParams), session.Channel);
    }

    private void SendHideAndStop(ICommonSession session, int arenaId)
    {
        RaiseNetworkEvent(new LavalandBossHudHideEvent(arenaId), session.Channel);
        RaiseNetworkEvent(new LavalandBossMusicStopEvent(arenaId), session.Channel);
    }

    private void SendHideAndStopToParticipants(LavalandBossArenaComponent arena)
    {
        foreach (var participant in arena.Participants)
        {
            if (_players.TryGetSessionById(participant, out var session))
                SendHideAndStop(session, arena.ArenaId);
        }
    }

    private void OnBossBeforeDamageChanged(Entity<LavalandBossComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled ||
            ent.Comp.Arena is not { Valid: true } arenaUid ||
            !TryComp<LavalandBossArenaComponent>(arenaUid, out var arena) ||
            arena.Ended ||
            !IncreasesDamage(args.Damage))
        {
            return;
        }

        if (IsDamageOriginFromArenaParticipant(arena, args.Origin))
            return;

        // Explosions do not currently preserve an attacker origin. Keep them from
        // waking a dormant boss, but don't make them useless during an active fight.
        if (arena.FightStarted && args.Origin == null)
            return;

        args.Cancelled = true;
    }

    private void OnBossDamageChanged(Entity<LavalandBossComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.Arena is not { Valid: true } arenaUid ||
            !TryComp<LavalandBossArenaComponent>(arenaUid, out var arena))
        {
            return;
        }

        if (!arena.FightStarted && ShouldStartFightByDamage(arena, args))
            StartFight((arenaUid, arena));

        TrySealArena((arenaUid, arena));
        SendHudUpdateToParticipants((arenaUid, arena));
    }

    private void OnBossMobStateChanged(Entity<LavalandBossComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead ||
            ent.Comp.Arena is not { Valid: true } arenaUid ||
            !TryComp<LavalandBossArenaComponent>(arenaUid, out var arena))
        {
            return;
        }

        if (ent.Comp.DeathSound != null)
            _audio.PlayPvs(ent.Comp.DeathSound, ent.Owner);

        if (!ent.Comp.DeathRewardsSpawned)
        {
            ent.Comp.DeathRewardsSpawned = true;
            SpawnDeathRewards(ent.Owner, ent.Comp);
        }

        QueueDel(ent.Owner);
        EndArena((arenaUid, arena));
    }

    private void SpawnDeathRewards(EntityUid bossUid, LavalandBossComponent boss)
    {
        if (boss.DeathRewards.Count == 0)
            return;

        if (!TryComp(bossUid, out TransformComponent? xform) ||
            xform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            if (!TryComp(bossUid, out TransformComponent? fallbackXform))
                return;

            foreach (var reward in boss.DeathRewards)
            {
                if (_prototype.HasIndex<EntityPrototype>(reward.Id))
                    Spawn(reward.Id, fallbackXform.Coordinates);
            }

            return;
        }

        var center = _map.LocalToTile(gridUid, grid, xform.Coordinates);
        for (var i = 0; i < boss.DeathRewards.Count; i++)
        {
            var reward = boss.DeathRewards[i];
            if (!_prototype.HasIndex<EntityPrototype>(reward.Id))
            {
                Log.Error($"Lavaland boss {ToPrettyString(bossUid)} has missing death reward prototype {reward}.");
                continue;
            }

            var offset = RewardOffsets[Math.Min(i, RewardOffsets.Length - 1)];
            var tile = center + offset;
            if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                tile = center;

            Spawn(reward.Id, _map.GridTileToLocal(gridUid, grid, tile));
        }
    }

    private void EndArena(Entity<LavalandBossArenaComponent> arena)
    {
        if (arena.Comp.Ended)
            return;

        arena.Comp.Ended = true;
        UnsealArena(arena.Comp);
        arena.Comp.SealedByConstruction = false;
        if (arena.Comp.DeleteOnBossDeath)
            arena.Comp.CleanupAt = _timing.CurTime + BossDeadCleanupDelay;

        SendHideAndStopToParticipants(arena.Comp);
    }

    private void CleanupArena(Entity<LavalandBossArenaComponent> arena)
    {
        if (arena.Comp.ReturnParticipantsOnDelete)
            ReturnParticipants(arena);

        SendHideAndStopToParticipants(arena.Comp);
        QueueDel(arena.Owner);
    }

    private void ResetBoss(Entity<LavalandBossArenaComponent> arena, TimeSpan now)
    {
        if (arena.Comp.Ended ||
            !Exists(arena.Comp.Boss) ||
            !TryComp<MapGridComponent>(arena.Comp.Grid, out var grid))
        {
            return;
        }

        _transform.SetCoordinates(arena.Comp.Boss, _map.GridTileToLocal(arena.Comp.Grid, grid, arena.Comp.BossSpawnTile));
        HealBossOnReset(arena.Comp);
        UnsealArena(arena.Comp);
        arena.Comp.SealedByConstruction = false;
        arena.Comp.FightStarted = false;
        arena.Comp.ScaledMaxHealth = arena.Comp.MaxHealth;
        arena.Comp.PeakParticipantCount = 0;
        arena.Comp.BossOutsideArenaSince = null;
        arena.Comp.NextBossLeashCheck = now + BossLeashCheckInterval;
        SetBossAiEnabled(arena.Comp.Boss, false);
        if (TryComp<MobThresholdsComponent>(arena.Comp.Boss, out var thresholds))
            _mobThreshold.SetMobStateThreshold(arena.Comp.Boss, FixedPoint2.New(arena.Comp.MaxHealth), MobState.Dead, thresholds);

        RaiseLocalEvent(arena.Comp.Boss, new LavalandBossResetEvent(arena.Owner, arena.Comp.BossSpawnTile));

        SendHideAndStopToParticipants(arena.Comp);

        arena.Comp.NextHudUpdate = now;
    }

    private void StartFight(Entity<LavalandBossArenaComponent> arena)
    {
        if (arena.Comp.FightStarted ||
            arena.Comp.Ended ||
            !Exists(arena.Comp.Boss))
        {
            return;
        }

        TryScaleBossHpForParticipants((arena.Owner, arena.Comp));

        arena.Comp.FightStarted = true;
        arena.Comp.EmptySince = null;
        arena.Comp.HasResetWhileEmpty = false;
        arena.Comp.BossOutsideArenaSince = null;
        arena.Comp.NextBossLeashCheck = _timing.CurTime + BossLeashCheckInterval;
        arena.Comp.NextHudUpdate = _timing.CurTime;

        SetBossAiEnabled(arena.Comp.Boss, true);
        RaiseLocalEvent(arena.Comp.Boss, new LavalandBossFightStartedEvent(arena.Owner));

        foreach (var participant in arena.Comp.Participants)
        {
            if (!_players.TryGetSessionById(participant, out var session))
                continue;

            SendHudUpdate(session, arena.Comp);
            SendMusicStart(session, arena.Comp);
        }
    }

    private bool ShouldStartFightByProximity(
        Entity<LavalandBossArenaComponent> arena,
        IEnumerable<ICommonSession> sessions)
    {
        if (!Exists(arena.Comp.Boss) ||
            !TryComp(arena.Comp.Boss, out TransformComponent? bossXform) ||
            bossXform.GridUid != arena.Comp.Grid)
        {
            return false;
        }

        var maxDistance = MathF.Max(0.1f, arena.Comp.FightStartDistance);
        var maxDistanceSquared = maxDistance * maxDistance;
        var bossPosition = bossXform.Coordinates.Position;

        foreach (var session in sessions)
        {
            if (session.AttachedEntity is not { Valid: true } attached ||
                !TryComp(attached, out TransformComponent? xform) ||
                xform.GridUid != arena.Comp.Grid)
            {
                continue;
            }

            if ((xform.Coordinates.Position - bossPosition).LengthSquared() <= maxDistanceSquared)
                return true;
        }

        return false;
    }

    private bool ShouldStartFightByDamage(LavalandBossArenaComponent arena, DamageChangedEvent args)
    {
        return args.DamageIncreased && IsDamageOriginFromArenaParticipant(arena, args.Origin);
    }

    private void UpdateBossLeash(Entity<LavalandBossArenaComponent> arena, TimeSpan now)
    {
        if (IsEntityInsideInnerArena(arena.Comp.Boss, arena.Comp))
        {
            arena.Comp.BossOutsideArenaSince = null;
            return;
        }

        arena.Comp.BossOutsideArenaSince ??= now;
        if (now - arena.Comp.BossOutsideArenaSince < BossLeashResetDelay)
            return;

        ResetBoss(arena, now);
    }

    private bool IsDamageOriginFromArenaParticipant(LavalandBossArenaComponent arena, EntityUid? damageOrigin)
    {
        if (damageOrigin is not { Valid: true } origin ||
            !Exists(origin))
        {
            return false;
        }

        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } attached ||
                !IsCombatParticipant(attached, arena))
            {
                continue;
            }

            if (origin == attached || IsTransformChildOf(origin, attached))
                return true;
        }

        return false;
    }

    private static bool IncreasesDamage(DamageSpecifier damage)
    {
        foreach (var amount in damage.DamageDict.Values)
        {
            if (amount > 0)
                return true;
        }

        return false;
    }

    internal bool IsCountedParticipant(EntityUid uid, LavalandBossArenaComponent arena)
    {
        return !HasComp<GuardianComponent>(uid) &&
               !HasComp<PAIComponent>(uid) &&
               IsCombatParticipant(uid, arena);
    }

    internal bool IsCombatParticipant(EntityUid uid, LavalandBossArenaComponent arena)
    {
        return Exists(uid) &&
               !HasComp<GhostComponent>(uid) &&
               !_container.IsEntityOrParentInContainer(uid) && // DS14
               !IsDead(uid) &&
               IsEntityInsideInnerArena(uid, arena);
    }

    private bool IsEntityInsideInnerArena(EntityUid uid, LavalandBossArenaComponent arena)
    {
        if (!TryComp(uid, out TransformComponent? xform) ||
            xform.GridUid != arena.Grid ||
            !TryComp<MapGridComponent>(arena.Grid, out var grid))
        {
            return false;
        }

        var tile = _map.LocalToTile(arena.Grid, grid, xform.Coordinates);
        // Cave: the pockets around the fight square count too, up to the passage seal.
        if (arena.ParticipantExtent > 0)
            return Math.Max(Math.Abs(tile.X), Math.Abs(tile.Y)) <= arena.ParticipantExtent;

        return IsInsideInnerArena(arena, tile);
    }

    private bool IsTransformChildOf(EntityUid uid, EntityUid expectedAncestor)
    {
        for (var i = 0; i < 32; i++)
        {
            if (!Exists(uid) ||
                !TryComp(uid, out TransformComponent? xform))
            {
                return false;
            }

            var parent = xform.ParentUid;
            if (!parent.IsValid() || parent == uid)
                return false;

            if (parent == expectedAncestor)
                return true;

            uid = parent;
        }

        return false;
    }

    private void SetBossAiEnabled(EntityUid boss, bool enabled)
    {
        if (TryComp<HTNComponent>(boss, out var htn))
            _htn.SetHTNEnabled((boss, htn), enabled, enabled ? 0.75f : 0f);
    }

    private void HealBossOnReset(LavalandBossArenaComponent arena)
    {
        if (!TryComp<DamageableComponent>(arena.Boss, out var damageable))
            return;

        var totalDamage = damageable.TotalDamage.Float();
        if (totalDamage <= 0f)
            return;

        var heal = Math.Max(totalDamage * arena.EmptyResetHealFraction, arena.EmptyResetMinHeal);
        heal = Math.Clamp(heal, 0f, totalDamage);
        if (heal <= 0f)
            return;

        if (heal >= totalDamage - 0.01f)
        {
            _damageable.ClearAllDamage((arena.Boss, damageable));
            return;
        }

        _damageable.HealDistributed((arena.Boss, damageable), FixedPoint2.New(-heal), origin: arena.Boss);
    }

    private void ReturnParticipants(Entity<LavalandBossArenaComponent> arena)
    {
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } attached ||
                !Exists(attached) ||
                !TryComp(attached, out TransformComponent? xform) ||
                xform.GridUid != arena.Comp.Grid)
            {
                continue;
            }

            if (arena.Comp.ReturnCoordinates.TryGetValue(session.UserId, out var coordinates))
                _transform.SetMapCoordinates(attached, coordinates);
        }
    }

    private void OnArenaShutdown(Entity<LavalandBossArenaComponent> ent, ref ComponentShutdown args)
    {
        SendHideAndStopToParticipants(ent.Comp);
    }

    private bool IsDead(EntityUid uid)
    {
        return TryComp(uid, out MobStateComponent? mobState) && mobState.CurrentState == MobState.Dead;
    }

    private float GetCurrentBossHealth(EntityUid boss, float maxHealth)
    {
        if (!TryComp<DamageableComponent>(boss, out var damageable))
            return maxHealth;

        return Math.Clamp(maxHealth - damageable.TotalDamage.Float(), 0f, maxHealth);
    }

    private bool HasNearbyNonTerrainGrid(EntityUid terrainGridUid, Vector2 center, float radius)
    {
        _nearbyGrids.Clear();

        var mapId = Transform(terrainGridUid).MapID;
        var bounds = Box2.CenteredAround(center, Vector2.One * (radius * 2f + 1f));
        _mapManager.FindGridsIntersecting(mapId, bounds, ref _nearbyGrids);

        foreach (var grid in _nearbyGrids)
        {
            if (grid.Owner != terrainGridUid)
                return true;
        }

        return false;
    }

    private bool HasPersistentAnchoredEntityInFootprint(
        EntityUid terrainGridUid,
        MapGridComponent terrainGrid,
        Vector2i center,
        int width,
        int height)
    {
        var bounds = GetReservationBounds(center, width, height);

        for (var x = bounds.Left; x < bounds.Right; x++)
        {
            for (var y = bounds.Bottom; y < bounds.Top; y++)
            {
                var anchored = _map.GetAnchoredEntitiesEnumerator(terrainGridUid, terrainGrid, new Vector2i(x, y));
                while (anchored.MoveNext(out var uid))
                {
                    if (uid == null ||
                        TerminatingOrDeleted(uid.Value) ||
                        IsClearableTerrainEntity(uid.Value))
                    {
                        continue;
                    }

                    return true;
                }
            }
        }

        return false;
    }

    private void ClearClearableTerrainEntities(EntityUid terrainGridUid, MapGridComponent terrainGrid, Box2i bounds)
    {
        _anchoredToDelete.Clear();

        for (var x = bounds.Left; x < bounds.Right; x++)
        {
            for (var y = bounds.Bottom; y < bounds.Top; y++)
            {
                var anchored = _map.GetAnchoredEntitiesEnumerator(terrainGridUid, terrainGrid, new Vector2i(x, y));
                while (anchored.MoveNext(out var uid))
                {
                    if (uid != null &&
                        !TerminatingOrDeleted(uid.Value) &&
                        IsClearableTerrainEntity(uid.Value))
                    {
                        _anchoredToDelete.Add(uid.Value);
                    }
                }
            }
        }

        foreach (var uid in _anchoredToDelete)
        {
            if (!TerminatingOrDeleted(uid))
                QueueDel(uid);
        }

        _anchoredToDelete.Clear();
    }

    private bool IsClearableTerrainEntity(EntityUid uid)
    {
        if (HasComp<TileEntityEffectComponent>(uid) ||
            HasComp<ChasmComponent>(uid) ||
            HasComp<OreVeinComponent>(uid))
        {
            return true;
        }

        var prototypeId = MetaData(uid).EntityPrototype?.ID;
        return prototypeId is not null &&
               (prototypeId.StartsWith("WallRockBasalt", StringComparison.Ordinal) ||
                prototypeId is "FloorLavaEntity" or "FloorChasmEntity");
    }

    private static bool IsInsideArenaExclusion(
        Vector2 center,
        LavalandPlanetPrototype planet,
        LavalandBossArenaPrototype arenaPrototype,
        float arenaRadius)
    {
        var landingRadius = arenaPrototype.LandingExclusionRadius + arenaRadius;
        if (landingRadius > 0f && center.LengthSquared() <= landingRadius * landingRadius)
            return true;

        if (planet.TerminalReservationEnabled)
        {
            var outpostRadius = arenaPrototype.OutpostExclusionRadius + arenaRadius;
            if (outpostRadius > 0f &&
                Vector2.DistanceSquared(center, planet.TerminalGridOffset) <= outpostRadius * outpostRadius)
            {
                return true;
            }
        }

        if (planet.FtlEnabled)
        {
            var ftlRadius = arenaPrototype.FtlBeaconExclusionRadius + arenaRadius;
            if (ftlRadius > 0f &&
                Vector2.DistanceSquared(center, planet.FtlBeaconOffset) <= ftlRadius * ftlRadius)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetPlacementLimit(LavalandPlanetPrototype planet, LavalandBossArenaPrototype arenaPrototype, int width, int height)
    {
        if (planet.MapHalfSize <= 0)
            return 0;

        var boundaryPadding = planet.BoundaryEnabled
            ? Math.Max(0, planet.BoundaryLavaWidth) + Math.Max(1, planet.BoundaryWallWidth)
            : 0;

        var arenaHalf = Math.Max(width, height) / 2 + EntranceDepth + 2;
        var padding = MathF.Ceiling(Math.Max(0f, arenaPrototype.MapEdgePadding)) + boundaryPadding + arenaHalf;
        return Math.Max(0, planet.MapHalfSize - (int) padding);
    }

    private static float GetArenaRadius(int width, int height)
    {
        var diameter = Math.Max(width, height) + EntranceDepth * 2;
        return diameter * 0.5f;
    }

    private static float GetDefaultFightStartDistance(int width, int height)
    {
        return Math.Clamp(Math.Min(width, height) * FightStartDistanceFraction,
            MinFightStartDistance,
            MaxFightStartDistance);
    }

    private static bool IsInsideInnerArena(LavalandBossArenaComponent arena, Vector2i tile)
    {
        var (minX, maxX, minY, maxY) = GetInnerBounds(arena);
        return tile.X >= minX && tile.X <= maxX && tile.Y >= minY && tile.Y <= maxY;
    }

    private static (int MinX, int MaxX, int MinY, int MaxY) GetInnerBounds(LavalandBossArenaComponent arena)
    {
        var halfWidth = arena.Width / 2;
        var halfHeight = arena.Height / 2;
        return (-halfWidth + 1, halfWidth - 1, -halfHeight + 1, halfHeight - 1);
    }

    private static Box2i GetReservationBounds(Vector2i center, int width, int height)
    {
        var halfWidth = width / 2 + EntranceDepth;
        var halfHeight = height / 2 + EntranceDepth;
        return new Box2i(
            center.X - halfWidth,
            center.Y - halfHeight,
            center.X + halfWidth + 1,
            center.Y + halfHeight + 1);
    }

    private void AddFloorRect(
        List<(Vector2i Index, Tile Tile)> tiles,
        ITileDefinition tileDef,
        int minX,
        int minY,
        int maxX,
        int maxY)
    {
        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                tiles.Add((new Vector2i(x, y), CreateTile(tileDef)));
            }
        }
    }

    private Tile CreateTile(ITileDefinition tileDef)
    {
        return new Tile(tileDef.TileId,
            variant: tileDef is ContentTileDefinition contentTile
                ? _tile.PickVariant(contentTile)
                : (byte) 0);
    }

    private void SpawnAnchored(string prototype, EntityUid gridUid, MapGridComponent grid, Vector2i index)
    {
        var uid = Spawn(prototype, _map.GridTileToLocal(gridUid, grid, index));
        if (!TryComp(uid, out TransformComponent? xform) || xform.Anchored)
            return;

        _transform.AnchorEntity((uid, xform), (gridUid, grid), index);
    }

    private static int NormalizeArenaSize(int value)
    {
        value = Math.Clamp(value, MinArenaSize, MaxArenaSize);
        if (value % 2 == 0)
            value++;

        return Math.Min(value, MaxArenaSize);
    }
    private void TryScaleBossHpForParticipants(Entity<LavalandBossArenaComponent> arena)
    {
        var count = arena.Comp.Participants.Count;
        if (count == 0 || count <= arena.Comp.PeakParticipantCount)
            return;

        arena.Comp.PeakParticipantCount = count;

        float newMax;
        if (!arena.Comp.FightStarted)
            newMax = arena.Comp.MaxHealth * (1f + 0.20f * (count - 1));
        else
            newMax = arena.Comp.ScaledMaxHealth * 1.30f;

        if (newMax <= arena.Comp.ScaledMaxHealth)
            return;

        var healDelta = newMax - arena.Comp.ScaledMaxHealth;
        arena.Comp.ScaledMaxHealth = newMax;

        if (TryComp<DamageableComponent>(arena.Comp.Boss, out var damageable))
            _damageable.HealDistributed((arena.Comp.Boss, damageable), FixedPoint2.New(healDelta), origin: arena.Comp.Boss);

        if (TryComp<MobThresholdsComponent>(arena.Comp.Boss, out var thresholds))
            _mobThreshold.SetMobStateThreshold(arena.Comp.Boss, FixedPoint2.New(newMax), MobState.Dead, thresholds);
    }
}
