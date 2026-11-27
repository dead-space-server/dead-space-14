// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.DeadSpace.Storyteller;

public sealed class StoryFlowContainer : Container
{
    public float Gap = 4f;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = float.IsFinite(availableSize.X) ? availableSize.X : float.PositiveInfinity;
        var x = 0f;
        var lineHeight = 0f;
        var height = 0f;
        var widest = 0f;
        foreach (var child in Children)
        {
            if (!child.Visible)
                continue;

            child.Measure(new Vector2(width, float.PositiveInfinity));
            var size = child.DesiredSize;
            if (x > 0f && x + size.X > width)
            {
                height += lineHeight + Gap;
                x = 0f;
                lineHeight = 0f;
            }

            x += size.X + Gap;
            widest = MathF.Max(widest, x);
            lineHeight = MathF.Max(lineHeight, size.Y);
        }

        height += lineHeight;
        return new Vector2(float.IsFinite(width) ? MathF.Min(widest, width) : widest, height);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var x = 0f;
        var y = 0f;
        var lineHeight = 0f;
        foreach (var child in Children)
        {
            if (!child.Visible)
                continue;

            var size = child.DesiredSize;
            if (x > 0f && x + size.X > finalSize.X)
            {
                y += lineHeight + Gap;
                x = 0f;
                lineHeight = 0f;
            }

            child.Arrange(UIBox2.FromDimensions(new Vector2(x, y), size));
            x += size.X + Gap;
            lineHeight = MathF.Max(lineHeight, size.Y);
        }

        return finalSize;
    }
}
