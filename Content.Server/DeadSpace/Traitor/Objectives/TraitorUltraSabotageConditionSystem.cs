// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.Ame.Components;
using Content.Server.Cargo.Components;
using Content.Server.Medical.CrewMonitoring;
using Content.Server.Salvage.Expeditions;
using Content.Server.Salvage.Magnet;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Systems;
using Content.Server.SurveillanceCamera;
using Content.Server.Worldgen.Components.Debris;
using Content.Shared.Atmos.Components;
using Content.Shared.Cargo.Components;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;
using Content.Shared.Radio.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Traitor.Objectives;

public sealed partial class TraitorUltraSabotageConditionSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TraitorUltraSabotageConditionComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<TraitorUltraSabotageConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<TraitorUltraSabotageConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnAssigned(Entity<TraitorUltraSabotageConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        ent.Comp.GroupStates.Clear();
        ent.Comp.TargetStation = GetObjectiveStation(args.Mind);

        if (ent.Comp.TargetStation == null)
        {
            args.Cancelled = true;
            return;
        }

        foreach (var group in ent.Comp.Groups)
        {
            var state = new TraitorUltraSabotageGroupState();
            CollectTargets(group, state, ent.Comp.TargetStation.Value);

            state.Required = group.Required <= 0 ? Math.Max(1, state.Targets.Count) : group.Required;
            ent.Comp.GroupStates.Add(state);

            if (state.Targets.Count < state.Required || state.Required <= 0)
            {
                args.Cancelled = true;
                return;
            }
        }
    }

    private void OnAfterAssign(Entity<TraitorUltraSabotageConditionComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        _metaData.SetEntityName(ent.Owner, Loc.GetString(ent.Comp.Title), args.Meta);
        _metaData.SetEntityDescription(ent.Owner, Loc.GetString(ent.Comp.Description), args.Meta);
    }

    private void OnGetProgress(Entity<TraitorUltraSabotageConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = GetProgress(ent.Comp);
    }

    private float GetProgress(TraitorUltraSabotageConditionComponent comp)
    {
        RefreshTargets(comp);

        var totalRequired = 0;
        var completed = 0;

        for (var i = 0; i < comp.GroupStates.Count; i++)
        {
            var state = comp.GroupStates[i];
            var group = comp.Groups[i];
            totalRequired += state.Required;

            var completedInGroup = 0;
            foreach (var target in state.Targets)
            {
                if (IsSabotaged(target, group))
                    completedInGroup++;
            }

            completed += Math.Min(completedInGroup, state.Required);
        }

        if (totalRequired <= 0)
            return 0f;

        return Math.Clamp(completed / (float) totalRequired, 0f, 1f);
    }

    private void RefreshTargets(TraitorUltraSabotageConditionComponent comp)
    {
        if (comp.TargetStation is not { } station)
            return;

        for (var i = 0; i < comp.GroupStates.Count && i < comp.Groups.Count; i++)
        {
            CollectTargets(comp.Groups[i], comp.GroupStates[i], station);
        }
    }

    private void CollectTargets(TraitorUltraSabotageGroup group, TraitorUltraSabotageGroupState state, EntityUid station)
    {
        var targetGasMiners = GroupTargetsGasMiners(group);

        var query = AllEntityQuery<MetaDataComponent>();
        while (query.MoveNext(out var uid, out var metadata))
        {
            if (TerminatingOrDeleted(uid) ||
                state.Targets.Contains(uid) ||
                metadata.EntityPrototype?.ID is not { } prototypeId ||
                !group.Prototypes.Contains(new EntProtoId(prototypeId)))
            {
                continue;
            }

            if (!TryComp(uid, out TransformComponent? xform) ||
                _station.GetOwningStation(uid, xform) != station)
            {
                continue;
            }

            if (targetGasMiners && !IsTargetStationGasMiner(xform))
                continue;

            state.Targets.Add(uid);
        }
    }

    private bool IsTargetStationGasMiner(TransformComponent xform)
    {
        if (xform.GridUid is not { } grid)
            return false;

        if (HasComp<TradeStationComponent>(grid) ||
            HasComp<CargoShuttleComponent>(grid) ||
            HasComp<EmergencyShuttleComponent>(grid) ||
            HasComp<ArrivalsShuttleComponent>(grid) ||
            HasComp<LavalandShuttleComponent>(grid) ||
            HasComp<SalvageMagnetTargetComponent>(grid) ||
            HasComp<OwnedDebrisComponent>(grid))
        {
            return false;
        }

        return xform.MapUid == null || !HasComp<SalvageExpeditionComponent>(xform.MapUid.Value);
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

    private bool IsSabotaged(EntityUid target, TraitorUltraSabotageGroup group)
    {
        if (TerminatingOrDeleted(target))
            return true;

        if (!TryComp(target, out TransformComponent? xform) || !xform.Anchored)
            return true;

        if (GroupTargetsGasMiners(group))
            return !HasComp<GasMinerComponent>(target);

        if (GroupTargetsAmeControllers(group))
            return !HasComp<AmeControllerComponent>(target);

        if (GroupTargetsCrewMonitoring(group))
            return !HasComp<CrewMonitoringServerComponent>(target);

        if (GroupTargetsCameraRouters(group))
            return !HasComp<SurveillanceCameraRouterComponent>(target);

        if (GroupTargetsTelecomServers(group))
            return !HasComp<TelecomServerComponent>(target);

        return false;
    }

    private static bool GroupTargetsGasMiners(TraitorUltraSabotageGroup group)
    {
        return group.Prototypes.Any(proto => proto.ToString().StartsWith("GasMiner", StringComparison.Ordinal));
    }

    private static bool GroupTargetsAmeControllers(TraitorUltraSabotageGroup group)
    {
        return group.Prototypes.Any(proto => proto.ToString().StartsWith("AmeController", StringComparison.Ordinal));
    }

    private static bool GroupTargetsCrewMonitoring(TraitorUltraSabotageGroup group)
    {
        return group.Prototypes.Any(proto => proto.ToString() == "CrewMonitoringServer");
    }

    private static bool GroupTargetsCameraRouters(TraitorUltraSabotageGroup group)
    {
        return group.Prototypes.Any(proto => proto.ToString().StartsWith("SurveillanceCamera", StringComparison.Ordinal));
    }

    private static bool GroupTargetsTelecomServers(TraitorUltraSabotageGroup group)
    {
        return group.Prototypes.Any(proto => proto.ToString().StartsWith("TelecomServer", StringComparison.Ordinal));
    }
}
