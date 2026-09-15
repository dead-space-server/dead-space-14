// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Eui;
using Content.Shared.DeadSpace.NewLife;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client.DeadSpace.NewLife;

[UsedImplicitly]
public sealed class NewLifeEui : BaseEui
{
    private readonly NewLifeWindow _window = new();
    private bool _closed;

    public NewLifeEui()
    {
        _window.ConfirmButton.OnPressed += _ =>
        {
            _window.SetSubmitting();
            SendMessage(new NewLifeConfirmMessage());
        };
        _window.CancelButton.OnPressed += _ => _window.Close();
        _window.OnClose += () =>
        {
            if (_closed)
                return;
            _closed = true;
            SendMessage(new CloseEuiMessage());
        };
    }

    public override void Opened() => _window.OpenCentered();

    public override void HandleState(EuiStateBase state)
    {
        if (state is NewLifeEuiState newLife)
            _window.UpdateState(newLife);
    }

    public override void Closed()
    {
        _closed = true;
        _window.Close();
    }
}
