// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Taipan.Components;
using Content.Server.Destructible;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles.Jobs;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.Traitor.Objectives;

public sealed partial class TraitorUltraDestroyStationAiCoreConditionSystem : EntitySystem
{
    private const string StationAiJob = "StationAi";

    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedStationAiSystem _stationAi = default!;
    [Dependency] private StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TraitorUltraDestroyStationAiCoreConditionComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<TraitorUltraDestroyStationAiCoreConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<TraitorUltraDestroyStationAiCoreConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnAssigned(Entity<TraitorUltraDestroyStationAiCoreConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        ent.Comp.TargetStation = GetObjectiveStation(args.Mind);
        ent.Comp.TargetCore = null;

        if (ent.Comp.TargetStation is not { } station ||
            HasComp<StationTaipanComponent>(station) ||
            !TryFindJobStationAiCore(station, out var core) ||
            !HasActiveStationAiPlayerInCore(core))
        {
            args.Cancelled = true;
            return;
        }

        ent.Comp.TargetCore = core;
    }

    private void OnAfterAssign(Entity<TraitorUltraDestroyStationAiCoreConditionComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        _metaData.SetEntityName(ent.Owner, Loc.GetString(ent.Comp.Title), args.Meta);
        _metaData.SetEntityDescription(ent.Owner, Loc.GetString(ent.Comp.Description), args.Meta);
    }

    private void OnGetProgress(Entity<TraitorUltraDestroyStationAiCoreConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = IsCoreDestroyed(ent.Comp.TargetCore) ? 1f : 0f;
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

    private bool TryFindJobStationAiCore(EntityUid station, out EntityUid core)
    {
        var query = EntityQueryEnumerator<StationAiCoreComponent, ContainerSpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var spawnPoint, out var xform))
        {
            if (spawnPoint.Job != StationAiJob ||
                TerminatingOrDeleted(uid) ||
                _station.GetOwningStation(uid, xform) != station)
            {
                continue;
            }

            core = uid;
            return true;
        }

        core = default;
        return false;
    }

    private bool HasActiveStationAiPlayerInCore(EntityUid core)
    {
        if (!_stationAi.TryGetHeld((core, CompOrNull<StationAiCoreComponent>(core)), out var held) ||
            held == null ||
            TerminatingOrDeleted(held.Value) ||
            !HasComp<ActorComponent>(held.Value) ||
            !HasComp<StationAiHeldComponent>(held.Value) ||
            !_mind.TryGetMind(held.Value, out var mindId, out _))
        {
            return false;
        }

        return _jobs.MindHasJobWithId(mindId, StationAiJob);
    }

    private bool IsCoreDestroyed(EntityUid? core)
    {
        if (core == null)
            return false;

        if (TerminatingOrDeleted(core.Value))
            return true;

        return TryComp<DestructibleComponent>(core.Value, out var destructible) && destructible.IsBroken;
    }
}
