// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Medical;

/// <summary>
/// A column of segments lit from the bottom up, like the health spine of a RIG suit.
/// </summary>
public sealed class HoloSegmentMeter : Control
{
    private const float SegmentWidth = 22f;
    private const float SegmentHeight = 10f;
    private const float Gap = 3f;
    private const float EmptyFill = 0.12f;
    private const float EmptyFrame = 0.35f;

    private int _segments = 12;
    private float _fraction;

    public Color FillColor { get; set; } = Color.White;

    public int Segments
    {
        get => _segments;
        set
        {
            _segments = Math.Max(1, value);
            InvalidateMeasure();
        }
    }

    /// <summary>Lit share from 0 to 1.</summary>
    public float Fraction
    {
        get => _fraction;
        set => _fraction = float.IsNaN(value) ? 0f : Math.Clamp(value, 0f, 1f);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return new Vector2(SegmentWidth, _segments * SegmentHeight + (_segments - 1) * Gap);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var gap = Gap * UIScale;
        var height = (PixelSize.Y - gap * (_segments - 1)) / _segments;
        if (height <= 0f || PixelSize.X <= 0f)
            return;

        // Any health left at all keeps the bottom segment lit, so it never reads as an empty column.
        var lit = (int) MathF.Round(_fraction * _segments);
        if (lit == 0 && _fraction > 0f)
            lit = 1;

        for (var i = 0; i < _segments; i++)
        {
            var bottom = PixelSize.Y - i * (height + gap);
            var rect = new UIBox2(0f, bottom - height, PixelSize.X, bottom);
            if (i < lit)
            {
                handle.DrawRect(rect, FillColor);
                continue;
            }

            handle.DrawRect(rect, FillColor.WithAlpha(EmptyFill));
            handle.DrawRect(rect, FillColor.WithAlpha(EmptyFrame), false);
        }
    }
}
