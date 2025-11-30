// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Content.Server.GameTicking.Rules;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Map;

namespace Content.Server.DeadSpace.GoalGamerules;

public sealed class DockOnStationMapRuleSystem : StationEventSystem<DockOnStationMapRuleComponent>
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;
    protected override void Added(EntityUid uid, DockOnStationMapRuleComponent comp, GameRuleComponent rule, GameRuleAddedEvent args)
    {
        if (comp.GridPath is not {} gridPath)
        {
            Log.Error($"[LoadOnStationMapRule] No GridPath specified on {ToPrettyString(uid)}");
            ForceEndSelf(uid, rule);
            return;
        }

        MapId mapId = MapId.Nullspace;
        EntityUid stationGrid = EntityUid.Invalid;

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
                        stationGrid = stationDataGrid;
                        break;
                    }
                }
            }
        }

        var opts = DeserializationOptions.Default with { InitializeMaps = true };
        _entityManager.System<MapLoaderSystem>().TryLoadGrid(mapId, gridPath, out var shuttle);
        if (_entityManager.Deleted(shuttle))
        {
            Log.Error("Ошибка: Шаттл не существует или был удалён.");
            return;
        }
        if (!_entityManager.TryGetComponent(shuttle, out ShuttleComponent? shuttleComponent))
        {
            Log.Error("Ошибка: Не найден ShuttleComponent у заспавненного шаттла.");
            return;
        }

        if (!_entityManager.System<ShuttleSystem>().TryFTLDock(shuttle.Value, shuttleComponent, stationGrid, out _))
        {
            Log.Warning("Предупреждение: Стыковка не выполнена.");
        }

        Log.Info($"[LoadOnStationMapRule] Loaded grid from {gridPath} onto station map {mapId}");

        var ev = new RuleLoadedGridsEvent(mapId, new List<EntityUid> { shuttle.Value.Owner });
        RaiseLocalEvent(uid, ref ev);

        base.Added(uid, comp, rule, args);
    }
}
