using Content.Client.Eui;
using Content.Shared.DeadSpace.Administration;
using Content.Shared.Eui;

namespace Content.Client.DeadSpace.Administration.UI;

public sealed class TimeTransferEui : BaseEui
{
    private readonly TimeTransferWindow _window;

    public TimeTransferEui()
    {
        _window = new TimeTransferWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.OnSubmit += (ckey, mode, overall, entries) => SendMessage(new TimeTransferEuiMessage(ckey, mode, overall, entries));
    }

    public override void Opened()
    {
        base.Opened();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not TimeTransferEuiState s)
            return;

        _window.SetStatus(s.Status, s.Success);
    }
}