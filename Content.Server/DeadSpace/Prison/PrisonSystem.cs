using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.DeadSpace.Languages;
using Content.Server.DeadSpace.Prison.Components;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Server.Roles;
using Content.Server.Station.Systems;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.Languages.Components;
using Content.Shared.DeadSpace.Prison;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Preferences;
using Content.Shared.Projectiles;
using Content.Shared.Roles;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.Player;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Prison;

public sealed partial class PrisonSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private ITaskManager _taskManager = default!;
    [Dependency] private IServerPreferencesManager _preferences = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private RoleSystem _role = default!;
    [Dependency] private StationSpawningSystem _spawning = default!;
    [Dependency] private LanguageSystem _language = default!;

    private readonly HashSet<NetUserId> _prisonUsers = [];
    private readonly Dictionary<NetUserId, ICommonSession> _prisonSessions = new();
    private readonly Dictionary<NetUserId, TimeSpan> _pendingPrisonConnections = new();
    private readonly Dictionary<EntityUid, Dictionary<EntityUid, FixedPoint2>> _prisonDamageByTarget = new();
    private readonly Dictionary<EntityUid, Dictionary<EntityUid, FixedPoint2>> _prisonFaunaDamageByTarget = new();
    private readonly HashSet<NetUserId> _crossFactionRewardedVictims = [];
    private readonly Dictionary<NetUserId, PendingFaunaReward> _pendingFaunaRewards = new();
    private readonly HashSet<NetUserId> _faunaRewardInProgress = [];
    private readonly object _faunaRewardLock = new();
    private readonly SemaphoreSlim _murderPenaltySemaphore = new(1, 1);
    private readonly Dictionary<NetUserId, ProtoId<PrisonFactionPrototype>> _prisonFactions = new();
    private readonly Dictionary<NetUserId, PrisonFactionEui> _factionEuis = new();
    private readonly Dictionary<NetUserId, TimeSpan> _factionSelectionDeadlines = new();
    private readonly Dictionary<NetUserId, EntityUid> _factionSelectionLocks = new();
    private readonly Dictionary<NetUserId, TimeSpan> _pendingSentenceAcceleration = new();
    private static readonly ProtoId<StartingGearPrototype> PrisonerGear = "PrisonerGear";
    private static readonly TimeSpan PendingPrisonConnectionLifetime = TimeSpan.FromMinutes(2);
    private const int SourceParentSearchDepth = 6;
    private bool _enabled;
    private int _murderPenaltyMinutes;
    private int _crossFactionKillRewardMinutes;
    private int _factionSelectionSeconds;
    private float _sentenceTimeMultiplier;
    private bool _sentenceAccelerationRunning;
    private bool _suppressFactionEuiReopen;

    private readonly TimeSpan _safeguardUpdateRate = TimeSpan.FromSeconds(10);
    private TimeSpan _nextSafeguardUpdate;

    private readonly TimeSpan _activeBanRefreshRate = TimeSpan.FromMinutes(1);
    private TimeSpan _nextActiveBanRefresh;
    private bool _activeBanRefreshRunning;

    private readonly TimeSpan _sentenceAccelerationFlushRate = TimeSpan.FromMinutes(1);
    private TimeSpan _nextSentenceAccelerationFlush;

    private readonly TimeSpan _factionSelectionRefreshRate = TimeSpan.FromSeconds(1);
    private TimeSpan _nextFactionSelectionRefresh;

    public bool Enabled => _enabled;
    public bool Ready => _enabled && TryGetSpawnCoordinates(out _);

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCCCVars.PrisonEnabled, OnPrisonEnabledChanged, true);
        Subs.CVar(_cfg, CCCCVars.PrisonMurderPenaltyMinutes, value => _murderPenaltyMinutes = value, true);
        Subs.CVar(_cfg,
            CCCCVars.PrisonSentenceTimeMultiplier,
            value => _sentenceTimeMultiplier = Math.Clamp(value, 1f, 2f),
            true);
        Subs.CVar(_cfg,
            CCCCVars.PrisonCrossFactionKillRewardMinutes,
            value => _crossFactionKillRewardMinutes = Math.Max(0, value),
            true);
        Subs.CVar(_cfg,
            CCCCVars.PrisonFactionSelectionSeconds,
            value => _factionSelectionSeconds = Math.Clamp(value, 5, 120),
            true);

        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnPlayerBeforeSpawn);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<MindRoleAddAttemptEvent>(OnMindRoleAddAttempt);
        SubscribeLocalEvent<InGameOocMessageAttemptEvent>(OnInGameOocMessageAttempt);
        SubscribeLocalEvent<AttackAttemptEvent>(OnPrisonerAttackAttempt);
        SubscribeLocalEvent<AttemptShootEvent>(OnPrisonerAttemptShoot);
        SubscribeLocalEvent<DamageableComponent, DamageModifyEvent>(OnPrisonerDamageModify);
        SubscribeLocalEvent<PrisonFactionMemberComponent, ExaminedEvent>(OnFactionExamined);
        SubscribeLocalEvent<PrisonBoundComponent, DamageChangedEvent>(OnPrisonDamageChanged, before: [typeof(MobThresholdSystem)]);
        SubscribeLocalEvent<PrisonSpawnedFaunaComponent, DamageChangedEvent>(OnPrisonFaunaDamageChanged, before: [typeof(MobThresholdSystem)]);
        SubscribeLocalEvent<MobStateChangedEvent>(OnPrisonMobStateChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        _player.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPrisonEnabledChanged(bool enabled)
    {
        _enabled = enabled;
        if (!enabled && _prisonUsers.Count > 0)
            RefreshPrisonBanState();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        foreach (var (userId, reduction) in _pendingSentenceAcceleration)
        {
            if (reduction > TimeSpan.Zero && _player.TryGetSessionById(userId, out var session))
                ApplySentenceAcceleration(userId, CreateBanRefreshCheck(session), reduction);
        }

        _prisonDamageByTarget.Clear();
        _prisonFaunaDamageByTarget.Clear();
        _crossFactionRewardedVictims.Clear();
        _suppressFactionEuiReopen = true;
        try
        {
            foreach (var eui in _factionEuis.Values.ToArray())
            {
                if (!eui.IsShutDown)
                    eui.Close();
            }
        }
        finally
        {
            _suppressFactionEuiReopen = false;
        }
        _prisonFactions.Clear();
        _factionEuis.Clear();
        _factionSelectionDeadlines.Clear();
        foreach (var userId in _factionSelectionLocks.Keys.ToArray())
            UnlockFactionSelection(userId);
        _pendingSentenceAcceleration.Clear();
        _sentenceAccelerationRunning = false;
        lock (_faunaRewardLock)
        {
            _pendingFaunaRewards.Clear();
            _faunaRewardInProgress.Clear();
        }
    }

    private void OnPrisonerAttackAttempt(AttackAttemptEvent args)
    {
        if (args.Cancelled || !IsEntityPrisoner(args.Uid))
        {
            return;
        }

        if (!TryComp(args.Uid, out TransformComponent? xform) || !IsPrisonMap(xform.MapID))
        {
            args.Cancel();
            return;
        }

        if (args.Target is { } target &&
            TryGetMind(target, out var targetMindId, out var targetMind) &&
            !IsMindPrisoner(targetMindId, targetMind))
        {
            args.Cancel();
        }
    }

    private void OnPrisonerAttemptShoot(ref AttemptShootEvent args)
    {
        if (!args.Cancelled && IsEntityPrisoner(args.User) &&
            (!TryComp(args.User, out TransformComponent? xform) || !IsPrisonMap(xform.MapID)))
        {
            args.Cancelled = true;
        }
    }

    private void OnPrisonerDamageModify(EntityUid target, DamageableComponent component, DamageModifyEvent args)
    {
        if (args.Damage.Empty ||
            !TryGetDamageSourceMind(args.Origin, out var sourceMindId, out var sourceMind) ||
            !IsMindPrisoner(sourceMindId, sourceMind) ||
            !TryGetMind(target, out var targetMindId, out var targetMind) ||
            IsMindPrisoner(targetMindId, targetMind))
        {
            return;
        }

        args.Damage = new DamageSpecifier();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        AccumulateSentenceAcceleration(frameTime);

        if (_timing.CurTime >= _nextFactionSelectionRefresh)
        {
            _nextFactionSelectionRefresh = _timing.CurTime + _factionSelectionRefreshRate;
            UpdateFactionSelections();
        }

        if (!_sentenceAccelerationRunning &&
            _pendingSentenceAcceleration.Count > 0 &&
            _timing.CurTime >= _nextSentenceAccelerationFlush)
        {
            _nextSentenceAccelerationFlush = _timing.CurTime + _sentenceAccelerationFlushRate;
            FlushSentenceAcceleration();
        }

        if (_timing.CurTime >= _nextSafeguardUpdate)
        {
            _nextSafeguardUpdate = _timing.CurTime + _safeguardUpdateRate;
            SafeguardPrisoners();
        }

        if (_prisonUsers.Count == 0 ||
            _activeBanRefreshRunning ||
            _timing.CurTime < _nextActiveBanRefresh)
        {
            return;
        }

        _nextActiveBanRefresh = _timing.CurTime + _activeBanRefreshRate;
        RefreshActivePrisonBans();
    }

    public bool RegisterPrisonerConnection(NetUserId userId, IReadOnlyCollection<BanDef> bans)
    {
        if (IsUserCurrentlyAntagonist(userId))
            return false;

        if (!CanUsePrisonForBans(bans))
            return false;

        _prisonUsers.Add(userId);
        _prisonSessions.Remove(userId);
        _pendingPrisonConnections[userId] = _timing.RealTime + PendingPrisonConnectionLifetime;
        return true;
    }

    public bool CanUsePrisonForBans(IReadOnlyCollection<BanDef> bans)
    {
        if (!_enabled || !Ready || bans.Count == 0)
            return false;

        return GetLatestActiveServerBan(bans)?.SendToPrison == true;
    }

    public bool TrySendToPrison(ICommonSession session, BanDef ban)
    {
        if (IsSessionAntagonist(session))
            return false;

        if (!_enabled || !Ready || !IsPrisonServerBan(ban))
            return false;

        BindPrisonSession(session);
        var registered = new PrisonerRegisteredEvent(session);
        RaiseLocalEvent(ref registered);

        // A lobby session has no round body to move. Keep the connection alive and
        // let PlayerBeforeSpawn create the prisoner body when they actually join.
        if (!_gameTicker.UserHasJoinedGame(session))
        {
            SendPrisonMessage(session, ban);
            return true;
        }

        if (!_prisonFactions.TryGetValue(session.UserId, out var faction))
        {
            if (session.AttachedEntity is { } waitingEntity &&
                Exists(waitingEntity) &&
                !HasComp<GhostComponent>(waitingEntity) &&
                TryGetSpawnCoordinates(out var waitingCoordinates))
            {
                SendEntityToPrison(waitingEntity, waitingCoordinates, session.UserId);
            }

            BeginFactionSelection(session);
            SendPrisonMessage(session, ban);
            return true;
        }

        if (!TryGetSpawnCoordinates(faction, out var coordinates))
            return false;

        if (session.AttachedEntity is { } entity && Exists(entity) && !HasComp<GhostComponent>(entity))
        {
            if (!HasComp<PrisonBoundComponent>(entity) ||
                !IsPrisonMap(Transform(entity).MapID) ||
                !TryComp<PrisonFactionMemberComponent>(entity, out var member) ||
                member.Faction != faction)
            {
                SendEntityToPrison(entity, coordinates, session.UserId);
            }
        }
        else
        {
            if (!TryGetHumanoidProfile(session, out var profile))
                return false;

            SpawnPrisonMob(session, profile, coordinates);
        }
        SendPrisonMessage(session, ban);
        return true;
    }

    public bool IsUserPrisoner(NetUserId userId)
    {
        if (_prisonUsers.Contains(userId))
            return true;

        return _player.TryGetSessionById(userId, out var session)
               && session.AttachedEntity is { } entity
               && HasComp<PrisonBoundComponent>(entity);
    }

    public PrisonFactionEuiState GetFactionEuiState(ICommonSession? session = null)
    {
        var options = _prototype.EnumeratePrototypes<PrisonFactionPrototype>()
            .Where(faction => HasFactionSpawnPoint(faction.ID))
            .OrderBy(faction => faction.Order)
            .ThenBy(faction => faction.ID)
            .Select(faction => new PrisonFactionOption(
                faction.ID,
                faction.Name,
                faction.Feature,
                faction.Color))
            .ToList();

        var secondsRemaining = 0;
        if (session != null && _factionSelectionDeadlines.TryGetValue(session.UserId, out var deadline))
        {
            secondsRemaining = Math.Max(
                0,
                (int) Math.Ceiling((deadline - _timing.CurTime).TotalSeconds));
        }

        return new PrisonFactionEuiState(options, secondsRemaining);
    }

    internal bool TrySelectFaction(ICommonSession session, string factionId)
    {
        if (!_enabled ||
            !IsUserPrisoner(session.UserId) ||
            !_prototype.TryIndex<PrisonFactionPrototype>(factionId, out var faction) ||
            !TryGetSpawnCoordinates(faction.ID, out var coordinates))
        {
            return false;
        }

        var alreadySelected = _prisonFactions.TryGetValue(session.UserId, out var selected);
        if ((!alreadySelected && !_factionSelectionDeadlines.ContainsKey(session.UserId)) ||
            (alreadySelected && selected != faction.ID))
        {
            return false;
        }

        var joined = _gameTicker.UserHasJoinedGame(session);
        var attached = session.AttachedEntity;
        var needsBody = joined &&
            (attached is not { } attachedEntity ||
             !Exists(attachedEntity) ||
             HasComp<GhostComponent>(attachedEntity));
        HumanoidCharacterProfile? profile = null;
        if (needsBody && !TryGetHumanoidProfile(session, out profile))
            return false;

        if (!alreadySelected)
            _prisonFactions[session.UserId] = faction.ID;

        UnlockFactionSelection(session.UserId);
        _factionSelectionDeadlines.Remove(session.UserId);

        if (joined)
        {
            if (needsBody)
            {
                SpawnPrisonMob(session, profile!, coordinates);
            }
            else if (attached is { } entity &&
                     (!HasComp<PrisonBoundComponent>(entity) ||
                      !IsPrisonMap(Transform(entity).MapID) ||
                      !TryComp<PrisonFactionMemberComponent>(entity, out var member) ||
                      member.Faction != faction.ID))
            {
                SendEntityToPrison(entity, coordinates, session.UserId);
            }
        }

        if (alreadySelected)
            return true;

        _chat.DispatchServerMessage(
            session,
            Loc.GetString("prison-faction-selected-message", ("faction", Loc.GetString(faction.Name))));

        foreach (var eui in _factionEuis.Values)
        {
            if (!eui.IsShutDown)
                eui.StateDirty();
        }

        return true;
    }

    internal void OnFactionEuiClosed(ICommonSession session, PrisonFactionEui eui)
    {
        if (_factionEuis.GetValueOrDefault(session.UserId) == eui)
            _factionEuis.Remove(session.UserId);

        if (_suppressFactionEuiReopen ||
            session.Status == SessionStatus.Disconnected ||
            _prisonFactions.ContainsKey(session.UserId) ||
            !_factionSelectionDeadlines.ContainsKey(session.UserId) ||
            !IsUserPrisoner(session.UserId))
        {
            return;
        }

        _taskManager.RunOnMainThread(() =>
        {
            if (session.Status != SessionStatus.Disconnected &&
                IsUserPrisoner(session.UserId) &&
                _factionSelectionDeadlines.ContainsKey(session.UserId) &&
                !_prisonFactions.ContainsKey(session.UserId))
            {
                OpenFactionSelection(session);
            }
        });
    }

    private void OpenFactionSelection(ICommonSession session)
    {
        if (!_enabled ||
            !_factionSelectionDeadlines.ContainsKey(session.UserId) ||
            _prisonFactions.ContainsKey(session.UserId) ||
            _factionEuis.TryGetValue(session.UserId, out var current) && !current.IsShutDown)
        {
            return;
        }

        var eui = new PrisonFactionEui(this, session);
        _factionEuis[session.UserId] = eui;
        _eui.OpenEui(eui, session);
    }

    private void BeginFactionSelection(ICommonSession session)
    {
        if (_prisonFactions.ContainsKey(session.UserId))
        {
            UnlockFactionSelection(session.UserId);
            return;
        }

        if (!_enabled || !Ready)
            return;

        LockFactionSelection(session);
        _factionSelectionDeadlines.TryAdd(
            session.UserId,
            _timing.CurTime + TimeSpan.FromSeconds(_factionSelectionSeconds));
        OpenFactionSelection(session);
    }

    private void LockFactionSelection(ICommonSession session)
    {
        if (session.AttachedEntity is not { } entity ||
            !Exists(entity) ||
            HasComp<GhostComponent>(entity))
        {
            UnlockFactionSelection(session.UserId);
            return;
        }

        if (_factionSelectionLocks.TryGetValue(session.UserId, out var previous) && previous != entity)
        {
            if (Exists(previous))
                RemComp<PrisonFactionSelectionLockedComponent>(previous);
        }

        EnsureComp<PrisonFactionSelectionLockedComponent>(entity);
        if (TryComp<PhysicsComponent>(entity, out var physics))
            _physics.ResetDynamics(entity, physics);
        _factionSelectionLocks[session.UserId] = entity;
    }

    private void UnlockFactionSelection(NetUserId userId)
    {
        if (!_factionSelectionLocks.Remove(userId, out var entity) || !Exists(entity))
            return;

        RemComp<PrisonFactionSelectionLockedComponent>(entity);
    }

    private void UpdateFactionSelections()
    {
        foreach (var (userId, deadline) in _factionSelectionDeadlines.ToArray())
        {
            if (_prisonFactions.ContainsKey(userId))
            {
                _factionSelectionDeadlines.Remove(userId);
                UnlockFactionSelection(userId);
                continue;
            }

            if (!_player.TryGetSessionById(userId, out var session) ||
                session.Status == SessionStatus.Disconnected)
            {
                _factionSelectionDeadlines.Remove(userId);
                UnlockFactionSelection(userId);
                continue;
            }

            if (_timing.CurTime < deadline)
            {
                if (_factionEuis.TryGetValue(userId, out var activeEui) && !activeEui.IsShutDown)
                    activeEui.StateDirty();
                continue;
            }

            if (!TrySelectAutomaticFaction(session))
            {
                _factionSelectionDeadlines.Remove(userId);
                UnlockFactionSelection(userId);
                session.Channel.Disconnect(Loc.GetString("prison-unavailable-message"));
                continue;
            }

            if (_factionEuis.TryGetValue(userId, out var eui) && !eui.IsShutDown)
                eui.Close();
        }
    }

    private bool TrySelectAutomaticFaction(ICommonSession session)
    {
        var counts = new Dictionary<ProtoId<PrisonFactionPrototype>, int>();
        foreach (var (userId, faction) in _prisonFactions)
        {
            if (_prisonUsers.Contains(userId) && _player.TryGetSessionById(userId, out _))
                counts[faction] = counts.GetValueOrDefault(faction) + 1;
        }

        var available = _prototype.EnumeratePrototypes<PrisonFactionPrototype>()
            .Where(faction => HasFactionSpawnPoint(faction.ID))
            .ToList();
        if (available.Count == 0)
            return false;

        var minimum = available.Min(faction => counts.GetValueOrDefault(faction.ID));
        var candidates = available
            .Where(faction => counts.GetValueOrDefault(faction.ID) == minimum)
            .ToList();
        return TrySelectFaction(session, _random.Pick(candidates).ID);
    }

    private void SetPrisonFaction(EntityUid entity, ProtoId<PrisonFactionPrototype> faction)
    {
        EnsureComp<PrisonFactionMemberComponent>(entity).Faction = faction;
    }

    private void OnFactionExamined(Entity<PrisonFactionMemberComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !_prototype.TryIndex(ent.Comp.Faction, out var faction))
            return;

        args.PushMarkup(Loc.GetString("prison-faction-examine", ("faction", Loc.GetString(faction.Name))));
    }

    public async Task<PrisonSentence?> GetReducibleSentence(NetUserId userId)
    {
        if (!IsUserPrisoner(userId) || !_player.TryGetSessionById(userId, out var session))
            return null;

        var check = CreateBanRefreshCheck(session);
        var bans = await _db.GetBansAsync(
            check.Address,
            check.UserId,
            check.HwId,
            check.ModernHwIds,
            includeUnbanned: false);
        var latest = GetLatestActiveServerBan(bans);

        if (latest?.Id is not { } banId ||
            !IsPrisonServerBan(latest) ||
            latest.ExpirationTime == null)
        {
            return null;
        }

        return new PrisonSentence(banId);
    }

    public async Task<TimeSpan> TryReduceSentence(
        NetUserId userId,
        int expectedBanId,
        TimeSpan reduction)
    {
        if (reduction <= TimeSpan.Zero)
            return TimeSpan.Zero;

        // Reload before every atomic update so simultaneous fauna and ore rewards cannot overwrite each other.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            IPAddress? address = null;
            ImmutableArray<byte>? hwId = null;
            ImmutableArray<ImmutableArray<byte>>? modernHwIds = null;
            if (_player.TryGetSessionById(userId, out var session))
            {
                var check = CreateBanRefreshCheck(session);
                address = check.Address;
                hwId = check.HwId;
                modernHwIds = check.ModernHwIds;
            }

            var bans = await _db.GetBansAsync(
                address,
                userId,
                hwId,
                modernHwIds,
                includeUnbanned: false);
            var latest = GetLatestActiveServerBan(bans);
            if (latest?.Id != expectedBanId ||
                !IsPrisonServerBan(latest) ||
                latest.ExpirationTime is not { } expiration)
            {
                return TimeSpan.Zero;
            }

            var now = DateTimeOffset.UtcNow;
            var updated = expiration - reduction;
            if (updated < now)
                updated = now;

            if (!await _db.TrySetActivePrisonBanExpiration(expectedBanId, expiration, updated))
                continue;

            return expiration - updated;
        }

        return TimeSpan.Zero;
    }

    public void RefreshPrisonBanState()
    {
        _nextActiveBanRefresh = TimeSpan.Zero;

        if (_activeBanRefreshRunning)
            return;

        RefreshActivePrisonBans();
    }

    public bool IsEntityPrisoner(EntityUid entity)
    {
        if (HasComp<PrisonBoundComponent>(entity))
            return true;

        return _mind.TryGetMind(entity, out var mindId, out var mind)
               && IsMindPrisoner(mindId, mind);
    }

    public bool IsMindPrisoner(EntityUid mindId, MindComponent? mind = null)
    {
        return Resolve(mindId, ref mind, false)
               && mind.UserId is { } userId
               && IsUserPrisoner(userId);
    }

    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        if (!IsUserPrisoner(ev.PlayerSession.UserId))
            return;

        _chat.DispatchServerMessage(
            ev.PlayerSession,
            Loc.GetString(
                "prison-chat-join-message",
                ("percent", GetSentenceAccelerationPercent()),
                ("minutes", _crossFactionKillRewardMinutes),
                ("seconds", _factionSelectionSeconds)));
    }

    private void OnPlayerBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        if (!IsUserPrisoner(ev.Player.UserId))
            return;

        BindPrisonSession(ev.Player);
        ev.Handled = true;

        if (!_enabled || !TryGetSpawnCoordinates(out _))
        {
            ev.Player.Channel.Disconnect(Loc.GetString("prison-unavailable-message"));
            return;
        }

        if (!_prisonFactions.TryGetValue(ev.Player.UserId, out var faction))
        {
            BeginFactionSelection(ev.Player);
            return;
        }

        if (!TryGetSpawnCoordinates(faction, out var coordinates))
        {
            ev.Player.Channel.Disconnect(Loc.GetString("prison-unavailable-message"));
            return;
        }

        SpawnPrisonMob(ev.Player, ev.Profile, coordinates);
        _chat.DispatchServerMessage(
            ev.Player,
            Loc.GetString(
                "prison-arrival-message",
                ("percent", GetSentenceAccelerationPercent()),
                ("minutes", _crossFactionKillRewardMinutes)));
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (!IsUserPrisoner(ev.Player.UserId) && !HasComp<PrisonBoundComponent>(ev.Entity))
            return;

        BindPrisonSession(ev.Player);

        if (!_enabled || !TryGetSpawnCoordinates(out _))
        {
            ev.Player.Channel.Disconnect(Loc.GetString("prison-unavailable-message"));
            return;
        }

        if (HasComp<GhostComponent>(ev.Entity))
        {
            UnlockFactionSelection(ev.Player.UserId);
            RemovePrisonBound(ev.Entity);
            return;
        }

        if (!_prisonFactions.TryGetValue(ev.Player.UserId, out var faction))
        {
            if (TryGetSpawnCoordinates(out var waitingCoordinates))
                SendEntityToPrison(ev.Entity, waitingCoordinates, ev.Player.UserId);

            BeginFactionSelection(ev.Player);
            return;
        }

        if (!TryGetSpawnCoordinates(faction, out var coordinates))
        {
            ev.Player.Channel.Disconnect(Loc.GetString("prison-unavailable-message"));
            return;
        }

        var xform = Transform(ev.Entity);
        if (IsPrisonMap(xform.MapID))
        {
            var hadPrisonEquipment = HasComp<PrisonBoundComponent>(ev.Entity);
            var prisonBound = EnsureComp<PrisonBoundComponent>(ev.Entity);
            ApplyPrisonLanguage(ev.Entity, prisonBound);
            SetPrisonFaction(ev.Entity, faction);
            if (!hadPrisonEquipment)
            {
                DropInventory(ev.Entity);
                EquipPrisoner(ev.Entity, ev.Player.UserId);
            }
            return;
        }

        SendEntityToPrison(ev.Entity, coordinates, ev.Player.UserId);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus != SessionStatus.Disconnected)
        {
            if (_prisonUsers.Contains(e.Session.UserId))
                BindPrisonSession(e.Session);
            return;
        }

        // Authorization for a replacement connection happens before its session is created.
        // Do not let the old session's late disconnect erase the newly registered prison state.
        if (_pendingPrisonConnections.ContainsKey(e.Session.UserId))
            return;

        if (_prisonSessions.TryGetValue(e.Session.UserId, out var trackedSession) &&
            !ReferenceEquals(trackedSession, e.Session))
        {
            return;
        }

        UnlockFactionSelection(e.Session.UserId);
        if (_pendingSentenceAcceleration.Remove(e.Session.UserId, out var reduction) &&
            reduction > TimeSpan.Zero)
        {
            ApplySentenceAcceleration(
                e.Session.UserId,
                CreateBanRefreshCheck(e.Session),
                reduction);
        }

        RemoveDisconnectedPrisonState(e.Session.UserId);
    }

    private void BindPrisonSession(ICommonSession session)
    {
        _prisonUsers.Add(session.UserId);
        _pendingPrisonConnections.Remove(session.UserId);
        _prisonSessions[session.UserId] = session;
    }

    private void RemoveDisconnectedPrisonState(NetUserId userId)
    {
        _prisonUsers.Remove(userId);
        _prisonSessions.Remove(userId);
        _pendingPrisonConnections.Remove(userId);
        _factionSelectionDeadlines.Remove(userId);
        if (_factionEuis.Remove(userId, out var eui) && !eui.IsShutDown)
            eui.Close();
    }

    private void OnMindRoleAddAttempt(MindRoleAddAttemptEvent args)
    {
        if (!args.Antagonist)
            return;

        var userId = args.Mind.UserId;
        var prisoner = userId is { } prisonerId && IsUserPrisoner(prisonerId);
        var onPrisonMap = args.Mind.OwnedEntity is { } body &&
                          TryComp(body, out TransformComponent? xform) &&
                          IsPrisonMap(xform.MapID);
        if (!prisoner && !onPrisonMap)
            return;

        args.Cancel();

        if (userId is { } blockedUserId && _player.TryGetSessionById(blockedUserId, out var session))
            _chat.DispatchServerMessage(session, Loc.GetString("prison-antag-role-blocked"));
    }

    private void OnInGameOocMessageAttempt(ref InGameOocMessageAttemptEvent args)
    {
        if (args.Type != InGameOOCChatType.Dead || !IsUserPrisoner(args.Session.UserId))
            return;

        args.Cancelled = true;
        _chat.DispatchServerMessage(args.Session, Loc.GetString("prison-ghost-chat-blocked"));
    }

    private void OnPrisonDamageChanged(EntityUid uid, PrisonBoundComponent component, DamageChangedEvent args)
    {
        if (args.DamageDelta == null)
        {
            if (args.Damageable.TotalDamage == FixedPoint2.Zero)
                _prisonDamageByTarget.Remove(uid);

            return;
        }

        if (!TryGetPrisonerMind(uid, out var targetMindId, out _))
        {
            _prisonDamageByTarget.Remove(uid);
            return;
        }

        var delta = args.DamageDelta.GetTotal();
        if (!args.DamageIncreased)
        {
            if (args.Damageable.TotalDamage == FixedPoint2.Zero)
            {
                _prisonDamageByTarget.Remove(uid);
                return;
            }

            ReducePrisonDamageContributors(uid, -delta);
            return;
        }

        if (delta <= FixedPoint2.Zero ||
            !TryGetDamageSourceMind(args.Origin, out var sourceMindId, out var sourceMind) ||
            sourceMindId == targetMindId ||
            !IsMindPrisoner(sourceMindId, sourceMind))
        {
            return;
        }

        if (!_prisonDamageByTarget.TryGetValue(uid, out var sourceDamage))
        {
            sourceDamage = new Dictionary<EntityUid, FixedPoint2>();
            _prisonDamageByTarget[uid] = sourceDamage;
        }

        sourceDamage[sourceMindId] = sourceDamage.GetValueOrDefault(sourceMindId) + delta;
    }

    private void OnPrisonFaunaDamageChanged(
        EntityUid uid,
        PrisonSpawnedFaunaComponent component,
        DamageChangedEvent args)
    {
        if (args.DamageDelta == null)
        {
            if (args.Damageable.TotalDamage == FixedPoint2.Zero)
                _prisonFaunaDamageByTarget.Remove(uid);

            return;
        }

        var delta = args.DamageDelta.GetTotal();
        if (!args.DamageIncreased)
        {
            if (args.Damageable.TotalDamage == FixedPoint2.Zero)
                _prisonFaunaDamageByTarget.Remove(uid);
            else
                ReduceDamageContributors(_prisonFaunaDamageByTarget, uid, -delta);

            return;
        }

        if (delta <= FixedPoint2.Zero ||
            !TryGetDamageSourceMind(args.Origin, out var sourceMindId, out var sourceMind) ||
            !IsMindPrisoner(sourceMindId, sourceMind))
        {
            return;
        }

        if (!_prisonFaunaDamageByTarget.TryGetValue(uid, out var sourceDamage))
        {
            sourceDamage = new Dictionary<EntityUid, FixedPoint2>();
            _prisonFaunaDamageByTarget[uid] = sourceDamage;
        }

        sourceDamage[sourceMindId] = sourceDamage.GetValueOrDefault(sourceMindId) + delta;
    }

    private void OnPrisonMobStateChanged(MobStateChangedEvent args)
    {
        if (!_enabled ||
            args.NewMobState != MobState.Dead ||
            args.OldMobState >= args.NewMobState)
        {
            return;
        }

        if (TryComp<PrisonSpawnedFaunaComponent>(args.Target, out var fauna))
        {
            OnPrisonFaunaKilled((args.Target, fauna), ref args);
            return;
        }

        var target = args.Target;
        if (!TryGetPrisonerMind(target, out var targetMindId, out _))
        {
            _prisonDamageByTarget.Remove(target);
            return;
        }

        if (TryGetDamageSourceMind(args.Origin, out var sourceMindId, out var sourceMind) &&
            sourceMindId != targetMindId &&
            IsMindPrisoner(sourceMindId, sourceMind))
        {
            ApplyPrisonerKillOutcome(target, sourceMind);
            _prisonDamageByTarget.Remove(target);
            return;
        }

        if (TryGetLargestPrisonDamageContributor(target, targetMindId, out _, out var contributorMind))
            ApplyPrisonerKillOutcome(target, contributorMind);

        _prisonDamageByTarget.Remove(target);
    }

    private void ApplyPrisonerKillOutcome(EntityUid target, MindComponent killerMind)
    {
        if (IsCrossFactionKill(target, killerMind))
        {
            var rewardEligible = _crossFactionKillRewardMinutes > 0 &&
                                 TryGetPrisonerMind(target, out _, out var targetMind) &&
                                 targetMind.UserId is { } victimUserId &&
                                 _crossFactionRewardedVictims.Add(victimUserId);
            ApplyCrossFactionKillOutcome(killerMind, rewardEligible);

            return;
        }

        if (_murderPenaltyMinutes > 0)
            AddPrisonMurderPenalty(killerMind);
    }

    private bool IsCrossFactionKill(EntityUid target, MindComponent killerMind)
    {
        return killerMind.OwnedEntity is { } killer &&
               TryComp<PrisonFactionMemberComponent>(killer, out var killerFaction) &&
               TryComp<PrisonFactionMemberComponent>(target, out var targetFaction) &&
               killerFaction.Faction != targetFaction.Faction;
    }

    private async void ApplyCrossFactionKillOutcome(MindComponent killerMind, bool rewardEligible)
    {
        if (killerMind.UserId is not { } userId ||
            !_player.TryGetSessionById(userId, out var session))
            return;

        try
        {
            var check = CreateBanRefreshCheck(session);
            var bans = await _db.GetBansAsync(
                check.Address,
                check.UserId,
                check.HwId,
                check.ModernHwIds,
                includeUnbanned: false);
            var latest = GetLatestActiveServerBan(bans);
            if (latest?.Id is not { } banId || !IsPrisonServerBan(latest))
                return;

            if (latest.ExpirationTime == null)
            {
                await _db.SetBanPrisonAccess(banId, false);
                _taskManager.RunOnMainThread(() => RevokePermanentPrisonAccess(userId));
                return;
            }

            if (!rewardEligible)
                return;

            var applied = await TryReduceSentence(
                userId,
                banId,
                TimeSpan.FromMinutes(_crossFactionKillRewardMinutes));
            if (applied <= TimeSpan.Zero)
                return;

            _taskManager.RunOnMainThread(() =>
            {
                RefreshPrisonBanState();
                if (_player.TryGetSessionById(userId, out var session))
                {
                    _chat.DispatchServerMessage(
                        session,
                        Loc.GetString(
                            "prison-cross-faction-kill-reward-message",
                            ("minutes", applied.TotalMinutes.ToString("N1"))));
                }
            });
        }
        catch (Exception e)
        {
            Log.Error($"Failed to apply prison cross-faction kill outcome for {userId}: {e}");
        }
    }

    private void OnPrisonFaunaKilled(Entity<PrisonSpawnedFaunaComponent> ent, ref MobStateChangedEvent args)
    {
        if (ent.Comp.SentenceReductionMinutes <= 0)
            return;

        MindComponent? sourceMind = null;
        if (TryGetDamageSourceMind(args.Origin, out var sourceMindId, out sourceMind))
        {
            if (!IsMindPrisoner(sourceMindId, sourceMind))
            {
                _prisonFaunaDamageByTarget.Remove(ent.Owner);
                return;
            }
        }
        else
        {
            if (!TryGetLargestPrisonFaunaDamageContributor(ent.Owner, out sourceMindId, out sourceMind))
            {
                _prisonFaunaDamageByTarget.Remove(ent.Owner);
                return;
            }
        }

        _prisonFaunaDamageByTarget.Remove(ent.Owner);
        if (sourceMind.UserId is not { } userId || !_player.TryGetSessionById(userId, out var session))
            return;

        var check = CreateBanRefreshCheck(session);
        var startRewardTask = false;
        lock (_faunaRewardLock)
        {
            var pendingMinutes = _pendingFaunaRewards.TryGetValue(userId, out var pending)
                ? pending.Minutes
                : 0;
            _pendingFaunaRewards[userId] = new PendingFaunaReward(
                check,
                pendingMinutes + ent.Comp.SentenceReductionMinutes);
            startRewardTask = _faunaRewardInProgress.Add(userId);
        }

        if (startRewardTask)
            ApplyPendingFaunaRewards(userId);
    }

    private async void ApplyPendingFaunaRewards(NetUserId userId)
    {
        while (true)
        {
            PendingFaunaReward pending;
            lock (_faunaRewardLock)
            {
                if (!_pendingFaunaRewards.Remove(userId, out pending))
                {
                    _faunaRewardInProgress.Remove(userId);
                    return;
                }
            }

            if (pending.Minutes <= 0)
                continue;

            try
            {
                var check = pending.Check;
                var bans = await _db.GetBansAsync(
                    check.Address,
                    check.UserId,
                    check.HwId,
                    check.ModernHwIds,
                    includeUnbanned: false);
                var latestBan = GetLatestActiveServerBan(bans);

                if (latestBan?.Id is not { } banId ||
                    !IsPrisonServerBan(latestBan) ||
                    latestBan.ExpirationTime == null)
                {
                    continue;
                }

                var applied = await TryReduceSentence(
                    userId,
                    banId,
                    TimeSpan.FromMinutes(pending.Minutes));
                if (applied <= TimeSpan.Zero)
                    continue;

                var appliedMinutes = applied.TotalMinutes.ToString("N1");

                _taskManager.RunOnMainThread(() =>
                {
                    _nextActiveBanRefresh = TimeSpan.Zero;
                    if (_player.TryGetSessionById(userId, out var currentSession))
                    {
                        _chat.DispatchServerMessage(
                            currentSession,
                            Loc.GetString("prison-fauna-reward-message", ("minutes", appliedMinutes)));
                    }

                    RefreshPrisonBanState();
                });
            }
            catch (Exception e)
            {
                Log.Error($"Failed to apply prison fauna reward for {userId}: {e}");
            }
        }
    }

    private void SpawnPrisonMob(ICommonSession session, HumanoidCharacterProfile profile, EntityCoordinates coordinates)
    {
        if (_mind.TryGetMind(session.UserId, out _, out var existingMind) && !existingMind.IsVisitingEntity)
            _mind.WipeMind(session);

        var newMind = _mind.CreateMind(session.UserId, profile.Name);
        _mind.SetUserId(newMind, session.UserId);

        var mob = _spawning.SpawnPlayerMob(coordinates, null, profile, null);
        EnsureComp<PrisonBoundComponent>(mob);
        EquipPrisoner(mob, session.UserId);
        BindPrisonSession(session);
        _mind.TransferTo(newMind, mob);
    }

    private bool TryGetHumanoidProfile(ICommonSession session, [NotNullWhen(true)] out HumanoidCharacterProfile? profile)
    {
        if (_preferences.TryGetCachedPreferences(session.UserId, out var preferences) &&
            preferences.SelectedCharacter is HumanoidCharacterProfile humanoid)
        {
            profile = humanoid;
            return true;
        }

        profile = null;
        return false;
    }

    private void SendEntityToPrison(
        EntityUid entity,
        EntityCoordinates coordinates,
        NetUserId userId)
    {
        DropInventory(entity);

        _transform.SetCoordinates(entity, coordinates);
        _transform.AttachToGridOrMap(entity);

        EnsureComp<PrisonBoundComponent>(entity);
        EquipPrisoner(entity, userId);
    }

    private void EquipPrisoner(EntityUid entity, NetUserId userId)
    {
        var prisonBound = EnsureComp<PrisonBoundComponent>(entity);
        ApplyPrisonLanguage(entity, prisonBound);

        if (!_prisonFactions.TryGetValue(userId, out var factionId) ||
            !_prototype.TryIndex(factionId, out var faction))
        {
            return;
        }

        SetPrisonFaction(entity, factionId);
        _spawning.EquipStartingGear(entity, PrisonerGear, raiseEvent: false);
        _spawning.EquipStartingGear(entity, faction.Gear, raiseEvent: false);
    }

    private void ApplyPrisonLanguage(EntityUid entity, PrisonBoundComponent prisonBound)
    {
        if (!prisonBound.LanguageOverridden)
        {
            prisonBound.HadLanguageComponent = TryComp<LanguageComponent>(entity, out var previous);
            if (previous != null)
            {
                prisonBound.PreviousKnownLanguages.UnionWith(previous.KnownLanguages);
                prisonBound.PreviousCantSpeakLanguages.UnionWith(previous.CantSpeakLanguages);
                prisonBound.PreviousUnlockLanguages.UnionWith(previous.UnlockLanguagesAfterMakeSentient);
                prisonBound.PreviousSelectedLanguage = previous.SelectedLanguage;
            }

            prisonBound.LanguageOverridden = true;
        }

        _language.SetExclusiveLanguage(entity, LanguageSystem.PrisonLanguageId);
    }

    private void RestorePrisonLanguage(EntityUid entity, PrisonBoundComponent prisonBound)
    {
        if (!prisonBound.LanguageOverridden)
            return;

        if (!prisonBound.HadLanguageComponent)
        {
            RemComp<LanguageComponent>(entity);
            return;
        }

        var language = EnsureComp<LanguageComponent>(entity);
        language.KnownLanguages.Clear();
        language.KnownLanguages.UnionWith(prisonBound.PreviousKnownLanguages);
        language.CantSpeakLanguages.Clear();
        language.CantSpeakLanguages.UnionWith(prisonBound.PreviousCantSpeakLanguages);
        language.UnlockLanguagesAfterMakeSentient.Clear();
        language.UnlockLanguagesAfterMakeSentient.UnionWith(prisonBound.PreviousUnlockLanguages);
        language.SelectedLanguage = prisonBound.PreviousSelectedLanguage;
        Dirty(entity, language);
    }

    private void RemovePrisonBound(EntityUid entity)
    {
        if (TryComp<PrisonBoundComponent>(entity, out var prisonBound))
            RestorePrisonLanguage(entity, prisonBound);

        RemComp<PrisonBoundComponent>(entity);
    }

    private void DropInventory(EntityUid entity)
    {
        if (_inventory.TryGetContainerSlotEnumerator(entity, out var enumerator))
        {
            while (enumerator.NextItem(out var item, out var slot))
            {
                if (_inventory.TryUnequip(entity, entity, slot.Name, true, true))
                    _physics.ApplyAngularImpulse(item, ThrowingSystem.ThrowAngularImpulse);
            }
        }

        if (!TryComp(entity, out HandsComponent? hands))
            return;

        foreach (var hand in _hands.EnumerateHands((entity, hands)))
        {
            _hands.TryDrop((entity, hands), hand, checkActionBlocker: false, doDropInteraction: false);
        }
    }

    private void AccumulateSentenceAcceleration(float frameTime)
    {
        if (!_enabled || _sentenceTimeMultiplier <= 1f || frameTime <= 0f)
            return;

        var bonus = TimeSpan.FromSeconds(frameTime * (_sentenceTimeMultiplier - 1f));
        if (bonus <= TimeSpan.Zero)
            return;

        foreach (var userId in _prisonUsers)
        {
            if (!CanAccelerateSentence(userId))
                continue;

            _pendingSentenceAcceleration[userId] =
                _pendingSentenceAcceleration.GetValueOrDefault(userId) + bonus;
        }
    }

    private bool CanAccelerateSentence(NetUserId userId)
    {
        if (!_player.TryGetSessionById(userId, out var session) ||
            session.AttachedEntity is not { } entity ||
            !Exists(entity) ||
            !HasComp<PrisonBoundComponent>(entity) ||
            HasComp<GhostComponent>(entity) ||
            !TryComp(entity, out TransformComponent? xform) ||
            !IsPrisonMap(xform.MapID) ||
            !TryComp<MobStateComponent>(entity, out var mobState))
        {
            return false;
        }

        return mobState.CurrentState != MobState.Dead;
    }

    private async void FlushSentenceAcceleration()
    {
        _sentenceAccelerationRunning = true;
        var pending = _pendingSentenceAcceleration
            .Where(entry => entry.Value >= TimeSpan.FromSeconds(1))
            .ToArray();

        foreach (var (userId, _) in pending)
            _pendingSentenceAcceleration.Remove(userId);

        var appliedAny = false;
        foreach (var (userId, reduction) in pending)
        {
            try
            {
                var sentence = await GetReducibleSentence(userId);
                if (sentence == null)
                    continue;

                if (await TryReduceSentence(userId, sentence.Value.BanId, reduction) > TimeSpan.Zero)
                    appliedAny = true;
            }
            catch (Exception e)
            {
                Log.Error($"Failed to accelerate prison sentence for {userId}: {e}");
            }
        }

        _taskManager.RunOnMainThread(() =>
        {
            _sentenceAccelerationRunning = false;
            if (appliedAny)
                RefreshPrisonBanState();
        });
    }

    private async void ApplySentenceAcceleration(
        NetUserId userId,
        PrisonBanRefreshCheck check,
        TimeSpan reduction)
    {
        try
        {
            var bans = await _db.GetBansAsync(
                check.Address,
                check.UserId,
                check.HwId,
                check.ModernHwIds,
                includeUnbanned: false);
            var latest = GetLatestActiveServerBan(bans);
            if (latest?.Id is not { } banId ||
                !IsPrisonServerBan(latest) ||
                latest.ExpirationTime == null)
            {
                return;
            }

            if (await TryReduceSentence(userId, banId, reduction) <= TimeSpan.Zero)
                return;

            _taskManager.RunOnMainThread(RefreshPrisonBanState);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to accelerate prison sentence for {userId}: {e}");
        }
    }

    private void SafeguardPrisoners()
    {
        if (!_enabled || !TryGetSpawnCoordinates(out _))
            return;

        var query = EntityQueryEnumerator<PrisonBoundComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (HasComp<GhostComponent>(uid))
            {
                if (TryComp<PrisonBoundComponent>(uid, out var prisonBound))
                    RestorePrisonLanguage(uid, prisonBound);
                RemCompDeferred<PrisonBoundComponent>(uid);
                continue;
            }

            if (IsPrisonMap(xform.MapID))
            {
                if (TryComp<PrisonBoundComponent>(uid, out var prisonBound))
                    ApplyPrisonLanguage(uid, prisonBound);

                if (TryGetPrisonerMind(uid, out _, out var prisonerMind) &&
                    prisonerMind.UserId is { } prisonerId &&
                    !_prisonFactions.ContainsKey(prisonerId) &&
                    _player.TryGetSessionById(prisonerId, out var prisonerSession))
                {
                    BeginFactionSelection(prisonerSession);
                }
                continue;
            }

            if (!TryGetPrisonerMind(uid, out _, out var mind) || mind.UserId is not { } userId)
                continue;

            EntityCoordinates coordinates;
            var foundCoordinates = _prisonFactions.TryGetValue(userId, out var faction)
                ? TryGetSpawnCoordinates(faction, out coordinates)
                : TryGetSpawnCoordinates(out coordinates);
            if (foundCoordinates)
                SendEntityToPrison(uid, coordinates, userId);
        }
    }

    private async void RefreshActivePrisonBans()
    {
        _activeBanRefreshRunning = true;

        try
        {
            var checks = new List<PrisonBanRefreshRequest>();

            foreach (var userId in _prisonUsers.ToArray())
            {
                if (_pendingPrisonConnections.TryGetValue(userId, out var deadline) &&
                    _timing.RealTime < deadline)
                {
                    continue;
                }

                _pendingPrisonConnections.Remove(userId);
                if (!_player.TryGetSessionById(userId, out var session))
                {
                    RemoveDisconnectedPrisonState(userId);
                    continue;
                }

                BindPrisonSession(session);
                checks.Add(new PrisonBanRefreshRequest(session, CreateBanRefreshCheck(session)));
            }

            if (checks.Count == 0)
                return;

            var results = new List<PrisonBanRefreshResult>();
            foreach (var request in checks)
            {
                var check = request.Check;
                var bans = await _db.GetBansAsync(
                    check.Address,
                    check.UserId,
                    check.HwId,
                    check.ModernHwIds,
                    includeUnbanned: false);

                results.Add(new PrisonBanRefreshResult(
                    request.Session,
                    GetLatestActiveServerBan(bans)));
            }

            _taskManager.RunOnMainThread(() => ApplyActivePrisonBanRefresh(results));
        }
        catch (Exception e)
        {
            Log.Error($"Failed to refresh prison ban state: {e}");
        }
        finally
        {
            _activeBanRefreshRunning = false;
        }
    }

    private PrisonBanRefreshCheck CreateBanRefreshCheck(ICommonSession session)
    {
        var channel = session.Channel;
        ImmutableArray<byte>? hwId = channel.UserData.HWId;

        if (hwId.Value.Length == 0 || !_cfg.GetCVar(CCVars.BanHardwareIds))
            hwId = null;

        return new PrisonBanRefreshCheck(
            session.UserId,
            channel.RemoteEndPoint.Address,
            hwId,
            channel.UserData.ModernHWIds);
    }

    private void ApplyActivePrisonBanRefresh(List<PrisonBanRefreshResult> results)
    {
        foreach (var result in results)
        {
            var userId = result.Session.UserId;
            if (!_prisonSessions.TryGetValue(userId, out var trackedSession) ||
                !ReferenceEquals(trackedSession, result.Session) ||
                !_player.TryGetSessionById(userId, out var session) ||
                !ReferenceEquals(session, result.Session))
            {
                continue;
            }

            if (result.LatestBan is { SendToPrison: false } directBan)
            {
                ClearPrisonState(session);
                session.Channel.Disconnect(directBan.FormatBanMessage(_cfg, _loc));
                continue;
            }

            if (result.LatestBan == null)
            {
                ClearPrisonState(session);
                _chat.DispatchServerMessage(session, Loc.GetString("prison-release-message"));
                continue;
            }

            if (!Ready)
            {
                ClearPrisonState(session);
                session.Channel.Disconnect(result.LatestBan.FormatBanMessage(_cfg, _loc));
            }
        }
    }

    private void ClearPrisonState(ICommonSession session)
    {
        UnlockFactionSelection(session.UserId);
        RemoveDisconnectedPrisonState(session.UserId);
        _prisonFactions.Remove(session.UserId);
        _pendingSentenceAcceleration.Remove(session.UserId);

        if (session.AttachedEntity is { } entity && Exists(entity))
        {
            RemovePrisonBound(entity);
            RemComp<PrisonFactionMemberComponent>(entity);
        }
    }

    private bool TryGetPrisonerMind(EntityUid entity, out EntityUid mindId, out MindComponent mind)
    {
        return TryGetMind(entity, out mindId, out mind) &&
               IsMindPrisoner(mindId, mind);
    }

    private bool TryGetDamageSourceMind(EntityUid? source, out EntityUid mindId, out MindComponent mind)
    {
        mindId = default;
        mind = default!;

        if (source == null)
            return false;

        if (TryGetMind(source.Value, out mindId, out mind))
            return true;

        if (TryGetProjectileSourceMind(source.Value, out mindId, out mind))
            return true;

        if (TryGetThrownItemSourceMind(source.Value, out mindId, out mind))
            return true;

        var current = source.Value;
        for (var i = 0; i < SourceParentSearchDepth; i++)
        {
            if (!TryComp(current, out TransformComponent? transform))
                return false;

            var parent = transform.ParentUid;
            if (parent == current)
                return false;

            if (TryGetMind(parent, out mindId, out mind))
                return true;

            if (TryGetProjectileSourceMind(parent, out mindId, out mind))
                return true;

            if (TryGetThrownItemSourceMind(parent, out mindId, out mind))
                return true;

            current = parent;
        }

        return false;
    }

    private bool TryGetProjectileSourceMind(EntityUid uid, out EntityUid mindId, out MindComponent mind)
    {
        mindId = default;
        mind = default!;

        if (!TryComp<ProjectileComponent>(uid, out var projectile))
            return false;

        if (projectile.Shooter != null && TryGetMind(projectile.Shooter.Value, out mindId, out mind))
            return true;

        return projectile.Weapon != null &&
               TryGetMind(projectile.Weapon.Value, out mindId, out mind);
    }

    private bool TryGetThrownItemSourceMind(EntityUid uid, out EntityUid mindId, out MindComponent mind)
    {
        mindId = default;
        mind = default!;

        return TryComp<ThrownItemComponent>(uid, out var thrown) &&
               thrown.Thrower != null &&
               TryGetMind(thrown.Thrower.Value, out mindId, out mind);
    }

    private bool TryGetMind(EntityUid uid, out EntityUid mindId, out MindComponent mind)
    {
        mindId = default;
        mind = default!;

        if (!TryComp<MindContainerComponent>(uid, out var mindContainer) ||
            mindContainer.Mind == null)
        {
            return false;
        }

        var mindEntity = mindContainer.Mind.Value;
        if (!TryComp<MindComponent>(mindEntity, out var mindComponent))
            return false;

        mindId = mindEntity;
        mind = mindComponent;
        return true;
    }

    private bool TryGetLargestPrisonDamageContributor(
        EntityUid target,
        EntityUid targetMindId,
        out EntityUid sourceMindId,
        out MindComponent sourceMind)
    {
        sourceMindId = default;
        sourceMind = default!;

        if (!_prisonDamageByTarget.TryGetValue(target, out var sources))
            return false;

        var highest = FixedPoint2.Zero;
        var found = false;

        foreach (var (candidateMindId, damage) in sources)
        {
            MindComponent? candidateMind = null;
            if (candidateMindId == targetMindId ||
                damage <= highest ||
                !Resolve(candidateMindId, ref candidateMind, false) ||
                !IsMindPrisoner(candidateMindId, candidateMind))
            {
                continue;
            }

            sourceMindId = candidateMindId;
            sourceMind = candidateMind;
            highest = damage;
            found = true;
        }

        return found;
    }

    private bool TryGetLargestPrisonFaunaDamageContributor(
        EntityUid target,
        out EntityUid sourceMindId,
        out MindComponent sourceMind)
    {
        sourceMindId = default;
        sourceMind = default!;

        if (!_prisonFaunaDamageByTarget.TryGetValue(target, out var sources))
            return false;

        var highest = FixedPoint2.Zero;
        var found = false;
        foreach (var (candidateMindId, damage) in sources)
        {
            MindComponent? candidateMind = null;
            if (damage <= highest ||
                !Resolve(candidateMindId, ref candidateMind, false) ||
                !IsMindPrisoner(candidateMindId, candidateMind))
            {
                continue;
            }

            sourceMindId = candidateMindId;
            sourceMind = candidateMind;
            highest = damage;
            found = true;
        }

        return found;
    }

    private void ReducePrisonDamageContributors(EntityUid target, FixedPoint2 healing)
    {
        ReduceDamageContributors(_prisonDamageByTarget, target, healing);
    }

    private static void ReduceDamageContributors(
        Dictionary<EntityUid, Dictionary<EntityUid, FixedPoint2>> damageByTarget,
        EntityUid target,
        FixedPoint2 healing)
    {
        if (healing <= FixedPoint2.Zero || !damageByTarget.TryGetValue(target, out var sources))
            return;

        var totalTrackedDamage = FixedPoint2.Zero;
        foreach (var damage in sources.Values)
        {
            if (damage > FixedPoint2.Zero)
                totalTrackedDamage += damage;
        }

        if (totalTrackedDamage <= healing)
        {
            damageByTarget.Remove(target);
            return;
        }

        var sourceMindIds = new EntityUid[sources.Count];
        sources.Keys.CopyTo(sourceMindIds, 0);

        foreach (var sourceMindId in sourceMindIds)
        {
            var damage = sources[sourceMindId];
            var reduction = damage / totalTrackedDamage * healing;
            var remaining = damage - reduction;
            if (remaining <= FixedPoint2.Zero)
                sources.Remove(sourceMindId);
            else
                sources[sourceMindId] = remaining;
        }

        if (sources.Count == 0)
            damageByTarget.Remove(target);
    }

    private async void AddPrisonMurderPenalty(MindComponent killerMind)
    {
        if (killerMind.UserId is not { } userId)
            return;

        await _murderPenaltySemaphore.WaitAsync();
        try
        {
            await ApplyPrisonMurderPenalty(userId);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to apply prison murder penalty for {userId}: {e}");
        }
        finally
        {
            _murderPenaltySemaphore.Release();
        }
    }

    private async Task ApplyPrisonMurderPenalty(NetUserId userId)
    {
        var minutes = Math.Max(1, _murderPenaltyMinutes);
        var now = DateTimeOffset.UtcNow;
        var expiration = now + TimeSpan.FromMinutes(minutes);
        var roundIds = _gameTicker.RoundId != 0
            ? ImmutableArray.Create(_gameTicker.RoundId)
            : ImmutableArray<int>.Empty;

        IPAddress? address = null;
        ImmutableArray<byte>? hwId = null;
        ImmutableArray<ImmutableArray<byte>>? modernHwIds = null;
        if (_player.TryGetSessionById(userId, out var session))
        {
            var check = CreateBanRefreshCheck(session);
            address = check.Address;
            hwId = check.HwId;
            modernHwIds = check.ModernHwIds;
        }

        var bans = await _db.GetBansAsync(
            address,
            userId,
            hwId,
            modernHwIds,
            includeUnbanned: false);

        var latestBan = GetLatestActiveServerBan(bans);
        if (latestBan == null || !IsPrisonServerBan(latestBan))
            return;

        if (IsPermanentPrisonBan(latestBan) && latestBan.Id is { } permanentBanId)
        {
            await _db.SetBanPrisonAccess(permanentBanId, false);
            _taskManager.RunOnMainThread(() => RevokePermanentPrisonAccess(userId));
            return;
        }

        if (latestBan.ExpirationTime is { } activeExpiration &&
            activeExpiration > now)
        {
            expiration = activeExpiration + TimeSpan.FromMinutes(minutes);
        }

        var ban = new BanDef(
            null,
            BanType.Server,
            ImmutableArray.Create(userId),
            ImmutableArray<(IPAddress address, int cidrMask)>.Empty,
            ImmutableArray<ImmutableTypedHwid>.Empty,
            now,
            expiration,
            roundIds,
            TimeSpan.Zero,
            Loc.GetString("prison-murder-penalty-reason"),
            NoteSeverity.High,
            null,
            null,
            sendToPrison: true);

        await _db.AddBanAsync(ban);

        _taskManager.RunOnMainThread(() => ApplyPrisonMurderPenalty(userId, minutes));
    }

    private void ApplyPrisonMurderPenalty(NetUserId userId, int minutes)
    {
        _nextActiveBanRefresh = TimeSpan.Zero;

        if (_player.TryGetSessionById(userId, out var session))
        {
            _chat.DispatchServerMessage(
                session,
                Loc.GetString("prison-murder-penalty-message", ("minutes", minutes)));
        }
    }

    private void RevokePermanentPrisonAccess(NetUserId userId)
    {
        _nextActiveBanRefresh = TimeSpan.Zero;

        if (!_player.TryGetSessionById(userId, out var session))
            return;

        ClearPrisonState(session);
        session.Channel.Disconnect(Loc.GetString("prison-murder-permanent-message"));
    }

    private bool IsUserCurrentlyAntagonist(NetUserId userId)
    {
        return _mind.TryGetMind(userId, out var mindId, out _)
               && _role.MindIsAntagonist(mindId);
    }

    private bool IsSessionAntagonist(ICommonSession session)
    {
        return _mind.TryGetMind(session, out var mindId, out _)
               && _role.MindIsAntagonist(mindId);
    }

    private bool TryGetSpawnCoordinates(out EntityCoordinates coordinates)
    {
        return TryGetSpawnCoordinates(null, out coordinates);
    }

    private bool HasFactionSpawnPoint(ProtoId<PrisonFactionPrototype> faction)
    {
        var query = EntityQueryEnumerator<PrisonSpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out _, out var spawn, out var xform))
        {
            if (xform.MapID != MapId.Nullspace && spawn.Faction == faction)
                return true;
        }

        return false;
    }

    private bool TryGetSpawnCoordinates(
        ProtoId<PrisonFactionPrototype>? faction,
        out EntityCoordinates coordinates)
    {
        var spawns = new List<EntityCoordinates>();

        var query = EntityQueryEnumerator<PrisonSpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out _, out var spawn, out var xform))
        {
            if (xform.MapID == MapId.Nullspace)
                continue;

            if (faction == null || spawn.Faction == faction)
                spawns.Add(xform.Coordinates);
        }

        if (spawns.Count == 0)
        {
            coordinates = EntityCoordinates.Invalid;
            return false;
        }

        coordinates = _random.Pick(spawns);
        return true;
    }

    public bool IsPrisonMap(MapId mapId)
    {
        var mapQuery = EntityQueryEnumerator<PrisonMapComponent, TransformComponent>();
        while (mapQuery.MoveNext(out _, out _, out var mapXform))
        {
            if (mapXform.MapID == mapId)
                return true;
        }

        // Keep spawn points as a fallback for tests and manually loaded prison maps.
        var query = EntityQueryEnumerator<PrisonSpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID == mapId)
                return true;
        }

        return false;
    }

    private void SendPrisonMessage(ICommonSession session, BanDef ban)
    {
        if (ban.ExpirationTime == null)
        {
            _chat.DispatchServerMessage(
                session,
                Loc.GetString(
                    "prison-sent-permanent-message",
                    ("seconds", _factionSelectionSeconds)));
            return;
        }

        var remaining = ban.ExpirationTime - DateTimeOffset.UtcNow;
        var minutes = remaining is { TotalMinutes: > 0 }
            ? Math.Ceiling(remaining.Value.TotalMinutes).ToString("N0")
            : "0";

        _chat.DispatchServerMessage(
            session,
            Loc.GetString(
                "prison-sent-message",
                ("remaining", minutes),
                ("percent", GetSentenceAccelerationPercent()),
                ("minutes", _crossFactionKillRewardMinutes),
                ("seconds", _factionSelectionSeconds)));
    }

    private int GetSentenceAccelerationPercent()
    {
        return Math.Max(0, (int) MathF.Round((_sentenceTimeMultiplier - 1f) * 100f));
    }

    private static bool IsActiveServerBan(BanDef ban)
    {
        return ban.Type == BanType.Server
               && ban.Unban == null
               && (ban.ExpirationTime == null || ban.ExpirationTime > DateTimeOffset.UtcNow);
    }

    private static bool IsPrisonServerBan(BanDef ban)
    {
        return IsActiveServerBan(ban) && ban.SendToPrison;
    }

    private static bool IsPermanentPrisonBan(BanDef ban)
    {
        return IsPrisonServerBan(ban) && ban.ExpirationTime == null;
    }

    private static BanDef? GetLatestActiveServerBan(IEnumerable<BanDef> bans)
    {
        return bans
            .Where(IsActiveServerBan)
            .OrderByDescending(ban => ban.BanTime)
            .ThenByDescending(ban => ban.Id)
            .FirstOrDefault();
    }

    private readonly record struct PrisonBanRefreshCheck(
        NetUserId UserId,
        IPAddress Address,
        ImmutableArray<byte>? HwId,
        ImmutableArray<ImmutableArray<byte>> ModernHwIds);

    private readonly record struct PrisonBanRefreshRequest(
        ICommonSession Session,
        PrisonBanRefreshCheck Check);

    private readonly record struct PendingFaunaReward(
        PrisonBanRefreshCheck Check,
        int Minutes);

    private readonly record struct PrisonBanRefreshResult(
        ICommonSession Session,
        BanDef? LatestBan);
}

[ByRefEvent]
public readonly record struct PrisonerRegisteredEvent(ICommonSession Session);

public readonly record struct PrisonSentence(int BanId);
