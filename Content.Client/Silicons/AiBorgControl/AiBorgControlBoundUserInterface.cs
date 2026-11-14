using Content.Shared.Silicons.AiBorgControl;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Silicons.AiBorgControl;

[UsedImplicitly]
public sealed class AiBorgControlBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private AiBorgControlWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<AiBorgControlWindow>();
        _window.OnTake += borg => SendMessage(new AiBorgControlTakeMessage(borg));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is AiBorgControlState s)
            _window?.Update(s);
    }
}
