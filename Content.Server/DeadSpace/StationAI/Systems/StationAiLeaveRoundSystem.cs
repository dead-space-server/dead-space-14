// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Chat.Systems;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Server.Silicons.StationAi;
using Content.Server.Station.Systems;
using Content.Shared.DeadSpace.StationAI;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.StationAI.Systems;

public sealed partial class StationAiLeaveRoundSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private GhostSystem _ghost = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StationAiSystem _stationAi = default!;
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private StationSystem _station = default!;

    private readonly ProtoId<JobPrototype> _stationAiJob = "StationAi";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<StationAiLeaveRoundConfirmedEvent>(OnLeaveRoundConfirmed);
    }

    private void OnLeaveRoundConfirmed(StationAiLeaveRoundConfirmedEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } ai || !HasComp<StationAiHeldComponent>(ai))
            return;

        TryLeaveRound(ai);
    }

    public bool TryLeaveRound(EntityUid ai, bool force = false)
    {
        if (!_stationAi.TryGetCore(ai, out var core) || core.Comp == null)
            return false;

        NetUserId? userId = null;
        if (_mind.TryGetMind(ai, out var mindId, out var mind))
        {
            userId = mind.UserId;

            if (!_ghost.OnGhostAttempt(mindId, canReturnGlobal: false, forced: force, mind: mind))
            {
                if (!force)
                    _popup.PopupEntity(Loc.GetString("station-ai-leave-round-failed"), ai, ai, PopupType.MediumCaution);

                return false;
            }
        }
        else if (!force)
        {
            _popup.PopupEntity(Loc.GetString("station-ai-leave-round-no-mind"), ai, ai, PopupType.MediumCaution);
            return false;
        }

        var aiName = Name(ai);

        var station = userId != null
            ? FreeStationJobs(core.Owner, userId.Value)
            : _station.GetOwningStation(core.Owner);
        if (station != null)
        {
            _chat.DispatchStationAnnouncement(
                station.Value,
                Loc.GetString("station-ai-leave-round-announcement", ("name", aiName)),
                Loc.GetString("station-ai-leave-round-sender"),
                playDefaultSound: false);
        }

        _container.TryRemoveFromContainer(ai, force: true);
        QueueDel(ai);
        return true;
    }

    public bool TryResolveAi(EntityUid target, out EntityUid ai)
    {
        if (HasComp<StationAiHeldComponent>(target))
        {
            ai = target;
            return true;
        }

        if (TryComp<StationAiHolderComponent>(target, out var holder) && holder.Slot.Item is { } held)
        {
            ai = held;
            return true;
        }

        if (TryComp<MovementRelayTargetComponent>(target, out var relay) &&
            HasComp<StationAiHeldComponent>(relay.Source))
        {
            ai = relay.Source;
            return true;
        }

        ai = default;
        return false;
    }

    private EntityUid? FreeStationJobs(EntityUid core, NetUserId userId)
    {
        var owningStation = _station.GetOwningStation(core);
        if (owningStation != null && TryFreeStationJobs(owningStation.Value, userId))
            return owningStation;

        foreach (var station in _station.GetStations())
        {
            if (station == owningStation)
                continue;

            if (TryFreeStationJobs(station, userId))
                return station;
        }

        return owningStation;
    }

    private bool TryFreeStationJobs(EntityUid station, NetUserId userId)
    {
        if (!_stationJobs.TryGetPlayerJobs(station, userId, out var jobs) || !jobs.Contains(_stationAiJob))
            return false;

        foreach (var job in jobs)
        {
            _stationJobs.TryAdjustJobSlot(station, job, 1, clamp: true);
        }

        _stationJobs.TryRemovePlayerJobs(station, userId);
        return true;
    }
}
