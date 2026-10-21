// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.EUI;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Content.Shared.Eui;

namespace Content.Server.DeadSpace.Lavaland.ChromiteCaves;

/// <summary>"Jump into the shimmering pit?"</summary>
public sealed class LavalandCavePitEui : BaseEui
{
    private readonly LavalandChromiteCavesSystem _system;
    private readonly EntityUid _lavalandMap;
    private bool _handled;

    public LavalandCavePitEui(LavalandChromiteCavesSystem system, EntityUid lavalandMap)
    {
        _system = system;
        _lavalandMap = lavalandMap;
    }

    public override void Opened()
    {
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        return new LavalandCavePitEuiState();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (_handled || msg is not LavalandCavePitResponseMessage response)
            return;

        _handled = true;
        _system.HandleJumpResponse(Player, _lavalandMap, response.Accepted);
        Close();
    }

    public override void Closed()
    {
        base.Closed();
        if (!_handled)
            _system.HandleJumpResponse(Player, _lavalandMap, false);
    }
}
