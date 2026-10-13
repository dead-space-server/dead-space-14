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
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is PdaArcadeUiState arcade)
            _fragment?.Apply(arcade.Entries);
    }
}
