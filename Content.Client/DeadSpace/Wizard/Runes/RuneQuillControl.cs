// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.Input;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Wizard.Runes;

public sealed class RuneQuillControl : LayoutContainer
{
    public event Action<List<Vector2>>? StrokeFinished;
    public event Action? CloseRequested;

    public readonly List<RunePrototype> Drawn = new();
    public readonly List<RunePrototype> Hints = new();
    public RuneKind? Expected = RuneKind.Form;

    private const float SheetWidth = 320f;
    private const float SheetHeight = 500f;
    private const float SlotTop = 70f;
    private const float SlotHeight = 140f;
    private const float SlotGap = 14f;
    private const float HintTop = 386f;
    private const float HintCell = 46f;
    private const int HintColumns = 6;
    private const float BurnTime = 1.1f;
    private const float DoneTime = 1f;
    private const float AppearTime = 0.35f;

    private static readonly Color Parchment = Color.FromHex("#f3ead3");
    private static readonly Color Edge = Color.FromHex("#cdb88b");
    private static readonly Color Ink = Color.FromHex("#2b1d3a");
    private static readonly Color Ember = Color.FromHex("#ff7a1a");
    private static readonly Color Bad = Color.FromHex("#c0262f");

    private readonly IClyde _clyde;
    private readonly Font _title;
    private readonly Font _font;
    private readonly Font _small;
    private readonly Font _tiny;
    private readonly List<Vector2> _stroke = new();
    private readonly List<Vector2> _quad = new(6);
    private readonly List<float> _appeared = new();
    private bool _drawing;
    private float _time;
    private float _burn = -1f;
    private float _done = -1f;
    private string? _warning;
    private float _warningFade;
    private Vector2 _mouse;

    public bool Finished => _burn >= 0f || _done >= 0f;

    public bool Expired => _burn >= BurnTime || _done >= DoneTime;

    public RuneQuillControl()
    {
        MouseFilter = MouseFilterMode.Stop;
        _clyde = IoCManager.Resolve<IClyde>();
        var cache = IoCManager.Resolve<IResourceCache>();
        _title = cache.NotoStack(variation: "Bold", size: 14);
        _font = cache.NotoStack(variation: "Bold", size: 11);
        _small = cache.NotoStack(size: 9);
        _tiny = cache.NotoStack(size: 7);
    }

    private UIBox2 Sheet
    {
        get
        {
            var size = new Vector2(SheetWidth, SheetHeight) * UIScale;
            return UIBox2.FromDimensions((PixelSize - size) / 2f, size);
        }
    }

    private UIBox2 Slot(int index)
    {
        var sheet = Sheet;
        var scale = UIScale;
        var top = sheet.Top + (SlotTop + index * (SlotHeight + SlotGap)) * scale;
        return new UIBox2(sheet.Left + 40f * scale, top, sheet.Right - 40f * scale, top + SlotHeight * scale);
    }

    private UIBox2 CloseBox
    {
        get
        {
            var sheet = Sheet;
            var size = 22f * UIScale;
            return UIBox2.FromDimensions(new Vector2(sheet.Right - size - 8f * UIScale, sheet.Top + 8f * UIScale), new Vector2(size));
        }
    }

    public void Accept(RunePrototype rune)
    {
        Drawn.Add(rune);
        _appeared.Add(_time);
    }

    public void Burn()
    {
        _drawing = false;
        _stroke.Clear();
        _burn = 0f;
    }

    public void Done()
    {
        _drawing = false;
        _stroke.Clear();
        _done = 0f;
    }

    public void Warn(string text)
    {
        _warning = text;
        _warningFade = 2.2f;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _time += args.DeltaSeconds;
        _warningFade = MathF.Max(0f, _warningFade - args.DeltaSeconds);

        if (_burn >= 0f)
            _burn += args.DeltaSeconds;
        else if (_done >= 0f)
            _done += args.DeltaSeconds;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Handled)
            return;

        if (args.Function == EngineKeyFunctions.CloseModals ||
            args.Function == EngineKeyFunctions.WindowCloseAll ||
            args.Function == ContentKeyFunctions.EscapeContext)
        {
            CloseRequested?.Invoke();
            args.Handle();
            return;
        }

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        args.Handle();
        if (Finished)
            return;

        if (CloseBox.Contains(args.RelativePixelPosition))
        {
            CloseRequested?.Invoke();
            return;
        }

        if (!Sheet.Contains(args.RelativePixelPosition))
            return;

        _drawing = true;
        _stroke.Clear();
        _stroke.Add(args.RelativePixelPosition);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick || !_drawing)
            return;

        _drawing = false;
        var stroke = new List<Vector2>(_stroke);
        _stroke.Clear();
        StrokeFinished?.Invoke(stroke);
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        _mouse = args.RelativePixelPosition;
        if (!_drawing)
            return;

        var sheet = Sheet;
        var point = Vector2.Clamp(args.RelativePixelPosition, sheet.TopLeft, sheet.BottomRight);
        if (_stroke.Count > 0 && Vector2.DistanceSquared(_stroke[^1], point) < 9f)
            return;

        if (_stroke.Count < 600)
            _stroke.Add(point);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var scale = UIScale;
        var sheet = Sheet;
        var burn = _burn >= 0f ? Math.Clamp(_burn / BurnTime, 0f, 1f) : 0f;
        var fade = burn > 0.6f ? 1f - (burn - 0.6f) / 0.4f : 1f;
        var done = _done >= 0f ? Math.Clamp(_done / DoneTime, 0f, 1f) : 0f;
        if (done > 0.6f)
            fade = 1f - (done - 0.6f) / 0.4f;

        handle.DrawRect(new UIBox2(Vector2.Zero, PixelSize), Color.Black.WithAlpha(0.35f * fade));
        handle.DrawRect(new UIBox2(sheet.TopLeft + new Vector2(5f, 7f) * scale, sheet.BottomRight + new Vector2(5f, 7f) * scale), Color.Black.WithAlpha(0.3f * fade));

        var paper = Color.InterpolateBetween(Parchment, Color.InterpolateBetween(Ember, Color.Black, burn), MathF.Min(1f, burn * 1.6f));
        handle.DrawRect(sheet, paper.WithAlpha(fade));
        for (var i = 0; i < 5; i++)
        {
            var inset = i * 3f * scale;
            Frame(handle, new UIBox2(sheet.TopLeft + new Vector2(inset), sheet.BottomRight - new Vector2(inset)), Edge.WithAlpha(0.22f * (5 - i) / 5f * fade), 3f * scale);
        }

        if (burn > 0f)
        {
            DrawBurn(handle, sheet, burn, scale);
            return;
        }

        var glow = RuneCircleArt.Glow(_clyde);
        if (done > 0f && Drawn.Count > 0)
            Texture(handle, glow, sheet.Center, sheet.Height * (0.8f + done), Drawn[^1].Color.WithAlpha(0.5f * (1f - done)));

        Centered(handle, _title, Loc.GetString("rune-quill-title"), new Vector2(sheet.Center.X, sheet.Top + 16f * scale), Ink.WithAlpha(0.85f * fade));
        var hint = done > 0f
            ? Loc.GetString("rune-quill-done")
            : Expected switch
            {
                RuneKind.Form => Loc.GetString("rune-quill-hint-form"),
                RuneKind.Element => Loc.GetString("rune-quill-hint-element"),
                _ => Loc.GetString("rune-quill-done"),
            };
        Centered(handle, _small, hint, new Vector2(sheet.Center.X, sheet.Top + 40f * scale), Ink.WithAlpha(0.6f * fade));

        var close = CloseBox;
        var closeColor = close.Contains(_mouse) ? Bad : Ink.WithAlpha(0.45f);
        Line(handle, close.TopLeft + new Vector2(5f * scale), close.BottomRight - new Vector2(5f * scale), 2f * scale, closeColor.WithAlpha(closeColor.A * fade));
        Line(handle, new Vector2(close.Right - 5f * scale, close.Top + 5f * scale), new Vector2(close.Left + 5f * scale, close.Bottom - 5f * scale), 2f * scale, closeColor.WithAlpha(closeColor.A * fade));

        for (var i = 0; i < 2; i++)
            DrawSlot(handle, i, scale, fade);

        DrawHints(handle, sheet, scale, fade);

        for (var i = 1; i < _stroke.Count; i++)
            Line(handle, _stroke[i - 1], _stroke[i], 2.6f * scale, Ink.WithAlpha(0.9f));

        if (_warning != null && _warningFade > 0f)
            Centered(handle, _font, _warning, new Vector2(sheet.Center.X, sheet.Bottom + 10f * scale), Bad.WithAlpha(MathF.Min(1f, _warningFade / 0.4f)));
    }

    private void DrawSlot(DrawingHandleScreen handle, int index, float scale, float fade)
    {
        var box = Slot(index);
        var kind = index == 0 ? RuneKind.Form : RuneKind.Element;
        var label = Loc.GetString(index == 0 ? "rune-circle-part-form" : "rune-circle-part-element");

        if (index < Drawn.Count)
        {
            var rune = Drawn[index];
            var age = Math.Clamp((_time - _appeared[index]) / AppearTime, 0f, 1f);
            var ease = 1f - (1f - age) * (1f - age);
            var size = box.Height * (0.6f + 0.2f * ease);
            var rect = UIBox2.FromDimensions(box.Center - new Vector2(size / 2f), new Vector2(size));
            Texture(handle, RuneCircleArt.Glow(_clyde), box.Center, size * 1.6f, rune.Color.WithAlpha((0.25f + 0.5f * (1f - age)) * fade));
            DrawGlyph(handle, rune.Stroke, rect, Color.InterpolateBetween(rune.Color, Ink, 0.25f).WithAlpha(fade), 4f * scale);
            DrawGlyph(handle, rune.Stroke, rect, rune.Color.WithAlpha(fade), 2.4f * scale);
            Centered(handle, _font, Loc.GetString(rune.Name), new Vector2(box.Center.X, box.Bottom - 16f * scale), Color.InterpolateBetween(rune.Color, Ink, 0.55f).WithAlpha(fade));
            return;
        }

        var active = Expected == kind;
        var pulse = active ? 0.5f + 0.5f * MathF.Sin(_time * 4f) : 0f;
        DashedFrame(handle, box, Ink.WithAlpha((active ? 0.25f + 0.25f * pulse : 0.12f) * fade), 1.4f * scale, 8f * scale);
        Centered(handle, _small, label, new Vector2(box.Center.X, box.Center.Y - 7f * scale), Ink.WithAlpha((active ? 0.45f + 0.2f * pulse : 0.2f) * fade));
    }

    private void DrawHints(DrawingHandleScreen handle, UIBox2 sheet, float scale, float fade)
    {
        if (Hints.Count == 0 || Expected == null)
            return;

        var top = sheet.Top + HintTop * scale;
        Line(handle, new Vector2(sheet.Left + 30f * scale, top - 6f * scale), new Vector2(sheet.Right - 30f * scale, top - 6f * scale), 1f * scale, Ink.WithAlpha(0.15f * fade));
        var columns = Math.Min(HintColumns, Hints.Count);
        var left = sheet.Center.X - columns * HintCell * scale / 2f;
        for (var i = 0; i < Hints.Count; i++)
        {
            var rune = Hints[i];
            var center = new Vector2(left + (i % HintColumns + 0.5f) * HintCell * scale, top + (i / HintColumns * 50f + 14f) * scale);
            if (center.Y > sheet.Bottom - 16f * scale)
                break;

            var hovered = (_mouse - center).Length() < 18f * scale;
            var glyph = 24f * scale;
            DrawGlyph(handle, rune.Stroke, UIBox2.FromDimensions(center - new Vector2(glyph / 2f), new Vector2(glyph)), rune.Color.WithAlpha((hovered ? 0.95f : 0.55f) * fade), 1.4f * scale);
            var name = Loc.GetString(rune.Name);
            Centered(handle, _tiny, name, new Vector2(center.X, center.Y + 13f * scale), Ink.WithAlpha((hovered ? 0.8f : 0.4f) * fade));
        }
    }

    private void DrawBurn(DrawingHandleScreen handle, UIBox2 sheet, float burn, float scale)
    {
        var glow = RuneCircleArt.Glow(_clyde);
        var fade = burn > 0.6f ? 1f - (burn - 0.6f) / 0.4f : 1f;
        var random = new Random(17);
        for (var i = 0; i < 26; i++)
        {
            var at = new Vector2(sheet.Left + random.NextSingle() * sheet.Width, sheet.Top + random.NextSingle() * sheet.Height);
            var rise = burn * (40f + random.NextSingle() * 80f) * scale;
            var flicker = 0.6f + 0.4f * MathF.Sin(_time * (8f + i) + i);
            Texture(handle, glow, at - new Vector2(0f, rise), (30f + random.NextSingle() * 50f) * scale * (1f - burn * 0.5f), Ember.WithAlpha(0.7f * flicker * fade));
        }

        Texture(handle, glow, sheet.Center, sheet.Height * (1f + burn), Ember.WithAlpha(0.45f * fade));
        Centered(handle, _font, Loc.GetString("rune-quill-burned"), new Vector2(sheet.Center.X, sheet.Center.Y - 8f * scale), Color.White.WithAlpha(fade));
    }

    private void DashedFrame(DrawingHandleScreen handle, UIBox2 box, Color color, float width, float dash)
    {
        DashedLine(handle, box.TopLeft, box.TopRight, color, width, dash);
        DashedLine(handle, box.BottomLeft, box.BottomRight, color, width, dash);
        DashedLine(handle, box.TopLeft, box.BottomLeft, color, width, dash);
        DashedLine(handle, box.TopRight, box.BottomRight, color, width, dash);
    }

    private void DashedLine(DrawingHandleScreen handle, Vector2 a, Vector2 b, Color color, float width, float dash)
    {
        var length = (b - a).Length();
        if (length < 0.01f)
            return;

        var dir = (b - a) / length;
        for (var t = 0f; t < length; t += dash * 2f)
            Quad(handle, a + dir * t, a + dir * MathF.Min(length, t + dash), width, color);
    }

    private void DrawGlyph(DrawingHandleScreen handle, IReadOnlyList<Vector2> points, UIBox2 rect, Color color, float width)
    {
        if (points.Count < 2)
            return;

        var inner = new UIBox2(rect.TopLeft + rect.Size * 0.15f, rect.BottomRight - rect.Size * 0.15f);
        Vector2 Map(Vector2 p) => inner.TopLeft + new Vector2(p.X * inner.Width, p.Y * inner.Height);

        for (var i = 1; i < points.Count; i++)
            Line(handle, Map(points[i - 1]), Map(points[i]), width, color);
    }

    private void Texture(DrawingHandleScreen handle, Texture texture, Vector2 center, float diameter, Color color)
    {
        if (color.A <= 0.001f || diameter <= 0.5f)
            return;

        handle.DrawTextureRect(texture, UIBox2.FromDimensions(center - new Vector2(diameter / 2f), new Vector2(diameter)), color);
    }

    private void Frame(DrawingHandleScreen handle, UIBox2 box, Color color, float width)
    {
        Quad(handle, box.TopLeft, box.TopRight, width, color);
        Quad(handle, box.BottomLeft, box.BottomRight, width, color);
        Quad(handle, box.TopLeft, box.BottomLeft, width, color);
        Quad(handle, box.TopRight, box.BottomRight, width, color);
    }

    private void Line(DrawingHandleScreen handle, Vector2 a, Vector2 b, float width, Color color)
    {
        Quad(handle, a, b, width, color);
        if (width > 2f)
            handle.DrawCircle(b, width / 2f, color);
    }

    private void Quad(DrawingHandleScreen handle, Vector2 a, Vector2 b, float width, Color color)
    {
        var delta = b - a;
        var length = delta.Length();
        if (length < 0.01f)
            return;

        var normal = new Vector2(-delta.Y, delta.X) / length * (width / 2f);
        _quad.Clear();
        _quad.Add(a + normal);
        _quad.Add(a - normal);
        _quad.Add(b + normal);
        _quad.Add(b + normal);
        _quad.Add(a - normal);
        _quad.Add(b - normal);
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _quad, color);
    }

    private static void Centered(DrawingHandleScreen handle, Font font, string text, Vector2 top, Color color)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            if (font.TryGetCharMetrics(rune, 1f, out var metrics))
                width += metrics.Advance;
        }

        handle.DrawString(font, new Vector2(top.X - width / 2f, top.Y), text, color);
    }
}
