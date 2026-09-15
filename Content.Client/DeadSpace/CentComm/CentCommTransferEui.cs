// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Eui;
using Content.Shared.DeadSpace.CentComm;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client.DeadSpace.CentComm;

[UsedImplicitly]
public sealed class CentCommTransferEui : BaseEui
{
    private readonly CentCommTransferWindow _window = new();

    public CentCommTransferEui()
    {
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.OnTransfer += SendMessage;
    }

    public override void Opened() => _window.OpenCentered();
    public override void Closed() => _window.Close();

    public override void HandleState(EuiStateBase state)
    {
        if (state is CentCommTransferState transfer)
            _window.SetState(transfer);
    }
}
