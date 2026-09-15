using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.DeadSpace.Prison;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.GameTicking.Rules;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared.DeadSpace.AshWalkers;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.AshWalkers;

public sealed class AshWalkerLobbySystem : EntitySystem
{
    [Dependency] private readonly IBanManager _bans = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly GhostRoleSystem _ghostRoles = default!;
    [Dependency] private readonly StationJobsSystem _jobs = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly ProtoId<JobPrototype> Job = "AshWalker";
    private static readonly EntProtoId Station = "AshWalkerStation";
    private readonly Dictionary<NetUserId, PendingSpawn> _pending = new();
    private TimeSpan _nextRefresh;
    private bool _rolesDirty;

    private sealed record PendingSpawn(EntityUid Station, HumanoidCharacterProfile Profile);

    public override void Initialize()
    {
        SubscribeLocalEvent<RulePlayerSpawningEvent>(_ => RefreshJobs());
        SubscribeLocalEvent<StationJobsGetAvailableJobsEvent>(OnAvailableJobs);
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn, after: [typeof(PrisonSystem), typeof(DeathMatchRuleSystem)]);
        SubscribeLocalEvent<AshWalkerEggComponent, TakeGhostRoleEvent>(OnRoleTaken, after: [typeof(GhostRoleSystem)]);
        SubscribeLocalEvent<AshWalkerEggComponent, GhostRoleAvailabilityEvent>(OnRoleAvailability);
        SubscribeLocalEvent<LavalandMapComponent, ComponentShutdown>(OnMapShutdown);
        SubscribeLocalEvent<PlayerAttachedEvent>(args => RemovePending(args.Player.UserId));
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeNetworkEvent<AshWalkerLobbyStateRequestEvent>(OnStateRequest);
        SubscribeNetworkEvent<AshWalkerLobbyCancelEvent>(OnCancel);
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
        base.Shutdown();
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + TimeSpan.FromSeconds(1);
        ProcessPendingSpawns();
        RefreshJobs();
    }

    private bool IsPreparing(EntityUid map)
    {
        var query = EntityQueryEnumerator<StationLavalandComponent>();
        while (query.MoveNext(out var lavaland))
        {
            if (lavaland.GeneratedMap == map && lavaland.GenerationTask is { IsCompleted: false })
                return true;
        }

        return false;
    }

    private void OnAvailableJobs(ref StationJobsGetAvailableJobsEvent args)
    {
        if (!_cfg.GetCVar(CCCCVars.AshWalkersEnabled) ||
            !TryComp<AshWalkerStationComponent>(args.Station, out var station) ||
            station.Map is not { } map || !IsPreparing(map))
            return;

        // Accept waiting candidates without publishing nonexistent eggs as late-join slots.
        args.Jobs[Job] = args.Profiles.Count;
    }

    private bool CanJoin(ICommonSession player)
    {
        if (!_cfg.GetCVar(CCCCVars.AshWalkersEnabled) || _bans.GetJobBans(player.UserId)?.Contains(Job) == true)
            return false;

        var allowed = new IsRoleAllowedEvent(player, [Job], ["GenericTeamAntagonist"]);
        RaiseLocalEvent(ref allowed);
        var restricted = new GetDisallowedJobsEvent(player, new HashSet<ProtoId<JobPrototype>>());
        RaiseLocalEvent(ref restricted);
        return !allowed.Cancelled && !restricted.Jobs.Contains(Job);
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent args)
    {
        if (args.Cancelled || args.Handled || args.Deferred)
            return;

        if (args.JobId != Job.Id && !HasComp<AshWalkerStationComponent>(args.Station))
        {
            RemovePending(args.Player.UserId);
            return;
        }

        args.Cancelled = true;
        args.Reason = Loc.GetString("ash-walker-job-unavailable");
        if (!TryComp<AshWalkerStationComponent>(args.Station, out var station) || station.Map is not { } map ||
            args.JobId != Job.Id || !CanJoin(args.Player))
            return;

        if (IsPreparing(map))
        {
            _pending[args.Player.UserId] = new PendingSpawn(args.Station, args.Profile);
            _rolesDirty = true;
            SendState(args.Player);
            args.Cancelled = false;
            args.Reason = null;
            args.Deferred = true;
            return;
        }

        if (!TryTakeEgg(args.Player, map))
            return;

        args.Cancelled = false;
        args.Reason = null;
        args.Handled = true;
    }

    private IEnumerable<Entity<GhostRoleComponent>> GetAvailableEggs(EntityUid map, ICommonSession? player = null)
    {
        return _ghostRoles.GhostRoles.Where(role => HasComp<AshWalkerEggComponent>(role) &&
            Transform(role).MapUid == map && _ghostRoles.CanTakeGhost(role, role.Comp, player));
    }

    private bool TryTakeEgg(ICommonSession player, EntityUid map)
    {
        foreach (var role in GetAvailableEggs(map, player).ToArray())
        {
            if (_ghostRoles.Takeover(player, role.Comp.Identifier))
                return true;
        }

        return false;
    }

    private void ProcessPendingSpawns()
    {
        if (_ticker.RunLevel != GameRunLevel.InRound)
        {
            if (_ticker.RunLevel == GameRunLevel.PostRound)
            {
                foreach (var player in _pending.Keys.ToArray())
                    RemovePending(player);
            }
            return;
        }

        foreach (var group in _pending.ToArray().GroupBy(entry => entry.Value.Station))
        {
            if (!TryComp<AshWalkerStationComponent>(group.Key, out var station) || station.Map is not { } map ||
                !HasComp<LavalandMapComponent>(map) || TerminatingOrDeleted(map))
            {
                foreach (var player in group.Select(entry => entry.Key))
                    RemovePending(player, "ash-walker-job-unavailable");
                continue;
            }

            if (_cfg.GetCVar(CCCCVars.AshWalkersEnabled) && IsPreparing(map))
                continue;

            var profiles = new Dictionary<NetUserId, HumanoidCharacterProfile>();
            foreach (var (player, pending) in group)
            {
                if (!_players.TryGetSessionById(player, out var session) ||
                    session.Status == SessionStatus.Disconnected || session.AttachedEntity != null || !CanJoin(session))
                {
                    RemovePending(player, "ash-walker-job-unavailable");
                    continue;
                }

                var priority = pending.Profile.JobPriorities.GetValueOrDefault(Job, JobPriority.High);
                profiles[player] = pending.Profile.WithJobPriorities([new(Job, priority)]);
            }

            if (profiles.Count == 0)
                continue;

            var claimant = _players.GetSessionById(profiles.Keys.First());
            _jobs.TrySetJobSlot(group.Key, Job.Id, GetAvailableEggs(map, claimant).Count());
            var assigned = _jobs.AssignJobs(profiles, [group.Key], useRoundStartJobs: false);
            foreach (var player in assigned.Keys)
            {
                if (_players.TryGetSessionById(player, out var session) && TryTakeEgg(session, map))
                    _ticker.PlayerJoinGame(session);
            }

            foreach (var player in profiles.Keys)
                RemovePending(player, "ash-walker-job-unavailable");
        }
    }

    private void OnRoleAvailability(Entity<AshWalkerEggComponent> ent, ref GhostRoleAvailabilityEvent args)
    {
        if (!_cfg.GetCVar(CCCCVars.AshWalkersEnabled) ||
            HasComp<AshWalkerIncubatingEggComponent>(ent) ||
            ent.Comp.HomeMap is not { } map || Transform(ent).MapUid != map ||
            !HasComp<LavalandMapComponent>(map))
        {
            args.Cancel();
            return;
        }

        foreach (var pending in _pending.Values)
        {
            if (!TryComp<AshWalkerStationComponent>(pending.Station, out var station) || station.Map != map)
                continue;

            if (args.Player == null || !_pending.TryGetValue(args.Player.UserId, out var own) || own.Station != pending.Station)
                args.Cancel();
            return;
        }
    }

    private void OnRoleTaken(Entity<AshWalkerEggComponent> ent, ref TakeGhostRoleEvent args)
    {
        if (!args.TookRole || ent.Comp.HomeMap is not { } map || !HasComp<LavalandMapComponent>(map))
            return;

        var station = GetOrCreateStation(map);
        _jobs.TryGetJobSlot(station, Job.Id, out var slots);
        if (slots is null or <= 0)
            _jobs.TrySetJobSlot(station, Job.Id, 1);

        _jobs.TryAssignJob(station, Job.Id, args.Player.UserId);
        RemovePending(args.Player.UserId);
        RefreshJobs();
    }

    private EntityUid GetOrCreateStation(EntityUid map)
    {
        var stations = EntityQueryEnumerator<AshWalkerStationComponent>();
        while (stations.MoveNext(out var uid, out var station))
        {
            if (station.Map == map && !TerminatingOrDeleted(uid))
                return uid;
        }

        var created = Spawn(Station, MapCoordinates.Nullspace);
        Comp<AshWalkerStationComponent>(created).Map = map;
        return created;
    }

    private void RefreshJobs()
    {
        if (_cfg.GetCVar(CCCCVars.AshWalkersEnabled))
        {
            var generators = EntityQueryEnumerator<StationLavalandComponent>();
            while (generators.MoveNext(out var generator))
            {
                if (generator.GenerationTask is { IsCompleted: false } && generator.GeneratedMap is { } map &&
                    HasComp<LavalandMapComponent>(map) && !TerminatingOrDeleted(map))
                    GetOrCreateStation(map);
            }
        }

        var counts = new Dictionary<EntityUid, int>();
        foreach (var role in _ghostRoles.GhostRoles)
        {
            if (!HasComp<AshWalkerEggComponent>(role) || !_ghostRoles.CanTakeGhost(role, role.Comp) ||
                Transform(role).MapUid is not { } map)
                continue;

            counts[map] = counts.GetValueOrDefault(map) + 1;
        }

        foreach (var map in counts.Keys)
            GetOrCreateStation(map);

        var stations = EntityQueryEnumerator<AshWalkerStationComponent>();
        while (stations.MoveNext(out var uid, out var station))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            var count = station.Map is { } map ? counts.GetValueOrDefault(map) : 0;
            if (_jobs.TryGetJobSlot(uid, Job.Id, out var slots) && slots != count)
                _jobs.TrySetJobSlot(uid, Job.Id, count);
        }

        if (_rolesDirty)
        {
            _rolesDirty = false;
            _ghostRoles.UpdateAllEui();
        }
    }

    private void RemovePending(NetUserId player, string? reason = null)
    {
        if (!_pending.Remove(player))
            return;

        _rolesDirty = true;
        if (!_players.TryGetSessionById(player, out var session) || session.Status == SessionStatus.Disconnected)
            return;

        SendState(session);
        if (reason != null)
            _chat.DispatchServerMessage(session, Loc.GetString(reason));
    }

    private void SendState(ICommonSession player)
    {
        RaiseNetworkEvent(new AshWalkerLobbyStateEvent(_pending.ContainsKey(player.UserId)), player.Channel);
    }

    private void OnStateRequest(AshWalkerLobbyStateRequestEvent args, EntitySessionEventArgs session)
    {
        SendState(session.SenderSession);
    }

    private void OnCancel(AshWalkerLobbyCancelEvent args, EntitySessionEventArgs session)
    {
        RemovePending(session.SenderSession.UserId);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.Disconnected)
            RemovePending(args.Session.UserId);
    }

    private void OnMapShutdown(Entity<LavalandMapComponent> ent, ref ComponentShutdown args)
    {
        var stations = EntityQueryEnumerator<AshWalkerStationComponent>();
        while (stations.MoveNext(out var uid, out var station))
        {
            if (station.Map == ent.Owner)
                QueueDel(uid);
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        foreach (var player in _pending.Keys.ToArray())
            RemovePending(player);
        _nextRefresh = TimeSpan.Zero;
    }
}
