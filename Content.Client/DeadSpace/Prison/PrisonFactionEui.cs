using Content.Client.Eui;
using Content.Shared.DeadSpace.Prison;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client.DeadSpace.Prison;

[UsedImplicitly]
public sealed class PrisonFactionEui : BaseEui
{
    private readonly PrisonFactionWindow _window;
    private bool _serverClosing;

    public PrisonFactionEui()
    {
        _window = new PrisonFactionWindow();
        _window.OnClose += OnWindowClosed;
        _window.OnFactionConfirmed += faction => SendMessage(new PrisonFactionSelectedMessage(faction));
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _serverClosing = true;
        try
        {
            _window.Close();
        }
        finally
        {
            _serverClosing = false;
        }
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is PrisonFactionEuiState factionState)
            _window.UpdateState(factionState);
    }

    private void OnWindowClosed()
    {
        if (!_serverClosing)
            SendMessage(new CloseEuiMessage());
    }
}
