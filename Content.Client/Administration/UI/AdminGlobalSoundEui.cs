// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Client.Eui;
using Content.Shared.DeadSpace.Administration.Events;
using Content.Shared.Eui;

namespace Content.Client.Administration.UI;

public sealed class AdminGlobalSoundEui : BaseEui
{
    private readonly AdminGlobalSoundWindow _window = new();

    public AdminGlobalSoundEui()
    {
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.BrowseRequested += message => SendMessage(message);
        _window.PlayRequested += message => SendMessage(message);
        _window.ControlRequested += action =>
            SendMessage(new AdminGlobalSoundEuiMessage.Control { Action = action });
        _window.QueueAddRequested += message => SendMessage(message);
        _window.QueueRemoveRequested += message => SendMessage(message);
        _window.QueueClearRequested += message => SendMessage(message);
        _window.QueueSkipRequested += message => SendMessage(message);
        _window.QueueStopRequested += message => SendMessage(message);
        _window.FolderQueueRequested += message => SendMessage(message);
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is AdminGlobalSoundEuiState soundState)
            _window.UpdateState(soundState);
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _window.Close();
    }
}
