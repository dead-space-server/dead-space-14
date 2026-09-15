// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.ERT;

namespace Content.Client.DeadSpace.ERT;

public sealed class ErtResponseSystem : SharedErtResponseSystem
{
    public ErtAdminStateResponse? LastState { get; private set; }

    public event Action? OnStateUpdated;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ErtAdminStateResponse>(OnErtAdminStateResponse);
        SubscribeNetworkEvent<ErtAdminActionResult>(OnErtAdminActionResult);
    }

    private void OnErtAdminStateResponse(ErtAdminStateResponse msg, EntitySessionEventArgs args)
    {
        LastState = msg;
        OnStateUpdated?.Invoke();
    }

    private void OnErtAdminActionResult(ErtAdminActionResult msg, EntitySessionEventArgs args)
    {
        Log.Warning(msg.Message);
    }

    public void RequestAdminState()
    {
        RaiseNetworkEvent(new RequestErtAdminStateMessage());
    }

    public void AdminSetPoints(int points)
    {
        RaiseNetworkEvent(new AdminSetPointsMessage(points));
    }

    public void AdminSetCooldown(int seconds)
    {
        RaiseNetworkEvent(new AdminSetCooldownMessage(seconds));
    }

    public void AdminSetReason(int requestId, string reason)
    {
        RaiseNetworkEvent(new AdminSetErtReasonMessage(requestId, reason));
    }

    public void AdminModifyEntry(int requestId, int seconds)
    {
        RaiseNetworkEvent(new AdminModifyErtEntryMessage(requestId, seconds));
    }

    public void AdminDeleteErt(int requestId)
    {
        RaiseNetworkEvent(new AdminDeleteErtMessage(requestId));
    }

    public void AdminRejectRequest(int requestId, bool sendNotification = true)
    {
        RaiseNetworkEvent(new AdminRejectErtRequestMessage(requestId, sendNotification));
    }

    public void AdminApproveRequestManual(int requestId, bool sendNotification = true)
    {
        RaiseNetworkEvent(new AdminApproveErtRequestManualMessage(requestId, sendNotification));
    }

    public void AdminApproveRequestAuto(int requestId, bool sendNotification = true)
    {
        RaiseNetworkEvent(new AdminApproveErtRequestAutoMessage(requestId, sendNotification));
    }

    public void AdminSetApprovedTeam(int requestId, string protoId)
    {
        RaiseNetworkEvent(new AdminSetApprovedErtTeamMessage(requestId, protoId));
    }

    public void QueueAutoApprovedRequest(string protoId, string reason, bool sendNotification = true)
    {
        RaiseNetworkEvent(new AdminCallErtMessage(protoId, reason, sendNotification));
    }

    public void AdminSendErtNow(int requestId)
    {
        RaiseNetworkEvent(new AdminSendErtNowMessage(requestId));
    }

    public void AdminPromoteManualApprovedRequest(int requestId)
    {
        RaiseNetworkEvent(new AdminPromoteManualApprovedErtMessage(requestId));
    }

    public void AdminMoveApprovedRequestToManual(int requestId)
    {
        RaiseNetworkEvent(new AdminMoveApprovedErtToManualMessage(requestId));
    }
}
