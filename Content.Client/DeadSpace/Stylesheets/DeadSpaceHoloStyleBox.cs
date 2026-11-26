// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client.DeadSpace.Stylesheets;

[Flags]
public enum HoloBrackets : byte
{
    None = 0,
    TopLeft = 1 << 0,
    TopRight = 1 << 1,
    BottomLeft = 1 << 2,
    BottomRight = 1 << 3,
    Top = TopLeft | TopRight,
    Bottom = BottomLeft | BottomRight,
    All = Top | Bottom,
}

/// <summary>
/// A projected panel of the "Dead Space" style: background with an optional cut lower-right corner, a thin frame
/// along it and bright L-shaped brackets on the corners.
/// </summary>
public sealed class DeadSpaceHoloStyleBox : StyleBox
{
    public Color BackgroundColor { get; set; }
    public Color BorderColor { get; set; }

    /// <summary>Frame thickness per side in UI units.</summary>
    public Thickness BorderThickness { get; set; } = new(1);

    /// <summary>Cut of the lower-right corner in UI units; 0 - square.</summary>
    public float Chamfer { get; set; }

    public Color BracketColor { get; set; }

    /// <summary>Length of each bracket arm in UI units; 0 - no brackets.</summary>
    public float BracketLength { get; set; }

    public float BracketThickness { get; set; } = 2f;

    public HoloBrackets Brackets { get; set; } = HoloBrackets.All;

    private readonly Vector2[] _triangle = new Vector2[3];

    public DeadSpaceHoloStyleBox()
    {
    }

    public DeadSpaceHoloStyleBox(DeadSpaceHoloStyleBox other) : base(other)
    {
        BackgroundColor = other.BackgroundColor;
        BorderColor = other.BorderColor;
        BorderThickness = other.BorderThickness;
        Chamfer = other.Chamfer;
        BracketColor = other.BracketColor;
        BracketLength = other.BracketLength;
        BracketThickness = other.BracketThickness;
        Brackets = other.Brackets;
    }

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        var chamfer = MathF.Min(Chamfer * uiScale, MathF.Min(box.Width, box.Height) / 2f);
        DrawBackground(handle, box, chamfer);

        if (BorderColor.A > 0f)
            DrawFrame(handle, box, chamfer, BorderThickness.Scale(uiScale));

        var length = BracketLength * uiScale;
        if (length > 0f && BracketColor.A > 0f && Brackets != HoloBrackets.None)
            DrawBrackets(handle, box, chamfer, length, MathF.Max(1f, MathF.Round(BracketThickness * uiScale)));
    }

    private void DrawBackground(DrawingHandleScreen handle, UIBox2 box, float chamfer)
    {
        if (BackgroundColor.A <= 0f)
            return;

        if (chamfer <= 0f)
        {
            handle.DrawRect(box, BackgroundColor);
            return;
        }

        // Everything above the cut, then the strip beside it, then the cut itself.
        handle.DrawRect(new UIBox2(box.Left, box.Top, box.Right, box.Bottom - chamfer), BackgroundColor);
        handle.DrawRect(new UIBox2(box.Left, box.Bottom - chamfer, box.Right - chamfer, box.Bottom), BackgroundColor);
        _triangle[0] = new Vector2(box.Right - chamfer, box.Bottom - chamfer);
        _triangle[1] = new Vector2(box.Right, box.Bottom - chamfer);
        _triangle[2] = new Vector2(box.Right - chamfer, box.Bottom);
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _triangle, BackgroundColor);
    }

    private void DrawFrame(DrawingHandleScreen handle, UIBox2 box, float chamfer, Thickness width)
    {
        if (width.Top > 0f)
            handle.DrawRect(new UIBox2(box.Left, box.Top, box.Right, box.Top + width.Top), BorderColor);
        if (width.Left > 0f)
            handle.DrawRect(new UIBox2(box.Left, box.Top, box.Left + width.Left, box.Bottom), BorderColor);
        if (width.Right > 0f)
            handle.DrawRect(new UIBox2(box.Right - width.Right, box.Top, box.Right, box.Bottom - chamfer), BorderColor);
        if (width.Bottom > 0f)
            handle.DrawRect(new UIBox2(box.Left, box.Bottom - width.Bottom, box.Right - chamfer, box.Bottom), BorderColor);

        var diagonal = MathF.Max(width.Right, width.Bottom);
        if (chamfer <= 0f || diagonal <= 0f)
            return;

        // The cut: a run of small squares along the diagonal reads as a crisp line at any scale.
        var steps = (int) MathF.Ceiling(chamfer);
        for (var i = 0; i < steps; i++)
        {
            var x = box.Right - chamfer + i;
            var y = box.Bottom - i - diagonal;
            handle.DrawRect(new UIBox2(x, y, x + diagonal, y + diagonal), BorderColor);
        }
    }

    private void DrawBrackets(DrawingHandleScreen handle, UIBox2 box, float chamfer, float length, float width)
    {
        length = MathF.Min(length, MathF.Min(box.Width, box.Height) / 3f);

        if ((Brackets & HoloBrackets.TopLeft) != 0)
        {
            handle.DrawRect(new UIBox2(box.Left, box.Top, box.Left + length, box.Top + width), BracketColor);
            handle.DrawRect(new UIBox2(box.Left, box.Top, box.Left + width, box.Top + length), BracketColor);
        }

        if ((Brackets & HoloBrackets.TopRight) != 0)
        {
            handle.DrawRect(new UIBox2(box.Right - length, box.Top, box.Right, box.Top + width), BracketColor);
            handle.DrawRect(new UIBox2(box.Right - width, box.Top, box.Right, box.Top + length), BracketColor);
        }

        if ((Brackets & HoloBrackets.BottomLeft) != 0)
        {
            handle.DrawRect(new UIBox2(box.Left, box.Bottom - width, box.Left + length, box.Bottom), BracketColor);
            handle.DrawRect(new UIBox2(box.Left, box.Bottom - length, box.Left + width, box.Bottom), BracketColor);
        }

        if ((Brackets & HoloBrackets.BottomRight) != 0)
        {
            // Sits before the cut, if there is one.
            var right = box.Right - chamfer;
            var bottom = box.Bottom - chamfer;
            handle.DrawRect(new UIBox2(right - length, box.Bottom - width, right, box.Bottom), BracketColor);
            handle.DrawRect(new UIBox2(box.Right - width, bottom - length, box.Right, bottom), BracketColor);
        }
    }

    protected override float GetDefaultContentMargin(Margin margin)
    {
        return margin switch
        {
            Margin.Top => BorderThickness.Top,
            Margin.Bottom => BorderThickness.Bottom,
            Margin.Right => BorderThickness.Right,
            Margin.Left => BorderThickness.Left,
            _ => throw new ArgumentOutOfRangeException(nameof(margin), margin, null),
        };
    }
}
