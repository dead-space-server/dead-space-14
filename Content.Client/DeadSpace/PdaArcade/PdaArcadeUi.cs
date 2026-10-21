// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.UserInterface.Fragments;
using Content.Shared.CartridgeLoader;
using Content.Shared.DeadSpace.PdaArcade;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.PdaArcade;

public sealed partial class PdaArcadeUi : UIFragment
{
    private PdaArcadeFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new PdaArcadeFragment();
        _fragment.ScoreReady += (game, score) =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(new PdaArcadeScoreMessage
            {
                Game = game,
                Score = score,
            }));
        };
        _fragment.NearbyReady += () => userInterface.SendMessage(new CartridgeUiMessage(new PdaArcadeNearbyMessage()));
        _fragment.InviteReady += (game, target) =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(new PdaArcadeInviteMessage
            {
                Game = game,
                Target = target,
            }));
        };
        _fragment.MoveReady += move =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(new PdaArcadeMoveMessage { Move = move }));
        };
        _fragment.LeaveReady += () => userInterface.SendMessage(new CartridgeUiMessage(new PdaArcadeLeaveMessage()));
        _fragment.AgainReady += () => userInterface.SendMessage(new CartridgeUiMessage(new PdaArcadeAgainMessage()));
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is PdaArcadeUiState arcade)
            _fragment?.Apply(arcade);
    }
}
