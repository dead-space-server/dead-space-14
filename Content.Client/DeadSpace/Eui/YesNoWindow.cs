// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Localization;

namespace Content.Client.Eui;

public sealed class YesNoWindow : DefaultWindow
{
    public readonly Button NoButton;
    public readonly Button YesButton;

    public RichTextLabel MessageLabel { get; }

    public YesNoWindow(string title, string text)
    {
        Title = title;
        MinSize = new Vector2(420, 160);
        MessageLabel = new RichTextLabel { MaxWidth = 440, HorizontalExpand = true };
        MessageLabel.SetMessage(text);
        YesButton = new Button { Text = Loc.GetString("yes-no-window-yes"), HorizontalExpand = true };
        NoButton = new Button { Text = Loc.GetString("yes-no-window-no"), HorizontalExpand = true };

        Contents.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 12,
            Children =
            {
                MessageLabel,
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 8,
                    Children = { NoButton, YesButton },
                },
            },
        });
    }
}
