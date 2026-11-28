// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Content.Server.Cargo.Systems;
using Content.Server.Shuttles.Components;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Physics;
using Content.Shared.SSDIndicator;
using Robust.Shared.Physics.Components;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.Parallax;
using Content.Server.Procedural;
using Content.Server.Temperature.Systems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Ghost;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weather;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.PlanetSurvey;

public sealed partial class SurveyDirectorSystem : EntitySystem
{
    private static readonly TimeSpan TickRate = TimeSpan.FromSeconds(1);
    private const float PadClearance = 24f;
    private const string CampFire = "Bonfire";
    private const int Blocking = (int) (CollisionGroup.Impassable | CollisionGroup.MidImpassable | CollisionGroup.HighImpassable | CollisionGroup.MachineLayer);

    [Dependency] private AnchorableSystem _anchorable = default!;
    [Dependency] private BiomeSystem _biome = default!;
    [Dependency] private CargoSystem _cargo = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private DungeonSystem _dungeon = default!;
    [Dependency] private ITileDefinitionManager _tileDefs = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private GunSystem _gun = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedWeatherSystem _weather = default!;
    [Dependency] private TemperatureSystem _temperature = default!;

    private readonly List<(EntityUid Uid, Vector2 Pos)> _players = new();
    private readonly List<(Vector2i Index, Tile Tile)> _reserved = new();
    private readonly List<EntityUid> _scratch = new();
    private readonly List<SurveySpot> _guarded = new();
    private readonly HashSet<EntityUid> _clutter = new();

    public override void Initialize()
    {
        base.Initialize();
        InitializeCargo();
        InitializePressure();
        InitializeRelics();
        InitializeOutposts();
        InitializeReflex();
        InitializeEavesdrop();
    }

    public void Wake(Entity<SurveyDirectorComponent> ent)
    {
        var paused = _meta.GetPauseTime(ent.Owner);
        foreach (var squad in ent.Comp.Squads)
        {
            squad.NextOrders += paused;
            squad.LastSeen += paused;
            squad.NextChatter += paused;
        }

        ent.Comp.Dormant = false;
        _map.SetPaused(ent.Owner, false);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        _bakeWatch.Restart();
        var query = EntityQueryEnumerator<SurveyDirectorComponent>();
        while (query.MoveNext(out var uid, out var director))
        {
            if (director.Ready && director.Bake.Count > 0)
                Bake((uid, director));

            if (director.Dormant)
            {
                if (director.Ready && director.Bake.Count == 0)
                    _map.SetPaused(uid, true);
                continue;
            }

            if (!director.Ready || now < director.NextTick)
                continue;

            director.NextTick = now + TickRate;
            Tick((uid, director));
        }

        UpdateStamps(now);
        UpdateReflex(now);
        UpdateRunAndGun(now);
        UpdateCurses(now);
        if (now >= _nextRelicTick)
            ClearObjectiveViewers();
        UpdateRelics(now);
    }

    private void Tick(Entity<SurveyDirectorComponent> ent)
    {
        CollectPlayers(ent.Owner);
        UpdateSight(ent);
        TickCargo(ent);

        switch (ent.Comp.Kind)
        {
            case SurveyKind.Raiders:
                TickHumans(ent, false);
                break;
            case SurveyKind.Pirates:
                TickHumans(ent, true);
                break;
            case SurveyKind.Factory:
                TickFactory(ent);
                break;
            case SurveyKind.Beasts:
                TickBeasts(ent);
                break;
        }

        TickSupport(ent);
        TickRadio(ent);
        TickWeather(ent);
        TickPressure(ent);
        TickHunt(ent);
        TickAlarm(ent);
        TickQrf(ent);
        TickCore(ent);
        TickOutposts(ent);
        TickObjectives(ent);
        ent.Comp.ReissueAll = false;
    }

    public async Task Populate(Entity<SurveyDirectorComponent> ent, SurveyWorldPrototype world, int seed)
    {
        var map = ent.Owner;
        var grid = Comp<MapGridComponent>(map);
        var random = new System.Random(seed);
        var spots = new List<SurveySpot>();
        _guarded.Clear();

        var dungeonCount = world.Dungeons.Count == 0 ? 0 : world.DungeonCount;
        var radius = dungeonCount <= 1 ? world.Spread * 0.5f : Math.Max(world.Spread, 26f / MathF.Sin(MathF.PI / dungeonCount));

        var heading = (float) random.NextDouble() * MathF.Tau;
        var dir = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var distance = world.SiteMin + (world.SiteMax - world.SiteMin) * (float) random.NextDouble();
        var centre = dir * Math.Max(distance, radius + PadClearance + 20f);
        if (world.ArenaTile is { } arena)
            FillArena(map, grid, centre, 130, arena);

        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        for (var i = 0; i < dungeonCount; i++)
        {
            var pos = centre + Around(random, i, dungeonCount) * radius;
            var config = _proto.Index(world.Dungeons[random.Next(world.Dungeons.Count)]);
            var dungeons = await _dungeon.GenerateDungeonAsync(config, map, grid, (Vector2i) pos, seed + i);
            AddPoi(ent, pos, world.Kind == SurveyKind.DeadElement ? SurveyPoiKind.Shelter : SurveyPoiKind.Settlement);
            foreach (var dungeon in dungeons)
            {
                foreach (var tile in dungeon.AllTiles)
                {
                    min = Vector2.Min(min, tile);
                    max = Vector2.Max(max, tile);
                }

                foreach (var room in dungeon.Rooms)
                {
                    spots.Add(new SurveySpot(room.Center, room.Tiles.ToList()));
                }
            }
        }

        Vector2? padLanding = null;
        Vector2? padFar = null;
        if (world.FactoryHalls)
        {
            var (low, high, pad, dock) = BuildFactory(ent, world, centre, spots, random);
            min = Vector2.Min(min, low);
            max = Vector2.Max(max, high);
            padLanding = pad;
            padFar = dock;
        }

        if (min.X <= max.X)
        {
            ent.Comp.BaseCentre = (min + max) / 2f;
            ent.Comp.BaseRadius = Vector2.Distance(min, max) / 2f;
        }
        else
        {
            ent.Comp.BaseCentre = centre;
            ent.Comp.BaseRadius = radius;
        }

        centre = ent.Comp.BaseCentre;
        var reach = ent.Comp.BaseRadius + PadClearance + 8f;
        var landing = padLanding ?? centre - dir * reach;
        var far = padFar ?? centre + dir * reach;
        ent.Comp.Centre = centre;
        ent.Comp.Landing = landing;
        ent.Comp.Edge = padLanding is { } padEdge ? padEdge + new Vector2(0f, PadSize / 2f + PadGap + 2f) : centre - dir * (ent.Comp.BaseRadius + 6f);
        ent.Comp.Far = far;
        Reserve(map, ent.Comp.Edge, 2);
        Beacon(map, landing, Loc.GetString("survey-beacon-landing", ("planet", Name(map))));
        Beacon(map, far, Loc.GetString("survey-beacon-far", ("planet", Name(map))));

        spots.RemoveAll(s => NearPad(ent.Comp, s.Center, PadClearance));
        await SpawnOutposts(ent, world, random, spots, dir, far, seed);
        var ring = Math.Max(radius + 34f, ent.Comp.BaseRadius + 14f);

        for (var i = 0; i < world.Camps && !world.FactoryHalls; i++)
        {
            var pos = ent.Comp.BaseCentre + Around(random, i, world.Camps) * ring;
            if (NearPad(ent.Comp, pos, PadClearance))
                continue;

            var camp = OpenSpot(map, pos, 3);
            SpawnAtPosition(CampFire, new EntityCoordinates(map, camp.Center));
            spots.Add(camp);
        }

        if (world.Kind == SurveyKind.Beasts || spots.Count == 0)
        {
            var wanted = Math.Max(4, world.PackCount + world.Herds + 1);
            for (var i = 0; i < wanted; i++)
            {
                var pos = centre + Around(random, i, wanted) * world.Spread * (0.8f + 0.6f * (float) random.NextDouble());
                if (!NearPad(ent.Comp, pos, PadClearance))
                    spots.Add(OpenSpot(map, pos, 4));
            }
        }

        var arenaRadius = ring + 10f;
        arenaRadius = Math.Max(arenaRadius, Vector2.Distance(landing, centre) + 16f);
        foreach (var outpost in ent.Comp.Outposts)
        {
            arenaRadius = Math.Max(arenaRadius, Vector2.Distance(outpost.Pos, centre) + 36f);
        }

        foreach (var spot in spots)
        {
            foreach (var tile in spot.Tiles)
            {
                ent.Comp.Extent = Math.Max(ent.Comp.Extent, Vector2.Distance(tile + new Vector2(0.5f, 0.5f), centre));
            }
        }

        arenaRadius = Math.Max(arenaRadius, ent.Comp.Extent + ArenaMargin);

        ent.Comp.ArenaRadius = arenaRadius;
        if (world.Border is { } border)
            SpawnBorder(ent, world, border, arenaRadius);

        switch (world.Kind)
        {
            case SurveyKind.Raiders:
            case SurveyKind.Pirates:
                SpawnHumans(ent, world, random, spots);
                break;
            case SurveyKind.Factory:
                SpawnFactory(ent, world, random, spots);
                break;
            case SurveyKind.Beasts:
                SpawnBeasts(ent, world, random, spots);
                break;
            case SurveyKind.DeadElement:
                for (var i = 0; i < world.CargoCount; i++)
                {
                    var pos = centre + Around(random, i, world.CargoCount) * (radius + world.Spread) * (0.9f + 0.4f * (float) random.NextDouble());
                    _guarded.Add(OpenSpot(map, pos, 2));
                }
                break;
        }

        SpawnCargo(ent, world, random, spots);
        SpawnSites(ent, world, random, spots);
        SpawnLamps(ent, world, random, spots);
        SpawnTurrets(ent, world, random, spots);
        if (!world.FactoryHalls)
            SpawnScatter(ent, world, random, arenaRadius, far);
        SpawnSupport(ent, world, random, spots);
        BuildObjectives(ent, world);
        ent.Comp.NextWave = _timing.CurTime + TimeSpan.FromSeconds(90);

        if (world.Calm is { } calm)
            _weather.SetWeather(Transform(map).MapID, _proto.Index(calm), null);

        if (world.Storm != null)
            ent.Comp.StormAt = _timing.CurTime + RandomGap(world);

        ent.Comp.Rain = world.Calm?.Id.Contains("Rain") == true || world.Storm?.Id == "Storm";
        QueueBake(ent);
        ent.Comp.Ready = true;
        ent.Comp.NextTick = _timing.CurTime + TickRate;
    }

    private const float BakeMargin = 12f;
    private const float ArenaMargin = 8f;
    private static readonly TimeSpan ReplanGap = TimeSpan.FromSeconds(0.4);
    private const double BakeBudgetMs = 1.2;
    private const double LobbyBakeBudgetMs = 10;
    private const int BakeEvery = 2;
    private int _bakeTick;
    private readonly System.Diagnostics.Stopwatch _bakeWatch = new();

    private void QueueBake(Entity<SurveyDirectorComponent> ent)
    {
        if (!TryComp<BiomeComponent>(ent, out var biome))
            return;

        biome.Enabled = false;
        var size = BiomeSystem.BakeChunkSize;
        var reach = ent.Comp.ArenaRadius + BakeMargin;
        var centre = ent.Comp.BaseCentre;
        var low = BiomeSystem.BakeChunkOrigin(new Vector2i((int) MathF.Floor(centre.X - reach), (int) MathF.Floor(centre.Y - reach)));
        var high = BiomeSystem.BakeChunkOrigin(new Vector2i((int) MathF.Ceiling(centre.X + reach), (int) MathF.Ceiling(centre.Y + reach)));

        var chunks = new List<Vector2i>();
        for (var x = low.X; x <= high.X; x += size)
        {
            for (var y = low.Y; y <= high.Y; y += size)
            {
                var middle = new Vector2(x + size / 2f, y + size / 2f);
                if (Vector2.Distance(middle, centre) <= reach + size)
                    chunks.Add(new Vector2i(x, y));
            }
        }

        var landing = ent.Comp.Landing;
        foreach (var chunk in chunks.OrderBy(c => Vector2.DistanceSquared(c + new Vector2(size / 2f, size / 2f), landing)))
        {
            ent.Comp.Bake.Enqueue(chunk);
        }
    }

    private void Bake(Entity<SurveyDirectorComponent> ent)
    {
        if (!TryComp<BiomeComponent>(ent, out var biome) || !TryComp<MapGridComponent>(ent, out var grid))
        {
            ent.Comp.Bake.Clear();
            return;
        }

        var lobby = ent.Comp.Dormant && _ticker.RunLevel == GameRunLevel.PreRoundLobby;
        if (!lobby && _bakeTick++ % BakeEvery != 0)
            return;

        var budget = lobby ? LobbyBakeBudgetMs : BakeBudgetMs;
        while (ent.Comp.Bake.Count > 0 && _bakeWatch.Elapsed.TotalMilliseconds < budget)
        {
            _biome.BakeChunk(ent, biome, grid, ent.Comp.Bake.Dequeue());
        }

        if (ent.Comp.Bake.Count == 0)
            RemComp<BiomeComponent>(ent);
    }

    private static bool NearPad(SurveyDirectorComponent director, Vector2 pos, float range)
    {
        return Vector2.Distance(pos, director.Landing) < range || Vector2.Distance(pos, director.Far) < range;
    }

    private static Vector2 Around(System.Random random, int index, int count)
    {
        var angle = (index + (float) random.NextDouble() * 0.5f) * MathF.Tau / Math.Max(1, count);
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }

    private void Reserve(EntityUid map, Vector2 center, int radius)
    {
        _reserved.Clear();
        _biome.ReserveTiles(map, Box2.CenteredAround(center, new Vector2(radius * 2 + 1)), _reserved);
        _reserved.Clear();
    }

    private Vector2 Grounded(EntityUid map, Vector2 pos)
    {
        if (!TryComp<SurveyDirectorComponent>(map, out var director) || director.Anchors.Count == 0 ||
            !TryComp<MapGridComponent>(map, out var grid))
        {
            return pos;
        }

        if (_map.TryGetTileRef(map, grid, _map.TileIndicesFor(map, grid, new EntityCoordinates(map, pos)), out var tile) && !tile.Tile.IsEmpty)
            return pos;

        var best = director.Anchors[0];
        foreach (var anchor in director.Anchors)
        {
            if (Vector2.DistanceSquared(anchor, pos) < Vector2.DistanceSquared(best, pos))
                best = anchor;
        }

        return best;
    }

    private SurveySpot OpenSpot(EntityUid map, Vector2 center, int radius)
    {
        center = Grounded(map, center);
        Reserve(map, center, radius);
        var tiles = new List<Vector2i>();
        var origin = new Vector2i((int) MathF.Floor(center.X), (int) MathF.Floor(center.Y));
        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                tiles.Add(origin + new Vector2i(x, y));
            }
        }

        return new SurveySpot(center, tiles) { Open = true };
    }

    private void FillArena(EntityUid map, MapGridComponent grid, Vector2 centre, int radius, string tileId)
    {
        RemComp<BiomeComponent>(map);
        var tile = new Tile(_tileDefs[tileId].TileId);
        var tiles = new List<(Vector2i, Tile)>();
        var origin = new Vector2i((int) centre.X, (int) centre.Y);
        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                if (x * x + y * y <= radius * radius)
                    tiles.Add((origin + new Vector2i(x, y), tile));
            }
        }

        _map.SetTiles(map, grid, tiles);
    }

    private void SpawnBorder(Entity<SurveyDirectorComponent> ent, SurveyWorldPrototype world, EntProtoId wall, float radius)
    {
        var map = ent.Owner;
        var centre = ent.Comp.BaseCentre;
        var outer = radius + 1.6f;
        var reach = (int) MathF.Ceiling(outer) + 1;
        var origin = new Vector2i((int) MathF.Floor(centre.X), (int) MathF.Floor(centre.Y));

        for (var x = -reach; x <= reach; x++)
        {
            for (var y = -reach; y <= reach; y++)
            {
                var d = MathF.Sqrt(x * x + y * y);
                if (d < radius || d > outer)
                    continue;

                var tile = origin + new Vector2i(x, y);
                if (world.ArenaTile == null)
                {
                    _reserved.Clear();
                    _biome.ReserveTiles(map, new Box2(tile.X + 0.1f, tile.Y + 0.1f, tile.X + 0.9f, tile.Y + 0.9f), _reserved);
                }

                SpawnAtPosition(wall, new EntityCoordinates(map, tile + new Vector2(0.5f, 0.5f)));
            }
        }
    }

    private void SpawnTurrets(Entity<SurveyDirectorComponent> ent, SurveyWorldPrototype world, System.Random random, List<SurveySpot> spots)
    {
        if (world.Turret is not { } turret)
            return;

        var rooms = spots.Where(s => !s.Open && s.Tiles.Count >= 9).OrderBy(_ => random.Next()).ToList();
        var placed = 0;
        foreach (var room in rooms)
        {
            if (placed >= world.TurretCount + ent.Comp.Outposts.Count)
                break;

            SpawnAtPosition(turret, new EntityCoordinates(ent.Owner, FreeTile(ent.Owner, room, random)));
            placed++;
        }
    }

    private void Beacon(EntityUid map, Vector2 at, string name)
    {
        Reserve(map, at, 14);
        var beacon = SpawnAtPosition(null, new EntityCoordinates(map, at));
        _meta.SetEntityName(beacon, name);
        EnsureComp<FTLBeaconComponent>(beacon);
    }

    private Vector2 FreeTile(EntityUid map, SurveySpot spot, System.Random random)
    {
        if (spot.Tiles.Count == 0 || !TryComp<MapGridComponent>(map, out var grid))
            return spot.Center;

        for (var i = 0; i < 32; i++)
        {
            var tile = spot.Tiles[random.Next(spot.Tiles.Count)];
            if (!_map.TryGetTileRef(map, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                continue;

            if (!_anchorable.TileFree((map, grid), tile, Blocking, Blocking) || Cluttered(map, tile))
                continue;

            return tile + new Vector2(0.5f, 0.5f);
        }

        var middle = new Vector2i((int) MathF.Floor(spot.Center.X), (int) MathF.Floor(spot.Center.Y));
        for (var ring = 0; ring <= 6; ring++)
        {
            for (var x = -ring; x <= ring; x++)
            {
                for (var y = -ring; y <= ring; y++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) != ring)
                        continue;

                    var tile = middle + new Vector2i(x, y);
                    if (!_map.TryGetTileRef(map, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                        continue;

                    if (_anchorable.TileFree((map, grid), tile, Blocking, Blocking) && !Cluttered(map, tile))
                        return tile + new Vector2(0.5f, 0.5f);
                }
            }
        }

        return spot.Center;
    }

    private Vector2 Walkable(EntityUid map, Vector2 pos, int reach = 4)
    {
        if (!TryComp<MapGridComponent>(map, out var grid))
            return pos;

        var middle = new Vector2i((int) MathF.Floor(pos.X), (int) MathF.Floor(pos.Y));
        for (var ring = 0; ring <= reach; ring++)
        {
            for (var x = -ring; x <= ring; x++)
            {
                for (var y = -ring; y <= ring; y++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) != ring)
                        continue;

                    var tile = middle + new Vector2i(x, y);
                    if (!_map.TryGetTileRef(map, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                        continue;

                    if (_anchorable.TileFree((map, grid), tile, Blocking, Blocking) && !Blocked(map, tile))
                        return tile + new Vector2(0.5f, 0.5f);
                }
            }
        }

        return pos;
    }

    private bool Blocked(EntityUid map, Vector2i tile)
    {
        _clutter.Clear();
        var box = new Box2(tile.X + 0.1f, tile.Y + 0.1f, tile.X + 0.9f, tile.Y + 0.9f);
        _lookup.GetEntitiesIntersecting(Transform(map).MapID, box, _clutter, LookupFlags.Static);
        foreach (var uid in _clutter)
        {
            if (TryComp<PhysicsComponent>(uid, out var body) && body.Hard && body.CanCollide)
                return true;
        }

        return false;
    }

    private bool Cluttered(EntityUid map, Vector2i tile)
    {
        _clutter.Clear();
        var box = new Box2(tile.X + 0.1f, tile.Y + 0.1f, tile.X + 0.9f, tile.Y + 0.9f);
        _lookup.GetEntitiesIntersecting(Transform(map).MapID, box, _clutter, LookupFlags.Dynamic | LookupFlags.Static);
        foreach (var uid in _clutter)
        {
            if (TryComp<PhysicsComponent>(uid, out var body) && body.Hard && body.CanCollide)
                return true;
        }

        return false;
    }

    private void SpawnScatter(Entity<SurveyDirectorComponent> ent, SurveyWorldPrototype world, System.Random random, float radius, Vector2 far)
    {
        var map = ent.Owner;
        var centre = ent.Comp.Centre;

        for (var i = 0; i < world.ScatterCount && world.Scatter.Count > 0; i++)
        {
            var angle = (float) random.NextDouble() * MathF.Tau;
            var pos = centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (8f + (float) random.NextDouble() * Math.Max(4f, radius - 14f));
            if (NearPad(ent.Comp, pos, 10f))
                continue;

            var spot = OpenSpot(map, pos, 0);
            SpawnAtPosition(world.Scatter[random.Next(world.Scatter.Count)], new EntityCoordinates(map, spot.Center));
        }

        var path = centre - ent.Comp.Landing;
        var perp = Vector2.Normalize(new Vector2(-path.Y, path.X));
        for (var i = 0; i < world.TrapCount && world.Traps.Count > 0; i++)
        {
            var along = 0.45f + 0.4f * (float) random.NextDouble();
            var pos = ent.Comp.Landing + path * along + perp * ((float) random.NextDouble() * 12f - 6f);
            var spot = OpenSpot(map, pos, 0);
            EnsureComp<SurveyNativeMineComponent>(SpawnAtPosition(world.Traps[random.Next(world.Traps.Count)], new EntityCoordinates(map, spot.Center)));
        }
    }

    private SurveySpot NextSpot(List<SurveySpot> spots, System.Random random)
    {
        foreach (var spot in spots)
        {
            if (spot.Taken)
                continue;

            spot.Taken = true;
            return spot;
        }

        return spots[random.Next(spots.Count)];
    }

    private EntityUid SpawnMember(Entity<SurveyDirectorComponent> ent, EntProtoId proto, SurveySpot spot, int squad, System.Random random)
    {
        var at = FreeTile(ent.Owner, spot, random);
        var uid = SpawnAtPosition(proto, new EntityCoordinates(ent.Owner, at));
        RemComp<GhostRoleComponent>(uid);
        RemComp<GhostTakeoverAvailableComponent>(uid);
        RemComp<SSDIndicatorComponent>(uid);

        var member = EnsureComp<SurveyMemberComponent>(uid);
        member.Planet = ent.Owner;
        member.Squad = squad;

        if (Board(uid) is { } board)
        {
            if (member.Voice.Length > 0)
                board.SetValue(SurveyKeys.Voice, member.Voice);
            board.SetValue(SurveyKeys.Post, new EntityCoordinates(ent.Owner, at));
            board.SetValue(SurveyKeys.Den, new EntityCoordinates(ent.Owner, spot.Center));
            board.SetValue(SurveyKeys.Role, SurveyRole.None);
        }

        return uid;
    }

    private sealed class SurveySpot(Vector2 center, List<Vector2i> tiles)
    {
        public readonly Vector2 Center = center;
        public readonly List<Vector2i> Tiles = tiles;
        public bool Taken;
        public bool Open;
        public int Outpost = -1;
        public string? Tag;
    }

    private void CollectPlayers(EntityUid map)
    {
        _players.Clear();
        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid != map || HasComp<GhostComponent>(uid) || !_mobState.IsAlive(uid))
                continue;

            _players.Add((uid, _xform.GetWorldPosition(xform)));
        }
    }

    private EntityUid? NearestPlayer(Vector2 from, float range, out float distance)
    {
        EntityUid? best = null;
        distance = range;
        foreach (var (uid, pos) in _players)
        {
            var d = Vector2.Distance(pos, from);
            if (d > distance)
                continue;

            distance = d;
            best = uid;
        }

        return best;
    }

    private int PlayersNear(Vector2 from, float range)
    {
        var count = 0;
        foreach (var (_, pos) in _players)
        {
            if (Vector2.DistanceSquared(pos, from) <= range * range)
                count++;
        }

        return count;
    }

    private Vector2 Pos(EntityUid uid)
    {
        return _xform.GetWorldPosition(uid);
    }

    private EntityCoordinates At(EntityUid map, Vector2 pos)
    {
        return new EntityCoordinates(map, new Vector2(MathF.Floor(pos.X) + 0.5f, MathF.Floor(pos.Y) + 0.5f));
    }

    private NPCBlackboard? Board(EntityUid uid)
    {
        return TryComp<HTNComponent>(uid, out var htn) ? htn.Blackboard : null;
    }

    private void Replan(EntityUid uid, bool force = false)
    {
        if (!TryComp<HTNComponent>(uid, out var htn))
            return;

        if (!force && TryComp<SurveyMemberComponent>(uid, out var member))
        {
            var now = _timing.CurTime;
            if (now < member.NextReplan)
            {
                member.ReplanPending = true;
                return;
            }

            member.NextReplan = now + ReplanGap;
            member.ReplanPending = false;
        }

        _htn.Replan(htn);
    }

    private bool Alive(EntityUid uid)
    {
        return !TerminatingOrDeleted(uid) && _mobState.IsAlive(uid);
    }

    private float Health(EntityUid uid)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return 1f;

        if (!_thresholds.TryGetThresholdForState(uid, MobState.Critical, out var limit) &&
            !_thresholds.TryGetThresholdForState(uid, MobState.Dead, out limit))
        {
            return 1f;
        }

        var max = limit.Value.Float();
        return max <= 0f ? 1f : Math.Clamp(1f - damageable.TotalDamage.Float() / max, 0f, 1f);
    }

    private bool Crippled(EntityUid uid)
    {
        if (!TryComp<LimbBodyComponent>(uid, out var limbs))
            return false;

        return Useless(limbs, LimbZone.LegLeft) || Useless(limbs, LimbZone.LegRight) ||
               Useless(limbs, LimbZone.ArmLeft) || Useless(limbs, LimbZone.ArmRight);
    }

    private bool SensorDead(EntityUid uid)
    {
        if (TryComp<LimbBodyComponent>(uid, out var limbs))
            return Useless(limbs, LimbZone.Head) || limbs.EyesGone >= 2;

        return Health(uid) < 0.4f;
    }

    private static bool Useless(LimbBodyComponent limbs, LimbZone zone)
    {
        var index = LimbWoundRules.Index(zone);
        return (uint) index < (uint) limbs.Zones.Count && LimbWoundRules.Useless(limbs.Zones[index]);
    }

    private void Bark(EntityUid uid, string pool)
    {
        if (!Alive(uid) || Board(uid) is not { } board)
            return;

        board.SetValue(SurveyKeys.Bark, pool);
        Replan(uid);
    }

    private void BarkOne(SurveySquad squad, string pool, Vector2? near = null)
    {
        EntityUid? speaker = null;
        var best = float.MaxValue;
        foreach (var member in squad.Members)
        {
            if (!Alive(member))
                continue;

            var d = near is { } at ? Vector2.DistanceSquared(Pos(member), at) : _random.NextFloat();
            if (d >= best)
                continue;

            best = d;
            speaker = member;
        }

        if (speaker is { } uid)
            Bark(uid, pool);
    }

    private TimeSpan RandomGap(SurveyWorldPrototype world)
    {
        var min = world.StormGapMin.TotalSeconds;
        var max = Math.Max(min, world.StormGapMax.TotalSeconds);
        return TimeSpan.FromSeconds(min + _random.NextDouble() * (max - min));
    }

    private Vector2 Centroid(SurveySquad squad, out int alive)
    {
        var sum = Vector2.Zero;
        alive = 0;
        foreach (var member in squad.Members)
        {
            if (!Alive(member))
                continue;

            sum += Pos(member);
            alive++;
        }

        return alive == 0 ? squad.Home.Position : sum / alive;
    }
}
