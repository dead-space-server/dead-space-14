using System.Numerics;
using Content.Shared.DeadSpace.Fishing;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.Fishing;

public sealed class LavaFishingBar : Control
{
    public bool Active;
    public LavaFishingReelState State;
    public float ZoneWidth;
    public Color ZoneColor;

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (!Active)
            return;

        var zone = new UIBox2((State.ZonePosition - ZoneWidth / 2) * PixelWidth, 4 * UIScale,
            (State.ZonePosition + ZoneWidth / 2) * PixelWidth, PixelHeight - 4 * UIScale);
        handle.DrawRect(zone, ZoneColor);
        var inset = new Vector2(2 * UIScale);
        handle.DrawRect(new UIBox2(zone.TopLeft + inset, zone.BottomRight - inset), Color.Black.WithAlpha(0.65f));

        var marker = State.FishPosition * PixelWidth;
        handle.DrawRect(new UIBox2(marker - 3 * UIScale, 0, marker + 3 * UIScale, PixelHeight), Color.Black);
        handle.DrawRect(new UIBox2(marker - UIScale, UIScale, marker + UIScale, PixelHeight - UIScale), Color.White);
        handle.DrawRect(new UIBox2(marker - 4 * UIScale, UIScale, marker + 4 * UIScale, 3 * UIScale), Color.White);
        handle.DrawRect(new UIBox2(marker - 4 * UIScale, PixelHeight - 3 * UIScale, marker + 4 * UIScale, PixelHeight - UIScale), Color.White);
    }
}
