// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Client.DeadSpace.Surgery;

public sealed class SurgeryGuideOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;

    public bool Active;
    public SurgeryMark Mark;
    public bool SawBar;
    public bool SawVertical;
    public bool Point;
    public int SawNeed = 3;
    public int Saw;
    public float SawWindow = 0.35f;
    public float SawPhase;
    public Vector2 SawAt;
    public bool Cone;
    public Vector2 ConeFrom;
    public Vector2 PanelPos;
    public Vector2 PanelSize;
    public bool Bubble;
    public Vector2 BubbleFrom;
    public Vector2 BubblePos;
    public Vector2 BubbleSize;
    public float Cover;
    public float Fill;
    public float Left = 0.035f;
    public float Right = 0.035f;
    public bool Inward;
    public Vector2 LocalA;
    public Vector2 LocalB;
    public Vector2 Start;
    public Vector2 End;
    public EntityUid Anchor;
    public Color Ink = new(0.12f, 0.12f, 0.13f, 0.95f);
    public float Radius = 0.014f;
    public Vector2 ScreenStart;
    public Vector2 ScreenEnd;
    public readonly List<(Vector2 From, Vector2 To)> Cuts = new();
    public readonly List<(float Along, float Left)> Sear = new();
    public readonly List<(int Crack, Vector2 At, bool Done)> Glue = new();
    public bool SearGreen;
    public Color BubbleColor = SurgeryHint.Back;
    public float SawHalf = 0.12f;
    public Func<EntityUid, Vector2, Vector2>? Map;

    public SurgeryGuideOverlay()
    {
        ZIndex = 200;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Space == OverlaySpace.ScreenSpace)
        {
            Snap();
            DrawScreen(args.ScreenHandle);
            return;
        }

        var handle = args.WorldHandle;
        Snap();
        foreach (var cut in Cuts)
        {
            handle.DrawLine(cut.From, cut.To, new Color(0.55f, 0.08f, 0.08f, 0.9f));
        }

        if (!Active || Mark == SurgeryMark.None)
            return;

        var delta = End - Start;
        var length = delta.Length();
        if (length < 0.01f)
            return;

        var dir = delta / length;
        var side = new Vector2(-dir.Y, dir.X);
        switch (Mark)
        {
            case SurgeryMark.Dash:
                Dash(handle, dir, Radius * 7f, Radius, Ink);
                if (Sear.Count > 0)
                    Sears(handle);
                break;
            case SurgeryMark.Fine:
                Dash(handle, dir, Radius * 5f, Radius * 0.7f, Ink);
                break;
            case SurgeryMark.Spread:
                Triangles(handle, dir, side, Ink);
                break;
            case SurgeryMark.Scissors:
                Scissors(handle, dir, side, Ink);
                break;
            case SurgeryMark.Sear:
                Sears(handle);
                break;
            case SurgeryMark.Glue:
                Dash(handle, dir, Radius * 5f, Radius * 0.5f, Ink.WithAlpha(0.45f));
                Cracks(handle);
                break;
            case SurgeryMark.Spark:
                if (Point)
                    handle.DrawCircle((Start + End) * 0.5f, 0.05f, new Color(0.98f, 0.55f, 0.12f, 0.95f));
                else
                    Spark(handle, Ink);
                break;
        }
        Moved?.Invoke();
    }

    public Action? Moved;

    private void Snap()
    {
        if (Map == null || !IoCManager.Resolve<IEntityManager>().EntityExists(Anchor))
            return;

        Start = Map(Anchor, LocalA);
        End = Map(Anchor, LocalB);
    }

    private void Cracks(DrawingHandleWorld handle)
    {
        var open = new Color(0.1f, 0.06f, 0.05f, 0.95f);
        var glued = new Color(0.93f, 0.9f, 0.72f, 0.95f);
        for (var i = 1; i < Glue.Count; i++)
        {
            var (crack, at, done) = Glue[i];
            var (before, from, doneBefore) = Glue[i - 1];
            if (crack != before)
                continue;

            var ink = done && doneBefore ? glued : open;
            var span = at - from;
            var length = span.Length();
            if (length < 0.001f)
                continue;

            var side = new Vector2(-span.Y, span.X) / length * 0.009f;
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, new[] { from - side, from + side, at + side, at - side }, ink);
        }

        foreach (var (_, at, done) in Glue)
            handle.DrawCircle(at, 0.012f, done ? glued : open);
    }

    private bool PixelSears(DrawingHandleWorld handle, Color rim, Color core)
    {
        if (!Texels(out var a, out var b))
            return false;

        foreach (var (along, left) in Sear)
        {
            if (left <= 0f)
                continue;

            var c = a + (b - a) * along;
            var r = MathF.Max(0.75f, left * 32f);
            for (var y = (int) MathF.Floor(c.Y - r - 1); y <= (int) MathF.Ceiling(c.Y + r + 1); y++)
            {
                for (var x = (int) MathF.Floor(c.X - r - 1); x <= (int) MathF.Ceiling(c.X + r + 1); x++)
                {
                    var distance = (new Vector2(x + 0.5f, y + 0.5f) - c).Length();
                    if (distance <= r - 0.5f)
                        Pixel(handle, x, y, core);
                    else if (distance <= r + 0.5f)
                        Pixel(handle, x, y, rim);
                }
            }
        }

        return true;
    }

    private void Sears(DrawingHandleWorld handle)
    {
        var rim = SearGreen ? new Color(0.25f, 0.85f, 0.35f, 0.95f) : new Color(0.98f, 0.55f, 0.12f, 0.95f);
        var core = SearGreen ? new Color(0.6f, 1f, 0.65f, 0.5f) : new Color(1f, 0.86f, 0.45f, 0.55f);
        if (PixelSears(handle, rim, core))
            return;

        foreach (var (along, left) in Sear)
        {
            if (left <= 0f)
                continue;

            var at = Start + (End - Start) * along;
            handle.DrawCircle(at, left, core);
            handle.DrawCircle(at, left, rim, false);
        }
    }

    private bool Texels(out Vector2 a, out Vector2 b)
    {
        a = default;
        b = default;
        if (Map == null || !IoCManager.Resolve<IEntityManager>().EntityExists(Anchor))
            return false;

        a = new Vector2(LocalA.X * 32f + 16f, 16f - LocalA.Y * 32f);
        b = new Vector2(LocalB.X * 32f + 16f, 16f - LocalB.Y * 32f);
        return true;
    }

    private void Pixel(DrawingHandleWorld handle, int x, int y, Color ink)
    {
        var corners = new[]
        {
            Map!(Anchor, new Vector2((x - 16f) / 32f, (16f - y) / 32f)),
            Map(Anchor, new Vector2((x + 1 - 16f) / 32f, (16f - y) / 32f)),
            Map(Anchor, new Vector2((x + 1 - 16f) / 32f, (16f - y - 1) / 32f)),
            Map(Anchor, new Vector2((x - 16f) / 32f, (16f - y - 1) / 32f)),
        };
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, corners, ink);
    }

    private bool PixelDash(DrawingHandleWorld handle, Color ink, int on, int off)
    {
        if (!Texels(out var a, out var b))
            return false;

        var points = SurgeryWoundPainter.Trace(a, b);
        for (var i = 0; i < points.Count; i++)
        {
            if (i % (on + off) < on)
                Pixel(handle, points[i].X, points[i].Y, ink);
        }

        if (Fill > 0f)
        {
            var filled = (int) MathF.Round(points.Count * Math.Clamp(Fill, 0f, 1f));
            for (var i = 0; i < filled; i++)
                Pixel(handle, points[i].X, points[i].Y, new Color(0.35f, 0.9f, 0.45f, 0.95f));
        }

        return true;
    }

    private void Dash(DrawingHandleWorld handle, Vector2 dir, float dash, float radius, Color ink)
    {
        if (PixelDash(handle, ink, radius >= Radius * 0.9f ? 2 : 1, 1))
            return;

        var length = (End - Start).Length();
        dash = MathF.Min(dash, length * 0.3f);
        var gap = dash * 0.6f;
        var count = Math.Max(2, (int) MathF.Round((length + gap) / (dash + gap)));
        var period = (length + gap) / count;
        var piece = period - gap;
        var side = new Vector2(-dir.Y, dir.X) * radius;
        for (var i = 0; i < count; i++)
        {
            var a = Start + dir * (i * period);
            var b = a + dir * piece;
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, new[] { a - side, a + side, b + side, b - side }, ink);
        }
    }

    private void Triangles(DrawingHandleWorld handle, Vector2 dir, Vector2 side, Color ink)
    {
        var length = (End - Start).Length();
        var rows = length < 0.15f ? 1 : 2;
        var size = Math.Clamp(length * 0.45f, 0.035f, 0.075f);
        if (rows > 1)
            size = MathF.Min(size, length / (rows + 1f) * 0.75f);
        var near = Inward ? 0.02f : MathF.Max(Math.Clamp(length * 0.25f, 0.02f, 0.08f), size + 0.01f);
        var far = MathF.Max(Math.Clamp(length * 0.7f, Inward ? 0.08f : near, 0.2f), near);
        var left = Math.Clamp(Left, near, far);
        var right = Math.Clamp(Right, near, far);
        var aim = Inward ? -1f : 1f;
        for (var i = 1; i <= rows; i++)
        {
            var at = Start + (End - Start) * (i / (rows + 1f));
            Triangle(handle, at - side * left, -side * aim, dir, size, ink);
            Triangle(handle, at + side * right, side * aim, dir, size, ink);
        }
    }

    private static void Triangle(DrawingHandleWorld handle, Vector2 tip, Vector2 aim, Vector2 dir, float size, Color ink)
    {
        var baseAt = tip - aim * size;
        var wing = dir * size * 0.6f;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, new[] { tip, baseAt + wing, baseAt - wing }, ink);
    }

    private void Scissors(DrawingHandleWorld handle, Vector2 dir, Vector2 side, Color ink)
    {
        var mid = (Start + End) * 0.5f;
        var a = dir * 0.04f;
        var b = side * 0.012f;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, new[] { mid + a, mid - a + b, mid - a - b }, ink);
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, new[] { mid - a, mid + a + b, mid + a - b }, ink);
    }

    private void Spark(DrawingHandleWorld handle, Color ink)
    {
        var count = 5;
        var hot = new Color(0.98f, 0.55f, 0.12f, 0.95f);
        var core = new Color(1f, 0.92f, 0.55f, 0.95f);
        for (var i = 0; i <= count; i++)
        {
            var at = Start + (End - Start) * (i / (float) count);
            handle.DrawCircle(at, 0.04f, hot);
            handle.DrawCircle(at, 0.016f, core);
            handle.DrawCircle(at, 0.006f, ink);
        }
    }

    private void DrawScreen(DrawingHandleScreen handle)
    {
        if (Cone && PanelSize.X > 8f)
        {
            var tl = PanelPos + new Vector2(10f, 10f);
            var tr = PanelPos + new Vector2(PanelSize.X - 10f, 10f);
            var br = PanelPos + PanelSize - new Vector2(10f, 10f);
            var bl = PanelPos + new Vector2(10f, PanelSize.Y - 10f);
            var (a, b) = ConeFrom.X < PanelPos.X ? (tl, bl)
                : ConeFrom.X > PanelPos.X + PanelSize.X ? (tr, br)
                : ConeFrom.Y < PanelPos.Y ? (tl, tr)
                : (bl, br);
            var ray = new Color(0.78f, 0.27f, 0.27f, 0.7f);
            handle.DrawLine(ConeFrom, a, ray);
            handle.DrawLine(ConeFrom, b, ray);
            handle.DrawCircle(ConeFrom, 4f, SurgeryHint.Back);
            handle.DrawCircle(ConeFrom, 2.5f, ray.WithAlpha(1f));
        }

        if (Bubble && BubbleSize.X > 4f)
        {
            var center = BubblePos + BubbleSize / 2f;
            var aim = BubbleFrom - center;
            var length = aim.Length();
            if (length > 1f)
            {
                var dir = aim / length;
                var wing = new Vector2(-dir.Y, dir.X) * 8f;
                var reach = BubbleSize.Y / 2f;
                var tip = center + dir * (reach + 22f);
                var root = center + dir * reach;
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, new[] { tip, root + wing, root - wing }, BubbleColor);
            }
        }

        if (!SawBar)
            return;

        var window = Math.Clamp(SawWindow, 0.08f, 0.92f);
        var span = SawHalf * 320f;
        var track = SawVertical
            ? UIBox2.FromDimensions(new Vector2(SawAt.X + 28f, SawAt.Y - 80f), new Vector2(16, 160))
            : UIBox2.FromDimensions(new Vector2(SawAt.X - 80f, SawAt.Y - 42f), new Vector2(160, 16));
        handle.DrawRect(track, new Color(0.06f, 0.06f, 0.07f, 1f));
        var gate = SawVertical
            ? UIBox2.FromDimensions(new Vector2(track.Left, track.Top + window * 160f - span / 2f), new Vector2(16, span))
            : UIBox2.FromDimensions(new Vector2(track.Left + window * 160f - span / 2f, track.Top), new Vector2(span, 16));
        handle.DrawRect(gate, new Color(0.93f, 0.91f, 0.84f, 1f));
        var head = SawVertical
            ? UIBox2.FromDimensions(new Vector2(track.Left - 5f, track.Top + SawPhase * 160f - 5f), new Vector2(26, 10))
            : UIBox2.FromDimensions(new Vector2(track.Left + SawPhase * 160f - 5f, track.Top - 5f), new Vector2(10, 26));
        handle.DrawRect(head, new Color(0.86f, 0.14f, 0.12f, 1f));
        var pips = Math.Clamp(SawNeed, 1, 6);
        for (var i = 0; i < pips; i++)
        {
            var pip = SawVertical
                ? UIBox2.FromDimensions(new Vector2(track.Right + 4f, track.Top + i * 18f), new Vector2(8, 12))
                : UIBox2.FromDimensions(new Vector2(track.Left + i * 18f, track.Bottom + 4f), new Vector2(12, 8));
            handle.DrawRect(pip, i < Saw ? new Color(0.9f, 0.2f, 0.14f, 1f) : new Color(0.22f, 0.22f, 0.24f, 1f));
        }
    }
}

public enum SurgeryMark
{
    None,
    Dash,
    Fine,
    Spread,
    Scissors,
    Spark,
    Sear,
    Glue,
}
