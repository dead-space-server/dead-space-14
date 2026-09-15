using Content.Server.EUI;
using Content.Shared.DeadSpace.Prison;
using Content.Shared.Eui;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.Prison;

public sealed class PrisonFactionEui : BaseEui
{
    private readonly PrisonSystem _prison;
    private readonly ICommonSession _session;

    public PrisonFactionEui(PrisonSystem prison, ICommonSession session)
    {
        _prison = prison;
        _session = session;
    }

    public override EuiStateBase GetNewState()
    {
        return _prison.GetFactionEuiState(_session);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (IsShutDown || msg is not PrisonFactionSelectedMessage selected)
            return;

        if (_prison.TrySelectFaction(_session, selected.FactionId) && !IsShutDown)
            Close();
    }

    public override void Opened()
    {
        base.Opened();
        StateDirty();
    }

    public override void Closed()
    {
        base.Closed();
        _prison.OnFactionEuiClosed(_session, this);
    }
}
