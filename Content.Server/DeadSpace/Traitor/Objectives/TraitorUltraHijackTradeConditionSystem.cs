// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Cargo.Components;
using Content.Server.DeadSpace.Taipan.Components;
using Content.Server.Station.Systems;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Station.Components;

namespace Content.Server.DeadSpace.Traitor.Objectives;

public sealed partial class TraitorUltraHijackTradeConditionSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TraitorUltraHijackTradeConditionComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<TraitorUltraHijackTradeConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<TraitorUltraHijackTradeConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    public bool HasObjectiveForStation(EntityUid user, EntityUid station, out EntityUid mindId)
    {
        mindId = default;

        if (!_mind.TryGetMind(user, out var userMindId, out var mind))
            return false;

        foreach (var objective in mind.Objectives)
        {
            if (TryComp<TraitorUltraHijackTradeConditionComponent>(objective, out var condition) &&
                condition.TargetStation == station &&
                !condition.Completed)
            {
                mindId = userMindId;
                return true;
            }
        }

        return false;
    }

    public bool TryCompleteObjective(EntityUid mindId, EntityUid station)
    {
        if (!TryComp<MindComponent>(mindId, out var mind))
            return false;

        var completed = false;
        foreach (var objective in mind.Objectives)
        {
            if (!TryComp<TraitorUltraHijackTradeConditionComponent>(objective, out var condition) ||
                condition.TargetStation != station ||
                condition.Completed)
            {
                continue;
            }

            condition.Completed = true;
            completed = true;
        }

        return completed;
    }

    private void OnAssigned(Entity<TraitorUltraHijackTradeConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        ent.Comp.TargetStation = GetObjectiveStation(args.Mind);
        ent.Comp.Completed = false;

        if (ent.Comp.TargetStation is not { } station || !CanHijackTrade(station))
            args.Cancelled = true;
    }

    private void OnAfterAssign(Entity<TraitorUltraHijackTradeConditionComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        _metaData.SetEntityName(ent.Owner, Loc.GetString(ent.Comp.Title), args.Meta);
        _metaData.SetEntityDescription(ent.Owner, Loc.GetString(ent.Comp.Description), args.Meta);
    }

    private void OnGetProgress(Entity<TraitorUltraHijackTradeConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = ent.Comp.Completed ? 1f : 0f;
    }

    private EntityUid? GetObjectiveStation(MindComponent mind)
    {
        if (mind.OwnedEntity is { } owned && !TerminatingOrDeleted(owned))
        {
            if (_station.GetOwningStation(owned) is { } owningStation)
                return owningStation;

            if (TryComp(owned, out TransformComponent? xform) &&
                _station.GetStationInMap(xform.MapID) is { } mapStation)
            {
                return mapStation;
            }
        }

        var stations = _station.GetStations();
        return stations.Count == 1 ? stations[0] : null;
    }

    private bool CanHijackTrade(EntityUid station)
    {
        return !HasComp<StationTaipanComponent>(station) &&
            TryComp<StationCargoOrderDatabaseComponent>(station, out var database) &&
            !database.TradeHijacked &&
            HasTradeGrid(station);
    }

    private bool HasTradeGrid(EntityUid station)
    {
        if (!TryComp<StationDataComponent>(station, out var stationData))
            return false;

        foreach (var grid in stationData.Grids)
        {
            if (HasComp<TradeStationComponent>(grid))
                return true;
        }

        return false;
    }
}
