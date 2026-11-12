// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Wizard.Runes;

public enum RuneButtonIcon : byte
{
    Scroll,
    Erase,
    Close,
    Swap,
    Collapse,
}

public sealed class RuneButton : Control
{
    public const float Diameter = 38f;
    public const float SmallDiameter = 20f;

    public event Action? Pressed;

    private static readonly Color Gold = Color.FromHex("#e8c77a");
    private static readonly Color Night = Color.FromHex("#0b0614");

    private readonly RuneButtonIcon _icon;
    private readonly string _caption;
    private readonly bool _small;
    private readonly IClyde _clyde;
    private readonly Font _font;
    private readonly List<Vector2> _quad = new(6);
    private bool _hover;
    private float _glow;
    private float _press;

    public RuneButton(RuneButtonIcon icon, string caption, bool small = false)
    {
        _icon = icon;
        _caption = caption;
        _small = small;
        _clyde = IoCManager.Resolve<IClyde>();
        _font = IoCManager.Resolve<IResourceCache>().NotoStack(variation: "Bold", size: 8);
        MouseFilter = MouseFilterMode.Stop;
        SetSize = small ? new Vector2(SmallDiameter + 4f) : new Vector2(Diameter + 24f, Diameter + 16f);
        ToolTip = caption;
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();
        _hover = true;
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _hover = false;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _press = 0.25f;
        Pressed?.Invoke();
        args.Handle();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _glow = MathHelper.Lerp(_glow, _hover ? 1f : 0f, MathF.Min(1f, args.DeltaSeconds * 10f));
        _press = MathF.Max(0f, _press - args.DeltaSeconds);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var scale = UIScale;
        var radius = (_small ? SmallDiameter : Diameter) / 2f * scale * (1f - _press * 0.4f);
        var center = new Vector2(PixelSize.X / 2f, radius + 2f * scale);
        var accent = _icon == RuneButtonIcon.Close ? Color.FromHex("#ff7a86") : Gold;

        Glow(handle, center, radius * (3f + _glow), accent.WithAlpha(0.15f + 0.3f * _glow));
        handle.DrawCircle(center, radius, Night.WithAlpha(0.92f));
        handle.DrawCircle(center, radius * 0.86f, Color.InterpolateBetween(Night, accent, 0.08f + 0.1f * _glow));
        RingTexture(handle, center, radius, 1.6f * scale, accent.WithAlpha(0.65f + 0.35f * _glow));
        RingTexture(handle, center, radius * 0.8f, 1f * scale, accent.WithAlpha(0.25f + 0.2f * _glow));

        var ink = Color.InterpolateBetween(accent, Color.White, 0.35f * _glow);
        var s = radius * 0.42f;
        var w = 1.8f * scale;
        switch (_icon)
        {
            case RuneButtonIcon.Scroll:
                Line(handle, center + new Vector2(-s * 0.7f, -s), center + new Vector2(s * 0.7f, -s), w, ink);
                Line(handle, center + new Vector2(-s * 0.7f, s), center + new Vector2(s * 0.7f, s), w, ink);
                Line(handle, center + new Vector2(-s * 0.55f, -s), center + new Vector2(-s * 0.55f, s), w, ink);
                Line(handle, center + new Vector2(s * 0.55f, -s), center + new Vector2(s * 0.55f, s), w, ink);
                handle.DrawCircle(center + new Vector2(-s * 0.8f, -s), w * 1.4f, ink);
                handle.DrawCircle(center + new Vector2(s * 0.8f, s), w * 1.4f, ink);
                Line(handle, center + new Vector2(-s * 0.3f, -s * 0.4f), center + new Vector2(s * 0.3f, -s * 0.4f), w * 0.7f, ink.WithAlpha(0.7f));
                Line(handle, center + new Vector2(-s * 0.3f, s * 0.1f), center + new Vector2(s * 0.3f, s * 0.1f), w * 0.7f, ink.WithAlpha(0.7f));
                break;
            case RuneButtonIcon.Erase:
            {
                var a = center + new Vector2(-s, s * 0.2f);
                var b = center + new Vector2(-s * 0.2f, s);
                var c = center + new Vector2(s, -s * 0.2f);
                var d = center + new Vector2(s * 0.2f, -s);
                Line(handle, a, b, w, ink);
                Line(handle, b, c, w, ink);
                Line(handle, c, d, w, ink);
                Line(handle, d, a, w, ink);
                Line(handle, center + new Vector2(-s * 0.4f, -s * 0.4f), center + new Vector2(s * 0.4f, s * 0.4f), w, ink);
                Line(handle, center + new Vector2(-s, s * 1.15f), center + new Vector2(s, s * 1.15f), w * 0.7f, ink.WithAlpha(0.6f));
                break;
            }
            case RuneButtonIcon.Close:
                Line(handle, center + new Vector2(-s, -s), center + new Vector2(s, s), w * 1.2f, ink);
                Line(handle, center + new Vector2(-s, s), center + new Vector2(s, -s), w * 1.2f, ink);
                break;
            case RuneButtonIcon.Swap:
                Line(handle, center + new Vector2(-s, -s * 0.4f), center + new Vector2(s, -s * 0.4f), w, ink);
                Line(handle, center + new Vector2(s * 0.4f, -s * 0.9f), center + new Vector2(s, -s * 0.4f), w, ink);
                Line(handle, center + new Vector2(s, s * 0.4f), center + new Vector2(-s, s * 0.4f), w, ink);
                Line(handle, center + new Vector2(-s * 0.4f, s * 0.9f), center + new Vector2(-s, s * 0.4f), w, ink);
                break;
            case RuneButtonIcon.Collapse:
                Line(handle, center + new Vector2(-s, -s), center + new Vector2(s, -s), w, ink);
                Line(handle, center + new Vector2(-s, s), center + new Vector2(s, s), w, ink);
                Line(handle, center + new Vector2(-s, -s), center + new Vector2(-s, s), w, ink);
                Line(handle, center + new Vector2(s, -s), center + new Vector2(s, s), w, ink);
                Line(handle, center + new Vector2(-s * 0.1f, -s), center + new Vector2(-s * 0.1f, s), w, ink);
                handle.DrawCircle(center + new Vector2(-s * 0.55f, 0f), s * 0.32f, ink.WithAlpha(0.8f));
                break;
        }

        if (_small)
            return;

        var width = 0f;
        foreach (var rune in _caption.EnumerateRunes())
        {
            if (_font.TryGetCharMetrics(rune, 1f, out var metrics))
                width += metrics.Advance;
        }

        handle.DrawString(_font, new Vector2(center.X - width / 2f, center.Y + radius + 3f * scale), _caption, Color.White.WithAlpha(0.45f + 0.5f * _glow));
    }

    private void Glow(DrawingHandleScreen handle, Vector2 center, float diameter, Color color)
    {
        var size = new Vector2(diameter);
        handle.DrawTextureRect(RuneCircleArt.Glow(_clyde), UIBox2.FromDimensions(center - size / 2f, size), color);
    }

    private void RingTexture(DrawingHandleScreen handle, Vector2 center, float radius, float thickness, Color color)
    {
        var diameter = RuneCircleArt.RingDiameter(radius);
        var texture = RuneCircleArt.Ring(_clyde, thickness * RuneCircleArt.RingSize / diameter);
        var size = new Vector2(diameter);
        handle.DrawTextureRect(texture, UIBox2.FromDimensions(center - size / 2f, size), color);
    }

    private void Line(DrawingHandleScreen handle, Vector2 a, Vector2 b, float width, Color color)
    {
        Quad(handle, a, b, width + 1.2f, color.WithAlpha(color.A * 0.3f));
        Quad(handle, a, b, width, color);
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
}
