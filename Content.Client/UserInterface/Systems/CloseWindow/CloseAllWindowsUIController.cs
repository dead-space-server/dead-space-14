using Content.Client._Donate.Emerald;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;

namespace Content.Client.UserInterface.Systems.Info;

public sealed partial class CloseAllWindowsUIController : UIController
{
    [Dependency] private IInputManager _inputManager = default!;
    [Dependency] private IUserInterfaceManager _uiManager = default!;

    public override void Initialize()
    {
        _inputManager.SetInputCommand(EngineKeyFunctions.WindowCloseAll,
            InputCmdHandler.FromDelegate(session => CloseAllWindows()));
    }

    private void CloseAllWindows()
    {
        foreach (var childControl in new List<Control>(_uiManager.WindowRoot.Children))
        {
            if (childControl is BaseWindow baseWindow)
            {
                baseWindow.Close();
            }
            else if (childControl is EmeraldBaseWindow emeraldWindow)
            {
                emeraldWindow.Close();
            }
        }
    }
}

