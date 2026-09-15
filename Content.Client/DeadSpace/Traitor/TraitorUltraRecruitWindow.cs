// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.DeadSpace.Stylesheets;
using Content.Client.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.DeadSpace.Traitor;

public sealed class TraitorUltraRecruitWindow : DefaultWindow
{
    private const float ContentWidth = 570f;

    public readonly RichTextLabel BodyLabel;
    public readonly Button AcceptButton;
    public readonly Button DeclineButton;

    public TraitorUltraRecruitWindow()
    {
        MinSize = new Vector2(580, 205);
        SetSize = new Vector2(660, 235);

        BodyLabel = new RichTextLabel
        {
            HorizontalExpand = true,
            SetWidth = ContentWidth,
            MaxWidth = ContentWidth,
            Margin = new Thickness(10, 8, 10, 4),
        };
        AcceptButton = new Button
        {
            HorizontalExpand = true,
            MinWidth = 150,
            MinHeight = 30,
            TextAlign = Label.AlignMode.Center,
            StyleClasses = { DeadSpaceStyleClass.ControlPositive },
        };
        DeclineButton = new Button
        {
            HorizontalExpand = true,
            MinWidth = 150,
            MinHeight = 30,
            TextAlign = Label.AlignMode.Center,
            StyleClasses = { StyleClass.Negative },
        };

        var content = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 8,
            HorizontalExpand = true,
            Margin = new Thickness(10),
            Children =
            {
                BodyLabel,
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Align = AlignMode.Center,
                    SeparationOverride = 6,
                    Margin = new Thickness(8, 0, 8, 8),
                    Children =
                    {
                        AcceptButton,
                        DeclineButton,
                    }
                }
            }
        };

        Contents.AddChild(content);
    }

    public void SetState(string title, string body, string accept, string decline)
    {
        Title = title;
        BodyLabel.SetMessage(body);
        AcceptButton.Text = accept;
        DeclineButton.Text = decline;
    }
}
