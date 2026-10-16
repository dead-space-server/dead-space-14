// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.EUI;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Content.Shared.Eui;

namespace Content.Server.DeadSpace.Lavaland.ChromiteCaves;

/// <summary>"Climb out of the caves?"</summary>
public sealed class LavalandCaveExitEui : BaseEui
{
    private readonly LavalandChromiteCavesSystem _system;
    private readonly EntityUid _caves;
    private bool _handled;

    public LavalandCaveExitEui(LavalandChromiteCavesSystem system, EntityUid caves)
    {
        _system = system;
        _caves = caves;
    }

    public override void Opened()
    {
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        return new LavalandCaveExitEuiState();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (_handled || msg is not LavalandCaveExitResponseMessage response)
            return;

        _handled = true;
        _system.HandleExitResponse(Player, _caves, response.Accepted);
        Close();
    }

    public override void Closed()
    {
        base.Closed();
        if (!_handled)
            _system.HandleExitResponse(Player, _caves, false);
    }
}
