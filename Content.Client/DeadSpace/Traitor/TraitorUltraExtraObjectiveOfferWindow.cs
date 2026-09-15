// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.DeadSpace.Stylesheets;
using Content.Client.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.DeadSpace.Traitor;

public sealed class TraitorUltraExtraObjectiveOfferWindow : DefaultWindow
{
    private const float ContentWidth = 610f;

    public readonly RichTextLabel BodyLabel;
    public readonly RichTextLabel ObjectiveLabel;
    public readonly RichTextLabel RewardLabel;
    public readonly Button AcceptButton;
    public readonly Button DeclineButton;

    public TraitorUltraExtraObjectiveOfferWindow()
    {
        MinSize = new Vector2(620, 245);
        SetSize = new Vector2(690, 285);

        BodyLabel = MakeTextLabel(new Thickness(10, 8, 10, 2));
        ObjectiveLabel = MakeTextLabel(new Thickness(10, 0, 10, 0));
        RewardLabel = MakeTextLabel(new Thickness(10, 0, 10, 2));

        AcceptButton = new Button
        {
            HorizontalExpand = true,
            MinHeight = 30,
            TextAlign = Label.AlignMode.Center,
            StyleClasses = { DeadSpaceStyleClass.ControlPositive },
        };
        DeclineButton = new Button
        {
            HorizontalExpand = true,
            MinHeight = 30,
            TextAlign = Label.AlignMode.Center,
            StyleClasses = { StyleClass.Negative },
        };

        var content = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
            Margin = new Thickness(10),
            Children =
            {
                BodyLabel,
                ObjectiveLabel,
                RewardLabel,
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Align = AlignMode.Center,
                    SeparationOverride = 8,
                    Margin = new Thickness(8, 4, 8, 8),
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

    private static RichTextLabel MakeTextLabel(Thickness margin)
    {
        return new RichTextLabel
        {
            HorizontalExpand = true,
            SetWidth = ContentWidth,
            MaxWidth = ContentWidth,
            Margin = margin,
        };
    }

    public void SetState(string title, string body, string objective, string reward, string accept, string decline)
    {
        Title = title;
        BodyLabel.SetMessage(body);
        ObjectiveLabel.SetMessage(objective);
        RewardLabel.SetMessage(reward);
        AcceptButton.Text = accept;
        DeclineButton.Text = decline;
    }
}
