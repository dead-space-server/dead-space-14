// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.DeadSpace.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Medical;

/// <summary>
/// A small framed uppercase marker: "fracture", "complete" and so on. The colour carries the meaning: amber for a
/// warning, red for danger, green for done, the projection colour for neutral.
/// </summary>
public sealed class HoloTag : PanelContainer
{
    private const float BackgroundAlpha = 0.08f;

    private readonly Label _label;

    public HoloTag()
    {
        VerticalAlignment = VAlignment.Center;
        _label = new Label
        {
            StyleClasses = { DeadSpaceStyleClass.Subtitle },
            VerticalAlignment = VAlignment.Center,
        };
        AddChild(_label);
    }

    public HoloTag(string text, Color color) : this()
    {
        Set(text, color);
    }

    public void Set(string text, Color color)
    {
        _label.Text = text.ToUpperInvariant();
        _label.FontColorOverride = color;
        // The "Dead Space" style draws a hollow 2px frame; the others get a faint fill under a 1px frame.
        var holo = DeadSpaceStylePalette.Hologram;
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = holo ? Color.Transparent : color.WithAlpha(BackgroundAlpha),
            BorderColor = color,
            BorderThickness = new Thickness(holo ? 2 : 1),
            ContentMarginLeftOverride = 6,
            ContentMarginRightOverride = 6,
            ContentMarginTopOverride = 2,
            ContentMarginBottomOverride = 1,
        };
    }
}
