using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Administration;

[Serializable, NetSerializable]
public sealed class TimeTransferEuiState : EuiStateBase
{
    public readonly string? Status;
    public readonly bool Success;

    public TimeTransferEuiState(string? status = null, bool success = false)
    {
        Status = status;
        Success = success;
    }
}

[Serializable, NetSerializable]
public readonly record struct TimeTransferEntry(string Tracker, int Minutes);

[Serializable, NetSerializable]
public sealed class TimeTransferEuiMessage : EuiMessageBase
{
    public readonly string Ckey;
    public readonly TimeTransferMode Mode;
    public readonly int? OverallMinutes;
    public readonly List<TimeTransferEntry> Entries;

    public TimeTransferEuiMessage(string ckey, TimeTransferMode mode, int? overallMinutes, List<TimeTransferEntry> entries)
    {
        Ckey = ckey;
        Mode = mode;
        OverallMinutes = overallMinutes;
        Entries = entries;
    }
}

[Serializable, NetSerializable]
public enum TimeTransferMode : byte
{
    Add,
    Set,
}