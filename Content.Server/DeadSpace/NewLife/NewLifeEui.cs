// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.EUI;
using Content.Shared.DeadSpace.NewLife;
using Content.Shared.Eui;

namespace Content.Server.DeadSpace.NewLife;

public sealed class NewLifeEui(NewLifeSystem system, TimeSpan confirmAt) : BaseEui
{
    public TimeSpan ConfirmAt { get; } = confirmAt;

    public override void Opened()
    {
        base.Opened();
        StateDirty();
    }

    public override EuiStateBase GetNewState() => new NewLifeEuiState(system.GetState(Player), ConfirmAt);

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (IsShutDown || msg is not NewLifeConfirmMessage)
            return;
        if (!system.TryReturnToLobby(Player, this))
            StateDirty();
    }

    public override void Closed()
    {
        base.Closed();
        system.OnWindowClosed(Player, this);
    }
}
