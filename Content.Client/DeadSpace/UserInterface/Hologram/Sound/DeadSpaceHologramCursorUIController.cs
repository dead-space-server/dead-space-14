// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.DeadSpace.Stylesheets;
using Content.Shared.DeadSpace.CCCCVars;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.Utility;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Sound;

/// <summary>
/// Mouse cursor of the "Dead Space" interface style: a sharp sci-fi arrow, and a pointing hand over links, tabs and
/// sliders, painted in the projection colour (<see cref="CCCCVars.HologramColor"/>) and sized by
/// <see cref="CCCCVars.HologramCursorScale"/>. Text beams, resize arrows and other shapes stay the system ones.
/// <see cref="CCCCVars.HologramCursor"/> turns it off.
/// </summary>
/// <remarks>
/// The engine picks the cursor itself, after the UI controllers update, from the hovered control: its
/// <see cref="Control.CustomCursorShape"/> if it has one, else the system cursor for its
/// <see cref="Control.DefaultCursorShape"/>; over nothing, <see cref="IUserInterfaceManager.WorldCursor"/>. So this
/// gives the hovered control and the world our cursor as their custom one just before that, and the engine sets it
/// directly, without a system cursor in between. The control gets its own shape back when the mouse leaves it.
/// </remarks>
public sealed partial class DeadSpaceHologramCursorUIController : UIController
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IResourceManager _resources = default!;

    private static readonly ResPath CursorDirectory = new("/Textures/_DeadSpace/Interface/Cursors");

    private static readonly CursorImage ArrowImage = new("arrow.png", new Vector2i(4, 4), "arrow_large.png", new Vector2i(8, 8));
    private static readonly CursorImage HandImage = new("hand.png", new Vector2i(9, 4), "hand_large.png", new Vector2i(18, 8));

    /// <summary>Size of the small source images; the large ones are twice that.</summary>
    private const int BaseSize = 32;
    private const int MinSize = 12;
    private const int MaxSize = 256;
    private const float MinScale = 0.5f;
    private const float MaxScale = 3f;

    /// <summary>A scale this close to a whole number is rounded to it, so the pixels stay even.</summary>
    private const float IntegerSnap = 0.15f;

    /// <summary>Largest channel spread of a pixel that still counts as white/grey and takes the colour.</summary>
    private const int NeutralSpread = 12;

    /// <summary>Window edge, in interface units, where a resize cursor is shown (windows resize within 7).</summary>
    private const float ResizeEdge = 8f;

    private Color _color = DeadSpaceStylePalette.DefaultHologramColor;
    private float _cursorScale = 1f;
    private bool _enabled;
    private bool _dirty = true;
    private int _builtSize;
    private ICursor? _arrow;
    private ICursor? _hand;

    /// <summary>The control currently wearing one of our cursors, and its own shape to give back.</summary>
    private Control? _swapped;
    private Control.CursorShape _swappedShape;
    private ICursor? _swappedCursor;

    private bool _worldSet;

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(CCCCVars.HologramCursor, value => _enabled = value, true);
        _cfg.OnValueChanged(CCCCVars.HologramColor, OnColorChanged, true);
        _cfg.OnValueChanged(CCCCVars.HologramCursorScale, OnScaleChanged, true);
    }

    private void OnColorChanged(string hex)
    {
        _color = Color.FromHex(hex, DeadSpaceStylePalette.DefaultHologramColor);
        _dirty = true;
    }

    private void OnScaleChanged(float scale)
    {
        _cursorScale = float.IsFinite(scale) ? Math.Clamp(scale, MinScale, MaxScale) : 1f;
        _dirty = true;
    }

    /// <summary>Runs after this frame's input and before the engine picks the cursor.</summary>
    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_enabled || !DeadSpaceStylePalette.Hologram)
        {
            Release();
            return;
        }

        var size = CursorSize();
        if (_dirty || size != _builtSize)
            Rebuild(size);

        if (!_worldSet && UIManager.WorldCursor == null && _arrow != null)
        {
            UIManager.WorldCursor = _arrow;
            _worldSet = true;
        }

        UpdateTarget();
    }

    private void UpdateTarget()
    {
        // As the engine does: the control holding the mouse (a dragged window) goes first.
        var target = UIManager.ControlFocused ?? UIManager.CurrentlyHovered;

        if (_swapped != null)
        {
            // A control that set its own shape since (a window under the mouse does on every move) dropped ours.
            var wearing = _swapped.CustomCursorShape == _swappedCursor;
            if (wearing && target != null && _swapped == target && Wanted(target, _swappedShape) == _swappedCursor)
                return;

            ReleaseSwapped();
        }

        if (target == null || target.CustomCursorShape != null)
            return;

        var shape = target.DefaultCursorShape;
        if (Wanted(target, shape) is not { } cursor)
            return;

        _swapped = target;
        _swappedShape = shape;
        _swappedCursor = cursor;
        target.CustomCursorShape = cursor;
    }

    /// <summary>Our cursor for a control of this shape, or null to leave the engine's system cursor.</summary>
    private ICursor? Wanted(Control control, Control.CursorShape shape)
    {
        return shape switch
        {
            Control.CursorShape.Arrow => EdgeResizeCursor(control) ?? _arrow,
            // Pointer is the same value: tabs, sliders, list items and links.
            Control.CursorShape.Hand => _hand,
            _ => null,
        };
    }

    /// <summary>
    /// A window shows its resize arrows on itself, but where its content reaches into the resize edge the content is
    /// what the mouse hovers, and that shows a plain arrow while a drag would still resize. There the window's resize
    /// cursor is shown instead.
    /// </summary>
    private ICursor? EdgeResizeCursor(Control control)
    {
        var window = control.Parent;
        while (window != null && window is not BaseWindow)
        {
            window = window.Parent;
        }

        if (window == null)
            return null;

        StandardCursorShape shape;
        switch (window.DefaultCursorShape)
        {
            case Control.CursorShape.HResize:
                shape = StandardCursorShape.HResize;
                break;
            case Control.CursorShape.VResize:
                shape = StandardCursorShape.VResize;
                break;
            case Control.CursorShape.NWSEResize:
                shape = StandardCursorShape.NWSEResize;
                break;
            case Control.CursorShape.NESWResize:
                shape = StandardCursorShape.NESWResize;
                break;
            default:
                return null;
        }

        // The window's shape is only fresh while it gets the mouse moves; outside a drag also check that the mouse
        // really is at its edge.
        if (UIManager.ControlFocused == null)
        {
            var mouse = _input.MouseScreenPosition.Position - (Vector2) window.GlobalPixelPosition;
            var edge = ResizeEdge * window.UIScale;
            var size = window.PixelSize;
            if (mouse.X >= edge && mouse.Y >= edge && mouse.X <= size.X - edge && mouse.Y <= size.Y - edge)
                return null;
        }

        return _clyde.GetStandardCursor(shape);
    }

    /// <summary>Gives everything back to the engine's system cursors.</summary>
    private void Release()
    {
        ReleaseSwapped();

        if (_worldSet)
        {
            if (UIManager.WorldCursor == _arrow)
                UIManager.WorldCursor = null;

            _worldSet = false;
        }
    }

    private void ReleaseSwapped()
    {
        if (_swapped == null)
            return;

        // Unless the control set a shape of its own meanwhile; this also clears our custom cursor.
        if (!_swapped.Disposed && _swapped.CustomCursorShape == _swappedCursor)
            _swapped.DefaultCursorShape = _swappedShape;

        _swapped = null;
        _swappedCursor = null;
    }

    private int CursorSize()
    {
        var size = BaseSize * _cursorScale * UIManager.RootControl.UIScale;
        return float.IsFinite(size) ? Math.Clamp((int) MathF.Round(size), MinSize, MaxSize) : BaseSize;
    }

    private void Rebuild(int size)
    {
        // Everything wearing the old cursors goes back first and takes the new ones below, all before the engine
        // picks this frame's cursor.
        Release();

        var oldArrow = _arrow;
        var oldHand = _hand;

        _dirty = false;
        _builtSize = size;
        _arrow = Load(ArrowImage, size);
        _hand = Load(HandImage, size);

        // Deferred actions run after the engine has set the new cursor, so the old ones are no longer in use.
        if (oldArrow != null || oldHand != null)
        {
            UIManager.DeferAction(() =>
            {
                oldArrow?.Dispose();
                oldHand?.Dispose();
            });
        }
    }

    private ICursor? Load(CursorImage cursor, int size)
    {
        // The source closest to the wanted size: past the middle between them, the double size one.
        var large = size > BaseSize * 1.5f;
        var path = CursorDirectory / (large ? cursor.LargeFile : cursor.File);
        var hotspot = large ? cursor.LargeHotspot : cursor.Hotspot;

        try
        {
            using var stream = _resources.ContentFileRead(path);
            using var source = Image.Load<Rgba32>(stream);
            Paint(source.GetPixelSpan(), _color);

            var factor = (float) size / source.Width;
            var whole = MathF.Round(factor);
            if (whole >= 1f && MathF.Abs(factor - whole) <= IntegerSnap)
                factor = whole;

            var width = Math.Clamp((int) MathF.Round(source.Width * factor), 1, MaxSize);
            var height = Math.Clamp((int) MathF.Round(source.Height * factor), 1, MaxSize);
            if (width == source.Width && height == source.Height)
                return _clyde.CreateCursor(source, hotspot);

            using var scaled = new Image<Rgba32>(width, height);
            ScaleNearest(source.GetPixelSpan(), source.Width, source.Height, scaled.GetPixelSpan(), width, height);

            // The top-left corner of the hotspot pixel, where it lands in the scaled image.
            var scaledHotspot = new Vector2i(
                Math.Clamp(hotspot.X * width / source.Width, 0, width - 1),
                Math.Clamp(hotspot.Y * height / source.Height, 0, height - 1));

            return _clyde.CreateCursor(scaled, scaledHotspot);
        }
        catch (Exception e)
        {
            Log.Error($"Could not load hologram cursor {path}: {e.Message}");
            return null;
        }
    }

    private static void ScaleNearest(
        ReadOnlySpan<Rgba32> source,
        int sourceWidth,
        int sourceHeight,
        Span<Rgba32> target,
        int width,
        int height)
    {
        for (var y = 0; y < height; y++)
        {
            var row = y * sourceHeight / height * sourceWidth;
            for (var x = 0; x < width; x++)
            {
                target[y * width + x] = source[row + x * sourceWidth / width];
            }
        }
    }

    /// <summary>
    /// The cursors are drawn white with grey shading and a black outline: every neutral pixel is multiplied by the
    /// projection colour, so the body takes the colour and the outline stays dark.
    /// </summary>
    private static void Paint(Span<Rgba32> pixels, Color color)
    {
        for (var i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];
            if (pixel.A == 0)
                continue;

            var max = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
            var min = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
            if (max - min > NeutralSpread)
                continue;

            var level = max / 255f;
            pixels[i] = new Rgba32(
                (byte) Math.Round(color.R * level * 255f),
                (byte) Math.Round(color.G * level * 255f),
                (byte) Math.Round(color.B * level * 255f),
                pixel.A);
        }
    }

    private sealed record CursorImage(string File, Vector2i Hotspot, string LargeFile, Vector2i LargeHotspot);
}
