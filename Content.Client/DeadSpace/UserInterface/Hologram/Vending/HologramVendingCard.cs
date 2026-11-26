// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.DeadSpace.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Client.VendingMachines.UI;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Vending;

/// <summary>
/// One goods card of a vending machine in the "Dead Space" style: a 64 px sprite, name and stock in a square frame.
/// The card has a fixed layout (sprite size and one line per text), so every card in a grid is the same height
/// whatever the sprite or the length of the name.
/// </summary>
public sealed class HologramVendingCard : ContainerButton
{
    public const float SpriteSize = 64f;

    private readonly BoxContainer _content;
    private readonly Label _name;
    private readonly Label _stock;

    private StyleBox? _normal;
    private StyleBox? _hover;
    private StyleBox? _pressed;
    private StyleBox? _disabled;
    private uint _amount;

    public ListData Data { get; }
    public VendorItemKey Key { get; }
    public string ItemName { get; }

    public HologramVendingCard(ListData data, VendorItemKey key, string name)
    {
        Data = data;
        Key = key;
        ItemName = name;
        ToolTip = name;
        HorizontalExpand = true;

        var sprite = new EntityPrototypeView
        {
            SetSize = new Vector2(SpriteSize, SpriteSize),
            HorizontalAlignment = HAlignment.Center,
            // A 32 px item sprite is drawn at exactly 2x; larger sprites shrink to fit.
            Stretch = SpriteView.StretchMode.Fill,
        };
        sprite.SetPrototype(key.Prototype);

        _name = new Label
        {
            Text = name,
            ClipText = true,
            Align = Label.AlignMode.Center,
            HorizontalExpand = true,
        };

        _stock = new Label
        {
            ClipText = true,
            HorizontalExpand = true,
        };

        _content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8, 6),
            SeparationOverride = 2,
            Children = { sprite, _name, _stock },
        };

        AddChild(_content);
        RefreshStyle();
    }

    public void SetAmount(uint amount, bool enabled)
    {
        _amount = amount;
        Disabled = !enabled || amount == 0;
        _stock.Text = amount == 0
            ? Loc.GetString("hologram-vending-out-of-stock")
            : Loc.GetString("hologram-vending-stock", ("amount", amount));
        UpdateColors();
    }

    /// <summary>Rebuilds the frame and colours from the current palette.</summary>
    public void RefreshStyle()
    {
        // Square corners and no glow: hover and pressing light the frame up solid.
        _normal = DeadSpaceStyleBoxes.Holo(
            DeadSpaceStylePalette.ListItem,
            DeadSpaceStylePalette.BorderDark);
        _hover = DeadSpaceStyleBoxes.Holo(
            DeadSpaceStylePalette.ListItemHover,
            DeadSpaceStylePalette.HoverOutline);
        _pressed = DeadSpaceStyleBoxes.Holo(
            DeadSpaceStylePalette.ListItemPressed,
            DeadSpaceStylePalette.PressedOutline);
        _disabled = DeadSpaceStyleBoxes.Holo(
            DeadSpaceStylePalette.ControlDisabled.WithAlpha(0.55f),
            DeadSpaceStylePalette.BorderDisabled);

        UpdateColors();
        DrawModeChanged();
    }

    private void UpdateColors()
    {
        _name.FontColorOverride = DeadSpaceStylePalette.Text;
        _stock.FontColorOverride = _amount == 0
            ? DeadSpaceStylePalette.NegativeBorderHover
            : DeadSpaceStylePalette.CyanBright;
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();

        // Called from the base constructor too, before the boxes exist.
        if (_normal == null)
            return;

        StyleBoxOverride = DrawMode switch
        {
            DrawModeEnum.Hover => _hover,
            DrawModeEnum.Pressed => _pressed,
            DrawModeEnum.Disabled => _disabled,
            _ => _normal,
        };

        _content.Modulate = DrawMode == DrawModeEnum.Disabled ? Color.White.WithAlpha(0.45f) : Color.White;
    }
}
