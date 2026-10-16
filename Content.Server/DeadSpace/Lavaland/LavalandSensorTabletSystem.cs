// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
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
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly LavalandDayCycleSystem _dayCycle = default!;
    [Dependency] private readonly LavalandSecretBossSystem _secretBoss = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;

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
        });
    }

    private void OnOpened(Entity<LavalandSensorTabletComponent> ent, ref BoundUIOpenedEvent args)
    {
        // A fresh window has no outpost plan yet; the periodic update must not replace this state in the same tick.
        ent.Comp.SentBaseVersion = -1;
        ent.Comp.NextUpdate = _timing.CurTime + ent.Comp.UpdateInterval;
        UpdateUi(ent);
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
        var basePoints = GetBasePoints(ent.Comp);
        List<LavalandRadarPoint>? sentBase = null;
        if (ent.Comp.SentBaseVersion != _baseVersion)
        {
            sentBase = new List<LavalandRadarPoint>(basePoints);
            ent.Comp.SentBaseVersion = _baseVersion;
        }

        var points = new List<LavalandRadarPoint>();
        foreach (var (center, radius, color) in _secretBoss.GetNoiseAreas())
        {
            points.Add(new LavalandRadarPoint(center, color, radius, LavalandRadarPointKind.CelestialNoise));
        }
        // Marker beacons with a message: shown everywhere on lavaland, the text pops up under the mouse.
        var beacons = EntityQueryEnumerator<LavalandMarkerBeaconComponent, TransformComponent>();
        while (beacons.MoveNext(out var beaconUid, out var beacon, out var beaconXform))
        {
            if (beacon.Message is not { } message ||
                beaconXform.MapUid is not { } beaconMap || !HasComp<LavalandMapComponent>(beaconMap))
            {
                continue;
            }

            var beaconColor = _pointLight.TryGetLight(beaconUid, out var light) ? light.Color : Color.Orange;
            points.Add(new LavalandRadarPoint(_transform.GetWorldPosition(beaconXform), beaconColor, 0.6f,
                LavalandRadarPointKind.Beacon, message));
        }

        var scannedEntities = new HashSet<EntityUid>();
        var scannedTiles = new HashSet<(EntityUid Grid, Vector2i Tile)>();
        var users = new HashSet<EntityUid>();

        var sensors = EntityQueryEnumerator<SuitSensorComponent>();
        var extractors = EntityQueryEnumerator<LavalandMorkiteExtractorComponent, TransformComponent>();
        while (extractors.MoveNext(out var extractor, out _, out var extractorXform))
        {
            if (extractorXform.MapUid is not { } extractorMap || !HasComp<LavalandMapComponent>(extractorMap))
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
                userXform.MapUid is not { } mapUid || !HasComp<LavalandMapComponent>(mapUid) ||
                !users.Add(user))
            {
                continue;
            }

            var userPosition = _transform.GetWorldPosition(userXform);
            var dead = TryComp<MobStateComponent>(user, out var mobState) && mobState.CurrentState == MobState.Dead;

            // Deep night: no surroundings, marks flicker and drift.
            if (_dayCycle.IsTrackerJammed(mapUid))
            {
                if (_random.Prob(ent.Comp.JamDropChance))
                    continue;

                points.Add(new LavalandRadarPoint(
                    userPosition + _random.NextAngle().ToVec() * _random.NextFloat(0f, ent.Comp.JamScatter),
                    dead ? ent.Comp.DeadColor : ent.Comp.LivingColor,
                    ent.Comp.MobPointSize,
                    dead ? LavalandRadarPointKind.DeadMiner : LavalandRadarPointKind.LivingMiner));
                continue;
            }

            var grids = new List<Entity<MapGridComponent>>();
            _mapManager.FindGridsIntersecting(userXform.MapID,
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

        _ui.SetUiState(ent.Owner, LavalandSensorTabletUiKey.Key,
            new LavalandSensorTabletState(points, ent.Comp.ScanRadius, sentBase, GetStorm()));
    }

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
