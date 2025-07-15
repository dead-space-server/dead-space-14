using Content.Shared.Silicons.Borgs.Components;
using Robust.Client.UserInterface;

namespace Content.Client.Silicons.Borgs;

public sealed class BorgSelectTypeUserInterface : BoundUserInterface
{
    private BorgSelectTypeMenu? _menu;

    public BorgSelectTypeUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
            // DS14-start Changed for Taipan borgs
        _menu = new BorgSelectTypeMenu(Owner);
        _menu.ConfirmedBorgType += prototype => SendPredictedMessage(new BorgSelectTypeMessage(prototype));
        _menu.OpenCentered();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            if (_menu != null)
            {
                _menu.Dispose();
                _menu = null;
            }
        }
        // DS14-end
    }
}
