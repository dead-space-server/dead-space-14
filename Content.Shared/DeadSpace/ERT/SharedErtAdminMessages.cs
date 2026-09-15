// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.ERT
{
    [Serializable, NetSerializable]
    public sealed class RequestErtAdminStateMessage : EntityEventArgs
    {
    }

    [Serializable, NetSerializable]
    public sealed class ErtPendingRequestEntry
    {
        public int RequestId { get; }
        public string ProtoId { get; }
        public string Name { get; }
        public int SecondsRemaining { get; }
        public int Price { get; }
        public string RequestedByName { get; }
        public string? CallReason { get; }

        public ErtPendingRequestEntry(
            int requestId,
            string protoId,
            string name,
            int secondsRemaining,
            int price,
            string requestedByName,
            string? callReason = null)
        {
            RequestId = requestId;
            ProtoId = protoId;
            Name = name;
            SecondsRemaining = secondsRemaining;
            Price = price;
            RequestedByName = requestedByName;
            CallReason = callReason;
        }
    }

    [Serializable, NetSerializable]
    public sealed class ErtApprovedRequestEntry
    {
        public int RequestId { get; }
        public string ProtoId { get; }
        public string Name { get; }
        public int SecondsRemaining { get; }
        public int Price { get; }
        public string RequestedByName { get; }
        public string? CallReason { get; }

        public ErtApprovedRequestEntry(
            int requestId,
            string protoId,
            string name,
            int secondsRemaining,
            int price,
            string requestedByName,
            string? callReason = null)
        {
            RequestId = requestId;
            ProtoId = protoId;
            Name = name;
            SecondsRemaining = secondsRemaining;
            Price = price;
            RequestedByName = requestedByName;
            CallReason = callReason;
        }
    }

    [Serializable, NetSerializable]
    public sealed class ErtManualApprovedRequestEntry
    {
        public int RequestId { get; }
        public string ProtoId { get; }
        public string Name { get; }
        public int Price { get; }
        public string RequestedByName { get; }
        public string? CallReason { get; }

        public ErtManualApprovedRequestEntry(
            int requestId,
            string protoId,
            string name,
            int price,
            string requestedByName,
            string? callReason = null)
        {
            RequestId = requestId;
            ProtoId = protoId;
            Name = name;
            Price = price;
            RequestedByName = requestedByName;
            CallReason = callReason;
        }
    }

    [Serializable, NetSerializable]
    public sealed class ErtAdminStateResponse : EntityEventArgs
    {
        public ErtPendingRequestEntry[] PendingRequests { get; }
        public ErtApprovedRequestEntry[] ApprovedRequests { get; }
        public ErtManualApprovedRequestEntry[] ManualApprovedRequests { get; }
        public int Points { get; }
        public int CooldownSeconds { get; }

        public ErtAdminStateResponse(
            ErtPendingRequestEntry[] pendingRequests,
            ErtApprovedRequestEntry[] approvedRequests,
            ErtManualApprovedRequestEntry[] manualApprovedRequests,
            int points,
            int cooldownSeconds)
        {
            PendingRequests = pendingRequests;
            ApprovedRequests = approvedRequests;
            ManualApprovedRequests = manualApprovedRequests;
            Points = points;
            CooldownSeconds = cooldownSeconds;
        }
    }

    [Serializable, NetSerializable]
    public sealed class AdminModifyErtEntryMessage : EntityEventArgs
    {
        public int RequestId { get; }
        public int Seconds { get; }

        public AdminModifyErtEntryMessage(int requestId, int seconds)
        {
            RequestId = requestId;
            Seconds = seconds;
        }
    }

    [Serializable, NetSerializable]
    public sealed class AdminDeleteErtMessage : EntityEventArgs
    {
        public int RequestId { get; }

        public AdminDeleteErtMessage(int requestId)
        {
            RequestId = requestId;
        }
    }

    [Serializable, NetSerializable]
    public sealed class AdminSetPointsMessage : EntityEventArgs
    {
        public int Points { get; }

        public AdminSetPointsMessage(int points)
        {
            Points = points;
        }
    }

    [Serializable, NetSerializable]
    public sealed class AdminSetCooldownMessage : EntityEventArgs
    {
        public int Seconds { get; }

        public AdminSetCooldownMessage(int seconds)
        {
            Seconds = seconds;
        }
    }

    [Serializable, NetSerializable]
    public sealed class AdminSetErtReasonMessage : EntityEventArgs
    {
        public int RequestId { get; }
        public string Reason { get; }

        public AdminSetErtReasonMessage(int requestId, string reason)
        {
            RequestId = requestId;
            Reason = reason;
        }
    }

    [Serializable, NetSerializable]
    public sealed class AdminCallErtMessage : EntityEventArgs
    {
        public string ProtoId { get; }
        public string Reason { get; }
        public bool SendNotification { get; }

        public AdminCallErtMessage(string protoId, string reason, bool sendNotification = true)
        {
            ProtoId = protoId;
            Reason = reason;
            SendNotification = sendNotification;
        }
    }

    [Serializable, NetSerializable]
    public sealed class AdminRejectErtRequestMessage : EntityEventArgs
    {
        public int RequestId { get; }
        public bool SendNotification { get; }

        public AdminRejectErtRequestMessage(int requestId, bool sendNotification = true)
        {
            RequestId = requestId;
            SendNotification = sendNotification;
        }
    }

    [Serializable, NetSerializable]
    public sealed class AdminApproveErtRequestManualMessage : EntityEventArgs
    {
        public int RequestId { get; }
        public bool SendNotification { get; }

        public AdminApproveErtRequestManualMessage(int requestId, bool sendNotification = true)
        {
            RequestId = requestId;
            SendNotification = sendNotification;
        }
    }

    [Serializable, NetSerializable]
    public sealed class AdminApproveErtRequestAutoMessage : EntityEventArgs
    {
        public int RequestId { get; }
        public bool SendNotification { get; }

        public AdminApproveErtRequestAutoMessage(int requestId, bool sendNotification = true)
        {
            RequestId = requestId;
            SendNotification = sendNotification;
        }
    }

    [Serializable, NetSerializable]
    public sealed class AdminSetApprovedErtTeamMessage : EntityEventArgs
    {
        public int RequestId { get; }
        public string ProtoId { get; }

        public AdminSetApprovedErtTeamMessage(int requestId, string protoId)
        {
            RequestId = requestId;
            ProtoId = protoId;
        }
    }

    [Serializable, NetSerializable]
    public sealed class AdminSendErtNowMessage : EntityEventArgs
    {
        public int RequestId { get; }

        public AdminSendErtNowMessage(int requestId)
        {
            RequestId = requestId;
        }
    }

    [Serializable, NetSerializable]
    public sealed class AdminPromoteManualApprovedErtMessage : EntityEventArgs
    {
        public int RequestId { get; }

        public AdminPromoteManualApprovedErtMessage(int requestId)
        {
            RequestId = requestId;
        }
    }

    [Serializable, NetSerializable]
    public sealed class AdminMoveApprovedErtToManualMessage : EntityEventArgs
    {
        public int RequestId { get; }

        public AdminMoveApprovedErtToManualMessage(int requestId)
        {
            RequestId = requestId;
        }
    }

    [Serializable, NetSerializable]
    public sealed class ErtAdminActionResult : EntityEventArgs
    {
        public bool Success { get; }
        public string Message { get; }

        public ErtAdminActionResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }
    }

    [Serializable, NetSerializable]
    public sealed class RequestNukeCodesAdminStateMessage : EntityEventArgs
    {
    }

    [Serializable, NetSerializable]
    public sealed class NukeCodesStationEntry
    {
        public NetEntity Station { get; }
        public string Name { get; }

        public NukeCodesStationEntry(NetEntity station, string name)
        {
            Station = station;
            Name = name;
        }
    }

    [Serializable, NetSerializable]
    public sealed class NukeCodeSendReasonEntry
    {
        public string ProtoId { get; }
        public string Name { get; }

        public NukeCodeSendReasonEntry(string protoId, string name)
        {
            ProtoId = protoId;
            Name = name;
        }
    }

    [Serializable, NetSerializable]
    public sealed class NukeCodesPendingRequestEntry
    {
        public int RequestId { get; }
        public NetEntity Station { get; }
        public string StationName { get; }
        public string ReasonProtoId { get; }
        public string ReasonName { get; }
        public int SecondsRemaining { get; }
        public string RequestedByName { get; }
        public bool AwaitingAdminDecision { get; }

        public NukeCodesPendingRequestEntry(
            int requestId,
            NetEntity station,
            string stationName,
            string reasonProtoId,
            string reasonName,
            int secondsRemaining,
            string requestedByName,
            bool awaitingAdminDecision)
        {
            RequestId = requestId;
            Station = station;
            StationName = stationName;
            ReasonProtoId = reasonProtoId;
            ReasonName = reasonName;
            SecondsRemaining = secondsRemaining;
            RequestedByName = requestedByName;
            AwaitingAdminDecision = awaitingAdminDecision;
        }
    }

    [Serializable, NetSerializable]
    public sealed class NukeCodesAdminStateResponse : EntityEventArgs
    {
        public NukeCodesStationEntry[] Stations { get; }
        public NukeCodeSendReasonEntry[] Reasons { get; }
        public NukeCodesPendingRequestEntry[] PendingRequests { get; }

        public NukeCodesAdminStateResponse(
            NukeCodesStationEntry[] stations,
            NukeCodeSendReasonEntry[] reasons,
            NukeCodesPendingRequestEntry[] pendingRequests)
        {
            Stations = stations;
            Reasons = reasons;
            PendingRequests = pendingRequests;
        }
    }

    [Serializable, NetSerializable]
    public sealed class AdminQueueNukeCodesMessage : EntityEventArgs
    {
        public NetEntity Station { get; }
        public string ReasonProtoId { get; }

        public AdminQueueNukeCodesMessage(NetEntity station, string reasonProtoId)
        {
            Station = station;
            ReasonProtoId = reasonProtoId;
        }
    }

    [Serializable, NetSerializable]
    public sealed class AdminApproveNukeCodesMessage : EntityEventArgs
    {
        public int RequestId { get; }

        public AdminApproveNukeCodesMessage(int requestId)
        {
            RequestId = requestId;
        }
    }

    [Serializable, NetSerializable]
    public sealed class AdminCancelNukeCodesMessage : EntityEventArgs
    {
        public int RequestId { get; }

        public AdminCancelNukeCodesMessage(int requestId)
        {
            RequestId = requestId;
        }
    }

    [Serializable, NetSerializable]
    public sealed class NukeCodesAdminActionResult : EntityEventArgs
    {
        public bool Success { get; }
        public string Message { get; }

        public NukeCodesAdminActionResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }
    }
}
