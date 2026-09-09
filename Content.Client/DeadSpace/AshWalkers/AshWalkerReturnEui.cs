using Content.Client.Eui;
using Content.Shared.DeadSpace.UI;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client.DeadSpace.AshWalkers;

[UsedImplicitly]
public sealed class AshWalkerReturnEui : BaseEui
{
    private readonly AshWalkerReturnWindow _window = new();

    public AshWalkerReturnEui()
    {
        _window.YesButton.OnPressed += _ => SendMessage(new YesNoChoiceMessage(YesNoUiButton.Yes));
        _window.NoButton.OnPressed += _ => SendMessage(new YesNoChoiceMessage(YesNoUiButton.No));
        _window.OnClose += () => SendMessage(new YesNoChoiceMessage(YesNoUiButton.No));
    }

    public override void Opened() => _window.OpenCentered();

    public override void HandleState(EuiStateBase state)
    {
        if (state is not YesNoEuiState prompt)
            return;
        _window.Title = prompt.Title;
        _window.MessageLabel.SetMessage(prompt.Text);
    }

    public override void Closed() => _window.Close();
}
