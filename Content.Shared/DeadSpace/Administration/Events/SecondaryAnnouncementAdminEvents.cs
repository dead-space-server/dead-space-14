using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Administration.Events;

[Serializable, NetSerializable]
public sealed class SecondaryAnnouncementAdminStateRequestEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class SecondaryAnnouncementAdminSetStateEvent(bool enabled) : EntityEventArgs
{
    public bool Enabled = enabled;
}

[Serializable, NetSerializable]
public sealed class SecondaryAnnouncementAdminStateChangedEvent(bool enabled) : EntityEventArgs
{
    public bool Enabled = enabled;
}
