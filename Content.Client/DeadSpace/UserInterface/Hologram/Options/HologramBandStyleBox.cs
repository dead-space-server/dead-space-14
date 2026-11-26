// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Client.Graphics;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Options;

/// <summary>
/// A thin flat band centred in its box, with an optional 1px frame: the slim slider track, fill and grabber of the
/// "Dead Space" style. The box itself keeps its full size, so slider geometry and hit area don't change.
/// </summary>
public sealed class HologramBandStyleBox : StyleBox
{
    public Color BackgroundColor { get; set; }
    public Color BorderColor { get; set; }

    /// <summary>Band height in UI units, centred vertically; 0 - the full height of the box.</summary>
    public float BandHeight { get; set; }

    /// <summary>Band width in UI units, centred horizontally; 0 - the full width of the box.</summary>
    public float BandWidth { get; set; }

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        var left = box.Left;
        var right = box.Right;
        var top = box.Top;
        var bottom = box.Bottom;

        if (BandHeight > 0f)
        {
            var height = MathF.Min(box.Height, MathF.Max(1f, MathF.Round(BandHeight * uiScale)));
            top = MathF.Round(box.Center.Y - height / 2f);
            bottom = top + height;
        }

        if (BandWidth > 0f)
        {
            var width = MathF.Min(box.Width, MathF.Max(1f, MathF.Round(BandWidth * uiScale)));
            left = MathF.Round(box.Center.X - width / 2f);
            right = left + width;
        }

        if (right - left < 1f || bottom - top < 1f)
            return;

        var band = new UIBox2(left, top, right, bottom);

        if (BackgroundColor.A > 0f)
            handle.DrawRect(band, BackgroundColor);

        if (BorderColor.A > 0f)
            handle.DrawRect(band, BorderColor, filled: false);
    }
}
