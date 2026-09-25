using Content.Shared.DeadSpace.PatrolTablet;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.PatrolTablet;

[UsedImplicitly]
public sealed class PatrolTabletBoundUserInterface : BoundUserInterface
{
    private PatrolTabletWindow? _window;

    public PatrolTabletBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PatrolTabletWindow>();
        _window.OnRenameSquad += (squadId, newName) =>
            SendMessage(new PatrolTabletRenameSquadMessage(squadId, newName));
        _window.OnBulkAssignSquad += squadId =>
            SendMessage(new PatrolTabletBulkAssignSquadMessage(squadId));
        _window.OnClearList += () =>
            SendMessage(new PatrolTabletClearAllMessage());
        _window.OnClearSquad += squadId =>
            SendMessage(new PatrolTabletClearSquadMessage(squadId));
        _window.OnDeleteSquad += squadId =>
            SendMessage(new PatrolTabletDeleteSquadMessage(squadId));
        _window.OnCreateSquad += (name, iconId) =>
            SendMessage(new PatrolTabletCreateSquadMessage(name, iconId));
        _window.OnSendAnnouncement += (title, text) =>
            SendMessage(new PatrolTabletSendAnnouncementMessage(title, text));
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not PatrolTabletUpdateState tabletState)
            return;

        _window?.UpdateState(tabletState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }
}
