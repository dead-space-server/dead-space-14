using Content.Shared.DeadSpace.Administration.Events;

namespace Content.Client.DeadSpace.Administration;

public sealed class SecondaryAnnouncementAdminSystem : EntitySystem
{
    public event Action<bool>? StateChanged;

    public bool? Enabled { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SecondaryAnnouncementAdminStateChangedEvent>(OnStateChanged);
    }

    public void RequestState()
    {
        RaiseNetworkEvent(new SecondaryAnnouncementAdminStateRequestEvent());
    }

    public void SetEnabled(bool enabled)
    {
        RaiseNetworkEvent(new SecondaryAnnouncementAdminSetStateEvent(enabled));
    }

    private void OnStateChanged(SecondaryAnnouncementAdminStateChangedEvent ev)
    {
        Enabled = ev.Enabled;
        StateChanged?.Invoke(ev.Enabled);
    }
}
