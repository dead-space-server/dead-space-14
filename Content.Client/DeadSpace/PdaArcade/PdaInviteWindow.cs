// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.DeadSpace.PdaArcade;

public sealed class PdaInviteWindow : DefaultWindow
{
    public readonly Label Prompt;
    public readonly Button AcceptButton;
    public readonly Button DenyButton;

    public PdaInviteWindow()
    {
        Title = Loc.GetString("pda-arcade-invite-title");
        Prompt = new Label { Margin = new Thickness(8) };
        AcceptButton = new Button { Text = Loc.GetString("pda-arcade-accept"), Margin = new Thickness(6) };
        DenyButton = new Button { Text = Loc.GetString("pda-arcade-deny"), Margin = new Thickness(6) };
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = Control.HAlignment.Center,
        };
        row.AddChild(AcceptButton);
        row.AddChild(DenyButton);
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        box.AddChild(Prompt);
        box.AddChild(row);
        Contents.AddChild(box);
        MinSize = new Vector2(320, 140);
    }
}
