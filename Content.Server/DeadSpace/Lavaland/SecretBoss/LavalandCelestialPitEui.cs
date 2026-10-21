// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.EUI;
using Content.Shared.DeadSpace.Lavaland.SecretBoss;
using Content.Shared.Eui;

namespace Content.Server.DeadSpace.Lavaland.SecretBoss;

/// <summary>"You see something at the bottom. Jump?"</summary>
public sealed class LavalandCelestialPitEui : BaseEui
{
    private readonly LavalandSecretBossSystem _system;
    private readonly EntityUid _lavalandMap;
    private bool _handled;

    public LavalandCelestialPitEui(LavalandSecretBossSystem system, EntityUid lavalandMap)
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
        return new LavalandCelestialPitEuiState();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (_handled || msg is not LavalandCelestialPitResponseMessage response)
            return;

        _handled = true;
        _system.HandlePitResponse(Player, _lavalandMap, response.Accepted);
        Close();
    }

    public override void Closed()
    {
        base.Closed();
        if (!_handled)
            _system.HandlePromptClosed(Player, _lavalandMap);
    }
}
