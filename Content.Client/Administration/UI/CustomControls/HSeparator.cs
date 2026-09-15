using Content.Client.DeadSpace.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Administration.UI.CustomControls;

public sealed class HSeparator : Control
{
    // DS14-start
    public HSeparator()
    {
        AddChild(new PanelContainer
        {
            MinHeight = 1,
            StyleClasses = { DeadSpaceStyleClass.AccentDim },
        });
    }
    // DS14-end
}
