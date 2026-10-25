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
    public int Saw;
    public float SawWindow = 0.35f;
    public float SawPhase;
    public Vector2 SawAt;
    public bool Cone;
    public Vector2 ConeFrom;
    public bool HeadChoice;
    public Vector2 HeadFrom;
    public readonly List<(string Id, UIBox2 Box, Texture? Icon)> HeadBoxes = new();
    public Vector2 PanelPos;
    public Vector2 PanelSize;
    public bool Bubble;
    public Vector2 BubbleFrom;
    public Vector2 BubblePos;
    public Vector2 BubbleSize;
    public float Cover;
    public float Left = 0.035f;
    public float Right = 0.035f;
    public Vector2 LocalA;
    public Vector2 LocalB;
    public Vector2 Start;
    public Vector2 End;
    public EntityUid Anchor;
    public Color Ink = new(0.12f, 0.12f, 0.13f, 0.95f);
    public float Radius = 0.018f;
    public Vector2 ScreenStart;
    public Vector2 ScreenEnd;
    public readonly List<(Vector2 From, Vector2 To)> Cuts = new();

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
                Dash(handle, dir, 0.045f, Radius, Ink);
                break;
            case SurgeryMark.Fine:
                Dash(handle, dir, 0.028f, Radius * 0.7f, Ink);
                break;
            case SurgeryMark.Spread:
                Triangles(handle, dir, side, Ink);
                break;
            case SurgeryMark.Scissors:
                Scissors(handle, dir, side, Ink);
                break;
            case SurgeryMark.Spark:
                Spark(handle, Ink);
                break;
        }
        Moved?.Invoke();
    }

    public Action? Moved;

    private void Snap()
    {
        if (!IoCManager.Resolve<IEntityManager>().TryGetComponent(Anchor, out SpriteComponent? sprite) || sprite == null)
            return;

        var xform = IoCManager.Resolve<IEntityManager>().System<SharedTransformSystem>();
        var origin = xform.GetWorldPosition(Anchor);
        origin += sprite.Rotation.RotateVec(sprite.Offset);
        Start = origin + sprite.Rotation.RotateVec(LocalA);
        End = origin + sprite.Rotation.RotateVec(LocalB);
    }

    private void Dash(DrawingHandleWorld handle, Vector2 dir, float step, float radius, Color ink)
    {
        var count = Math.Max(3, (int) ((End - Start).Length() / step));
        for (var i = 0; i <= count; i += 2)
        {
            var t = i / (float) count;
            handle.DrawCircle(Start + (End - Start) * t, radius, ink);
        }
    }

    private void Triangles(DrawingHandleWorld handle, Vector2 dir, Vector2 side, Color ink)
    {
        var left = Math.Clamp(Left, 0.08f, 0.2f);
        var right = Math.Clamp(Right, 0.08f, 0.2f);
        for (var i = 1; i <= 3; i++)
        {
            var at = Start + (End - Start) * (i / 4f);
            Triangle(handle, at - side * left, -side, dir, ink);
            Triangle(handle, at + side * right, side, dir, ink);
        }
    }

    private static void Triangle(DrawingHandleWorld handle, Vector2 tip, Vector2 aim, Vector2 dir, Color ink)
    {
        var baseAt = tip - aim * 0.045f;
        var wing = dir * 0.02f;
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
        if (HeadChoice)
        {
            foreach (var (_, box, icon) in HeadBoxes)
            {
                handle.DrawRect(box, new Color(0.04f, 0.08f, 0.12f, 0.92f));
                if (icon != null)
                    handle.DrawTextureRect(icon, box, Color.White);
                else
                {
                    var tooth = UIBox2.FromDimensions(box.TopLeft + new Vector2(12f, 8f), new Vector2(8f, 10f));
                    handle.DrawRect(tooth, new Color(0.93f, 0.9f, 0.82f, 1f));
                    handle.DrawRect(UIBox2.FromDimensions(tooth.TopLeft + new Vector2(1f, 10f), new Vector2(2f, 6f)), new Color(0.93f, 0.9f, 0.82f, 1f));
                    handle.DrawRect(UIBox2.FromDimensions(tooth.TopLeft + new Vector2(5f, 10f), new Vector2(2f, 6f)), new Color(0.93f, 0.9f, 0.82f, 1f));
                }
            }

            if (HeadBoxes.Count > 0)
            {
                var first = HeadBoxes[0].Box;
                var tip = HeadFrom;
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, new[]
                {
                    tip,
                    first.TopLeft,
                    first.TopRight,
                }, new Color(0.55f, 0.72f, 0.78f, 0.2f));
            }
        }

        if (Cone && PanelSize.X > 8f)
        {
            var tl = PanelPos;
            var tr = PanelPos + new Vector2(PanelSize.X, 0f);
            var br = PanelPos + PanelSize;
            var bl = PanelPos + new Vector2(0f, PanelSize.Y);
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, new[] { ConeFrom, tl, tr, br, bl }, new Color(0.55f, 0.72f, 0.78f, 0.16f));
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
                var fill = new Color(0.96f, 0.97f, 0.98f, 0.94f);
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, new[] { tip, root + wing, root - wing }, fill);
            }
        }

        if (!SawBar)
            return;

        var left = SawAt.X - 80f;
        var top = SawAt.Y - 42f;
        var window = Math.Clamp(SawWindow, 0.08f, 0.92f);
        var span = MathF.Max(14f, 36f - Saw * 6f);
        handle.DrawRect(UIBox2.FromDimensions(new Vector2(left, top), new Vector2(160, 16)), new Color(0.06f, 0.06f, 0.07f, 1f));
        handle.DrawRect(UIBox2.FromDimensions(new Vector2(left + window * 160f - span / 2f, top), new Vector2(span, 16)), new Color(0.93f, 0.91f, 0.84f, 1f));
        var head = left + SawPhase * 160f;
        handle.DrawRect(UIBox2.FromDimensions(new Vector2(head - 5f, top - 5f), new Vector2(10, 26)), new Color(0.86f, 0.14f, 0.12f, 1f));
        for (var i = 0; i < 3; i++)
        {
            var pip = UIBox2.FromDimensions(new Vector2(left + i * 18f, top + 20f), new Vector2(12, 8));
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
}
