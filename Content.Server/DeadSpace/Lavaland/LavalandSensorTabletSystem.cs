// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Medical.SuitSensors;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mining.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Inventory;
using Robust.Shared.Player;
using Content.Shared.Wall;
using Robust.Shared.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Content.Server.DeadSpace.Lavaland.SecretBoss;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandSensorTabletSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly LavalandDayCycleSystem _dayCycle = default!;
    [Dependency] private readonly LavalandSecretBossSystem _secretBoss = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;

    private static readonly TimeSpan BaseCacheLifetime = TimeSpan.FromSeconds(30);

    // The outpost barely changes: its tiles are rescanned rarely and sent only when the plan actually differs.
    private readonly List<LavalandRadarPoint> _baseCache = new();
    private TimeSpan _baseCacheExpires;
    private int _baseVersion;
    private int _baseSignature;

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<LavalandSensorTabletComponent>(LavalandSensorTabletUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<LavalandSensorTabletSwitchLayerMessage>(OnSwitchLayer);
            subs.Event<LavalandSensorTabletSonarMessage>(OnSonar);
        });
        SubscribeLocalEvent<LavalandSonarCartridgeComponent, ExaminedEvent>(OnCartridgeExamined);
    }

    private void OnOpened(Entity<LavalandSensorTabletComponent> ent, ref BoundUIOpenedEvent args)
    {
        // A fresh window has no outpost plan yet; the periodic update must not replace this state in the same tick.
        ent.Comp.SentBaseVersion = -1;
        ent.Comp.NextUpdate = _timing.CurTime + ent.Comp.UpdateInterval;
        UpdateUi(ent);
    }

    private void OnSwitchLayer(Entity<LavalandSensorTabletComponent> ent, ref LavalandSensorTabletSwitchLayerMessage args)
    {
        if (!ent.Comp.CanSwitchLayer)
            return;

        ent.Comp.ViewCaves = !ent.Comp.ViewCaves;
        ent.Comp.SentBaseVersion = -1;
        UpdateUi(ent);
    }

    private void OnCartridgeExamined(Entity<LavalandSonarCartridgeComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("lavaland-sonar-cartridge-charges",
            ("charges", ent.Comp.Charges), ("max", ent.Comp.MaxCharges)));
    }

    /// <summary>The layer the tablet shows: the one chosen on it, or the one it is on.</summary>
    private bool ShowsCaves(Entity<LavalandSensorTabletComponent> ent)
    {
        return ent.Comp.CanSwitchLayer
            ? ent.Comp.ViewCaves
            : HasComp<LavalandChromiteCavesComponent>(Transform(ent).MapUid);
    }

    private bool IsOnLayer(EntityUid? map, bool caves)
    {
        return caves ? HasComp<LavalandChromiteCavesComponent>(map) : HasComp<LavalandMapComponent>(map);
    }

    /// <summary>
    /// One charge of the cartridge: a circle around the nearest boss arena on the layer the tablet is on, and a word
    /// in the chat about where it lies. The nearer the arena, the sharper the circle.
    /// </summary>
    private void OnSonar(Entity<LavalandSensorTabletComponent> ent, ref LavalandSensorTabletSonarMessage args)
    {
        var user = args.Actor;
        var now = _timing.CurTime;
        if (now < ent.Comp.NextSonar)
            return;

        if (!_itemSlots.TryGetSlot(ent, ent.Comp.SonarSlot, out var slot) || slot.Item is not { } cartridgeUid ||
            !TryComp<LavalandSonarCartridgeComponent>(cartridgeUid, out var cartridge))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-sonar-no-cartridge"), user, user);
            return;
        }

        if (cartridge.Charges <= 0)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-sonar-empty"), user, user);
            return;
        }

        var xform = Transform(ent);
        if (xform.MapUid is not { } map ||
            !HasComp<LavalandMapComponent>(map) && !HasComp<LavalandChromiteCavesComponent>(map))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-sonar-no-signal"), user, user);
            return;
        }

        ent.Comp.NextSonar = now + ent.Comp.SonarCooldown;
        cartridge.Charges--;
        _audio.PlayPvs(SonarSound, ent);

        var origin = _transform.GetWorldPosition(xform);
        var minibosses = FindMinibosses(map, origin, ent.Comp.SonarMinibossRange);
        var hasArena = TryGetNearestArena(map, origin, out var arenaCenter, out var arenaRadius);

        var center = Vector2.Zero;
        var radius = 0f;
        var fuzz = 1f;
        if (hasArena)
        {
            var distance = (arenaCenter - origin).Length();
            fuzz = Math.Clamp(distance / MathF.Max(1f, ent.Comp.SonarFuzzRange), 0f, 1f);

            // Blurred with distance: the circle widens and drifts off the arena's real place.
            center = arenaCenter + _random.NextAngle().ToVec() * _random.NextFloat(0f, fuzz * 35f);
            radius = arenaRadius + fuzz * 45f;

            var step = fuzz > 0.5f ? 50f : 10f;
            var shownDistance = MathF.Max(10f, MathF.Round((center - origin).Length() / step) * step);
            SendSonarChat(user, Loc.GetString("lavaland-sonar-found",
                ("distance", (int) shownDistance),
                ("direction", Loc.GetString(DirectionLoc(center - origin)))));
        }
        else
        {
            SendSonarChat(user, Loc.GetString("lavaland-sonar-nothing"));
        }

        foreach (var miniboss in minibosses)
        {
            var shownDistance = MathF.Max(5f, MathF.Round((miniboss - origin).Length() / 5f) * 5f);
            SendSonarChat(user, Loc.GetString("lavaland-sonar-miniboss",
                ("distance", (int) shownDistance),
                ("direction", Loc.GetString(DirectionLoc(miniboss - origin)))));
        }

        SendSonarChat(user, Loc.GetString("lavaland-sonar-charges", ("charges", cartridge.Charges)));
        ent.Comp.LastPing = new LavalandSonarPing(++ent.Comp.PingCounter, HasComp<LavalandChromiteCavesComponent>(map),
            hasArena, center, radius, fuzz, minibosses);
        UpdateUi(ent);
    }

    /// <summary>Living mini-bosses within the sonar's short reach, and tumors still holding theirs asleep.</summary>
    private List<Vector2> FindMinibosses(EntityUid map, Vector2 origin, float range)
    {
        var found = new List<Vector2>();
        var rangeSquared = range * range;

        void Consider(EntityUid uid, TransformComponent xform)
        {
            if (xform.MapUid != map ||
                TryComp<MobStateComponent>(uid, out var mob) && mob.CurrentState == MobState.Dead)
            {
                return;
            }

            var position = _transform.GetWorldPosition(xform);
            if (Vector2.DistanceSquared(origin, position) <= rangeSquared)
                found.Add(position);
        }

        var miners = EntityQueryEnumerator<LavalandMadMinerComponent, TransformComponent>();
        while (miners.MoveNext(out var uid, out _, out var xform))
            Consider(uid, xform);

        // An awakened elite is marked where it is; a sleeping one by its tumor.
        var elites = EntityQueryEnumerator<LavalandEliteComponent, TransformComponent>();
        while (elites.MoveNext(out var uid, out _, out var xform))
            Consider(uid, xform);

        var tumors = EntityQueryEnumerator<LavalandEliteTumorComponent, TransformComponent>();
        while (tumors.MoveNext(out var uid, out var tumor, out var xform))
        {
            if (tumor.Elite == null)
                Consider(uid, xform);
        }

        return found;
    }

    private static readonly Robust.Shared.Audio.SoundSpecifier SonarSound =
        new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Machines/scan_finish.ogg");

    private void SendSonarChat(EntityUid user, string message)
    {
        if (_players.TryGetSessionByEntity(user, out var session))
            _chat.DispatchServerMessage(session, message);
    }

    /// <summary>The compass point (one of eight) a vector points to, north up.</summary>
    private static string DirectionLoc(Vector2 vector)
    {
        var angle = MathF.Atan2(vector.Y, vector.X) * 180f / MathF.PI;
        var sector = (int) MathF.Round(((angle % 360f) + 360f) % 360f / 45f) % 8;
        return sector switch
        {
            0 => "lavaland-sonar-dir-east",
            1 => "lavaland-sonar-dir-northeast",
            2 => "lavaland-sonar-dir-north",
            3 => "lavaland-sonar-dir-northwest",
            4 => "lavaland-sonar-dir-west",
            5 => "lavaland-sonar-dir-southwest",
            6 => "lavaland-sonar-dir-south",
            _ => "lavaland-sonar-dir-southeast",
        };
    }

    /// <summary>The nearest boss arena on this map whose fight is not over yet.</summary>
    private bool TryGetNearestArena(EntityUid map, Vector2 origin, out Vector2 center, out float radius)
    {
        center = default;
        radius = 0f;
        var best = float.MaxValue;
        var arenas = EntityQueryEnumerator<LavalandBossArenaComponent>();
        while (arenas.MoveNext(out _, out var arena))
        {
            if (arena.Ended || arena.Map != map || !TryComp<MapGridComponent>(arena.Grid, out var grid))
                continue;

            var position = _map.GridTileToWorldPos(arena.Grid, grid, arena.BossSpawnTile);
            var distance = Vector2.DistanceSquared(origin, position);
            if (distance >= best)
                continue;

            best = distance;
            center = position;
            radius = MathF.Max(arena.Width, arena.Height) / 2f;
        }

        return best < float.MaxValue;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandSensorTabletComponent>();
        while (query.MoveNext(out var uid, out var tablet))
        {
            UpdateBeeping((uid, tablet), now);

            if (tablet.NextUpdate > now || !_ui.IsUiOpen(uid, LavalandSensorTabletUiKey.Key))
                continue;

            tablet.NextUpdate = now + tablet.UpdateInterval;
            UpdateUi((uid, tablet));
        }
    }

    /// <summary>Near the Celestial pit the tablet beeps loudly, faster the closer it gets.</summary>
    private void UpdateBeeping(Entity<LavalandSensorTabletComponent> ent, TimeSpan now)
    {
        if (now < ent.Comp.NextBeep)
            return;

        var xform = Transform(ent);
        if (!_secretBoss.TryGetPit(xform.MapUid, out var pit, out _, out var config) || config.BeepSound == null)
        {
            ent.Comp.NextBeep = now + TimeSpan.FromSeconds(1);
            return;
        }

        var distance = Vector2.Distance(_transform.GetWorldPosition(xform), pit);
        if (distance > config.BeepRange)
        {
            ent.Comp.NextBeep = now + TimeSpan.FromSeconds(0.5);
            return;
        }

        var closeness = 1f - Math.Clamp(distance / MathF.Max(0.1f, config.BeepRange), 0f, 1f);
        var interval = config.BeepIntervalFar + (config.BeepIntervalNear - config.BeepIntervalFar) * closeness;
        ent.Comp.NextBeep = now + interval;
        _audio.PlayPvs(config.BeepSound, ent, AudioParams.Default.WithVolume(config.BeepVolume).WithMaxDistance(20f));
    }

    private void UpdateUi(Entity<LavalandSensorTabletComponent> ent)
    {
        var caves = ShowsCaves(ent);
        List<LavalandRadarPoint>? sentBase = null;
        if (caves)
        {
            // No outpost down in the caves: an empty plan once, and the real one again back on the surface.
            if (ent.Comp.SentBaseVersion != CavesBaseVersion)
            {
                sentBase = new List<LavalandRadarPoint>();
                ent.Comp.SentBaseVersion = CavesBaseVersion;
            }
        }
        else
        {
            var basePoints = GetBasePoints(ent.Comp);
            if (ent.Comp.SentBaseVersion != _baseVersion)
            {
                sentBase = new List<LavalandRadarPoint>(basePoints);
                ent.Comp.SentBaseVersion = _baseVersion;
            }
        }

        var points = new List<LavalandRadarPoint>();
        if (!caves)
        {
            foreach (var (center, radius, color) in _secretBoss.GetNoiseAreas())
            {
                points.Add(new LavalandRadarPoint(center, color, radius, LavalandRadarPointKind.CelestialNoise));
            }
        }
        // Marker beacons with a message: shown everywhere on the layer, the text pops up under the mouse.
        var beacons = EntityQueryEnumerator<LavalandMarkerBeaconComponent, TransformComponent>();
        while (beacons.MoveNext(out var beaconUid, out var beacon, out var beaconXform))
        {
            if (beacon.Message is not { } message || !IsOnLayer(beaconXform.MapUid, caves))
                continue;

            var beaconColor = _pointLight.TryGetLight(beaconUid, out var light) ? light.Color : Color.Orange;
            points.Add(new LavalandRadarPoint(_transform.GetWorldPosition(beaconXform), beaconColor, 0.6f,
                LavalandRadarPointKind.Beacon, message));
        }

        var scannedEntities = new HashSet<EntityUid>();
        var scannedTiles = new HashSet<(EntityUid Grid, Vector2i Tile)>();
        var users = new HashSet<EntityUid>();

        // The Ash Wolf is marked with its orange head wherever it is on lavaland, lying or fighting. Beaten and
        // turned into the miners' borg it is one of them: shown as a miner, with the ground around it.
        var wolves = EntityQueryEnumerator<LavalandAshWolfComponent, TransformComponent>();
        while (wolves.MoveNext(out var wolfUid, out var wolf, out var wolfXform))
        {
            if (!IsOnLayer(wolfXform.MapUid, caves))
                continue;

            if (wolf.Tamed)
            {
                if (users.Add(wolfUid))
                    AddMiner(ent, points, scannedTiles, scannedEntities, wolfUid, wolfXform, wolfXform.MapUid!.Value, caves);
                continue;
            }

            points.Add(new LavalandRadarPoint(_transform.GetWorldPosition(wolfXform), Color.FromHex("#ff8a1e"), 1f,
                LavalandRadarPointKind.BossHead));
        }

        var sensors = EntityQueryEnumerator<SuitSensorComponent>();
        var extractors = EntityQueryEnumerator<LavalandMorkiteExtractorComponent, TransformComponent>();
        while (extractors.MoveNext(out var extractor, out _, out var extractorXform))
        {
            if (!IsOnLayer(extractorXform.MapUid, caves))
                continue;
            var hash = extractor.GetHashCode() & int.MaxValue;
            var angle = Angle.FromDegrees(hash % 360);
            var offset = angle.ToVec() * (8f + hash % 8);
            points.Add(new LavalandRadarPoint(
                _transform.GetWorldPosition(extractorXform) + offset,
                ent.Comp.MorkiteHintColor,
                ent.Comp.MorkiteHintRadius,
                LavalandRadarPointKind.MorkiteSearchArea));
        }

        while (sensors.MoveNext(out var sensorUid, out var sensor))
        {
            if (sensor.Mode != SuitSensorMode.SensorCords || sensor.User is not { } user ||
                !IsEquippedSensor(sensorUid, sensor, user) || !IsPlayerBody(user) ||
                !TryComp(user, out TransformComponent? userXform) ||
                userXform.MapUid is not { } mapUid || !IsOnLayer(mapUid, caves) ||
                !users.Add(user))
            {
                continue;
            }

            AddMiner(ent, points, scannedTiles, scannedEntities, user, userXform, mapUid, caves);
        }

        int? charges = _itemSlots.TryGetSlot(ent, ent.Comp.SonarSlot, out var slot) &&
                       TryComp<LavalandSonarCartridgeComponent>(slot.Item, out var cartridge)
            ? cartridge.Charges
            : null;

        _ui.SetUiState(ent.Owner, LavalandSensorTabletUiKey.Key,
            new LavalandSensorTabletState(points, ent.Comp.ScanRadius, sentBase, caves ? (byte) 0 : GetStorm(),
                caves, ent.Comp.CanSwitchLayer, charges, ent.Comp.LastPing));
    }

    /// <summary>A miner on the map: its mark and, unless the night jams the trackers, the ground around it.</summary>
    private void AddMiner(Entity<LavalandSensorTabletComponent> ent,
        List<LavalandRadarPoint> points,
        HashSet<(EntityUid Grid, Vector2i Tile)> scannedTiles,
        HashSet<EntityUid> scannedEntities,
        EntityUid user,
        TransformComponent userXform,
        EntityUid mapUid,
        bool caves)
    {
        var userPosition = _transform.GetWorldPosition(userXform);
        var dead = TryComp<MobStateComponent>(user, out var mobState) && mobState.CurrentState == MobState.Dead;

        // Deep night: no surroundings, marks flicker and drift.
        if (!caves && _dayCycle.IsTrackerJammed(mapUid))
        {
            if (_random.Prob(ent.Comp.JamDropChance))
                return;

            points.Add(new LavalandRadarPoint(
                userPosition + _random.NextAngle().ToVec() * _random.NextFloat(0f, ent.Comp.JamScatter),
                dead ? ent.Comp.DeadColor : ent.Comp.LivingColor,
                ent.Comp.MobPointSize,
                dead ? LavalandRadarPointKind.DeadMiner : LavalandRadarPointKind.LivingMiner));
            return;
        }

        var grids = new List<Entity<MapGridComponent>>();
        _map.FindGridsIntersecting(userXform.MapID,
            Box2.CenteredAround(userPosition, new Vector2(ent.Comp.ScanRadius * 2)),
            ref grids, includeMap: true);
        foreach (var grid in grids)
        {
            // The outpost has its own floor/wall marks; scanning it as terrain would paint over them.
            if (HasComp<LavalandOutpostComponent>(grid.Owner))
                continue;
            AddTerrain(points, scannedTiles, grid.Owner, grid.Comp, userXform.Coordinates, ent.Comp);
        }
        AddNearbyEntities(points, scannedEntities, user, userPosition, userXform.MapID, ent.Comp);

        points.Add(new LavalandRadarPoint(userPosition,
            dead ? ent.Comp.DeadColor : ent.Comp.LivingColor,
            ent.Comp.MobPointSize,
            dead ? LavalandRadarPointKind.DeadMiner : LavalandRadarPointKind.LivingMiner));
    }

    /// <summary>The "outpost plan version" of a tablet showing the caves, where there is none.</summary>
    private const int CavesBaseVersion = -2;

    private List<LavalandRadarPoint> GetBasePoints(LavalandSensorTabletComponent tablet)
    {
        var now = _timing.CurTime;
        if (now < _baseCacheExpires)
            return _baseCache;
        _baseCacheExpires = now + BaseCacheLifetime;
        _baseCache.Clear();

        var outposts = EntityQueryEnumerator<LavalandOutpostComponent, MapGridComponent>();
        while (outposts.MoveNext(out var gridUid, out _, out var baseGrid))
        {
            foreach (var tile in _map.GetAllTiles(gridUid, baseGrid))
            {
                if (tile.Tile.IsEmpty)
                    continue;
                var position = _transform.ToMapCoordinates(_map.ToCenterCoordinates(tile)).Position;
                _baseCache.Add(new LavalandRadarPoint(position, tablet.BaseFloorColor,
                    tablet.TerrainPointSize, LavalandRadarPointKind.BaseFloor));
                var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, baseGrid, tile.GridIndices);
                while (anchored.MoveNext(out var structure))
                {
                    if (structure is { } wall && HasComp<WallComponent>(wall))
                        _baseCache.Add(new LavalandRadarPoint(position, tablet.BaseWallColor,
                            tablet.TerrainPointSize, LavalandRadarPointKind.BaseWall));
                }
            }
        }

        // Nothing found yet (outpost still loading): try again on the next update instead of in 30 seconds.
        if (_baseCache.Count == 0)
            _baseCacheExpires = now;

        // Only a changed plan (a wall built or torn down) is sent to the open windows again.
        var signature = _baseCache.Count;
        foreach (var point in _baseCache)
            signature = HashCode.Combine(signature, point.Position, point.Kind);
        if (signature != _baseSignature)
        {
            _baseSignature = signature;
            _baseVersion++;
        }

        return _baseCache;
    }

    private bool IsEquippedSensor(EntityUid uid, SuitSensorComponent sensor, EntityUid user)
    {
        if (TerminatingOrDeleted(user) || _containers.IsEntityOrParentInContainer(user))
            return false;
        if (_inventory.TryGetSlotEntity(user, sensor.ActivationSlot, out var equipped) && equipped == uid)
            return true;
        return sensor.ActivationContainer != null &&
               _containers.TryGetContainingContainer(uid, out var container) &&
               container.Owner == user && container.ID == sensor.ActivationContainer;
    }

    private void AddTerrain(List<LavalandRadarPoint> points,
        HashSet<(EntityUid Grid, Vector2i Tile)> scannedTiles,
        EntityUid grid,
        MapGridComponent gridComp,
        EntityCoordinates userCoordinates,
        LavalandSensorTabletComponent tablet)
    {
        var center = _transform.WithEntityId(userCoordinates, grid).Position;
        var tileCenter = new Vector2i((int) MathF.Floor(center.X), (int) MathF.Floor(center.Y));
        var radius = (int) MathF.Ceiling(tablet.ScanRadius);

        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                if (x * x + y * y > tablet.ScanRadius * tablet.ScanRadius)
                    continue;

                var indices = tileCenter + new Vector2i(x, y);
                if (!scannedTiles.Add((grid, indices)))
                    continue;

                var tile = _map.GetTileRef(grid, gridComp, indices);
                if (tile.Tile.IsEmpty)
                    continue;

                // Lava on lavaland is an entity on top of the floor and is added by AddNearbyEntities;
                // tile ids like FloorLavalandAsh must not be mistaken for it.
                var worldPosition = _transform.ToMapCoordinates(new EntityCoordinates(grid,
                    new Vector2(indices.X + 0.5f, indices.Y + 0.5f))).Position;
                points.Add(new LavalandRadarPoint(worldPosition, tablet.FloorColor, tablet.TerrainPointSize,
                    LavalandRadarPointKind.Terrain));
            }
        }
    }

    private void AddNearbyEntities(List<LavalandRadarPoint> points,
        HashSet<EntityUid> scanned,
        EntityUid user,
        Vector2 center,
        MapId mapId,
        LavalandSensorTabletComponent tablet)
    {
        foreach (var candidate in _lookup.GetEntitiesInRange(mapId, center, tablet.ScanRadius))
        {
            if (candidate == user || !scanned.Add(candidate) || !TryComp(candidate, out TransformComponent? xform))
                continue;

            Color color;
            float size;
            LavalandRadarPointKind kind;
            if (HasComp<LavalandFaunaComponent>(candidate))
            {
                color = tablet.FaunaColor;
                size = tablet.MobPointSize;
                kind = LavalandRadarPointKind.Fauna;
            }
            else if (HasComp<OreVeinComponent>(candidate))
            {
                color = tablet.OreColor;
                size = tablet.TerrainPointSize;
                kind = LavalandRadarPointKind.Ore;
            }
            else if (MetaData(candidate).EntityPrototype?.ID.Contains("WallRock", StringComparison.OrdinalIgnoreCase) == true)
            {
                color = tablet.RockColor;
                size = tablet.TerrainPointSize;
                kind = LavalandRadarPointKind.Rock;
            }
            else if (MetaData(candidate).EntityPrototype?.ID == "FloorLavaEntity")
            {
                color = tablet.LavaColor;
                size = tablet.TerrainPointSize;
                kind = LavalandRadarPointKind.Lava;
            }
            else
            {
                continue;
            }

            points.Add(new LavalandRadarPoint(_transform.GetWorldPosition(xform), color, size, kind));
        }
    }

    /// <summary>How the ash storm on lavaland stands, for the tablet's screen.</summary>
    private byte GetStorm()
    {
        var storm = LavalandWeatherStage.Calm;
        var maps = EntityQueryEnumerator<LavalandMapComponent>();
        while (maps.MoveNext(out _, out var lavaland))
        {
            if (lavaland.WeatherStage > storm)
                storm = lavaland.WeatherStage;
        }

        return (byte) storm;
    }

    /// <summary>
    /// A player or a player's body: somebody is (or was) playing it. Corpses lying about lavaland that nobody ever
    /// played - unidentified bodies in old suits - are not shown.
    /// </summary>
    private bool IsPlayerBody(EntityUid uid)
    {
        if (HasComp<ActorComponent>(uid))
            return true;

        return TryComp<MindContainerComponent>(uid, out var container) && container.Mind is { } mindId &&
               TryComp<MindComponent>(mindId, out var mind) && (mind.UserId != null || mind.OriginalOwnerUserId != null);
    }
}
