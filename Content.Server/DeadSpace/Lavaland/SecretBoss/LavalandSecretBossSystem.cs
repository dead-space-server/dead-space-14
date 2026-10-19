// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Server.Backmen.Body.Components;
using Content.Server.DeadSpace.Lavaland.ChromiteCaves;
using Content.Server.DeadSpace.Lavaland.Celestial;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Chat.Managers;
using Content.Server.EUI;
using Content.Shared.Chat;
using Robust.Shared.Utility;
using Content.Server.Parallax;
using Content.Shared.Atmos;
using Content.Shared.Humanoid;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Mind;
using Content.Shared.Chasm;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Lavaland.Celestial;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Lavaland.Bosses;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.DeadSpace.Lavaland.SecretBoss;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Gravity;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Parallax;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Popups;
using Content.Shared.Slippery;
using Content.Shared.StatusEffectNew;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland.SecretBoss;

/// <summary>
/// The secret lavaland boss. Settings: <see cref="LavalandSecretBossConfig"/> in the planet prototype.
/// Dormant -> (all arena bosses dead) roar, pit and tablet interference -> players gather and jump -> fall ->
/// maze -> flower -> sleep -> Celestial fight on a glass square -> defeat cutscene, sleep by the pit, blessings.
/// </summary>
public sealed class LavalandSecretBossSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly CelestialSystem _celestial = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly Content.Shared.Inventory.InventorySystem _inventory = default!;
    [Dependency] private readonly Content.Shared.Hands.EntitySystems.SharedHandsSystem _hands = default!;
    [Dependency] private readonly Content.Shared.Humanoid.SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly EuiManager _eui = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    private static readonly Color BlessingColor = Color.FromHex("#c47bff");
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinition = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly LavalandPitLandingSystem _landing = default!;
    [Dependency] private readonly LavalandPitDropSystem _pitDrop = default!;
    [Dependency] private readonly LavalandChromiteCavesSystem _caves = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly Content.Shared.Camera.SharedCameraRecoilSystem _camera = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SleepingSystem _sleeping = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly Robust.Shared.Containers.SharedContainerSystem _containers = default!;
    [Dependency] private readonly Content.Shared.Stacks.SharedStackSystem _stacks = default!;
    [Dependency] private readonly Content.Shared.Storage.EntitySystems.SharedStorageSystem _storage = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan HudInterval = TimeSpan.FromSeconds(0.5);

    private readonly List<EntityUid> _toProcess = new();
    private readonly HashSet<Entity<LavalandFaunaComponent>> _fauna = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandCelestialDreamerComponent, BeforeDamageChangedEvent>(OnDreamerDamage);

        SubscribeLocalEvent<ChasmFallingAttemptEvent>(OnChasmFallingAttempt);
        SubscribeLocalEvent<LavalandSecretBossMobComponent, MobStateChangedEvent>(OnBossMobStateChanged);
        SubscribeLocalEvent<LavalandSecretBossComponent, ComponentShutdown>(OnShutdown);
    }

    #region Public helpers (tablet)

    /// <summary>Config and pit of the lavaland map the entity is on, while the pit matters.</summary>
    public bool TryGetPit(EntityUid? map, out Vector2 pitPosition, out Vector2 noiseCenter, out LavalandSecretBossConfig config)
    {
        pitPosition = default;
        noiseCenter = default;
        config = default!;
        if (!TryComp<LavalandSecretBossComponent>(map, out var boss) ||
            boss.Stage is LavalandSecretBossStage.Dormant or LavalandSecretBossStage.Awakening or LavalandSecretBossStage.Done ||
            boss.Pit is not { } pit || TerminatingOrDeleted(pit) ||
            !TryGetConfig(map.Value, out config))
        {
            return false;
        }

        pitPosition = _transform.GetWorldPosition(pit);
        noiseCenter = boss.NoiseCenter;
        return true;
    }

    /// <summary>Interference areas of every lavaland map with an open pit.</summary>
    public IEnumerable<(Vector2 Center, float Radius, Color Color)> GetNoiseAreas()
    {
        var query = EntityQueryEnumerator<LavalandSecretBossComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (TryGetPit(uid, out _, out var center, out var config))
                yield return (center, config.NoiseRadius, config.NoiseColor);
        }
    }

    #endregion

    /// <summary>
    /// Admin command: the pit and the Celestial's maze open on this lavaland right away, whether the arena bosses
    /// are dead or not. False if the secret boss is off there or the way down is already open (or was gone through).
    /// </summary>
    public bool ForceOpen(EntityUid map)
    {
        if (!TryGetConfig(map, out _))
            return false;

        var comp = EnsureComp<LavalandSecretBossComponent>(map);
        if (comp.Stage is not (LavalandSecretBossStage.Dormant or LavalandSecretBossStage.Awakening))
            return false;

        comp.Stage = LavalandSecretBossStage.Awakening;
        comp.StageTime = _timing.CurTime;
        return true;
    }

    private bool TryGetConfig(EntityUid map, out LavalandSecretBossConfig config)
    {
        config = default!;
        if (!TryComp<LavalandMapComponent>(map, out var lavaland) ||
            lavaland.Planet.Id == null || !_prototype.TryIndex(lavaland.Planet, out var planet) ||
            !planet.SecretBoss.Enabled)
        {
            return false;
        }

        config = planet.SecretBoss;
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandMapComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!TryGetConfig(uid, out var config))
                continue;

            var boss = EnsureComp<LavalandSecretBossComponent>(uid);
            UpdateStage((uid, boss), config, now);
            UpdateAwakenLines((uid, boss), config, now);
            UpdateAwakenShake((uid, boss), now);
        }

        UpdateDescents(now);
        UpdateRegeneration(now);
        UpdatePenalties(now);
    }

    private void UpdateStage(Entity<LavalandSecretBossComponent> ent, LavalandSecretBossConfig config, TimeSpan now)
    {
        var comp = ent.Comp;
        switch (comp.Stage)
        {
            case LavalandSecretBossStage.Dormant:
                if (now < comp.NextCheck)
                    return;
                comp.NextCheck = now + TimeSpan.FromSeconds(5);
                if (AllArenaBossesDead(ent))
                {
                    comp.Stage = LavalandSecretBossStage.Awakening;
                    comp.StageTime = now + config.AwakenDelay;
                }
                return;

            case LavalandSecretBossStage.Awakening:
                if (now >= comp.StageTime)
                    Awaken(ent, config);
                return;

            case LavalandSecretBossStage.PitOpen:
                CheckFlower(ent, config, now);
                if (now < comp.NextCheck)
                    return;
                comp.NextCheck = now + CheckInterval;
                ClearFaunaNearPit(ent, config);
                UpdateGathering(ent, config, now);
                return;

            case LavalandSecretBossStage.Sleeping:
                if (now < comp.StageTime)
                    return;
                if (comp.Step == 0)
                    PutEveryoneToSleep(ent, config, now);
                else
                    StartFight(ent, config, now);
                return;

            case LavalandSecretBossStage.Fight:
                UpdateFight(ent, config, now);
                return;

            case LavalandSecretBossStage.Done:
                if (!comp.PlayersAwake && now >= comp.StageTime)
                    WakeAndBless(ent, config);
                return;
        }
    }

    #region Awakening

    private bool AllArenaBossesDead(Entity<LavalandSecretBossComponent> ent)
    {
        var arenas = EntityQueryEnumerator<LavalandBossArenaComponent>();
        var caves = _caves.GetCaves(ent.Owner);
        while (arenas.MoveNext(out var arenaUid, out var arena))
        {
            // The Hierophant fights in the chromite caves under this lavaland.
            if (arena.Map != ent.Owner && (caves == null || arena.Map != caves))
                continue;

            ent.Comp.SeenArenas.Add(arenaUid);
            if (!arena.Ended)
                return false;
        }

        return ent.Comp.SeenArenas.Count > 0;
    }

    private void Awaken(Entity<LavalandSecretBossComponent> ent, LavalandSecretBossConfig config)
    {
        var mapUid = ent.Owner;
        if (!TryComp<MapGridComponent>(mapUid, out var grid) || !TryPlacePit(ent, config, grid))
        {
            // Try again a bit later (terrain may still be generating).
            ent.Comp.StageTime = _timing.CurTime + TimeSpan.FromSeconds(10);
            return;
        }

        BuildZone(ent, config);

        // No roar: the Celestial only speaks from its dream, line by line, right away - to everyone on lavaland.

        ent.Comp.AwakenLine = config.AwakenLines.Count > 0 ? 0 : -1;
        ent.Comp.NextAwakenLine = _timing.CurTime;

        ent.Comp.Stage = LavalandSecretBossStage.PitOpen;
        ent.Comp.NextCheck = _timing.CurTime;
        ClearFaunaNearPit(ent, config);
    }

    /// <summary>The words from the Celestial's dream, one subtitle after another, heard around the pit.</summary>
    private void UpdateAwakenLines(Entity<LavalandSecretBossComponent> ent, LavalandSecretBossConfig config, TimeSpan now)
    {
        var comp = ent.Comp;
        if (comp.AwakenLine < 0 || now < comp.NextAwakenLine)
            return;

        if (comp.AwakenLine >= config.AwakenLines.Count)
        {
            comp.AwakenLine = -1;
            return;
        }

        var line = Loc.GetString(config.AwakenLines[comp.AwakenLine]);
        var audience = LavalandAudience(ent);
        RaiseNetworkEvent(new CelestialSpeakEvent(line, config.AwakenLineDuration), audience);

        // Its voice with every line, a random one of its talk sounds.
        if (config.AwakenTalkSounds.Count > 0)
        {
            _audio.PlayGlobal(_audio.ResolveSound(_random.Pick(config.AwakenTalkSounds)), audience, false,
                AudioParams.Default.WithVolume(config.AwakenTalkVolume));
        }

        // Its next to last words shake all of lavaland.
        if (comp.AwakenLine == config.AwakenLines.Count - 2)
            comp.ShakeUntil = now + TimeSpan.FromSeconds(config.AwakenLineDuration);

        comp.AwakenLine++;
        comp.NextAwakenLine = now + TimeSpan.FromSeconds(config.AwakenLineDuration);
    }

    /// <summary>Every player on this lavaland and in the chromite caves under it.</summary>
    private Filter LavalandAudience(EntityUid lavaland)
    {
        var filter = Filter.BroadcastMap(Transform(lavaland).MapID);
        if (_caves.GetCaves(lavaland) is { } caves)
            filter.AddWhere(session =>
                session.AttachedEntity is { } mob && Transform(mob).MapUid == caves);
        return filter;
    }

    /// <summary>While the Celestial says its next to last words every screen on lavaland shakes hard.</summary>
    private void UpdateAwakenShake(Entity<LavalandSecretBossComponent> ent, TimeSpan now)
    {
        if (now >= ent.Comp.ShakeUntil || now < ent.Comp.NextShake)
            return;

        ent.Comp.NextShake = now + AwakenShakeInterval;
        foreach (var session in LavalandAudience(ent).Recipients)
        {
            if (session.AttachedEntity is { } mob)
                _camera.KickCamera(mob, _random.NextAngle().ToVec() * AwakenShakeStrength);
        }
    }

    private static readonly TimeSpan AwakenShakeInterval = TimeSpan.FromSeconds(0.08);
    private const float AwakenShakeStrength = 2.5f;

    private bool TryPlacePit(Entity<LavalandSecretBossComponent> ent, LavalandSecretBossConfig config, MapGridComponent grid)
    {
        var mapUid = ent.Owner;
        var origin = GetOutpostCenter(mapUid);
        var halfSize = 300f;
        if (TryComp<LavalandMapComponent>(mapUid, out var lavaland) &&
            lavaland.Planet.Id != null && _prototype.TryIndex(lavaland.Planet, out var planet) && planet.MapHalfSize > 0)
        {
            halfSize = planet.MapHalfSize - 24f;
        }

        var angle = _random.NextAngle();
        var distance = _random.NextFloat(config.PitMinDistance, Math.Max(config.PitMinDistance, config.PitMaxDistance));
        var position = origin + angle.ToVec() * distance;
        position = Vector2.Clamp(position, new Vector2(-halfSize), new Vector2(halfSize));
        var center = new Vector2i((int) MathF.Round(position.X), (int) MathF.Round(position.Y));

        // Clear a small basalt patch around the pit so rock or lava doesn't swallow it.
        const int clear = 3;
        if (TryComp<BiomeComponent>(mapUid, out var biome))
        {
            var reserved = new List<(Vector2i Index, Tile Tile)>();
            _biome.ReserveTiles(mapUid, new Box2(center.X - clear, center.Y - clear, center.X + clear + 1, center.Y + clear + 1),
                reserved, biome, grid);
        }

        if (!_tileDefinition.TryGetDefinition("FloorBasalt", out var basalt))
            return false;

        var tiles = new List<(Vector2i, Tile)>();
        for (var x = -clear; x <= clear; x++)
        {
            for (var y = -clear; y <= clear; y++)
            {
                var index = center + new Vector2i(x, y);
                tiles.Add((index, new Tile(basalt.TileId)));
                var anchored = _map.GetAnchoredEntitiesEnumerator(mapUid, grid, index);
                while (anchored.MoveNext(out var anchoredUid))
                {
                    if (!HasComp<MobStateComponent>(anchoredUid))
                        QueueDel(anchoredUid.Value);
                }
            }
        }

        _map.SetTiles(mapUid, grid, tiles);

        var pit = Spawn(config.PitPrototype, _map.GridTileToLocal(mapUid, grid, center));
        EnsureComp<LavalandCelestialPitComponent>(pit).LavalandMap = mapUid;
        ent.Comp.Pit = pit;

        // The interference circle hides where exactly the pit is.
        var offset = _random.NextAngle().ToVec() * _random.NextFloat(0f, config.NoiseRadius * 0.75f);
        ent.Comp.NoiseCenter = _transform.GetWorldPosition(pit) + offset;
        return true;
    }

    private Vector2 GetOutpostCenter(EntityUid map)
    {
        var outposts = EntityQueryEnumerator<LavalandOutpostComponent, MapGridComponent, TransformComponent>();
        while (outposts.MoveNext(out _, out _, out var grid, out var xform))
        {
            if (xform.MapUid == map)
                return Vector2.Transform(grid.LocalAABB.Center, _transform.GetWorldMatrix(xform));
        }

        return Vector2.Zero;
    }

    #endregion

    #region Zone generation

    private void BuildZone(Entity<LavalandSecretBossComponent> ent, LavalandSecretBossConfig config)
    {
        DeleteZone(ent.Comp);
        BuildMaze(ent.Comp, config, ent.Owner);
        BuildArena(ent.Comp, config);
    }

    private void DeleteZone(LavalandSecretBossComponent comp)
    {
        if (comp.MazeMap is { } maze && !TerminatingOrDeleted(maze))
            QueueDel(maze);
        if (comp.ArenaMap is { } arena && !TerminatingOrDeleted(arena))
            QueueDel(arena);
        comp.MazeMap = null;
        comp.ArenaMap = null;
        comp.Flower = null;
        comp.FlowerRoom.Clear();
        comp.MazeOpenTiles.Clear();
    }

    private EntityUid CreateZoneMap(Color light, out MapId mapId)
    {
        var mapUid = _map.CreateMap(out mapId);
        EnsureComp<LavalandNoTeleportZoneComponent>(mapUid);

        var mapLight = EnsureComp<MapLightComponent>(mapUid);
        mapLight.AmbientLightColor = light;
        Dirty(mapUid, mapLight);

        var gravity = EnsureComp<GravityComponent>(mapUid);
        gravity.Enabled = true;
        gravity.Inherent = true;
        Dirty(mapUid, gravity);

        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int) Gas.Oxygen] = Atmospherics.OxygenMolesStandard;
        moles[(int) Gas.Nitrogen] = Atmospherics.NitrogenMolesStandard;
        _atmosphere.SetMapAtmosphere(mapUid, false, new GasMixture(moles, Atmospherics.T20C));
        return mapUid;
    }

    /// <summary>
    /// Perfect maze (recursive backtracker) of 2x2 cells with 1 tile walls. Players start in an open room in the
    /// middle; each corner holds a room, one of them with the flower.
    /// </summary>
    private void BuildMaze(LavalandSecretBossComponent comp, LavalandSecretBossConfig config, EntityUid lavalandMap)
    {
        var mapUid = CreateZoneMap(config.MazeLightColor, out var mapId);
        comp.MazeMap = mapUid;
        EnsureComp<LavalandCelestialMazeComponent>(mapUid).LavalandMap = lavalandMap;
        _metaData.SetEntityName(mapUid, "Celestial maze");

        var cells = Math.Max(5, config.MazeCells);
        var size = cells * 3 + 1;
        var open = new bool[size, size];

        void OpenCell(int cx, int cy)
        {
            for (var dx = 0; dx < 2; dx++)
            for (var dy = 0; dy < 2; dy++)
                open[1 + cx * 3 + dx, 1 + cy * 3 + dy] = true;
        }

        void OpenPassage(int cx, int cy, int nx, int ny)
        {
            if (nx != cx)
            {
                var x = 3 + Math.Min(cx, nx) * 3;
                open[x, 1 + cy * 3] = true;
                open[x, 2 + cy * 3] = true;
            }
            else
            {
                var y = 3 + Math.Min(cy, ny) * 3;
                open[1 + cx * 3, y] = true;
                open[2 + cx * 3, y] = true;
            }
        }

        // Opens a block of cells into one room, including the wall posts inside it.
        void OpenRoom(int fromX, int fromY, int toX, int toY)
        {
            for (var x = 1 + fromX * 3; x <= 2 + toX * 3; x++)
            for (var y = 1 + fromY * 3; y <= 2 + toY * 3; y++)
                open[x, y] = true;
        }

        for (var cx = 0; cx < cells; cx++)
        for (var cy = 0; cy < cells; cy++)
            OpenCell(cx, cy);

        var visited = new bool[cells, cells];
        var start = new Vector2i(cells / 2, cells / 2);
        var stack = new Stack<Vector2i>();
        stack.Push(start);
        visited[start.X, start.Y] = true;
        var directions = new[] { new Vector2i(1, 0), new Vector2i(-1, 0), new Vector2i(0, 1), new Vector2i(0, -1) };
        while (stack.Count > 0)
        {
            var current = stack.Peek();
            var options = new List<Vector2i>();
            foreach (var dir in directions)
            {
                var next = current + dir;
                if (next.X >= 0 && next.Y >= 0 && next.X < cells && next.Y < cells && !visited[next.X, next.Y])
                    options.Add(next);
            }

            if (options.Count == 0)
            {
                stack.Pop();
                continue;
            }

            var chosen = _random.Pick(options);
            OpenPassage(current.X, current.Y, chosen.X, chosen.Y);
            visited[chosen.X, chosen.Y] = true;
            stack.Push(chosen);
        }

        // Start room in the middle, rooms in the corners.
        OpenRoom(start.X - 1, start.Y - 1, start.X + 1, start.Y + 1);
        var corners = new[]
        {
            (0, 0, 1, 1),
            (cells - 2, 0, cells - 1, 1),
            (0, cells - 2, 1, cells - 1),
            (cells - 2, cells - 2, cells - 1, cells - 1),
        };
        foreach (var (fx, fy, tx, ty) in corners)
        {
            OpenRoom(fx, fy, tx, ty);
        }

        var grid = _mapManager.CreateGridEntity(mapId);
        _metaData.SetEntityName(grid.Owner, "Celestial maze");
        RemComp<ImplicitRoofComponent>(grid.Owner);
        if (!_tileDefinition.TryGetDefinition(config.MazeFloorTile, out var floorDef))
            return;

        var tiles = new List<(Vector2i, Tile)>(size * size);
        for (var x = 0; x < size; x++)
        for (var y = 0; y < size; y++)
            tiles.Add((new Vector2i(x, y), new Tile(floorDef.TileId)));
        _map.SetTiles(grid.Owner, grid.Comp, tiles);

        for (var x = 0; x < size; x++)
        {
            for (var y = 0; y < size; y++)
            {
                if (open[x, y])
                {
                    comp.MazeOpenTiles.Add(_transform.ToMapCoordinates(
                        _map.GridTileToLocal(grid.Owner, grid.Comp, new Vector2i(x, y))));
                    continue;
                }

                Spawn(config.MazeWall, _map.GridTileToLocal(grid.Owner, grid.Comp, new Vector2i(x, y)));
            }
        }

        var startTile = new Vector2i(1 + start.X * 3 + 1, 1 + start.Y * 3 + 1);
        comp.MazeStart = _transform.ToMapCoordinates(_map.GridTileToLocal(grid.Owner, grid.Comp, startTile));

        // Flower in one random corner room.
        var (rfx, rfy, rtx, rty) = _random.Pick(corners);
        var roomMin = new Vector2i(1 + rfx * 3, 1 + rfy * 3);
        var roomMax = new Vector2i(2 + rtx * 3, 2 + rty * 3);
        var flowerTile = new Vector2i((roomMin.X + roomMax.X) / 2, (roomMin.Y + roomMax.Y) / 2);
        var flower = Spawn(config.FlowerPrototype, _map.GridTileToLocal(grid.Owner, grid.Comp, flowerTile));
        comp.Flower = flower;
        for (var x = roomMin.X; x <= roomMax.X; x++)
        for (var y = roomMin.Y; y <= roomMax.Y; y++)
            comp.FlowerRoom.Add(_transform.ToMapCoordinates(_map.GridTileToLocal(grid.Owner, grid.Comp, new Vector2i(x, y))));

        // A tiny grid far away where falling players wait: they see only each other.
        var holding = _mapManager.CreateGridEntity(mapId);
        _transform.SetMapCoordinates(holding.Owner, new MapCoordinates(new Vector2(size + 500, size + 500), mapId));
        var holdingTiles = new List<(Vector2i, Tile)>();
        for (var x = -1; x <= 1; x++)
        for (var y = -1; y <= 1; y++)
            holdingTiles.Add((new Vector2i(x, y), new Tile(floorDef.TileId)));
        _map.SetTiles(holding.Owner, holding.Comp, holdingTiles);
        comp.FallHolding = _transform.ToMapCoordinates(_map.GridTileToLocal(holding.Owner, holding.Comp, Vector2i.Zero));

        FillWithAir(grid.Owner, grid.Comp);
        FillWithAir(holding.Owner, holding.Comp);
    }

    /// <summary>
    /// Air on every tile of a zone grid, walled-in rooms too. Removing its grid atmosphere is no use: once its tiles
    /// are laid the grid is heavy enough to get one back automatically - an empty one, and its rooms are vacuum.
    /// </summary>
    private void FillWithAir(EntityUid gridUid, MapGridComponent grid)
    {
        var atmos = EnsureComp<GridAtmosphereComponent>(gridUid);
        EnsureComp<GasTileOverlayComponent>(gridUid);
        _atmosphere.RebuildGridAtmosphere((gridUid, atmos, grid));
    }

    /// <summary>Reinforced glass square over the void with The Circle parallax, walled in.</summary>
    private void BuildArena(LavalandSecretBossComponent comp, LavalandSecretBossConfig config)
    {
        var mapUid = CreateZoneMap(config.ArenaLightColor, out var mapId);
        comp.ArenaMap = mapUid;
        _metaData.SetEntityName(mapUid, "Celestial arena");

        var parallax = EnsureComp<ParallaxComponent>(mapUid);
        parallax.Parallax = config.ArenaParallax;
        Dirty(mapUid, parallax);

        var grid = _mapManager.CreateGridEntity(mapId);
        _metaData.SetEntityName(grid.Owner, "Celestial arena");
        RemComp<ImplicitRoofComponent>(grid.Owner);
        if (!_tileDefinition.TryGetDefinition(config.ArenaTile, out var glass))
            return;

        var half = Math.Max(5, config.ArenaSize / 2);
        var hole = Math.Clamp(config.ArenaHoleRadius, 0, half - 3);
        var tiles = new List<(Vector2i, Tile)>();
        for (var x = -half - 1; x <= half + 1; x++)
        {
            for (var y = -half - 1; y <= half + 1; y++)
            {
                // The Celestial hangs over a hole in the middle of the square.
                if (Math.Max(Math.Abs(x), Math.Abs(y)) < hole)
                    continue;
                tiles.Add((new Vector2i(x, y), new Tile(glass.TileId)));
            }
        }
        _map.SetTiles(grid.Owner, grid.Comp, tiles);

        // Invisible walls along the edge and around the hole.
        for (var x = -half - 1; x <= half + 1; x++)
        {
            for (var y = -half - 1; y <= half + 1; y++)
            {
                var ring = Math.Max(Math.Abs(x), Math.Abs(y));
                if (ring == half + 1)
                    Spawn(config.ArenaBorder, _map.GridTileToLocal(grid.Owner, grid.Comp, new Vector2i(x, y)));
                else if (hole > 0 && ring == hole)
                    Spawn(config.HoleFence, _map.GridTileToLocal(grid.Owner, grid.Comp, new Vector2i(x, y)));
            }
        }

        comp.ArenaCenter = _transform.ToMapCoordinates(_map.GridTileToLocal(grid.Owner, grid.Comp, Vector2i.Zero));
        FillWithAir(grid.Owner, grid.Comp);
    }

    #endregion

    #region Gathering and the jump

    private IEnumerable<(ICommonSession Session, EntityUid Mob)> LivingPlayersOn(EntityUid map)
    {
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } mob ||
                HasComp<GhostComponent>(mob) ||
                !TryComp<MobStateComponent>(mob, out var state) ||
                !_mobState.IsAlive(mob, state) ||
                Transform(mob).MapUid != map)
            {
                continue;
            }

            yield return (session, mob);
        }
    }

    private void UpdateGathering(Entity<LavalandSecretBossComponent> ent, LavalandSecretBossConfig config, TimeSpan now)
    {
        if (ent.Comp.Pit is not { } pit || TerminatingOrDeleted(pit))
            return;

        var pitPos = _transform.GetWorldPosition(pit);
        var candidates = LivingPlayersOn(ent.Owner).Where(p => !HasComp<LavalandPitDescentComponent>(p.Mob)).ToList();
        if (candidates.Count == 0)
            return;

        var near = candidates
            .Where(p => Vector2.Distance(_transform.GetWorldPosition(p.Mob), pitPos) <= config.GatherRange)
            .ToList();
        if (near.Count == 0 || config.RequireAllPlayers && near.Count < candidates.Count)
            return;

        foreach (var (session, _) in near)
        {
            if (ent.Comp.OpenPrompts.Contains(session) ||
                ent.Comp.PromptCooldowns.TryGetValue(session, out var cooldown) && now < cooldown)
            {
                continue;
            }

            ent.Comp.OpenPrompts.Add(session);
            _eui.OpenEui(new LavalandCelestialPitEui(this, ent.Owner), session);
        }
    }

    /// <summary>Answer to "you see something at the bottom, jump?".</summary>
    public void HandlePitResponse(ICommonSession session, EntityUid lavalandMap, bool accepted)
    {
        if (!TryComp<LavalandSecretBossComponent>(lavalandMap, out var comp) || !TryGetConfig(lavalandMap, out var config))
            return;

        comp.OpenPrompts.Remove(session);
        if (!accepted)
        {
            comp.PromptCooldowns[session] = _timing.CurTime + config.PromptCooldown;
            return;
        }

        if (comp.Stage != LavalandSecretBossStage.PitOpen ||
            session.AttachedEntity is not { Valid: true } mob ||
            !_mobState.IsAlive(mob) ||
            comp.Pit is not { } pit ||
            Vector2.Distance(_transform.GetWorldPosition(mob), _transform.GetWorldPosition(pit)) > config.GatherRange * 1.5f)
        {
            return;
        }

        StartJump((lavalandMap, comp), config, mob);
    }

    public void HandlePromptClosed(ICommonSession session, EntityUid lavalandMap)
    {
        if (TryComp<LavalandSecretBossComponent>(lavalandMap, out var comp))
            comp.OpenPrompts.Remove(session);
    }

    private void OnChasmFallingAttempt(ChasmFallingAttemptEvent args)
    {
        if (args.Cancelled ||
            !TryComp<LavalandCelestialPitComponent>(args.Chasm, out var pit) ||
            !TryComp<LavalandSecretBossComponent>(pit.LavalandMap, out var comp) ||
            comp.Stage == LavalandSecretBossStage.Done)
        {
            return;
        }

        // A thing thrown in falls down into the maze a while later.
        if (!HasComp<MobStateComponent>(args.Tripper))
        {
            if (comp.MazeMap is { } maze && !TerminatingOrDeleted(maze))
            {
                args.Cancel();
                _pitDrop.Drop(args.Tripper, comp.FallHolding, comp.MazeStart);
            }

            return;
        }

        // While the way down is open, stepping in is a jump; while the fight goes on the pit holds you up.
        args.Cancel();
        if (comp.Stage == LavalandSecretBossStage.PitOpen &&
            _mobState.IsAlive(args.Tripper) &&
            TryGetConfig(pit.LavalandMap, out var config))
        {
            StartJump((pit.LavalandMap, comp), config, args.Tripper);
        }
    }

    private void StartJump(Entity<LavalandSecretBossComponent> ent, LavalandSecretBossConfig config, EntityUid mob)
    {
        if (HasComp<LavalandPitDescentComponent>(mob) || ent.Comp.Pit is not { } pit)
            return;

        var jumping = EnsureComp<LavalandPitJumpingComponent>(mob);
        jumping.Style = _random.Pick(Enum.GetValues<LavalandPitJumpStyle>());
        jumping.Target = _transform.GetWorldPosition(pit) - _transform.GetWorldPosition(mob);
        jumping.Duration = (float) config.JumpDuration.TotalSeconds;
        Dirty(mob, jumping);

        var descent = EnsureComp<LavalandPitDescentComponent>(mob);
        descent.LavalandMap = ent.Owner;
        descent.Falling = false;
        descent.NextStep = _timing.CurTime + config.JumpDuration;

        Freeze(mob);
        ent.Comp.Participants.Add(mob);
    }

    /// <summary>The body is a doll for a while: no damage and no suffocation.</summary>
    private void Freeze(EntityUid mob)
    {
        var frozen = EnsureComp<LavalandPitFrozenComponent>(mob);
        if (!HasComp<RespiratorImmunityComponent>(mob))
        {
            AddComp<RespiratorImmunityComponent>(mob);
            frozen.AddedRespiratorImmunity = true;
        }
    }

    private void Unfreeze(EntityUid mob)
    {
        if (TryComp<LavalandPitFrozenComponent>(mob, out var frozen) && frozen.AddedRespiratorImmunity)
            RemComp<RespiratorImmunityComponent>(mob);
        RemComp<LavalandPitFrozenComponent>(mob);
    }

    private void UpdateDescents(TimeSpan now)
    {
        _toProcess.Clear();
        var query = EntityQueryEnumerator<LavalandPitDescentComponent>();
        while (query.MoveNext(out var uid, out var descent))
        {
            if (now >= descent.NextStep)
                _toProcess.Add(uid);
        }

        foreach (var uid in _toProcess)
        {
            if (!TryComp<LavalandPitDescentComponent>(uid, out var descent) ||
                !TryComp<LavalandSecretBossComponent>(descent.LavalandMap, out var comp) ||
                !TryGetConfig(descent.LavalandMap, out var config))
            {
                FinishDescent(uid);
                continue;
            }

            if (!descent.Falling)
            {
                RemComp<LavalandPitJumpingComponent>(uid);
                _transform.SetMapCoordinates(uid, comp.FallHolding);
                var falling = EnsureComp<LavalandPitFallingComponent>(uid);
                falling.Style = _random.Pick(Enum.GetValues<LavalandPitJumpStyle>());
                falling.Duration = (float) config.FallDuration.TotalSeconds;
                Dirty(uid, falling);
                descent.Falling = true;
                descent.NextStep = now + config.FallDuration;
                continue;
            }

            FinishDescent(uid);
            var offset = new Vector2(_random.NextFloat(-1f, 1f), _random.NextFloat(-1f, 1f));
            _transform.SetMapCoordinates(uid, new MapCoordinates(comp.MazeStart.Position + offset, comp.MazeStart.MapId));
            // The doll drops onto the floor of the own screen and gets up.
            _landing.StartLanding(uid, TimeSpan.FromSeconds(2.4));
        }
    }

    private void FinishDescent(EntityUid uid)
    {
        RemComp<LavalandPitJumpingComponent>(uid);
        RemComp<LavalandPitFallingComponent>(uid);
        RemComp<LavalandPitDescentComponent>(uid);
        Unfreeze(uid);
    }

    #endregion

    #region Flower and sleep

    private void CheckFlower(Entity<LavalandSecretBossComponent> ent, LavalandSecretBossConfig config, TimeSpan now)
    {
        if (ent.Comp.Flower is not { } flower || TerminatingOrDeleted(flower) || ent.Comp.MazeMap is not { } maze)
            return;

        var flowerPos = _transform.GetWorldPosition(flower);
        foreach (var (_, mob) in LivingPlayersOn(maze))
        {
            if (Vector2.Distance(_transform.GetWorldPosition(mob), flowerPos) > 0.7f)
                continue;

            TouchFlower(ent, config, now);
            return;
        }
    }

    private void TouchFlower(Entity<LavalandSecretBossComponent> ent, LavalandSecretBossConfig config, TimeSpan now)
    {
        ent.Comp.Stage = LavalandSecretBossStage.Sleeping;
        ent.Comp.Step = 0;
        ent.Comp.StageTime = now + config.SleepDelay;

        // The gas fills every passage of the maze, not only the flower room.
        foreach (var coords in ent.Comp.MazeOpenTiles.Count > 0 ? ent.Comp.MazeOpenTiles : ent.Comp.FlowerRoom)
        {
            Spawn(config.GasPrototype, coords);
        }

        if (ent.Comp.MazeMap is { } maze)
        {
            foreach (var (_, mob) in LivingPlayersOn(maze))
            {
                _popup.PopupEntity(Loc.GetString("lavaland-secret-boss-gas"), mob, mob, PopupType.LargeCaution);
            }
        }
    }

    private void PutEveryoneToSleep(Entity<LavalandSecretBossComponent> ent, LavalandSecretBossConfig config, TimeSpan now)
    {
        var sleepFor = config.DarkDelay + config.IntroLength + TimeSpan.FromSeconds(10);
        if (ent.Comp.MazeMap is { } maze)
        {
            foreach (var (session, mob) in LivingPlayersOn(maze))
            {
                FinishDescent(mob);
                ent.Comp.Participants.Add(mob);
                Sleep(mob, sleepFor);
                RaiseNetworkEvent(new LavalandSecretBossFadeEvent(1.5f, (float) config.DarkDelay.TotalSeconds + 2f, 2f),
                    session.Channel);
            }
        }

        ent.Comp.Step = 1;
        ent.Comp.StageTime = now + config.DarkDelay;
    }

    private void Sleep(EntityUid mob, TimeSpan duration, LavalandSecretBossConfig config)
    {
        Freeze(mob);
        _statusEffects.TryAddStatusEffectDuration(mob, config.SleepStatusEffect, duration);
    }

    private void Sleep(EntityUid mob, TimeSpan duration)
    {
        Freeze(mob);
        if (TryGetConfigFor(mob, out var config))
            _statusEffects.TryAddStatusEffectDuration(mob, config.SleepStatusEffect, duration);
    }

    private void Wake(EntityUid mob)
    {
        if (TryGetConfigFor(mob, out var config))
            _statusEffects.TryRemoveStatusEffect(mob, config.SleepStatusEffect);
        _sleeping.TryWaking(new Entity<SleepingComponent?>(mob, null), true);
        Unfreeze(mob);
    }

    #endregion

    #region Fight

    private void StartFight(Entity<LavalandSecretBossComponent> ent, LavalandSecretBossConfig config, TimeSpan now)
    {
        var comp = ent.Comp;
        var fighters = comp.Participants
            .Where(p => !TerminatingOrDeleted(p) && Transform(p).MapUid == comp.MazeMap && _mobState.IsAlive(p))
            .ToList();

        if (fighters.Count == 0 || comp.ArenaMap == null)
        {
            Fail(ent, config);
            return;
        }

        comp.Snapshots.Clear();
        foreach (var fighter in fighters)
        {
            comp.Snapshots[fighter] = new LavalandFighterSnapshot
            {
                Damage = TryComp<DamageableComponent>(fighter, out var damageable)
                    ? new DamageSpecifier(damageable.Damage)
                    : new DamageSpecifier(),
                BloodLevel = HasComp<BloodstreamComponent>(fighter) ? _bloodstream.GetBloodLevel(fighter) : 1f,
            };
        }

        // The fight is a dream: the real bodies sleep by the pit, untouchable, and an exact copy of each player
        // (looks, clothes, what is in the hands, the wounds) fights on the arena - the player in it.
        comp.DreamOriginals.Clear();
        comp.Participants.Clear();
        var radius = Math.Max(3f, config.ArenaSize / 2f - 3f);
        for (var i = 0; i < fighters.Count; i++)
        {
            var angle = MathF.Tau * i / fighters.Count;
            var position = comp.ArenaCenter.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            var original = fighters[i];
            var copy = MakeDreamCopy(original, new MapCoordinates(position, comp.ArenaCenter.MapId));

            comp.DreamOriginals[copy] = original;
            comp.Participants.Add(copy);
            Sleep(copy, config.IntroLength + TimeSpan.FromSeconds(10), config);

            FinishDescent(original);
            MoveNearPit(comp, original);
            EnsureComp<LavalandCelestialDreamerComponent>(original);
            Sleep(original, TimeSpan.FromHours(1), config);
            if (_mind.TryGetMind(original, out var mindId, out var mind))
                _mind.Visit(mindId, copy, mind);
        }

        // Spawned after the players are there: its intro cutscene goes to everyone on the arena map.
        var boss = Spawn(config.BossPrototype, comp.ArenaCenter);
        EnsureComp<LavalandSecretBossMobComponent>(boss).LavalandMap = ent.Owner;
        var health = config.BossHealth + config.BossHealthPerExtraPlayer * Math.Max(0, fighters.Count - 1);
        _thresholds.SetMobStateThreshold(boss, FixedPoint2.New(health), MobState.Dead);
        if (TryComp<CelestialComponent>(boss, out var celestial))
        {
            celestial.Phase2Damage = health * Math.Clamp(config.Phase2Fraction, 0.05f, 0.95f);
            celestial.Phase2Immortal = false;
            celestial.Stationary = true;
        }

        RaiseNetworkEvent(new LavalandCelestialBackdropEvent((float) config.IntroLength.TotalSeconds), FightersFilter(comp));

        comp.Boss = boss;
        comp.BossMaxHealth = health;
        comp.Stage = LavalandSecretBossStage.Fight;
        comp.StageTime = now + config.IntroLength;
        comp.PlayersAwake = false;
        comp.WipeSince = null;
        comp.NextHudUpdate = now;
    }

    private void UpdateFight(Entity<LavalandSecretBossComponent> ent, LavalandSecretBossConfig config, TimeSpan now)
    {
        var comp = ent.Comp;
        if (comp.Boss is not { } currentBoss || TerminatingOrDeleted(currentBoss) || _mobState.IsDead(currentBoss))
        {
            Victory(ent, config);
            return;
        }

        // Out of health is dead, whatever its thresholds say.
        if (comp.BossMaxHealth > 0f && TryComp<DamageableComponent>(currentBoss, out var bossDamage) &&
            bossDamage.TotalDamage.Float() >= comp.BossMaxHealth)
        {
            Victory(ent, config);
            return;
        }

        if (!comp.PlayersAwake)
        {
            if (now < comp.StageTime)
                return;

            comp.PlayersAwake = true;
            foreach (var fighter in comp.Participants)
            {
                if (!TerminatingOrDeleted(fighter) && Transform(fighter).MapUid == comp.ArenaMap)
                    Wake(fighter);
            }

            if (config.FightMusic != null)
            {
                var music = new LavalandBossMusicStartEvent(LavalandSecretBossComponent.HudId,
                    _audio.ResolveSound(config.FightMusic), AudioParams.Default.WithLoop(true).WithVolume(-4f));
                RaiseNetworkEvent(music, FightersFilter(comp));
            }
        }

        if (comp.Boss is not { } boss || TerminatingOrDeleted(boss))
            return;

        if (now >= comp.NextHudUpdate)
        {
            comp.NextHudUpdate = now + HudInterval;
            var damage = TryComp<DamageableComponent>(boss, out var damageable) ? damageable.TotalDamage.Float() : 0f;
            var hud = new LavalandBossHudUpdateEvent(LavalandSecretBossComponent.HudId, Loc.GetString(config.BossName),
                Math.Max(0f, comp.BossMaxHealth - damage), comp.BossMaxHealth, comp.Participants.Count);
            RaiseNetworkEvent(hud, FightersFilter(comp));
        }

        // Killed in the dream: the player is back in the body sleeping by the pit at once, without the fight's music,
        // health bar or backdrop. As a ghost of the copy they would be out of reach of all of it, and when the copy
        // was deleted the ghost was thrown off to the station.
        foreach (var (copy, original) in comp.DreamOriginals)
        {
            if (comp.WokenFromDream.Contains(copy) || !TerminatingOrDeleted(copy) && !_mobState.IsDead(copy))
                continue;

            comp.WokenFromDream.Add(copy);
            WakeFromDream(original);
        }

        // Anyone teleported out of the fight by some trick is put back.
        var alive = 0;
        foreach (var fighter in comp.Participants)
        {
            if (TerminatingOrDeleted(fighter) || !_mobState.IsAlive(fighter))
                continue;

            alive++;
            if (Transform(fighter).MapUid != comp.ArenaMap)
                _transform.SetMapCoordinates(fighter, comp.ArenaCenter.Offset(new Vector2(0f, config.ArenaSize / 2f - 3f)));
        }

        if (alive > 0)
        {
            comp.WipeSince = null;
            return;
        }

        comp.WipeSince ??= now;
        if (now - comp.WipeSince >= config.WipeDelay)
            Fail(ent, config);
    }

    /// <summary>The body asleep by the pit while its copy fights: nothing harms it meanwhile.</summary>
    private void OnDreamerDamage(Entity<LavalandCelestialDreamerComponent> ent, ref BeforeDamageChangedEvent args)
    {
        args.Cancelled = true;
    }

    /// <summary>
    /// A copy of the player for the dream: the same body prototype and looks, the same name, a copy of everything worn
    /// and held, the same wounds. It and everything on it are deleted when the dream ends.
    /// </summary>
    private EntityUid MakeDreamCopy(EntityUid original, MapCoordinates at)
    {
        var proto = MetaData(original).EntityPrototype?.ID ?? "MobHuman";
        var copy = Spawn(proto, at);
        _metaData.SetEntityName(copy, Name(original));
        if (HasComp<HumanoidAppearanceComponent>(original) && HasComp<HumanoidAppearanceComponent>(copy))
            _humanoid.CloneAppearance(original, copy);

        if (_inventory.TryGetSlots(original, out var slots))
        {
            foreach (var slot in slots)
            {
                if (!_inventory.TryGetSlotEntity(original, slot.Name, out var item) ||
                    MetaData(item.Value).EntityPrototype is not { } itemProto)
                {
                    continue;
                }

                var clone = CloneItem(item.Value, at, 0);
                if (!_inventory.TryEquip(copy, clone, slot.Name, silent: true, force: true))
                    QueueDel(clone);
            }
        }

        foreach (var held in _hands.EnumerateHeld(original))
        {
            if (MetaData(held).EntityPrototype == null)
                continue;

            var clone = CloneItem(held, at, 0);
            if (!_hands.TryPickupAnyHand(copy, clone, checkActionBlocker: false))
                QueueDel(clone);
        }

        if (TryComp<DamageableComponent>(original, out var damage) && TryComp<DamageableComponent>(copy, out var copyDamage))
            _damageable.SetDamage((copy, copyDamage), new DamageSpecifier(damage.Damage));

        return copy;
    }

    /// <summary>Containers inside containers this deep are still copied (a box in a bag on a belt).</summary>
    private const int CloneDepth = 4;

    /// <summary>
    /// A copy of an item for the dream copy: the same prototype and stack size, and copies of everything inside it
    /// (a backpack's or a belt's things, a gun's magazine) instead of whatever the prototype starts with.
    /// </summary>
    private EntityUid CloneItem(EntityUid item, MapCoordinates at, int depth)
    {
        var clone = Spawn(MetaData(item).EntityPrototype!.ID, at);
        if (TryComp<Content.Shared.Stacks.StackComponent>(item, out var stack) &&
            TryComp<Content.Shared.Stacks.StackComponent>(clone, out var cloneStack))
        {
            _stacks.SetCount((clone, cloneStack), stack.Count);
        }

        if (depth >= CloneDepth || !TryComp<Robust.Shared.Containers.ContainerManagerComponent>(item, out var manager))
            return clone;

        foreach (var container in _containers.GetAllContainers(item, manager))
        {
            if (!_containers.TryGetContainer(clone, container.ID, out var target))
                continue;

            // What the prototype starts with goes: the original's contents come instead.
            foreach (var starting in target.ContainedEntities.ToArray())
            {
                _containers.Remove(starting, target, force: true);
                QueueDel(starting);
            }

            foreach (var inner in container.ContainedEntities)
            {
                if (MetaData(inner).EntityPrototype == null || HasComp<MobStateComponent>(inner))
                    continue;

                var innerClone = CloneItem(inner, at, depth + 1);
                // Into a bag through the storage (it finds a place in the grid); failing that, straight in.
                var inserted = TryComp<Content.Shared.Storage.StorageComponent>(clone, out var storage) &&
                               target == storage.Container &&
                               _storage.Insert(clone, innerClone, out _, storageComp: storage, playSound: false,
                                   stackAutomatically: false);
                if (!inserted && !_containers.Insert(innerClone, target, force: true))
                    QueueDel(innerClone);
            }
        }

        return clone;
    }

    /// <summary>The dream is over: every player back in his own body by the pit, the copies and their things gone.</summary>
    /// <summary>The player of a copy killed in the dream goes back to the original body; the fight is over for them.</summary>
    private void WakeFromDream(EntityUid original)
    {
        if (TerminatingOrDeleted(original))
            return;

        if (_mind.TryGetMind(original, out var mindId, out var mind) && mind.VisitingEntity != null)
            _mind.UnVisit(mindId, mind);

        // Out of the fight: awake and on the feet by the pit at once, not asleep until the others are done.
        RemComp<LavalandCelestialDreamerComponent>(original);
        Wake(original);
        Unfreeze(original);

        if (!_players.TryGetSessionByEntity(original, out var session))
            return;

        var filter = Filter.SinglePlayer(session);
        RaiseNetworkEvent(new LavalandBossHudHideEvent(LavalandSecretBossComponent.HudId), filter);
        RaiseNetworkEvent(new LavalandBossMusicStopEvent(LavalandSecretBossComponent.HudId), filter);
        RaiseNetworkEvent(new CelestialCutsceneEndEvent(), filter);
    }

    private void EndDream(LavalandSecretBossComponent comp)
    {
        comp.WokenFromDream.Clear();
        if (comp.DreamOriginals.Count == 0)
            return;

        var originals = new List<EntityUid>();
        foreach (var (copy, original) in comp.DreamOriginals)
        {
            if (!TerminatingOrDeleted(original))
            {
                if (_mind.TryGetMind(original, out var mindId, out var mind) && mind.VisitingEntity != null)
                    _mind.UnVisit(mindId, mind);
                originals.Add(original);
            }

            if (!TerminatingOrDeleted(copy))
                QueueDel(copy);
        }

        comp.DreamOriginals.Clear();
        comp.Participants.Clear();
        comp.Participants.UnionWith(originals);
    }

    private Filter FightersFilter(LavalandSecretBossComponent comp)
    {
        var filter = Filter.Empty();
        foreach (var fighter in comp.Participants)
        {
            if (!TerminatingOrDeleted(fighter) && _players.TryGetSessionByEntity(fighter, out var session))
                filter.AddPlayer(session);
        }

        return filter;
    }

    private void StopHud(LavalandSecretBossComponent comp)
    {
        var filter = FightersFilter(comp);
        RaiseNetworkEvent(new LavalandBossHudHideEvent(LavalandSecretBossComponent.HudId), filter);
        RaiseNetworkEvent(new LavalandBossMusicStopEvent(LavalandSecretBossComponent.HudId), filter);
    }

    /// <summary>Everyone fell: bring the bodies back to the pit and let them try again.</summary>
    private void Fail(Entity<LavalandSecretBossComponent> ent, LavalandSecretBossConfig config)
    {
        var comp = ent.Comp;
        StopHud(comp);
        RaiseNetworkEvent(new CelestialCutsceneEndEvent(), FightersFilter(comp));
        if (comp.Boss is { } boss && !TerminatingOrDeleted(boss))
            QueueDel(boss);
        comp.Boss = null;

        var now = _timing.CurTime;
        EndDream(comp);
        foreach (var fighter in comp.Participants)
        {
            if (TerminatingOrDeleted(fighter))
                continue;

            FinishDescent(fighter);
            Wake(fighter);
            MoveNearPit(comp, fighter);
            RemComp<LavalandCelestialDreamerComponent>(fighter);
            if (comp.Snapshots.TryGetValue(fighter, out var snapshot))
                RestoreFighter(fighter, snapshot);

            // A minute of sleep as the price of the defeat.
            Sleep(fighter, config.LossSleep, config);
            var penalty = EnsureComp<LavalandCelestialPenaltyComponent>(fighter);
            penalty.WakeAt = now + config.LossSleep;
            penalty.SleepEffect = config.SleepStatusEffect;
        }

        comp.Participants.Clear();
        comp.Snapshots.Clear();
        if (config.RelocatePitOnLoss)
            RelocatePit(ent, config);
        BuildZone(ent, config);
        comp.Stage = LavalandSecretBossStage.PitOpen;
        comp.NextCheck = now + config.PromptCooldown;
    }

    /// <summary>Back to the state before the fight: alive, the same wounds, at least the same blood, the mind back in.</summary>
    private void RestoreFighter(EntityUid mob, LavalandFighterSnapshot snapshot)
    {
        if (TryComp<DamageableComponent>(mob, out var damageable))
            _damageable.SetDamage((mob, damageable), new DamageSpecifier(snapshot.Damage));

        if (TryComp<BloodstreamComponent>(mob, out var blood))
        {
            _bloodstream.TryModifyBleedAmount((mob, blood), -blood.BleedAmount);
            // Top the blood back up in small portions (the reference volume belongs to BloodstreamSystem).
            for (var i = 0; i < 200 && _bloodstream.GetBloodLevel((mob, blood)) < snapshot.BloodLevel; i++)
            {
                if (!_bloodstream.TryModifyBloodLevel((mob, blood), FixedPoint2.New(5)))
                    break;
            }
        }

        if (_mobState.IsDead(mob) || _mobState.IsCritical(mob))
            _mobState.ChangeMobState(mob, MobState.Alive);

        // Ghosted out of the dead body: put the player back in.
        if (_mind.TryGetMind(mob, out var mindId, out var mind) && mind.VisitingEntity != null)
            _mind.UnVisit(mindId, mind);
    }

    /// <summary>The old pit becomes an ordinary one; the way down opens somewhere else.</summary>
    private void RelocatePit(Entity<LavalandSecretBossComponent> ent, LavalandSecretBossConfig config)
    {
        if (!TryComp<MapGridComponent>(ent.Owner, out var grid))
            return;

        var oldPit = ent.Comp.Pit;
        if (!TryPlacePit(ent, config, grid))
            return;

        if (oldPit is { } old && !TerminatingOrDeleted(old))
            RemComp<LavalandCelestialPitComponent>(old);
        ClearFaunaNearPit(ent, config);
    }

    private void UpdatePenalties(TimeSpan now)
    {
        _toProcess.Clear();
        var query = EntityQueryEnumerator<LavalandCelestialPenaltyComponent>();
        while (query.MoveNext(out var uid, out var penalty))
        {
            if (now >= penalty.WakeAt)
                _toProcess.Add(uid);
        }

        foreach (var uid in _toProcess)
        {
            if (TryComp<LavalandCelestialPenaltyComponent>(uid, out var penalty))
                _statusEffects.TryRemoveStatusEffect(uid, penalty.SleepEffect);
            _sleeping.TryWaking(new Entity<SleepingComponent?>(uid, null), true);
            Unfreeze(uid);
            RemComp<LavalandCelestialPenaltyComponent>(uid);
        }
    }

    private void MoveNearPit(LavalandSecretBossComponent comp, EntityUid mob)
    {
        if (comp.Pit is not { } pit || TerminatingOrDeleted(pit))
            return;

        var pitCoords = _transform.GetMapCoordinates(pit);
        var offset = _random.NextAngle().ToVec() * _random.NextFloat(1.6f, 2.8f);
        _transform.SetMapCoordinates(mob, new MapCoordinates(pitCoords.Position + offset, pitCoords.MapId));
    }

    private void OnBossMobStateChanged(Entity<LavalandSecretBossMobComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead ||
            !TryComp<LavalandSecretBossComponent>(ent.Comp.LavalandMap, out var comp) ||
            !TryGetConfig(ent.Comp.LavalandMap, out var config))
        {
            return;
        }

        Victory((ent.Comp.LavalandMap, comp), config);
    }

    /// <summary>The Celestial died (or was deleted, e.g. killcelestial): defeat cutscene, sleep by the pit, blessings.</summary>
    private void Victory(Entity<LavalandSecretBossComponent> ent, LavalandSecretBossConfig config)
    {
        var comp = ent.Comp;
        if (comp.Stage != LavalandSecretBossStage.Fight)
            return;

        StopHud(comp);
        var length = _celestial.PlayDeathCutscene(FightersFilter(comp));
        if (comp.Boss is { } boss && !TerminatingOrDeleted(boss))
            QueueDel(boss);
        comp.Boss = null;

        var sleepFor = TimeSpan.FromSeconds(Math.Max(length.TotalSeconds, config.RewardSleep.TotalSeconds));
        EndDream(comp);
        foreach (var fighter in comp.Participants)
        {
            if (TerminatingOrDeleted(fighter))
                continue;

            if (_mobState.IsAlive(fighter))
                Sleep(fighter, sleepFor + TimeSpan.FromSeconds(5));
            MoveNearPit(comp, fighter);
        }

        // The way down is gone: the pit is an ordinary pit again.
        if (comp.Pit is { } pit)
            RemComp<LavalandCelestialPitComponent>(pit);

        DeleteZone(comp);
        comp.Stage = LavalandSecretBossStage.Done;
        comp.StageTime = _timing.CurTime + sleepFor;
        comp.PlayersAwake = false;
    }

    #endregion

    #region Reward

    private void WakeAndBless(Entity<LavalandSecretBossComponent> ent, LavalandSecretBossConfig config)
    {
        ent.Comp.PlayersAwake = true;
        foreach (var fighter in ent.Comp.Participants)
        {
            if (TerminatingOrDeleted(fighter))
                continue;

            // Every player of the fight gets one random blessing; the fallen keep it for when they are revived.
            RemComp<LavalandCelestialDreamerComponent>(fighter);
            if (_mobState.IsAlive(fighter))
                Wake(fighter);
            if (config.Blessings.Count == 0)
                continue;

            var blessing = _random.Pick(config.Blessings);
            ApplyBlessing(fighter, blessing, config);
            var text = Loc.GetString($"lavaland-blessing-{blessing.ToString().ToLowerInvariant()}");
            _popup.PopupEntity(text, fighter, fighter, PopupType.Large);

            // And in the chat, so it is not lost once the popup is gone.
            if (_players.TryGetSessionByEntity(fighter, out var session))
            {
                _chatManager.ChatMessageToOne(ChatChannel.Server, text,
                    $"[color=#c47bff][bold]{FormattedMessage.EscapeText(text)}[/bold][/color]",
                    EntityUid.Invalid, false, session.Channel, BlessingColor);
            }
        }

        ent.Comp.Participants.Clear();
    }

    private void ApplyBlessing(EntityUid mob, LavalandBlessing blessing, LavalandSecretBossConfig config)
    {
        switch (blessing)
        {
            case LavalandBlessing.NoSlip:
                EnsureComp<NoSlipComponent>(mob);
                break;

            case LavalandBlessing.Armor:
                var armor = EnsureComp<LavalandBlessingArmorComponent>(mob);
                armor.Modifiers = config.ArmorModifiers;
                Dirty(mob, armor);
                break;

            case LavalandBlessing.Health:
                if (!TryComp<MobThresholdsComponent>(mob, out var thresholds))
                    break;
                // Highest first so raised thresholds never collide with lower ones.
                foreach (var (damage, state) in thresholds.Thresholds.Reverse().ToList())
                {
                    if (damage > FixedPoint2.Zero)
                        _thresholds.SetMobStateThreshold(mob, damage + FixedPoint2.New(config.HealthBonus), state, thresholds);
                }
                break;

            case LavalandBlessing.NoBreathing:
                EnsureComp<RespiratorImmunityComponent>(mob);
                EnsureComp<LavalandWeatherImmuneComponent>(mob);
                break;

            case LavalandBlessing.Regeneration:
                var regen = EnsureComp<LavalandBlessingRegenComponent>(mob);
                regen.Amount = config.RegenAmount;
                regen.Interval = config.RegenInterval;
                regen.Excluded = new List<string>(config.RegenExcluded);
                regen.NextHeal = _timing.CurTime + regen.Interval;
                break;
        }
    }

    private void UpdateRegeneration(TimeSpan now)
    {
        var query = EntityQueryEnumerator<LavalandBlessingRegenComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var regen, out var damageable))
        {
            if (now < regen.NextHeal)
                continue;

            regen.NextHeal = now + regen.Interval;

            // Heals in crit too; only the dead stay as they are.
            if (_mobState.IsDead(uid))
                continue;

            var heal = new DamageSpecifier();
            foreach (var (type, value) in damageable.Damage.DamageDict)
            {
                if (value <= FixedPoint2.Zero || regen.Excluded.Contains(type))
                    continue;
                heal.DamageDict[type] = -FixedPoint2.Min(value, FixedPoint2.New(regen.Amount));
            }

            if (heal.DamageDict.Count > 0)
                _damageable.TryChangeDamage((uid, damageable), heal, true, false);
        }
    }

    #endregion

    /// <summary>Config of the secret boss whose participant this is (zone maps have no planet of their own).</summary>
    private bool TryGetConfigFor(EntityUid mob, out LavalandSecretBossConfig config)
    {
        var query = EntityQueryEnumerator<LavalandSecretBossComponent>();
        while (query.MoveNext(out var map, out var comp))
        {
            if (comp.Participants.Contains(mob) && TryGetConfig(map, out config))
                return true;
        }

        config = default!;
        return false;
    }

    /// <summary>No fauna within the configured radius of the pit: they are moved away, or quietly removed if unseen.</summary>
    private void ClearFaunaNearPit(Entity<LavalandSecretBossComponent> ent, LavalandSecretBossConfig config)
    {
        if (config.FaunaClearRadius <= 0f || ent.Comp.Pit is not { } pit || TerminatingOrDeleted(pit))
            return;

        var pitCoords = _transform.GetMapCoordinates(pit);
        _fauna.Clear();
        _lookup.GetEntitiesInRange(pitCoords, config.FaunaClearRadius, _fauna);
        foreach (var fauna in _fauna)
        {
            if (TerminatingOrDeleted(fauna) || _mobState.IsDead(fauna))
                continue;

            var position = _transform.GetWorldPosition(fauna);
            var seen = LivingPlayersOn(ent.Owner)
                .Any(p => Vector2.Distance(_transform.GetWorldPosition(p.Mob), position) < 12f);
            if (!seen)
            {
                QueueDel(fauna);
                continue;
            }

            var away = position - pitCoords.Position;
            var direction = away.LengthSquared() > 0.01f ? Vector2.Normalize(away) : _random.NextAngle().ToVec();
            _transform.SetWorldPosition(fauna.Owner, pitCoords.Position + direction * (config.FaunaClearRadius + 1f));
        }
    }

    private void OnShutdown(Entity<LavalandSecretBossComponent> ent, ref ComponentShutdown args)
    {
        DeleteZone(ent.Comp);
    }
}
