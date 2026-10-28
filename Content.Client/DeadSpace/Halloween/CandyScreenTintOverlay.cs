using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Maths;

namespace Content.Client.DeadSpace.Halloween;

/// <summary>
/// Slightly greens the player's screen (YourElement candy effect).
/// </summary>
public sealed class CandyScreenTintOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private static readonly Color TintColor = Color.FromHex("#2fa832");

    protected override void Draw(in OverlayDrawArgs args)
    {
        var bounds = args.ViewportBounds;
        var rect = new UIBox2(
            (float) bounds.Left, (float) bounds.Top, (float) bounds.Right, (float) bounds.Bottom);
        args.ScreenHandle.DrawRect(rect, TintColor.WithAlpha(0.32f));
    }
}
