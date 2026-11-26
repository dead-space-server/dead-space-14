// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.DeadSpace.Stylesheets;
using Robust.Client.Graphics;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Loadout;

public enum LoadoutBoxKind : byte
{
    /// <summary>The whole window body.</summary>
    Window,

    /// <summary>The window title bar.</summary>
    Header,

    /// <summary>One of the three columns of the loadout screen.</summary>
    Panel,

    /// <summary>The character preview frame.</summary>
    Preview,

    /// <summary>An equipment slot tile.</summary>
    Tile,
}

/// <summary>
/// Chrome of the loadout screen. Colours are read from <see cref="DeadSpaceStylePalette"/> on every draw, so the box
/// follows style and projection colour changes without being rebuilt. Fills are always opaque: the screen sits over
/// the profile editor and must not show it through. In the "Dead Space" style it is a square projected panel (thin
/// dim frame, bright corner brackets), otherwise a flat panel of the active palette. A selected tile gets a solid
/// bright frame, never a glow.
/// </summary>
public sealed class LoadoutStyleBox : StyleBox
{
    private const float PanelBracket = 10f;
    private const float TileBracket = 5f;

    private static readonly Thickness ThinFrame = new(1);
    private static readonly Thickness SelectedFrame = new(2);
    private static readonly Thickness BottomLine = new(0, 0, 0, 1);

    private readonly DeadSpaceHoloStyleBox _holo = new();
    private readonly StyleBoxFlat _flat = new();

    public LoadoutBoxKind Kind { get; }

    public bool Hovered { get; set; }

    public bool Selected { get; set; }

    public LoadoutStyleBox(LoadoutBoxKind kind, float contentMargin)
    {
        Kind = kind;
        SetContentMarginOverride(Margin.All, contentMargin);
    }

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        var hologram = DeadSpaceStylePalette.Hologram;
        var classic = DeadSpaceStylePalette.ClassicChrome;
        var thickness = ThinFrame;
        var bracket = 0f;
        var brackets = HoloBrackets.All;
        Color background;
        Color border;

        switch (Kind)
        {
            case LoadoutBoxKind.Window:
                background = DeadSpaceStylePalette.SurfaceFlat;
                border = hologram || classic ? DeadSpaceStylePalette.BorderControl : Color.Transparent;
                bracket = PanelBracket;
                break;
            case LoadoutBoxKind.Header:
                background = DeadSpaceStylePalette.SurfaceHeader;
                border = hologram ? DeadSpaceStylePalette.BorderHeader : DeadSpaceStylePalette.AccentDim;
                thickness = BottomLine;
                bracket = PanelBracket;
                brackets = HoloBrackets.Top;
                break;
            case LoadoutBoxKind.Panel:
                background = DeadSpaceStylePalette.SurfaceDark;
                border = hologram || classic ? DeadSpaceStylePalette.BorderDark : Color.Transparent;
                bracket = PanelBracket;
                break;
            case LoadoutBoxKind.Preview:
                background = DeadSpaceStylePalette.SurfaceInset;
                border = hologram || classic ? DeadSpaceStylePalette.BorderDark : Color.Transparent;
                bracket = PanelBracket;
                break;
            default:
                if (Selected)
                {
                    background = hologram ? DeadSpaceStylePalette.SurfaceInset : DeadSpaceStylePalette.ControlPressed;
                    border = hologram ? DeadSpaceStylePalette.BorderHeader : DeadSpaceStylePalette.Heading;
                    thickness = SelectedFrame;
                    bracket = TileBracket;
                }
                else if (Hovered)
                {
                    background = DeadSpaceStylePalette.ControlHover;
                    border = DeadSpaceStylePalette.HoverOutline;
                }
                else
                {
                    background = DeadSpaceStylePalette.SurfaceInset;
                    border = hologram ? DeadSpaceStylePalette.BorderControl : DeadSpaceStylePalette.BorderDark;
                }

                break;
        }

        background = background.WithAlpha(1f);

        if (!hologram)
        {
            _flat.BackgroundColor = background;
            _flat.BorderColor = border;
            _flat.BorderThickness = thickness;
            _flat.Draw(handle, box, uiScale);
            return;
        }

        _holo.BackgroundColor = background;
        _holo.BorderColor = border;
        _holo.BorderThickness = thickness;
        _holo.BracketColor = DeadSpaceStylePalette.BorderHeader;
        _holo.BracketLength = bracket;
        _holo.Brackets = brackets;
        _holo.Chamfer = 0f;
        _holo.Draw(handle, box, uiScale);
    }
}
