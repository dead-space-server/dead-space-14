// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.DeadSpace.Interfaces.Server;
using Content.Server.Chat.Managers;
using Content.Server.DeadSpace.Prison;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Station.Components;
using Content.Shared.DeadSpace.Arena;
using Content.Shared.DeadSpace.NewLife;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.NewLife;

public sealed partial class NewLifeSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly PrisonSystem _prison = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EuiManager _eui = default!;
    [Dependency] private readonly IChatManager _chat = default!;

    private static readonly TimeSpan RespawnDelay = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RulesDelay = TimeSpan.FromSeconds(5);
    private readonly Dictionary<NetUserId, LifeRecord> _records = new();
    private readonly Dictionary<NetUserId, NewLifeState> _lastStates = new();
    private readonly Dictionary<NetUserId, NewLifeEui> _windows = new();
    private readonly HashSet<NetUserId> _observers = new();
    private TimeSpan _nextUpdate;

    private enum UseState : byte
    {
        Unused,
        Reserved,
        Used,
    }

    private sealed class LifeRecord
    {
        public UseState Use;
        public EntityUid? Mind;
        public EntityUid? Body;
        public TimeSpan? AvailableAt;
        public TimeSpan NextRequest;
        public TimeSpan NextOpen;
        public readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase);
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<NewLifeStateRequestEvent>(OnStateRequest);
        SubscribeNetworkEvent<NewLifeOpenEvent>(OnOpen);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
        SubscribeLocalEvent<MindGotAddedEvent>(OnMindAdded);
        SubscribeLocalEvent<NewLifeTrackedBodyComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<NewLifeTrackedBodyComponent, EntityTerminatingEvent>(OnBodyTerminating);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
        InitializeSpawning();
    }

    public override void Shutdown()
    {
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
        foreach (var window in _windows.Values.ToArray())
            window.Close();
        base.Shutdown();
    }

    private LifeRecord GetRecord(NetUserId userId)
    {
        if (!_records.TryGetValue(userId, out var record))
            _records.Add(userId, record = new LifeRecord());
        return record;
    }

    private static bool HasAccess(NetUserId userId)
    {
        return IoCManager.Instance!.TryResolveType<IServerSponsorsManager>(out var sponsors) && sponsors.HasNewLife(userId);
    }

    private void OnStateRequest(NewLifeStateRequestEvent args, EntitySessionEventArgs ev)
    {
        var record = GetRecord(ev.SenderSession.UserId);
        if (_timing.CurTime < record.NextRequest)
            return;
        record.NextRequest = _timing.CurTime + TimeSpan.FromSeconds(1);
        SendState(ev.SenderSession, true);
    }

    private void OnOpen(NewLifeOpenEvent args, EntitySessionEventArgs ev)
    {
        var session = ev.SenderSession;
        if (!HasComp<GhostComponent>(session.AttachedEntity) || !HasAccess(session.UserId))
            return;

        var record = GetRecord(session.UserId);
        if (_timing.CurTime < record.NextOpen || _windows.ContainsKey(session.UserId))
            return;
        record.NextOpen = _timing.CurTime + TimeSpan.FromSeconds(1);

        var window = new NewLifeEui(this, _timing.CurTime + RulesDelay);
        _windows.Add(session.UserId, window);
        _eui.OpenEui(window, session);
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (HasComp<GhostComponent>(args.Entity))
            _observers.Add(args.Player.UserId);
        else
        {
            _observers.Remove(args.Player.UserId);
            CloseWindow(args.Player.UserId);
            TrackBody(args.Entity);
        }

        SendState(args.Player, true);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Disconnected)
            return;
        _observers.Remove(args.Session.UserId);
        _lastStates.Remove(args.Session.UserId);
        CloseWindow(args.Session.UserId);
    }

    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent args)
    {
        _observers.Remove(args.PlayerSession.UserId);
        CloseWindow(args.PlayerSession.UserId);
        SendState(args.PlayerSession, true);
    }

    private void OnMindAdded(MindGotAddedEvent args)
    {
        TrackBody(args.Container.Owner);
    }

    private void TrackBody(EntityUid body)
    {
        if (TerminatingOrDeleted(body) || HasComp<GhostComponent>(body) || HasComp<ArenaPlayerComponent>(body) ||
            !_mind.TryGetMind(body, out var mindId, out var mind) || mind.UserId is not { } userId ||
            HasComp<ArenaMindComponent>(mindId))
            return;

        var record = GetRecord(userId);
        if (record.Body != body)
        {
            record.Body = body;
            if (IsLivingBody(body))
                record.AvailableAt = null;
            else if (record.Mind != mindId)
                record.AvailableAt = _timing.CurTime + RespawnDelay;
            else
                record.AvailableAt ??= _timing.CurTime + RespawnDelay;
        }
        record.Mind = mindId;
        record.Names.Add(NormalizeName(mind.CharacterName ?? Name(body)));
        EnsureComp<NewLifeTrackedBodyComponent>(body).UserId = userId;
    }

    private bool IsLivingBody(EntityUid? body)
    {
        return body is { } uid && !TerminatingOrDeleted(uid) && !HasComp<GhostComponent>(uid) &&
               TryComp<MobStateComponent>(uid, out var state) && state.CurrentState != MobState.Dead;
    }

    private void OnMobStateChanged(Entity<NewLifeTrackedBodyComponent> ent, ref MobStateChangedEvent args)
    {
        if (!_records.TryGetValue(ent.Comp.UserId, out var record) || record.Body != ent.Owner)
            return;
        record.AvailableAt = args.NewMobState == MobState.Dead ? _timing.CurTime + RespawnDelay : null;
    }

    private void OnBodyTerminating(Entity<NewLifeTrackedBodyComponent> ent, ref EntityTerminatingEvent args)
    {
        if (_records.TryGetValue(ent.Comp.UserId, out var record) && record.Body == ent.Owner)
            record.AvailableAt ??= _timing.CurTime + RespawnDelay;
    }

    private string? GetRoundBlocker(NetUserId userId)
    {
        if (_ticker.RunLevel != GameRunLevel.InRound)
            return "new-life-unavailable-round";
        if (!_ticker.LobbyEnabled || _ticker.DisallowLateJoin)
            return "new-life-unavailable-late-join";
        if (_prison.IsUserPrisoner(userId))
            return "new-life-unavailable";

        var stations = EntityQueryEnumerator<StationSpawningComponent, StationJobsComponent>();
        while (stations.MoveNext(out var station, out _, out _))
        {
            if (!TerminatingOrDeleted(station))
                return null;
        }
        return "new-life-unavailable-station";
    }

    public NewLifeState GetState(ICommonSession session)
    {
        if (!HasAccess(session.UserId))
            return new(NewLifeAvailability.NoAccess);

        var record = GetRecord(session.UserId);
        if (record.Use == UseState.Used)
            return new(NewLifeAvailability.Used);

        if (GetRoundBlocker(session.UserId) is { } reason)
            return new(NewLifeAvailability.Unavailable, Reason: reason);

        if (session.AttachedEntity is not { } ghostId || !TryComp<GhostComponent>(ghostId, out var ghost) ||
            ghost.CanGhostInteract || MetaData(ghostId).EntityPrototype?.ID == GameTicker.AdminObserverPrototypeName.Id ||
            !_mind.TryGetMind(session, out var mindId, out var mind) || mind.PreventGhosting || HasComp<ArenaMindComponent>(mindId))
            return new(NewLifeAvailability.Unavailable, Reason: "new-life-unavailable-ghost");

        if (IsLivingBody(record.Body) || IsLivingBody(mind.OwnedEntity))
            return new(NewLifeAvailability.Unavailable, Reason: "new-life-unavailable-alive");

        record.AvailableAt ??= _timing.CurTime + RespawnDelay;
        return _timing.CurTime < record.AvailableAt
            ? new(NewLifeAvailability.Waiting, record.AvailableAt)
            : new(NewLifeAvailability.Available);
    }

    private void SendState(ICommonSession session, bool force = false)
    {
        if (session.Status != SessionStatus.InGame)
            return;

        var state = GetState(session);
        if (!force && _lastStates.TryGetValue(session.UserId, out var previous) && state == previous)
            return;
        _lastStates[session.UserId] = state;
        RaiseNetworkEvent(new NewLifeStateEvent(state), session.Channel);
        if (_windows.TryGetValue(session.UserId, out var window))
        {
            if (state.Availability == NewLifeAvailability.NoAccess)
                window.Close();
            else
                window.StateDirty();
        }
    }

    public bool TryReturnToLobby(ICommonSession session, NewLifeEui window)
    {
        if (!_windows.TryGetValue(session.UserId, out var current) || current != window ||
            _timing.CurTime < window.ConfirmAt || GetState(session).Availability != NewLifeAvailability.Available)
            return false;

        var record = GetRecord(session.UserId);
        record.Use = UseState.Reserved;
        record.Body = null;
        CloseWindow(session.UserId);
        _ticker.Respawn(session);
        _chat.DispatchServerMessage(session, Loc.GetString("new-life-lobby-instructions"));
        return true;
    }

    public void OnWindowClosed(ICommonSession session, NewLifeEui window)
    {
        if (_windows.TryGetValue(session.UserId, out var current) && current == window)
            _windows.Remove(session.UserId);
    }

    private void CloseWindow(NetUserId userId)
    {
        if (_windows.TryGetValue(userId, out var window))
            window.Close();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextUpdate)
            return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);

        foreach (var userId in _observers)
        {
            if (_players.TryGetSessionById(userId, out var session))
                SendState(session);
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        foreach (var window in _windows.Values.ToArray())
            window.Close();
        foreach (var userId in _lastStates.Keys)
        {
            if (_players.TryGetSessionById(userId, out var session))
                RaiseNetworkEvent(new NewLifeStateEvent(default), session.Channel);
        }
        _records.Clear();
        _lastStates.Clear();
        _observers.Clear();
        _nextUpdate = TimeSpan.Zero;
    }
}
