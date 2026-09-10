// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Maps;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Medical.SuitSensors;
using Content.Shared.Mining.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandSensorTabletSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

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
        UpdateUi(ent);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandSensorTabletComponent>();
        while (query.MoveNext(out var uid, out var tablet))
        {
            if (tablet.NextUpdate > now || !_ui.IsUiOpen(uid, LavalandSensorTabletUiKey.Key))
                continue;

            tablet.NextUpdate = now + tablet.UpdateInterval;
            UpdateUi((uid, tablet));
        }
    }

    private void UpdateUi(Entity<LavalandSensorTabletComponent> ent)
    {
        var points = new List<LavalandRadarPoint>();
        var scannedEntities = new HashSet<EntityUid>();
        var scannedTiles = new HashSet<(EntityUid Grid, Vector2i Tile)>();

        var sensors = EntityQueryEnumerator<SuitSensorComponent>();
        while (sensors.MoveNext(out _, out var sensor))
        {
            if (sensor.Mode != SuitSensorMode.SensorCords || sensor.User is not { } user ||
                !TryComp<TransformComponent>(user, out var userXform) ||
                userXform.MapUid is not { } mapUid || !HasComp<LavalandMapComponent>(mapUid) ||
                userXform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            {
                continue;
            }

            var userPosition = _transform.GetWorldPosition(userXform);
            AddTerrain(points, scannedTiles, grid, gridComp, userXform.Coordinates, ent.Comp);
            AddNearbyEntities(points, scannedEntities, user, userPosition, userXform.MapID, ent.Comp);

            var dead = TryComp<MobStateComponent>(user, out var mobState) && mobState.CurrentState == MobState.Dead;
            points.Add(new LavalandRadarPoint(userPosition,
                dead ? ent.Comp.DeadColor : ent.Comp.LivingColor,
                ent.Comp.MobPointSize,
                dead ? LavalandRadarPointKind.DeadMiner : LavalandRadarPointKind.LivingMiner));
        }

        _ui.SetUiState(ent.Owner, LavalandSensorTabletUiKey.Key,
            new LavalandSensorTabletState(points, ent.Comp.ScanRadius));
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

                var definition = _turf.GetContentTileDefinition(tile);
                var color = definition.ID.Contains("Lava", StringComparison.OrdinalIgnoreCase)
                    ? tablet.LavaColor
                    : tablet.FloorColor;
                var worldPosition = _transform.ToMapCoordinates(new EntityCoordinates(grid,
                    new Vector2(indices.X + 0.5f, indices.Y + 0.5f))).Position;
                points.Add(new LavalandRadarPoint(worldPosition, color, tablet.TerrainPointSize,
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
            if (candidate == user || !scanned.Add(candidate) || !TryComp<TransformComponent>(candidate, out var xform))
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
            else
            {
                continue;
            }

            points.Add(new LavalandRadarPoint(_transform.GetWorldPosition(xform), color, size, kind));
        }
    }
}
