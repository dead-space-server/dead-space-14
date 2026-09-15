using System.Numerics;
using Content.Client.DeadSpace.Stylesheets;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Administration.UI.CustomControls;

public sealed class VSeparator : PanelContainer
{
    // DS14-start
    public VSeparator()
    {
        MinSize = new Vector2(1, 5);
        AddStyleClass(DeadSpaceStyleClass.AccentDim);
    }
    // DS14-end
}
