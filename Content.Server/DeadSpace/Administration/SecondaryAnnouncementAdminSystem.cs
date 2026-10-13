using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Shared.Administration;
using Content.Shared.DeadSpace.Administration.Events;
using Content.Shared.GameTicking;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.Administration;

/// <summary>
/// Runtime switch for targeted secondary announcements.
/// The state is intentionally not persisted and resets to enabled on round restart.
/// </summary>
public sealed class SecondaryAnnouncementAdminSystem : EntitySystem
{
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    public bool Enabled { get; private set; } = true;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<SecondaryAnnouncementAdminStateRequestEvent>(OnStateRequest);
        SubscribeNetworkEvent<SecondaryAnnouncementAdminSetStateEvent>(OnSetState);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnStateRequest(
        SecondaryAnnouncementAdminStateRequestEvent ev,
        EntitySessionEventArgs args)
    {
        if (!HasRoundPermission(args.SenderSession))
            return;

        RaiseNetworkEvent(
            new SecondaryAnnouncementAdminStateChangedEvent(Enabled),
            args.SenderSession);
    }

    private void OnSetState(
        SecondaryAnnouncementAdminSetStateEvent ev,
        EntitySessionEventArgs args)
    {
        if (!HasRoundPermission(args.SenderSession))
            return;

        if (Enabled == ev.Enabled)
        {
            RaiseNetworkEvent(
                new SecondaryAnnouncementAdminStateChangedEvent(Enabled),
                args.SenderSession);
            return;
        }

        Enabled = ev.Enabled;
        BroadcastState();

        var state = Enabled ? "enabled" : "disabled";
        _chatManager.SendAdminAnnouncement(
            $"{args.SenderSession.Name} {state} secondary announcements.",
            flagBlacklist: null,
            flagWhitelist: AdminFlags.Moderator);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        if (Enabled)
            return;

        Enabled = true;
        BroadcastState();
    }

    private bool HasRoundPermission(ICommonSession session)
    {
        return _adminManager.GetAdminData(session)?.HasFlag(AdminFlags.Round) == true;
    }

    private void BroadcastState()
    {
        var state = new SecondaryAnnouncementAdminStateChangedEvent(Enabled);

        foreach (var admin in _adminManager.ActiveAdmins)
        {
            if (_adminManager.GetAdminData(admin)?.HasFlag(AdminFlags.Round) != true)
                continue;

            RaiseNetworkEvent(state, admin);
        }
    }
}
