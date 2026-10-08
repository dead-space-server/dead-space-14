// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Ninja;

namespace Content.Client.DeadSpace.Ninja.UI;

public sealed class NinjaDisguiseBoundUserInterface : BoundUserInterface
{
    private NinjaDisguiseWindow? _window;

    public NinjaDisguiseBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = new NinjaDisguiseWindow();
        _window.OnClose += Close;

        _window.OnApplyEntry += index => SendMessage(new NinjaDisguiseApplyMessage(index));
        _window.OnResetPressed += () => SendMessage(new NinjaDisguiseResetMessage());

        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is NinjaDisguiseState castState)
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