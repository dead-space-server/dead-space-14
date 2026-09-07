using System.Numerics;
using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.DeadSpace.AshWalkers;

public sealed class AshWalkerReturnWindow : FancyWindow
{
    public readonly Button YesButton;
    public readonly Button NoButton;
    public readonly RichTextLabel MessageLabel;

    public AshWalkerReturnWindow()
    {
        MinSize = new Vector2(430, 190);
        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 12,
            Margin = new Thickness(12),
        };
        ContentsContainer.AddChild(root);
        MessageLabel = new RichTextLabel { MaxWidth = 405, VerticalExpand = true };
        root.AddChild(MessageLabel);
        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 12 };
        root.AddChild(buttons);
        YesButton = new Button { Text = Loc.GetString("ash-walker-return-accept"), HorizontalExpand = true };
        NoButton = new Button { Text = Loc.GetString("ash-walker-return-decline"), HorizontalExpand = true };
        buttons.AddChild(YesButton);
        buttons.AddChild(NoButton);
    }
}
