// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.DeadSpace.Stylesheets;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Loadout;

/// <summary>
/// An equipment slot of the loadout screen: a tile with the sprite of the item the character spawns with and a caption
/// under it. A slot that some loadout group can change shows a cog on hover and can be pressed; any other slot only
/// shows the item.
/// </summary>
public sealed class LoadoutSlotButton : BoxContainer
{
    public const float TileSize = 44f;
    private const float TileMargin = 2f;
    private const float CogSize = 12f;

    private readonly SlotTile _tile;

    public string Slot { get; }

    public bool Configurable { get; }

    public event Action<LoadoutSlotButton>? OnSlotPressed;

    public bool Selected
    {
        get => _tile.Box.Selected;
        set => _tile.Box.Selected = value;
    }

    public LoadoutSlotButton(string slot, string caption, EntityUid? item, bool configurable, Texture? cog)
        : this(slot, caption, CreateItemView(item), configurable, cog)
    {
    }

    /// <summary>A slot showing arbitrary content (a texture, a colour swatch) instead of an entity.</summary>
    public LoadoutSlotButton(string slot, string caption, Control content, bool configurable, Texture? cog)
    {
        Slot = slot;
        Configurable = configurable;
        Orientation = LayoutOrientation.Vertical;
        HorizontalAlignment = HAlignment.Center;
        SeparationOverride = 1;
        Margin = new Thickness(2, 1);

        _tile = new SlotTile(content, configurable, cog)
        {
            HorizontalAlignment = HAlignment.Center,
        };
        _tile.OnPressed += _ => OnSlotPressed?.Invoke(this);
        AddChild(_tile);

        AddChild(new Label
        {
            Text = caption,
            HorizontalAlignment = HAlignment.Center,
            ClipText = true,
            MaxWidth = TileSize + 44,
            StyleClasses = { StyleClass.LabelSubText },
        });
    }

    private static Control CreateItemView(EntityUid? item)
    {
        var sprite = new SpriteView
        {
            Scale = Vector2.One,
            // A fixed world rotation measures the item by its real bounds, so a 32 px sprite is drawn crisp at 1x.
            WorldRotation = Angle.Zero,
            OverrideDirection = Direction.South,
            Stretch = SpriteView.StretchMode.Fit,
        };
        sprite.SetEntity(item);
        return sprite;
    }

    /// <summary>Tooltip of the tile: what is equipped and what can change it.</summary>
    public void SetTooltip(string text)
    {
        _tile.ToolTip = text;
    }

    private sealed class SlotTile : ContainerButton
    {
        public readonly LoadoutStyleBox Box = new(LoadoutBoxKind.Tile, TileMargin);

        private readonly TextureRect _cog;
        private readonly bool _configurable;

        public SlotTile(Control content, bool configurable, Texture? cog)
        {
            _configurable = configurable;
            SetSize = new Vector2(TileSize, TileSize);
            RectClipContent = true;
            StyleBoxOverride = Box;
            // A fixed slot keeps its tooltip but cannot be pressed.
            Disabled = !configurable;
            MuteSounds = !configurable;

            content.MouseFilter = MouseFilterMode.Ignore;
            AddChild(content);

            _cog = new TextureRect
            {
                Texture = cog,
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                SetSize = new Vector2(CogSize, CogSize),
                HorizontalAlignment = HAlignment.Right,
                VerticalAlignment = VAlignment.Top,
                Margin = new Thickness(0, 2, 2, 0),
                MouseFilter = MouseFilterMode.Ignore,
                Visible = false,
            };
            AddChild(_cog);
        }

        protected override void DrawModeChanged()
        {
            base.DrawModeChanged();
            UpdateHover();
        }

        protected override void MouseEntered()
        {
            base.MouseEntered();
            UpdateHover();
        }

        protected override void MouseExited()
        {
            base.MouseExited();
            UpdateHover();
        }

        private void UpdateHover()
        {
            // The constructor of the base button runs this before our fields exist.
            if (_cog == null)
                return;

            var hovered = _configurable && IsHovered;
            Box.Hovered = hovered;
            _cog.Visible = hovered;
            _cog.ModulateSelfOverride = DeadSpaceStylePalette.Hologram
                ? DeadSpaceStylePalette.CyanBright
                : DeadSpaceStylePalette.Heading;
        }
    }
}
