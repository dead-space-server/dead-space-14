using Content.Client.UserInterface.Fragments;
using Content.Shared.CartridgeLoader;
using Content.Shared.DeadSpace.Thief;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.Thief.Cartridges;

/// <summary>
/// DS14: UIFragment of the ВорПРО program.
/// </summary>
public sealed partial class ThiefProgramUi : UIFragment
{
    private ThiefProgramUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new ThiefProgramUiFragment();
        _fragment.OnActionPressed += (action, requestId, listingId, amount) =>
        {
            var message = new ThiefProgramUiMessageEvent(action, requestId, listingId, amount);
            userInterface.SendMessage(new CartridgeUiMessage(message));
        };
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not ThiefProgramUiState thiefState)
            return;

        _fragment?.UpdateState(thiefState);
    }
}
