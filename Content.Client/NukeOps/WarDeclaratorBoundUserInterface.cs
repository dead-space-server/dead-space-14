using Content.Client.DeadSpace.NukeOps.UI; // DS14
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.NukeOps;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;

namespace Content.Client.NukeOps;

[UsedImplicitly]
public sealed class WarDeclaratorBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    // DS14-start
    [ViewVariables]
    private WarDeclaratorCommsMenu? _window;
    // DS14-end

    public WarDeclaratorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) {}

    protected override void Open()
    {
        base.Open();

        // DS14-start
        _window = this.CreateWindow<WarDeclaratorCommsMenu>();
        _window.OnActivated += OnWarDeclaratorActivated;
        _window.OnSecondaryAnnouncement += OnSecondaryAnnouncement;
        // DS14-end
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not WarDeclaratorBoundUserInterfaceState cast)
            return;

        _window.UpdateState(cast);
    }

    private void OnWarDeclaratorActivated(string message)
    {
        var maxLength = _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength);
        var msg = SharedChatSystem.SanitizeAnnouncement(message, maxLength);
        SendMessage(new WarDeclaratorActivateMessage(msg));
    }

    // DS14-start
    private void OnSecondaryAnnouncement(string title, string message)
    {
        var maxLength = _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength);

        title = SharedChatSystem.SanitizeAnnouncement(title, maxLength);
        message = SharedChatSystem.SanitizeAnnouncement(message, maxLength);

        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(message))
            return;

        SendMessage(new WarDeclaratorSecondaryAnnouncementMessage(title, message));
    }
    // DS14-end
}
