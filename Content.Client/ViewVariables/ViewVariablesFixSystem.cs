using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.ViewVariables;

// i hate ui
public sealed partial class ViewVariablesFixSystem : EntitySystem
{
    [Dependency] private IUserInterfaceManager _uiManager = default!;

    private readonly HashSet<int> _patchedWindows = new();

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        foreach (var child in _uiManager.WindowRoot.Children)
        {
            if (child is not DefaultWindow window)
                continue;

            var id = window.GetHashCode();
            if (_patchedWindows.Contains(id))
                continue;

            if (!TryPatchVVWindow(window))
                continue;

            _patchedWindows.Add(id);
        }
    }

    private static bool TryPatchVVWindow(DefaultWindow window)
    {
        foreach (var contentChild in window.Contents.Children)
        {
            if (contentChild is not ScrollContainer scroll)
                continue;

            foreach (var scrollChild in scroll.Children)
            {
                if (scrollChild is not BoxContainer box)
                    continue;

                foreach (var boxChild in box.Children)
                {
                    if (boxChild is not TabContainer)
                        continue;

                    scroll.HScrollEnabled = false;
                    return true;
                }
            }
        }

        return false;
    }
}
