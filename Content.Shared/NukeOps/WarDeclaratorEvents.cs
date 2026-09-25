using Robust.Shared.Serialization;

namespace Content.Shared.NukeOps;

[Serializable, NetSerializable]
public enum WarDeclaratorUiKey
{
    Key,
}

public enum WarConditionStatus : byte
{
    WarReady,
    YesWar,
    NoWarUnknown,
    // NoWarTimeout, // DS14 no stealth nuke
    NoWarSmallCrew,
    // NoWarShuttleDeparted // DS14 no stealth nuke
}

[Serializable, NetSerializable]
public sealed class WarDeclaratorBoundUserInterfaceState : BoundUserInterfaceState
{
    public WarConditionStatus? Status;
    public TimeSpan ShuttleDisabledTime;
    public TimeSpan EndTime;
    // DS14-start
    public bool SecondaryAnnouncementEnabled;
    // DS14-end

    public WarDeclaratorBoundUserInterfaceState(
        WarConditionStatus? status,
        TimeSpan endTime,
        TimeSpan shuttleDisabledTime,
        // DS14-start
        bool secondaryAnnouncementEnabled
        // DS14-end
        )
    {
        Status = status;
        EndTime = endTime;
        ShuttleDisabledTime = shuttleDisabledTime;
        // DS14-start
        SecondaryAnnouncementEnabled = secondaryAnnouncementEnabled;
        // DS14-end
    }

}

[Serializable, NetSerializable]
public sealed class WarDeclaratorActivateMessage : BoundUserInterfaceMessage
{
    public string Message { get; }

    public WarDeclaratorActivateMessage(string msg)
    {
        Message = msg;
    }
}

// DS14-start
[Serializable, NetSerializable]
public sealed class WarDeclaratorSecondaryAnnouncementMessage(string title, string text)
    : BoundUserInterfaceMessage
{
    public string Title { get; } = title;
    public string Text { get; } = text;
}
// DS14-end
