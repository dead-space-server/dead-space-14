using Content.Client.UserInterface;
using Content.Client.DeadSpace.PdaPainter.UI;
using Content.Shared.DeadSpace.PdaPainter;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.PdaPainter;

public sealed class PdaPainterBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private PdaPainterWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PdaPainterWindow>();
        _window.OnPixelsDrawn += OnPixelsDrawn;
        _window.OnSavePressed += templateId => SendMessage(new PdaPainterSaveMessage(templateId));
        _window.OnResetPressed += () => SendMessage(new PdaPainterResetMessage());
    }

    private void OnPixelsDrawn(List<int> indices, List<int> colors)
    {
        SendMessage(new PdaPainterDrawMessage(indices, colors));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window != null && state is PdaPainterBoundUserInterfaceState painterState)
            _window.UpdateState(painterState);
    }
}
