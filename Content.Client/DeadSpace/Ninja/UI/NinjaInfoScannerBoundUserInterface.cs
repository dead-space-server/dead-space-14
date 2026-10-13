// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Ninja;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.Ninja.UI;

public sealed class NinjaInfoScannerBoundUserInterface : BoundUserInterface
{
    private NinjaInfoScannerWindow? _window;

    public NinjaInfoScannerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = new NinjaInfoScannerWindow();
        _window.OnClose += Close;

        _window.OnScanPressed += () => SendMessage(new NinjaInfoScannerScanMessage());
        _window.OnEjectPressed += () => SendMessage(new NinjaInfoScannerEjectMessage());
        _window.OnTeleportPressed += () => SendMessage(new NinjaInfoScannerTeleportMessage());

        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is NinjaInfoScannerState castState)
        {
            _window?.UpdateState(castState);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _window?.Close();
        }
    }
}