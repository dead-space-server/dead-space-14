// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.RoundEnd.Credits;

public sealed class CreditsProgressBar : Control
{
    public float Value { get; set; } = 1f;

    public Color FillColor { get; set; } = Color.White;

    public Color BackColor { get; set; } = Color.White.WithAlpha(0.12f);

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        handle.DrawRect(new UIBox2(0, 0, size.X, size.Y), BackColor);
        handle.DrawRect(new UIBox2(0, 0, size.X * Math.Clamp(Value, 0f, 1f), size.Y), FillColor);
    }
}
