// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.RoundEnd.Credits;

public sealed class CreditsScrollHost : Control
{
    private float _width = 1f;
    private float _offset;

    public float ContentHeight { get; private set; }

    public void SetLayout(float width, float offset)
    {
        if (!MathHelper.CloseTo(width, _width))
        {
            _width = width;
            InvalidateMeasure();
        }

        if (MathHelper.CloseTo(offset, _offset, 0.1f))
            return;

        _offset = offset;
        InvalidateArrange();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        ContentHeight = 0f;
        foreach (var child in Children)
        {
            child.Measure(new Vector2(_width, float.PositiveInfinity));
            ContentHeight = Math.Max(ContentHeight, child.DesiredSize.Y);
        }

        return Vector2.Zero;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var left = (finalSize.X - _width) / 2f;
        foreach (var child in Children)
        {
            child.Arrange(UIBox2.FromDimensions(left, _offset, _width, child.DesiredSize.Y));
        }

        return finalSize;
    }
}
