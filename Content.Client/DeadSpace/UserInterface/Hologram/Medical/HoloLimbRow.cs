// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.DeadSpace.Stylesheets;
using Content.Shared.DeadSpace.Body.Limbs;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Medical;

/// <summary>
/// One body part of the scanner's limb list: name, condition as "left / max" and its findings as tags. Pressing it
/// picks the part, the same as pressing it on the body doll.
/// </summary>
public sealed class HoloLimbRow : ContainerButton
{
    private const float HoverAlpha = 0.45f;

    private readonly Label _name;
    private readonly Label _value;
    private readonly WrapContainer _tags;
    private bool _selected;

    public LimbZone Zone { get; }

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
                return;

            _selected = value;
            RefreshBox();
        }
    }

    public HoloLimbRow(LimbZone zone)
    {
        Zone = zone;
        HorizontalExpand = true;

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 8,
        };

        _name = new Label
        {
            MinWidth = 130,
            VerticalAlignment = VAlignment.Center,
        };
        _value = new Label
        {
            MinWidth = 80,
            VerticalAlignment = VAlignment.Center,
        };
        _tags = new WrapContainer
        {
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
            SeparationOverride = 4,
            CrossSeparationOverride = 2,
        };

        row.AddChild(_name);
        row.AddChild(_value);
        row.AddChild(_tags);
        AddChild(row);
        RefreshBox();
    }

    public void Set(string name, string value, Color valueColor)
    {
        _name.Text = name;
        _value.Text = value;
        _value.FontColorOverride = valueColor;
    }

    public void ClearTags()
    {
        _tags.RemoveAllChildren();
    }

    public void AddTag(string text, Color color)
    {
        _tags.AddChild(new HoloTag(text, color));
    }

    protected override void StylePropertiesChanged()
    {
        base.StylePropertiesChanged();
        RefreshBox();
    }

    private void RefreshBox()
    {
        // A picked part gets a solid bright frame (no glow); the rest are split by a dim line. The content margins stay
        // the same in both states, so picking a part doesn't shift the text.
        var hover = DrawMode == DrawModeEnum.Hover;
        StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = _selected
                ? DeadSpaceStylePalette.CyanSelection
                : hover
                    ? DeadSpaceStylePalette.ListItemHover.WithAlpha(HoverAlpha)
                    : Color.Transparent,
            BorderColor = _selected ? DeadSpaceStylePalette.BorderHeader : DeadSpaceStylePalette.BorderDark,
            BorderThickness = _selected ? new Thickness(2) : new Thickness(0, 0, 0, 1),
            ContentMarginLeftOverride = 10,
            ContentMarginRightOverride = 6,
            ContentMarginTopOverride = 4,
            ContentMarginBottomOverride = 4,
        };
    }
}
