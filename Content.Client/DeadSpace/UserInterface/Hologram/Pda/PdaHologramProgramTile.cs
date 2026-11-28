// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Pda;

/// <summary>
/// A program of the PDA in the "Dead Space" style: a square tile with the program's own icon at double size and its
/// name under it, plus the install/uninstall button on the program list.
/// </summary>
public sealed class PdaHologramProgramTile : ContainerButton
{
    /// <summary>Names longer than this are split over two lines at the space nearest to the middle.</summary>
    private const int SingleLineLength = 14;

    public readonly AnimatedTextureRect Icon;
    public readonly Button InstallButton;

    private readonly Label _firstLine;
    private readonly Label _secondLine;

    public PdaHologramProgramTile()
    {
        AddStyleClass(PdaHologramStyleClass.Tile);
        HorizontalExpand = true;
        MinSize = new Vector2(96, 116);

        Icon = new AnimatedTextureRect
        {
            HorizontalAlignment = HAlignment.Center,
            SetSize = new Vector2(64, 64),
        };
        Icon.DisplayRect.TextureScale = new Vector2(2, 2);
        Icon.DisplayRect.Stretch = TextureRect.StretchMode.KeepCentered;

        _firstLine = NameLine();
        _secondLine = NameLine();
        _secondLine.Visible = false;

        InstallButton = new Button
        {
            Visible = false,
            HorizontalAlignment = HAlignment.Center,
            MinWidth = 90,
            Margin = new Thickness(0, 4, 0, 0),
        };

        AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            SeparationOverride = 2,
            Children =
            {
                Icon,
                new Control { MinHeight = 4 },
                _firstLine,
                _secondLine,
                InstallButton,
            },
        });
    }

    /// <summary>Shows the program name in capitals, on two lines when it is long.</summary>
    public void SetName(string name)
    {
        ToolTip = name;
        var text = name.ToUpperInvariant();

        var split = -1;
        if (text.Length > SingleLineLength)
        {
            var middle = text.Length / 2;
            for (var offset = 0; offset <= middle; offset++)
            {
                if (middle - offset > 0 && text[middle - offset] == ' ')
                {
                    split = middle - offset;
                    break;
                }

                if (middle + offset < text.Length - 1 && text[middle + offset] == ' ')
                {
                    split = middle + offset;
                    break;
                }
            }
        }

        if (split < 0)
        {
            _firstLine.Text = text;
            _secondLine.Visible = false;
            return;
        }

        _firstLine.Text = text[..split];
        _secondLine.Text = text[(split + 1)..];
        _secondLine.Visible = true;
    }

    private static Label NameLine()
    {
        return new Label
        {
            Align = Label.AlignMode.Center,
            ClipText = true,
            HorizontalExpand = true,
            StyleClasses = { PdaHologramStyleClass.TileLabel },
        };
    }
}
