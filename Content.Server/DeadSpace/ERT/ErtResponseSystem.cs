// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.AlertLevel;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.DeadSpace.ERT.Components;
using Content.Server.DeadSpace.ERTCall;
using Content.Server.DeadSpace.Languages;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Station.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.DeadSpace.ERT;
using Content.Shared.DeadSpace.ERT.Prototypes;
using Content.Shared.DeadSpace.TimeWindow;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Pinpointer;
using Content.Shared.Storage;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Maths;
using Content.Shared.Administration;
using Content.Server.Administration.Managers;

namespace Content.Server.DeadSpace.ERT;

/// <summary>
/// Данные ожидаемой команды ERT.
/// </summary>
public sealed class PendingErtRequestData
{
    public int RequestId { get; set; }
    public ProtoId<ErtTeamPrototype> RequestedTeamId { get; set; }
    public TimedWindow DecisionWindow { get; set; } = default!;
    public string RequestedByName { get; set; } = string.Empty;
    public string? CallReason { get; set; }
    public EntityUid? StationUid { get; set; }
    public EntityUid? ConsoleUid { get; set; }
    public int ReservedPrice { get; set; }
}

public sealed class ManualApprovedErtRequestData
{
    public int RequestId { get; set; }
    public ProtoId<ErtTeamPrototype> TeamId { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public string? CallReason { get; set; }
    public EntityUid? StationUid { get; set; }
    public EntityUid? ConsoleUid { get; set; }
    public int ReservedPrice { get; set; }
    public EntityUid? PinpointerTarget { get; set; }
    public bool SuppressAnnouncements { get; set; }
}

public sealed class ApprovedErtRequestData
{
    public int RequestId { get; set; }
    public ProtoId<ErtTeamPrototype> TeamId { get; set; }
    public TimedWindow Window { get; set; } = default!;
    public string RequestedByName { get; set; } = string.Empty;
    public string? CallReason { get; set; }
    public EntityUid? StationUid { get; set; }
    public EntityUid? ConsoleUid { get; set; }
    public int ReservedPrice { get; set; }
    public EntityUid? PinpointerTarget { get; set; }
    public bool SuppressAnnouncements { get; set; }
}

// Работает для одной станции, потому что пока нет смысла делать для множества
public sealed class ErtResponseSystem : SharedErtResponseSystem
{
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly TimedWindowSystem _timedWindowSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoaderSystem = default!;
    [Dependency] private readonly AlertLevelSystem _alertLevelSystem = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPinpointerSystem _pinpointerSystem = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;

    private readonly Dictionary<int, PendingErtRequestData> _pendingRequests = new();
    private readonly Dictionary<int, ManualApprovedErtRequestData> _manualApprovedRequests = new();
    private readonly Dictionary<int, ApprovedErtRequestData> _approvedRequests = new();

    private TimedWindow? _coolDown;
    private readonly TimedWindow _defaultWindowWaitingSpecies = new(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    private List<WaitingSpeciesSettings> _windowWaitingSpecies = new();
    private int _nextRequestId = 1;

    private static readonly SoundSpecifier RequestSound = new SoundPathSpecifier("/Audio/_DeadSpace/Announcements/ERT/call.ogg");
    private static readonly SoundSpecifier DecisionSound = new SoundPathSpecifier("/Audio/_DeadSpace/Announcements/ERT/decision.ogg");
    private static readonly SoundSpecifier TeamChangedSound = new SoundPathSpecifier("/Audio/_DeadSpace/Announcements/centcomm.ogg");
    private static readonly TimeSpan PendingRequestLifetime = TimeSpan.FromMinutes(2);

    /// <summary>
    ///     Сумма очков для заказа обр, доступная в начале каждого раунда.
    /// </summary>
    private const int DefaultPoints = 8;

    /// <summary>
    ///     Текущий баланс очков.
    /// </summary>
    private int _points = DefaultPoints;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RequestErtAdminStateMessage>(OnRequestErtAdminState);
        SubscribeNetworkEvent<AdminModifyErtEntryMessage>(OnAdminModifyErtEntry);
        SubscribeNetworkEvent<AdminSetPointsMessage>(OnAdminSetPoints);
        SubscribeNetworkEvent<AdminDeleteErtMessage>(OnDeleteErt);
        SubscribeNetworkEvent<AdminSetCooldownMessage>(OnAdminSetCooldown);
        SubscribeNetworkEvent<AdminSetErtReasonMessage>(OnAdminSetReason);
        SubscribeNetworkEvent<AdminCallErtMessage>(OnAdminCallErt);
        SubscribeNetworkEvent<AdminRejectErtRequestMessage>(OnRejectRequest);
        SubscribeNetworkEvent<AdminApproveErtRequestManualMessage>(OnApproveRequestManual);
        SubscribeNetworkEvent<AdminApproveErtRequestAutoMessage>(OnApproveRequestAuto);
        SubscribeNetworkEvent<AdminSetApprovedErtTeamMessage>(OnSetApprovedTeam);
        SubscribeNetworkEvent<AdminSendErtNowMessage>(OnSendErtNow);
        SubscribeNetworkEvent<AdminPromoteManualApprovedErtMessage>(OnPromoteManualApprovedRequest);
        SubscribeNetworkEvent<AdminMoveApprovedErtToManualMessage>(OnMoveApprovedRequestToManual);

        SubscribeLocalEvent<ErtSpawnRuleComponent, RuleLoadedGridsEvent>(OnRuleLoadedGrids);
        SubscribeLocalEvent<ErtSpeciesRoleComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRequestErtAdminState(RequestErtAdminStateMessage msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Admin) ||
            !_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Fun))
        {
            Log.Warning($"Послан в пешее эротическое взаимодействие неадмина с админ обром " +
                        $"{args.SenderSession.Name} ({args.SenderSession.UserId}).");
            return;
        }
        var pendingEntries = new List<ErtPendingRequestEntry>();
        var approvedEntries = new List<ErtApprovedRequestEntry>();
        var manualApprovedEntries = new List<ErtManualApprovedRequestEntry>();

        foreach (var data in _pendingRequests.Values)
        {
            if (!_prototypeManager.TryIndex(data.RequestedTeamId, out var proto))
                continue;

            pendingEntries.Add(new ErtPendingRequestEntry(
                data.RequestId,
                data.RequestedTeamId.ToString(),
                proto.Name,
                _timedWindowSystem.GetSecondsRemaining(data.DecisionWindow),
                data.ReservedPrice,
                data.RequestedByName,
                data.CallReason));
        }

        foreach (var data in _approvedRequests.Values)
        {
            if (!_prototypeManager.TryIndex(data.TeamId, out var proto))
                continue;

            approvedEntries.Add(new ErtApprovedRequestEntry(
                data.RequestId,
                data.TeamId.ToString(),
                proto.Name,
                _timedWindowSystem.GetSecondsRemaining(data.Window),
                data.ReservedPrice,
                data.RequestedByName,
                data.CallReason));
        }

        foreach (var data in _manualApprovedRequests.Values)
        {
            if (!_prototypeManager.TryIndex(data.TeamId, out var proto))
                continue;

            manualApprovedEntries.Add(new ErtManualApprovedRequestEntry(
                data.RequestId,
                data.TeamId.ToString(),
                proto.Name,
                data.ReservedPrice,
                data.RequestedByName,
                data.CallReason));
        }

        var cooldownSeconds = 0;
        if (_coolDown != null && !_timedWindowSystem.IsExpired(_coolDown))
            cooldownSeconds = _timedWindowSystem.GetSecondsRemaining(_coolDown);

        RaiseNetworkEvent(
            new ErtAdminStateResponse(
                pendingEntries.ToArray(),
                approvedEntries.ToArray(),
                manualApprovedEntries.ToArray(),
                _points,
                cooldownSeconds),
            args.SenderSession.Channel);
    }

    private void OnAdminModifyErtEntry(AdminModifyErtEntryMessage msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Admin) ||
            !_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Fun))
        {
            Log.Warning($"Послан в пешее эротическое взаимодействие неадмина с админ обром " +
                        $"{args.SenderSession.Name} ({args.SenderSession.UserId}).");
            return;
        }
        if (!_approvedRequests.TryGetValue(msg.RequestId, out var data))
        {
            RaiseNetworkEvent(new ErtAdminActionResult(false, "No approved ERT request with that id"), args.SenderSession.Channel);
            return;
        }

        data.Window.Remaining = _timing.CurTime + TimeSpan.FromSeconds(msg.Seconds);

        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"Admin {args.SenderSession.Name} set approved ERT request #{msg.RequestId} arrival to {msg.Seconds} seconds");
        _chatManager.SendAdminAlert($"Админ {args.SenderSession.Name} изменил время прибытия авто-одобренной заявки ОБР #{msg.RequestId} на {msg.Seconds} сек.");

        RaiseNetworkEvent(new ErtAdminActionResult(true, "OK"), args.SenderSession.Channel);
    }

    private void OnAdminSetPoints(AdminSetPointsMessage msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Admin) ||
            !_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Fun))
        {
            Log.Warning($"Послан в пешее эротическое взаимодействие неадмина с админ обром " +
                        $"{args.SenderSession.Name} ({args.SenderSession.UserId}).");
            return;
        }
        _points = msg.Points;

        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"Admin {args.SenderSession.Name} set ERT points to {_points}");
        _chatManager.SendAdminAlert($"Админ {args.SenderSession.Name} установил баланс ОБР на {_points} очков.");

        RaiseNetworkEvent(new ErtAdminActionResult(true, "OK"), args.SenderSession.Channel);
    }

    private void OnDeleteErt(AdminDeleteErtMessage msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Admin) ||
            !_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Fun))
        {
            Log.Warning($"Послан в пешее эротическое взаимодействие неадмина с админ обром " +
                        $"{args.SenderSession.Name} ({args.SenderSession.UserId}).");
            return;
        }
        if (!_approvedRequests.Remove(msg.RequestId))
        {
            RaiseNetworkEvent(new ErtAdminActionResult(false, "No approved ERT request with that id"), args.SenderSession.Channel);
            return;
        }

        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"Admin {args.SenderSession.Name} deleted approved ERT request #{msg.RequestId}");
        _chatManager.SendAdminAlert($"Админ {args.SenderSession.Name} отменил авто-одобренную заявку ОБР #{msg.RequestId}.");

        RaiseNetworkEvent(new ErtAdminActionResult(true, "OK"), args.SenderSession.Channel);
    }

    private void OnAdminSetCooldown(AdminSetCooldownMessage msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Admin) ||
            !_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Fun))
        {
            Log.Warning($"Послан в пешее эротическое взаимодействие неадмина с админ обром " +
                        $"{args.SenderSession.Name} ({args.SenderSession.UserId}).");
            return;
        }
        var window = new TimedWindow(TimeSpan.FromSeconds(msg.Seconds), TimeSpan.FromSeconds(msg.Seconds));
        _timedWindowSystem.Reset(window);
        _coolDown = window;

        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"Admin {args.SenderSession.Name} set ERT cooldown to {msg.Seconds} seconds");
        _chatManager.SendAdminAlert($"Админ {args.SenderSession.Name} установил откат ОБР на {msg.Seconds} сек.");

        RaiseNetworkEvent(new ErtAdminActionResult(true, "OK"), args.SenderSession.Channel);
    }

    private void OnAdminSetReason(AdminSetErtReasonMessage msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Admin) ||
            !_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Fun))
        {
            Log.Warning($"Послан в пешее эротическое взаимодействие неадмина с админ обром " +
                        $"{args.SenderSession.Name} ({args.SenderSession.UserId}).");
            return;
        }
        if (!_approvedRequests.TryGetValue(msg.RequestId, out var data))
        {
            RaiseNetworkEvent(new ErtAdminActionResult(false, "No approved ERT request with that id"), args.SenderSession.Channel);
            return;
        }

        data.CallReason = msg.Reason;

        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"Admin {args.SenderSession.Name} set approved ERT request #{msg.RequestId} reason to '{msg.Reason}'");
        _chatManager.SendAdminAlert($"Админ {args.SenderSession.Name} изменил цель авто-одобренной заявки ОБР #{msg.RequestId} на '{msg.Reason}'.");

        RaiseNetworkEvent(new ErtAdminActionResult(true, "OK"), args.SenderSession.Channel);
    }

    private void OnAdminCallErt(AdminCallErtMessage msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Admin) ||
            !_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Fun))
        {
            Log.Warning($"Послан в пешее эротическое взаимодействие неадмина с админ обром " +
                        $"{args.SenderSession.Name} ({args.SenderSession.UserId}).");
            return;
        }
        var teamId = new ProtoId<ErtTeamPrototype>(msg.ProtoId);
        var success = TryCallErt(
            teamId,
            _stationSystem.GetOwningStation(args.SenderSession.AttachedEntity),
            out var result,
            toPay: true,
            needCooldown: true,
            needWarn: msg.SendNotification,
            callReason: msg.Reason,
            requestedByName: args.SenderSession.Name,
            suppressAnnouncements: !msg.SendNotification);

        _adminLogger.Add(
            LogType.Action,
            LogImpact.Medium,
            $"Admin {args.SenderSession.Name} queued ERT '{msg.ProtoId}' for auto spawn with reason '{msg.Reason}', announcements: {msg.SendNotification}");
        _chatManager.SendAdminAlert(
            $"Админ {args.SenderSession.Name} добавил новый вызов ОБР '{msg.ProtoId}' в очередь автоспавна" +
            (msg.SendNotification ? "." : " без оповещений."));
        RaiseNetworkEvent(new ErtAdminActionResult(success, result ?? "ERT queued successfully."), args.SenderSession.Channel);
    }

    private void OnRejectRequest(AdminRejectErtRequestMessage msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Admin) ||
            !_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Fun))
        {
            Log.Warning($"Послан в пешее эротическое взаимодействие неадмина с админ обром " +
                        $"{args.SenderSession.Name} ({args.SenderSession.UserId}).");
            return;
        }
        if (!_pendingRequests.Remove(msg.RequestId))
        {
            RaiseNetworkEvent(new ErtAdminActionResult(false, "No pending ERT request with that id"), args.SenderSession.Channel);
            return;
        }

        if (msg.SendNotification)
        {
            _chatSystem.DispatchGlobalAnnouncement(
                Loc.GetString("ert-console-request-rejected-announcement"),
                sender: Loc.GetString("ert-response-cso-sender"),
                announcementSound: DecisionSound,
                colorOverride: Color.FromHex("#1d8bad"),
                playSound: true,
                usePresetTTS: true,
                languageId: LanguageSystem.DefaultLanguageId);
        }

        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"Admin {args.SenderSession.Name} rejected pending ERT request #{msg.RequestId}");
        _chatManager.SendAdminAlert($"Админ {args.SenderSession.Name} отклонил заявку ОБР #{msg.RequestId}.");

        RaiseNetworkEvent(new ErtAdminActionResult(true, "OK"), args.SenderSession.Channel);
    }

    private void OnApproveRequestManual(AdminApproveErtRequestManualMessage msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Admin) ||
            !_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Fun))
        {
            Log.Warning($"Послан в пешее эротическое взаимодействие неадмина с админ обром " +
                        $"{args.SenderSession.Name} ({args.SenderSession.UserId}).");
            return;
        }
        if (!_pendingRequests.TryGetValue(msg.RequestId, out var request))
        {
            RaiseNetworkEvent(new ErtAdminActionResult(false, "No pending ERT request with that id"), args.SenderSession.Channel);
            return;
        }

        if (!_prototypeManager.TryIndex(request.RequestedTeamId, out var prototype))
        {
            RaiseNetworkEvent(new ErtAdminActionResult(false, "Prototype missing"), args.SenderSession.Channel);
            return;
        }

        _pendingRequests.Remove(msg.RequestId);
        _manualApprovedRequests[msg.RequestId] = new ManualApprovedErtRequestData
        {
            RequestId = request.RequestId,
            TeamId = request.RequestedTeamId,
            RequestedByName = request.RequestedByName,
            CallReason = request.CallReason,
            StationUid = request.StationUid,
            ConsoleUid = request.ConsoleUid,
            ReservedPrice = request.ReservedPrice,
            SuppressAnnouncements = !msg.SendNotification,
        };

        if (msg.SendNotification)
            AnnounceApprovedRequest(prototype);

        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"Admin {args.SenderSession.Name} manually approved pending ERT request #{msg.RequestId}");
        _chatManager.SendAdminAlert($"Админ {args.SenderSession.Name} одобрил заявку ОБР #{msg.RequestId} для ручного спавна.");

        RaiseNetworkEvent(new ErtAdminActionResult(true, "OK"), args.SenderSession.Channel);
    }

    private void OnApproveRequestAuto(AdminApproveErtRequestAutoMessage msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Admin) ||
            !_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Fun))
        {
            Log.Warning($"Послан в пешее эротическое взаимодействие неадмина с админ обром " +
                        $"{args.SenderSession.Name} ({args.SenderSession.UserId}).");
            return;
        }
        if (!_pendingRequests.TryGetValue(msg.RequestId, out var request))
        {
            RaiseNetworkEvent(new ErtAdminActionResult(false, "No pending ERT request with that id"), args.SenderSession.Channel);
            return;
        }

        if (!_prototypeManager.TryIndex(request.RequestedTeamId, out var prototype))
        {
            RaiseNetworkEvent(new ErtAdminActionResult(false, "Prototype missing"), args.SenderSession.Channel);
            return;
        }

        _pendingRequests.Remove(msg.RequestId);
        QueueApprovedRequest(request, prototype, suppressAnnouncements: !msg.SendNotification);
        if (msg.SendNotification)
            AnnounceApprovedRequest(prototype);

        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"Admin {args.SenderSession.Name} auto-approved pending ERT request #{msg.RequestId}");
        _chatManager.SendAdminAlert($"Админ {args.SenderSession.Name} одобрил заявку ОБР #{msg.RequestId} с автоматическим спавном.");

        RaiseNetworkEvent(new ErtAdminActionResult(true, "OK"), args.SenderSession.Channel);
    }

    private void OnSetApprovedTeam(AdminSetApprovedErtTeamMessage msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Admin) ||
            !_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Fun))
        {
            Log.Warning($"Послан в пешее эротическое взаимодействие неадмина с админ обром " +
                        $"{args.SenderSession.Name} ({args.SenderSession.UserId}).");
            return;
        }
        if (!_approvedRequests.TryGetValue(msg.RequestId, out var request))
        {
            RaiseNetworkEvent(new ErtAdminActionResult(false, "No approved ERT request with that id"), args.SenderSession.Channel);
            return;
        }

        var newTeam = new ProtoId<ErtTeamPrototype>(msg.ProtoId);
        if (!_prototypeManager.TryIndex(newTeam, out var prototype))
        {
            RaiseNetworkEvent(new ErtAdminActionResult(false, "Prototype missing"), args.SenderSession.Channel);
            return;
        }

        if (HasActiveRequest(prototype, request.PinpointerTarget, msg.RequestId))
        {
            RaiseNetworkEvent(new ErtAdminActionResult(false, Loc.GetString("ert-call-fail-already-waiting")), args.SenderSession.Channel);
            return;
        }

        var teamChanged = request.TeamId != newTeam;
        request.TeamId = newTeam;

        if (teamChanged && !request.SuppressAnnouncements)
            AnnounceChangedApprovedTeam(prototype);

        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"Admin {args.SenderSession.Name} changed approved ERT request #{msg.RequestId} team to '{msg.ProtoId}'");
        _chatManager.SendAdminAlert($"Админ {args.SenderSession.Name} сменил отряд авто-одобренной заявки ОБР #{msg.RequestId} на '{msg.ProtoId}'.");

        RaiseNetworkEvent(new ErtAdminActionResult(true, "OK"), args.SenderSession.Channel);
    }

    private void OnSendErtNow(AdminSendErtNowMessage msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Admin) ||
            !_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Fun))
        {
            Log.Warning($"Послан в пешее эротическое взаимодействие неадмина с админ обром " +
                        $"{args.SenderSession.Name} ({args.SenderSession.UserId}).");
            return;
        }
        if (!_approvedRequests.TryGetValue(msg.RequestId, out var request))
        {
            RaiseNetworkEvent(new ErtAdminActionResult(false, "No approved ERT request with that id"), args.SenderSession.Channel);
            return;
        }

        EnsureErtTeam(
            request.TeamId,
            request.CallReason,
            request.PinpointerTarget,
            announce: !request.SuppressAnnouncements);
        _approvedRequests.Remove(msg.RequestId);

        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"Admin {args.SenderSession.Name} sent approved ERT request #{msg.RequestId} immediately");
        _chatManager.SendAdminAlert($"Админ {args.SenderSession.Name} немедленно отправил авто-одобренную заявку ОБР #{msg.RequestId}.");
        RaiseNetworkEvent(new ErtAdminActionResult(true, "OK"), args.SenderSession.Channel);
    }

    private void OnPromoteManualApprovedRequest(AdminPromoteManualApprovedErtMessage msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Admin) ||
            !_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Fun))
        {
            Log.Warning($"Послан в пешее эротическое взаимодействие неадмина с админ обром " +
                        $"{args.SenderSession.Name} ({args.SenderSession.UserId}).");
            return;
        }
        if (!_manualApprovedRequests.TryGetValue(msg.RequestId, out var request))
        {
            RaiseNetworkEvent(new ErtAdminActionResult(false, "No manual-approved ERT request with that id"), args.SenderSession.Channel);
            return;
        }

        if (!_prototypeManager.TryIndex(request.TeamId, out var prototype))
        {
            RaiseNetworkEvent(new ErtAdminActionResult(false, "Prototype missing"), args.SenderSession.Channel);
            return;
        }

        _manualApprovedRequests.Remove(msg.RequestId);
        request.SuppressAnnouncements = true;
        QueueApprovedRequest(request, prototype);

        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"Admin {args.SenderSession.Name} promoted manual-approved ERT request #{msg.RequestId} to auto spawn");
        _chatManager.SendAdminAlert($"Админ {args.SenderSession.Name} запустил автоспавн для вручную одобренной заявки ОБР #{msg.RequestId}.");
        RaiseNetworkEvent(new ErtAdminActionResult(true, "OK"), args.SenderSession.Channel);
    }

    private void OnMoveApprovedRequestToManual(AdminMoveApprovedErtToManualMessage msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Admin) ||
            !_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Fun))
        {
            Log.Warning($"Послан в пешее эротическое взаимодействие неадмина с админ обром " +
                        $"{args.SenderSession.Name} ({args.SenderSession.UserId}).");
            return;
        }
        if (!_approvedRequests.TryGetValue(msg.RequestId, out var request))
        {
            RaiseNetworkEvent(new ErtAdminActionResult(false, "No approved ERT request with that id"), args.SenderSession.Channel);
            return;
        }

        _approvedRequests.Remove(msg.RequestId);
        _manualApprovedRequests[msg.RequestId] = new ManualApprovedErtRequestData
        {
            RequestId = request.RequestId,
            TeamId = request.TeamId,
            RequestedByName = request.RequestedByName,
            CallReason = request.CallReason,
            StationUid = request.StationUid,
            ConsoleUid = request.ConsoleUid,
            ReservedPrice = request.ReservedPrice,
            PinpointerTarget = request.PinpointerTarget,
            SuppressAnnouncements = request.SuppressAnnouncements,
        };

        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"Admin {args.SenderSession.Name} moved approved ERT request #{msg.RequestId} to manual approval");
        _chatManager.SendAdminAlert($"Админ {args.SenderSession.Name} перенёс авто-одобренную заявку ОБР #{msg.RequestId} в ручное одобрение.");
        RaiseNetworkEvent(new ErtAdminActionResult(true, "OK"), args.SenderSession.Channel);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _windowWaitingSpecies.Clear();
        _pendingRequests.Clear();
        _manualApprovedRequests.Clear();
        _approvedRequests.Clear();
        _coolDown = null;
        _points = DefaultPoints;
        _nextRequestId = 1;
    }

    private void OnMindAdded(Entity<ErtSpeciesRoleComponent> ent, ref MindAddedMessage args)
    {
        if (ent.Comp.Settings == null)
            return;

        _windowWaitingSpecies.Remove(ent.Comp.Settings);

        if (!_prototypeManager.TryIndex(ent.Comp.Settings.TeamId, out var prototype))
            return;

        if (!Exists(ent.Comp.Settings.SpawnPoint))
            return;

        var spawns = EntitySpawnCollection.GetSpawns(prototype.Spawns, _random);

        foreach (var proto in spawns)
        {
            Spawn(proto, Transform(ent.Comp.Settings.SpawnPoint).Coordinates);
        }
    }

    private void OnRuleLoadedGrids(Entity<ErtSpawnRuleComponent> ent, ref RuleLoadedGridsEvent args)
    {
        if (!_prototypeManager.TryIndex(ent.Comp.Team, out var prototype))
            return;

        var query = EntityQueryEnumerator<ErtSpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID != args.Map)
                continue;

            if (xform.GridUid is not { } grid || !args.Grids.Contains(grid))
                continue;

            if (prototype.Special != null)
            {
                var spec = Spawn(prototype.Special.Value, Transform(uid).Coordinates);

                if (Terminating(spec) || !HasComp<GhostRoleComponent>(spec))
                {
                    var ghostQuery = EntityQueryEnumerator<GhostRoleComponent, TransformComponent>();
                    spec = EntityUid.Invalid;
                    while (ghostQuery.MoveNext(out var ghostUid, out _, out var ghostXform))
                    {
                        if (ghostXform.MapID != args.Map)
                            continue;

                        spec = ghostUid;
                        break;
                    }

                    if (!spec.IsValid())
                        return;
                }

                var window = _defaultWindowWaitingSpecies.Clone();
                var settings = new WaitingSpeciesSettings(
                    args.Map,
                    window,
                    ent.Comp.Team,
                    uid,
                    ent.Comp.SuppressAnnouncements);

                EnsureComp<ErtSpeciesRoleComponent>(spec).Settings = settings;
                _timedWindowSystem.Reset(window);

                _windowWaitingSpecies.Add(settings);
                return;
            }

            var spawns = EntitySpawnCollection.GetSpawns(prototype.Spawns, _random);

            foreach (var proto in spawns)
            {
                Spawn(proto, Transform(uid).Coordinates);
            }
        }

        if (ent.Comp.PinpointerTarget != null && Exists(ent.Comp.PinpointerTarget.Value))
        {
            var pinQuery = EntityQueryEnumerator<PinpointerComponent, TransformComponent>();
            while (pinQuery.MoveNext(out var pinUid, out var pin, out var pinXform))
            {
                if (pinXform.MapID == args.Map)
                    _pinpointerSystem.SetTarget(pinUid, ent.Comp.PinpointerTarget.Value, pin);
            }
        }

        var queryStaff = EntityQueryEnumerator<ErtStaffComponent, TransformComponent>();
        while (queryStaff.MoveNext(out _, out var staff, out var xform))
        {
            if (xform.MapID != args.Map)
                continue;

            if (string.IsNullOrEmpty(ent.Comp.CallReason))
                continue;

            staff.CallReason = ent.Comp.CallReason;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        for (var i = _windowWaitingSpecies.Count - 1; i >= 0; i--)
        {
            var settings = _windowWaitingSpecies[i];

            if (!_timedWindowSystem.IsExpired(settings.Window))
                continue;

            _windowWaitingSpecies.RemoveAt(i);
            _mapSystem.DeleteMap(settings.MapId);

            if (!_prototypeManager.TryIndex(settings.TeamId, out var prototype))
                continue;

            if (prototype.CancelMessage != null && !settings.SuppressAnnouncements)
            {
                _chatSystem.DispatchGlobalAnnouncement(
                    message: prototype.CancelMessage,
                    sender: Loc.GetString("chat-manager-sender-announcement"),
                    colorOverride: Color.FromHex("#1d8bad"),
                    playSound: true,
                    usePresetTTS: true,
                    languageId: LanguageSystem.DefaultLanguageId);
            }
        }

        foreach (var (requestId, request) in _pendingRequests.ToArray())
        {
            if (!_timedWindowSystem.IsExpired(request.DecisionWindow))
                continue;

            _pendingRequests.Remove(requestId);

            if (!_prototypeManager.TryIndex(request.RequestedTeamId, out var prototype))
            {
                _adminLogger.Add(LogType.Action, LogImpact.Medium, $"ERT request #{requestId} could not be automatically approved because prototype '{request.RequestedTeamId}' is missing");
                _chatManager.SendAdminAlert($"Заявка ОБР #{requestId} не была автоматически одобрена: прототип '{request.RequestedTeamId}' не найден.");
                continue;
            }

            QueueApprovedRequest(request, prototype);
            AnnounceApprovedRequest(prototype);

            _adminLogger.Add(LogType.Action, LogImpact.Medium, $"ERT request #{requestId} automatically approved for auto spawn after {PendingRequestLifetime.TotalSeconds:0} seconds without admin decision");
            _chatManager.SendAdminAlert($"Заявка ОБР #{requestId} не была обработана за {PendingRequestLifetime.TotalSeconds:0} секунд и автоматически переведена в автоспавн.");
        }

        foreach (var (requestId, data) in _approvedRequests.ToArray())
        {
            if (!_timedWindowSystem.IsExpired(data.Window))
                continue;

            EnsureErtTeam(
                data.TeamId,
                data.CallReason,
                data.PinpointerTarget,
                announce: !data.SuppressAnnouncements);
            _approvedRequests.Remove(requestId);
        }
    }

    public bool TryCallErt(
        ProtoId<ErtTeamPrototype> team,
        EntityUid? station,
        out string? reason,
        bool toPay = true,
        bool needCooldown = true,
        bool needWarn = true,
        string? callReason = null,
        EntityUid? pinpointerTarget = null,
        string? requestedByName = null,
        bool suppressAnnouncements = false)
    {
        reason = "Вызван успешно.";

        if (!TryReserveCall(team, station, out var prototype, out reason, toPay, needCooldown, pinpointerTarget))
            return false;

        if (needWarn)
            AnnounceApprovedRequest(prototype);

        var requestId = _nextRequestId++;
        var effectiveRequester = string.IsNullOrWhiteSpace(requestedByName)
            ? Loc.GetString("ert-admin-requester-system")
            : requestedByName;

        _approvedRequests[requestId] = CreateApprovedRequest(
            requestId,
            team,
            prototype,
            effectiveRequester,
            callReason,
            station,
            null,
            toPay ? prototype.Price : 0,
            pinpointerTarget,
            suppressAnnouncements);

        return true;
    }

    public bool TrySubmitConsoleRequest(
        ProtoId<ErtTeamPrototype> team,
        EntityUid? station,
        string requestedByName,
        EntityUid? consoleUid,
        out string? reason,
        string? callReason = null)
    {
        reason = Loc.GetString("ert-response-call-submitted");

        if (!TryReserveCall(team, station, out var prototype, out reason, true, true))
            return false;

        var requestId = _nextRequestId++;
        var decisionWindow = new TimedWindow(PendingRequestLifetime, PendingRequestLifetime);
        _timedWindowSystem.Reset(decisionWindow);

        _pendingRequests[requestId] = new PendingErtRequestData
        {
            RequestId = requestId,
            RequestedTeamId = team,
            DecisionWindow = decisionWindow,
            RequestedByName = requestedByName,
            CallReason = callReason,
            StationUid = station,
            ConsoleUid = consoleUid,
            ReservedPrice = prototype.Price,
        };

        AnnounceConsoleRequestReceived();
        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"ERT request #{requestId} for '{team}' submitted by '{requestedByName}'");
        _chatManager.SendAdminAlert($"Поступила заявка ОБР #{requestId}: '{prototype.Name}' от '{requestedByName}'.");

        return true;
    }

    public EntityUid? EnsureErtTeam(
        ProtoId<ErtTeamPrototype> team,
        string? callReason = null,
        EntityUid? pinpointerTarget = null,
        bool announce = true)
    {
        if (!_prototypeManager.TryIndex(team, out var prototype))
            return null;

        var ruleEntity = Spawn(prototype.ErtRule, MapCoordinates.Nullspace);
        var ruleComp = EnsureComp<ErtSpawnRuleComponent>(ruleEntity);

        if (!_prototypeManager.TryIndex(ruleComp.Shuttle, out var shuttle))
        {
            QueueDel(ruleEntity);
            return null;
        }

        var opts = DeserializationOptions.Default with { InitializeMaps = true };
        var mapEntity = _mapSystem.CreateMap(out var mapId);
        if (!_mapLoaderSystem.TryLoadGrid(mapId, shuttle.Path, out var grid, opts))
        {
            Log.Error($"Failed to load grid from {shuttle.Path}!");
            QueueDel(mapEntity);
            QueueDel(ruleEntity);
            return null;
        }

        var grids = new List<EntityUid> { grid.Value };

        ruleComp.Team = team;
        ruleComp.ShuttleEntity = grid.Value;
        ruleComp.CallReason = callReason;
        ruleComp.PinpointerTarget = pinpointerTarget;
        ruleComp.SuppressAnnouncements = !announce;

        var addedEvent = new GameRuleAddedEvent(ruleEntity, prototype.ErtRule);
        RaiseLocalEvent(ruleEntity, ref addedEvent, true);

        _gameTicker.StartGameRule(ruleEntity);

        var loadedEvent = new RuleLoadedGridsEvent(mapId, grids);
        RaiseLocalEvent(ruleEntity, ref loadedEvent);

        if (announce && !string.IsNullOrEmpty(prototype.StartAnnouncement))
        {
            _chatSystem.DispatchGlobalAnnouncement(
                message: Loc.GetString(prototype.StartAnnouncement),
                sender: string.IsNullOrEmpty(prototype.Sender)
                    ? Loc.GetString("chat-manager-sender-announcement")
                    : Loc.GetString(prototype.Sender),
                colorOverride: Color.FromHex("#1d8bad"),
                announcementSound: prototype.StartAudio,
                playSound: true,
                usePresetTTS: true,
                languageId: LanguageSystem.DefaultLanguageId);
        }

        return ruleEntity;
    }

    public int GetBalance()
    {
        return _points;
    }

    private bool TryReserveCall(
        ProtoId<ErtTeamPrototype> team,
        EntityUid? station,
        out ErtTeamPrototype prototype,
        out string? reason,
        bool toPay,
        bool needCooldown,
        EntityUid? pinpointerTarget = null)
    {
        reason = "Вызван успешно.";

        if (!_prototypeManager.TryIndex(team, out var indexedPrototype))
        {
            prototype = default!;
            reason = Loc.GetString("ert-call-fail-prototype-missing");
            return false;
        }

        prototype = indexedPrototype;

        if (HasActiveRequest(prototype, pinpointerTarget))
        {
            reason = Loc.GetString(prototype.DispatchPerTarget && pinpointerTarget != null
                ? "ert-call-fail-patient-assigned"
                : "ert-call-fail-already-waiting");
            return false;
        }

        if (station != null && prototype.CodeBlackList != null)
        {
            var level = _alertLevelSystem.GetLevel(station.Value);
            if (prototype.CodeBlackList.Contains(level))
            {
                reason = Loc.GetString("ert-call-fail-code-blacklist", ("level", level));
                return false;
            }
        }

        if (needCooldown)
        {
            if (_coolDown != null && !_timedWindowSystem.IsExpired(_coolDown))
            {
                reason = Loc.GetString("ert-call-fail-cooldown", ("seconds", _timedWindowSystem.GetSecondsRemaining(_coolDown)));
                return false;
            }
        }

        if (toPay)
        {
            if (prototype.Price > _points)
            {
                reason = Loc.GetString(
                    "ert-call-fail-not-enough-points",
                    ("price", prototype.Price),
                    ("balance", _points));
                return false;
            }

            _points -= prototype.Price;
        }

        if (needCooldown)
        {
            var cooldown = prototype.Cooldown.Clone();
            _timedWindowSystem.Reset(cooldown);
            _coolDown = cooldown;
        }

        return true;
    }

    private bool HasActiveRequest(ErtTeamPrototype team, EntityUid? target = null, int? ignoredApprovedRequestId = null)
    {
        foreach (var pending in _pendingRequests.Values)
        {
            if (pending.RequestedTeamId == team.ID && (!team.DispatchPerTarget || target == null))
                return true;
        }

        foreach (var manualApproved in _manualApprovedRequests.Values)
        {
            if (manualApproved.TeamId == team.ID && (!team.DispatchPerTarget || manualApproved.PinpointerTarget == target))
                return true;
        }

        foreach (var approved in _approvedRequests.Values)
        {
            if (ignoredApprovedRequestId != null && approved.RequestId == ignoredApprovedRequestId.Value)
                continue;

            if (approved.TeamId == team.ID && (!team.DispatchPerTarget || approved.PinpointerTarget == target))
                return true;
        }

        if (!team.DispatchPerTarget)
            return false;

        var query = EntityQueryEnumerator<ErtSpawnRuleComponent>();
        while (query.MoveNext(out _, out var rule))
        {
            if (rule.Team == team.ID && rule.PinpointerTarget == target &&
                rule.ShuttleEntity is { } shuttle && !TerminatingOrDeleted(shuttle))
                return true;
        }

        return false;
    }

    private void QueueApprovedRequest(
        PendingErtRequestData request,
        ErtTeamPrototype prototype,
        bool suppressAnnouncements = false)
    {
        _approvedRequests[request.RequestId] = CreateApprovedRequest(
            request.RequestId,
            request.RequestedTeamId,
            prototype,
            request.RequestedByName,
            request.CallReason,
            request.StationUid,
            request.ConsoleUid,
            request.ReservedPrice,
            null,
            suppressAnnouncements);
    }

    private void QueueApprovedRequest(ManualApprovedErtRequestData request, ErtTeamPrototype prototype)
    {
        _approvedRequests[request.RequestId] = CreateApprovedRequest(
            request.RequestId,
            request.TeamId,
            prototype,
            request.RequestedByName,
            request.CallReason,
            request.StationUid,
            request.ConsoleUid,
            request.ReservedPrice,
            request.PinpointerTarget,
            request.SuppressAnnouncements);
    }

    private ApprovedErtRequestData CreateApprovedRequest(
        int requestId,
        ProtoId<ErtTeamPrototype> teamId,
        ErtTeamPrototype prototype,
        string requestedByName,
        string? callReason,
        EntityUid? stationUid,
        EntityUid? consoleUid,
        int reservedPrice,
        EntityUid? pinpointerTarget,
        bool suppressAnnouncements)
    {
        var window = prototype.TimeWindowToSpawn.Clone();
        _timedWindowSystem.Reset(window);

        return new ApprovedErtRequestData
        {
            RequestId = requestId,
            TeamId = teamId,
            Window = window,
            RequestedByName = requestedByName,
            CallReason = callReason,
            StationUid = stationUid,
            ConsoleUid = consoleUid,
            ReservedPrice = reservedPrice,
            PinpointerTarget = pinpointerTarget,
            SuppressAnnouncements = suppressAnnouncements,
        };
    }

    private void AnnounceApprovedRequest(ErtTeamPrototype prototype)
    {
        if (!prototype.AnnounceOnApproval)
            return;

        _chatSystem.DispatchGlobalAnnouncement(
            message: string.IsNullOrEmpty(prototype.Notification)
                ? Loc.GetString("ert-response-caused-messager", ("team", prototype.Name))
                : Loc.GetString(prototype.Notification),
            sender: string.IsNullOrEmpty(prototype.Sender)
                ? Loc.GetString("chat-manager-sender-announcement")
                : Loc.GetString(prototype.Sender),
            colorOverride: prototype.ApprovalColor ?? Color.FromHex("#1d8bad"),
            announcementSound: prototype.ApprovalAudio ?? DecisionSound,
            playSound: true,
            usePresetTTS: true,
            languageId: LanguageSystem.DefaultLanguageId);
    }

    private void AnnounceChangedApprovedTeam(ErtTeamPrototype prototype)
    {
        _chatSystem.DispatchGlobalAnnouncement(
            message: Loc.GetString("ert-response-team-changed-announcement", ("team", FormatTeamNameForAnnouncement(prototype))),
            sender: Loc.GetString("ert-response-cso-sender"),
            colorOverride: Color.FromHex("#1d8bad"),
            announcementSound: TeamChangedSound,
            playSound: true,
            usePresetTTS: true,
            languageId: LanguageSystem.DefaultLanguageId);
    }

    private static string FormatTeamNameForAnnouncement(ErtTeamPrototype prototype)
    {
        const string ertPrefix = "ОБР ";

        if (prototype.Name.StartsWith(ertPrefix, StringComparison.Ordinal))
            return $"{ertPrefix}\"{prototype.Name[ertPrefix.Length..]}\"";

        return prototype.Name;
    }

    private void AnnounceConsoleRequestReceived()
    {
        _chatSystem.DispatchGlobalAnnouncement(
            message: Loc.GetString("ert-console-request-submitted-announcement"),
            sender: Loc.GetString("ert-response-cso-sender"),
            colorOverride: Color.FromHex("#1d8bad"),
            announcementSound: RequestSound,
            playSound: true,
            usePresetTTS: true,
            languageId: LanguageSystem.DefaultLanguageId);
    }

    private void PlayGlobalSound(SoundSpecifier sound)
    {
        _audio.PlayGlobal(sound, Filter.Broadcast(), true);
    }
}

public sealed class WaitingSpeciesSettings
{
    public MapId MapId;
    public TimedWindow Window;
    public ProtoId<ErtTeamPrototype> TeamId;
    public EntityUid SpawnPoint;
    public bool SuppressAnnouncements;

    public WaitingSpeciesSettings(
        MapId mapId,
        TimedWindow window,
        ProtoId<ErtTeamPrototype> teamId,
        EntityUid spawnPoint,
        bool suppressAnnouncements)
    {
        MapId = mapId;
        Window = window;
        TeamId = teamId;
        SpawnPoint = spawnPoint;
        SuppressAnnouncements = suppressAnnouncements;
    }
}
