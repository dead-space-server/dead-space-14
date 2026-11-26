// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.DeadSpace.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Medical;

/// <summary>
/// A section title over a thin rule. In the "Dead Space" style it reads as the active tab of a RIG panel: a lit tab
/// with a bright underline over a dim line across the section; the other styles get a plain heading over a divider.
/// Colours are re-read on every restyle, so a style or projection colour change applies at once.
/// </summary>
public sealed class HoloSectionHeader : BoxContainer
{
    private readonly PanelContainer _tab;
    private readonly Label _label;
    private readonly PanelContainer _rule;
    private string _text = string.Empty;

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            _label.Text = value.ToUpperInvariant();
        }
    }

    public HoloSectionHeader()
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;

        _label = new Label
        {
            StyleClasses = { DeadSpaceStyleClass.SectionTitle },
        };
        _tab = new PanelContainer
        {
            HorizontalAlignment = HAlignment.Left,
        };
        _tab.AddChild(_label);
        _rule = new PanelContainer
        {
            MinHeight = 1,
            HorizontalExpand = true,
        };

        AddChild(_tab);
        AddChild(_rule);
        Refresh();
    }

    protected override void StylePropertiesChanged()
    {
        base.StylePropertiesChanged();
        Refresh();
    }

    private void Refresh()
    {
        var holo = DeadSpaceStylePalette.Hologram;

        _tab.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = holo ? DeadSpaceStylePalette.SurfaceTabActive : Color.Transparent,
            BorderColor = holo ? DeadSpaceStylePalette.BorderTabActive : Color.Transparent,
            BorderThickness = new Thickness(0, 0, 0, holo ? 2 : 0),
            ContentMarginLeftOverride = holo ? 14 : 0,
            ContentMarginRightOverride = holo ? 14 : 0,
            ContentMarginTopOverride = holo ? 4 : 0,
            ContentMarginBottomOverride = holo ? 3 : 2,
        };

        // The active tab of the mockup is white over the lit tab; elsewhere the heading keeps its style colour.
        _label.FontColorOverride = holo ? DeadSpaceStylePalette.Text : null;

        _rule.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = holo ? DeadSpaceStylePalette.BorderControl : DeadSpaceStylePalette.AccentDim,
        };
    }
}
