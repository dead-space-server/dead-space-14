using System.Numerics;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Utility;
using Robust.Shared.Maths;
using Content.Server.GameTicking.Rules;

using Content.Server.Station.Systems;
using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Map;

namespace Content.Server.DeadSpace.GoalGamerules;

public sealed class LoadOnStationMapRuleSystem : StationEventSystem<LoadOnStationMapRuleComponent>
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly TransformSystem _transform = default!;



    protected override void Added(EntityUid uid, LoadOnStationMapRuleComponent comp, GameRuleComponent rule, GameRuleAddedEvent args)
    {
        if (comp.GridPath is not {} gridPath)
        {
            Log.Error($"[LoadOnStationMapRule] No GridPath specified on {ToPrettyString(uid)}");
            ForceEndSelf(uid, rule);
            return;
        }

        MapId mapId = MapId.Nullspace;
        Vector2 stationPos = Vector2.Zero;

        foreach (var stationUid in _station.GetStations())
        {
            if (!TryComp<StationDataComponent>(stationUid, out var stationData))
                continue;

            foreach (var stationDataGrid in stationData.Grids)
            {
                if (HasComp<BecomesStationComponent>(stationDataGrid))
                {
                    var xform = Transform(stationDataGrid);
                    if (xform.MapID != MapId.Nullspace)
                    {
                        mapId = xform.MapID;
                        stationPos = xform.LocalPosition;
                        break;
                    }
                }
            }
        }

        Random random = new Random();
        Vector2 vector2d = new Vector2(random.Next(-500,500),random.Next(-500,500));
        vector2d.Normalize();
        vector2d *= comp.Radius;
        vector2d += stationPos;

        var opts = DeserializationOptions.Default with { InitializeMaps = true };
        if (!_mapLoader.TryLoadGrid(mapId, gridPath, out var result, opts, vector2d, random.Next(360) ))
        {
            Log.Error($"[LoadOnStationMapRule] Cannot load grid from {gridPath}");
            ForceEndSelf(uid, rule);
            return;
        }


        var grid = result.Value.Owner;

        Log.Info($"[LoadOnStationMapRule] Loaded grid from {gridPath} onto station map {mapId}");

        // Сообщаем системе геймрулов (если кому-то нужно)
        var ev = new RuleLoadedGridsEvent(mapId, new List<EntityUid> { grid });
        RaiseLocalEvent(uid, ref ev);

        base.Added(uid, comp, rule, args);
    }
}
