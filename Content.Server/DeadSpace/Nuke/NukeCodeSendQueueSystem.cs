// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Nuke;
using Content.Shared.Administration;
using Content.Shared.Administration.Logs;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.DeadSpace.ERT;
using Content.Shared.DeadSpace.Nuke;
using Content.Shared.DeadSpace.TimeWindow;
using Content.Shared.GameTicking;
using Content.Shared.Station.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Nuke;

public sealed class PendingNukeCodeRequestData
{
    public int RequestId { get; set; }
    public EntityUid StationUid { get; set; }
    public ProtoId<NukeCodeSendReasonPrototype> ReasonId { get; set; }
    public TimedWindow DecisionWindow { get; set; } = default!;
    public TimedWindow ReminderWindow { get; set; } = default!;
    public string RequestedByName { get; set; } = string.Empty;
    public bool Automatic { get; set; }
}

public sealed class NukeCodeSendQueueSystem : EntitySystem
{
    [Dependency] private readonly NukeCodePaperSystem _nukeCodePaper = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly TimedWindowSystem _timedWindowSystem = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;

    private static readonly TimeSpan DecisionLifetime = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ReminderInterval = TimeSpan.FromSeconds(30);

    private readonly Dictionary<int, PendingNukeCodeRequestData> _pendingRequests = new();
    private int _nextRequestId = 1;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RequestNukeCodesAdminStateMessage>(OnRequestAdminState);
        SubscribeNetworkEvent<AdminQueueNukeCodesMessage>(OnAdminQueueRequest);
        SubscribeNetworkEvent<AdminApproveNukeCodesMessage>(OnAdminApproveRequest);
        SubscribeNetworkEvent<AdminCancelNukeCodesMessage>(OnAdminCancelRequest);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        ICommonSession[]? decisionAdmins = null;
        foreach (var (requestId, request) in _pendingRequests.ToArray())
        {
            if (request.Automatic)
            {
                decisionAdmins ??= GetDecisionAdmins();
                if (decisionAdmins.Length == 0)
                {
                    CompleteRequest(
                        requestId,
                        Loc.GetString("nuke-codes-requester-auto-no-admin"),
                        out _);
                    continue;
                }

                if (_timedWindowSystem.IsExpired(request.ReminderWindow))
                {
                    SendDecisionReminder(request, decisionAdmins);
                    _timedWindowSystem.Reset(request.ReminderWindow);
                }

                continue;
            }

            if (!_timedWindowSystem.IsExpired(request.DecisionWindow))
                continue;

            CompleteRequest(requestId, Loc.GetString("nuke-codes-requester-auto-timeout"), out _);
        }
    }

    public bool TryQueueAdminRequest(
        EntityUid station,
        ProtoId<NukeCodeSendReasonPrototype> reasonId,
        string adminName,
        out string? result)
    {
        return TryQueueRequest(station, reasonId, adminName, out result, automatic: false);
    }

    public bool TryQueueAutomaticRequest(
        EntityUid station,
        ProtoId<NukeCodeSendReasonPrototype> reasonId,
        out string? result)
    {
        return TryQueueRequest(
            station,
            reasonId,
            Loc.GetString("nuke-codes-requester-system"),
            out result,
            automatic: true);
    }

    public bool TryApproveRequest(int requestId, string approvedByName, out string? result)
    {
        return CompleteRequest(requestId, approvedByName, out result);
    }

    public bool TryCancelRequest(int requestId, string cancelledByName, out string? result)
    {
        if (!_pendingRequests.Remove(requestId, out var request))
        {
            result = Loc.GetString("nuke-codes-admin-request-missing");
            return false;
        }

        _adminLogger.Add(
            LogType.Action,
            LogImpact.Medium,
            $"Admin {cancelledByName} cancelled nuke codes request #{requestId}");

        _chatManager.SendAdminAlert(Loc.GetString(
            "nuke-codes-admin-alert-cancelled",
            ("admin", cancelledByName),
            ("requestId", requestId),
            ("station", Name(request.StationUid))));

        result = Loc.GetString("nuke-codes-admin-cancelled");
        return true;
    }

    private bool TryQueueRequest(
        EntityUid station,
        ProtoId<NukeCodeSendReasonPrototype> reasonId,
        string requestedByName,
        out string? result,
        bool automatic)
    {
        result = Loc.GetString("nuke-codes-admin-queued");

        if (!HasComp<StationDataComponent>(station))
        {
            result = Loc.GetString("nuke-codes-admin-invalid-station");
            return false;
        }

        if (!_prototypeManager.TryIndex(reasonId, out var reason))
        {
            result = Loc.GetString("nuke-codes-admin-invalid-reason");
            return false;
        }

        foreach (var pending in _pendingRequests.Values)
        {
            if (pending.StationUid != station)
                continue;

            result = Loc.GetString("nuke-codes-admin-already-pending");
            return false;
        }

        var requestId = _nextRequestId++;
        var decisionWindow = new TimedWindow(DecisionLifetime, DecisionLifetime);
        var reminderWindow = new TimedWindow(ReminderInterval, ReminderInterval);
        _timedWindowSystem.Reset(decisionWindow);
        _timedWindowSystem.Reset(reminderWindow);

        var request = new PendingNukeCodeRequestData
        {
            RequestId = requestId,
            StationUid = station,
            ReasonId = reasonId,
            DecisionWindow = decisionWindow,
            ReminderWindow = reminderWindow,
            RequestedByName = requestedByName,
            Automatic = automatic,
        };
        _pendingRequests[requestId] = request;

        var reasonName = Loc.GetString(reason.Name);
        var stationName = Name(station);

        _adminLogger.Add(
            LogType.Action,
            LogImpact.Medium,
            $"Nuke codes request #{requestId} queued for station '{stationName}' with reason '{reasonId}' by '{requestedByName}'");

        if (automatic)
        {
            var decisionAdmins = GetDecisionAdmins();
            if (decisionAdmins.Length == 0)
            {
                return CompleteRequest(
                    requestId,
                    Loc.GetString("nuke-codes-requester-auto-no-admin"),
                    out result);
            }

            SendDecisionReminder(request, decisionAdmins);
        }
        else
        {
            _chatManager.SendAdminAlert(Loc.GetString(
                "nuke-codes-admin-alert-admin-queued",
                ("admin", requestedByName),
                ("requestId", requestId),
                ("station", stationName),
                ("reason", reasonName)));
        }

        return true;
    }

    private void OnRequestAdminState(RequestNukeCodesAdminStateMessage msg, EntitySessionEventArgs args)
    {
        if (!TryAuthorizeAdmin(args))
            return;

        RaiseNetworkEvent(GetAdminStateSnapshot(), args.SenderSession.Channel);
    }

    private void OnAdminQueueRequest(AdminQueueNukeCodesMessage msg, EntitySessionEventArgs args)
    {
        if (!TryAuthorizeAdmin(args))
            return;

        if (!TryGetEntity(msg.Station, out var station))
        {
            RaiseNetworkEvent(
                new NukeCodesAdminActionResult(false, Loc.GetString("nuke-codes-admin-invalid-station")),
                args.SenderSession.Channel);
            return;
        }

        var success = TryQueueAdminRequest(
            station.Value,
            new ProtoId<NukeCodeSendReasonPrototype>(msg.ReasonProtoId),
            args.SenderSession.Name,
            out var result);

        RaiseNetworkEvent(
            new NukeCodesAdminActionResult(success, result ?? string.Empty),
            args.SenderSession.Channel);
    }

    private void OnAdminApproveRequest(AdminApproveNukeCodesMessage msg, EntitySessionEventArgs args)
    {
        if (!TryAuthorizeAdmin(args))
            return;

        var success = TryApproveRequest(msg.RequestId, args.SenderSession.Name, out var result);
        RaiseNetworkEvent(
            new NukeCodesAdminActionResult(success, result ?? string.Empty),
            args.SenderSession.Channel);
    }

    private void OnAdminCancelRequest(AdminCancelNukeCodesMessage msg, EntitySessionEventArgs args)
    {
        if (!TryAuthorizeAdmin(args))
            return;

        var success = TryCancelRequest(msg.RequestId, args.SenderSession.Name, out var result);
        RaiseNetworkEvent(
            new NukeCodesAdminActionResult(success, result ?? string.Empty),
            args.SenderSession.Channel);
    }

    public NukeCodesAdminStateResponse GetAdminStateSnapshot()
    {
        var stations = new List<NukeCodesStationEntry>();
        var stationQuery = EntityQueryEnumerator<StationDataComponent, MetaDataComponent>();
        while (stationQuery.MoveNext(out var stationUid, out _, out var metadata))
        {
            stations.Add(new NukeCodesStationEntry(GetNetEntity(stationUid), metadata.EntityName));
        }

        var reasons = _prototypeManager.EnumeratePrototypes<NukeCodeSendReasonPrototype>()
            .Select(proto => new NukeCodeSendReasonEntry(proto.ID, Loc.GetString(proto.Name)))
            .OrderBy(entry => entry.Name)
            .ToArray();

        var pendingEntries = new List<NukeCodesPendingRequestEntry>();
        foreach (var data in _pendingRequests.Values)
        {
            if (!_prototypeManager.TryIndex(data.ReasonId, out var reason) ||
                !Exists(data.StationUid))
            {
                continue;
            }

            pendingEntries.Add(new NukeCodesPendingRequestEntry(
                data.RequestId,
                GetNetEntity(data.StationUid),
                Name(data.StationUid),
                data.ReasonId.ToString(),
                Loc.GetString(reason.Name),
                _timedWindowSystem.GetSecondsRemaining(
                    data.Automatic ? data.ReminderWindow : data.DecisionWindow),
                data.RequestedByName,
                data.Automatic));
        }

        return new NukeCodesAdminStateResponse(
            stations.OrderBy(entry => entry.Name).ToArray(),
            reasons,
            pendingEntries
                .OrderByDescending(entry => entry.AwaitingAdminDecision)
                .ThenBy(entry => entry.SecondsRemaining)
                .ToArray());
    }

    private bool TryAuthorizeAdmin(EntitySessionEventArgs args)
    {
        if (CanManageNukeCodes(args.SenderSession))
            return true;

        RaiseNetworkEvent(
            new NukeCodesAdminActionResult(false, Loc.GetString("nuke-codes-admin-permission-denied")),
            args.SenderSession.Channel);

        return false;
    }

    /// <summary>
    /// Mapping grants access to the ertui panel; Fun grants access to its nuke-code actions.
    /// Automatic dispatches wait only for an active administrator who can do both.
    /// </summary>
    private bool CanManageNukeCodes(ICommonSession admin)
    {
        return _adminManager.HasAdminFlag(admin, AdminFlags.Mapping) &&
               _adminManager.HasAdminFlag(admin, AdminFlags.Fun);
    }

    private ICommonSession[] GetDecisionAdmins()
    {
        return _adminManager.ActiveAdmins
            .Where(CanManageNukeCodes)
            .ToArray();
    }

    private void SendDecisionReminder(
        PendingNukeCodeRequestData request,
        IReadOnlyCollection<ICommonSession> decisionAdmins)
    {
        if (!_prototypeManager.TryIndex(request.ReasonId, out var reason) ||
            !Exists(request.StationUid))
        {
            return;
        }

        var message = Loc.GetString(
            "nuke-codes-admin-decision-reminder",
            ("requestId", request.RequestId),
            ("station", Name(request.StationUid)),
            ("reason", Loc.GetString(reason.Name)));

        _chatManager.ChatMessageToMany(
            ChatChannel.AdminAlert,
            message,
            message,
            default,
            hideChat: false,
            recordReplay: true,
            clients: decisionAdmins.Select(admin => admin.Channel),
            colorOverride: Color.Gold);
    }

    private bool CompleteRequest(int requestId, string approvedByName, out string? result)
    {
        if (!_pendingRequests.Remove(requestId, out var request))
        {
            result = Loc.GetString("nuke-codes-admin-request-missing");
            return false;
        }

        if (!Exists(request.StationUid) ||
            !HasComp<StationDataComponent>(request.StationUid))
        {
            result = Loc.GetString("nuke-codes-admin-invalid-station");
            return false;
        }

        if (!_prototypeManager.TryIndex(request.ReasonId, out var reason))
        {
            result = Loc.GetString("nuke-codes-admin-invalid-reason");
            return false;
        }

        var wasSent = _nukeCodePaper.SendNukeCodes(request.StationUid, reason.Announcement);
        result = wasSent
            ? Loc.GetString("nuke-codes-admin-sent")
            : Loc.GetString("nuke-codes-admin-send-failed");

        _adminLogger.Add(
            LogType.Action,
            LogImpact.High,
            $"Nuke codes request #{requestId} approved by '{approvedByName}' for station '{Name(request.StationUid)}' with reason '{request.ReasonId}'. Sent: {wasSent}");

        _chatManager.SendAdminAlert(Loc.GetString(
            wasSent ? "nuke-codes-admin-alert-sent" : "nuke-codes-admin-alert-send-failed",
            ("admin", approvedByName),
            ("requestId", requestId),
            ("station", Name(request.StationUid)),
            ("reason", Loc.GetString(reason.Name))));

        return wasSent;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _pendingRequests.Clear();
        _nextRequestId = 1;
    }
}
