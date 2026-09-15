using Content.Client._Donate.Emerald;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;

namespace Content.Client.UserInterface.Systems.Info;

public sealed class CloseRecentWindowUIController : UIController
{
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    List<Control> recentlyInteractedWindows = new();

    public override void Initialize()
    {
        _uiManager.OnKeyBindDown += OnKeyBindDown;
        _uiManager.WindowRoot.OnChildAdded += OnRootChildAdded;
        _uiManager.WindowRoot.OnChildRemoved += OnRootChildRemoved;

        _inputManager.SetInputCommand(EngineKeyFunctions.WindowCloseRecent,
            InputCmdHandler.FromDelegate(session => CloseMostRecentWindow()));
    }

    public void CloseMostRecentWindow()
    {
        for (int i = recentlyInteractedWindows.Count - 1; i >= 0; i--)
        {
            var window = recentlyInteractedWindows[i];
            recentlyInteractedWindows.RemoveAt(i);

            if (window is BaseWindow baseWindow && baseWindow.IsOpen)
            {
                baseWindow.Close();
                return;
            }

            if (window is EmeraldBaseWindow emeraldWindow && emeraldWindow.IsOpen)
            {
                emeraldWindow.Close();
                return;
            }
        }
    }

    private void OnKeyBindDown(Control control)
    {
        var window = GetWindowForControl(control);

        if (window != null)
        {
            SetMostRecentlyInteractedWindow(window);
        }
    }

    public void SetMostRecentlyInteractedWindow(Control window)
    {
        for (int i = recentlyInteractedWindows.Count - 1; i >= 0; i--)
        {
            if (recentlyInteractedWindows[i] == window)
            {
                if (i == recentlyInteractedWindows.Count - 1)
                    return;
                else
                {
                    recentlyInteractedWindows.RemoveAt(i);
                    break;
                }
            }
        }

        recentlyInteractedWindows.Add(window);
    }

    private Control? GetWindowForControl(Control? control)
    {
        if (control == null)
            return null;

        if (control is BaseWindow || control is EmeraldBaseWindow)
            return control;

        return GetWindowForControl(control.Parent);
    }

    private void OnRootChildAdded(Control control)
    {
        if (control is BaseWindow || control is EmeraldBaseWindow)
        {
            SetMostRecentlyInteractedWindow(control);
        }
    }

    private void OnRootChildRemoved(Control control)
    {
        // DS14-start - custom Emerald windows participate in the same recent-window lifecycle.
        if (control is BaseWindow || control is EmeraldBaseWindow)
            recentlyInteractedWindows.Remove(control);
        // DS14-end
    }

    public bool HasClosableWindow()
    {
        for (var i = recentlyInteractedWindows.Count - 1; i >= 0; i--)
        {
            var window = recentlyInteractedWindows[i];

            if (window is BaseWindow baseWindow && baseWindow.IsOpen)
                return true;

            if (window is EmeraldBaseWindow emeraldWindow && emeraldWindow.IsOpen)
                return true;
        }

        return false;
    }
}
