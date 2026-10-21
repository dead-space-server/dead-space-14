// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;

namespace Content.Client.DeadSpace.Lavaland.Bosses;

/// <summary>Cinema bars and the boss name of the boss intro.</summary>
public sealed class LavalandBossIntroOverlay : Overlay
{
    private const string FontPath = "/Fonts/NotoSans/NotoSans-Bold.ttf";
    private static readonly Color SubtitleColor = Color.FromHex("#ffd9b0");

    private readonly Font _titleFont;
    private readonly Font _subtitleFont;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    /// <summary>0..1: how far the black bars have come in.</summary>
    public float Bars;

    /// <summary>0..1: opacity of the name.</summary>
    public float TitleAlpha;

    public string Title = string.Empty;

    /// <summary>Colour of the name (red for the bloodier bosses).</summary>
    public Color TitleColor = Color.FromHex("#ff7a2e");
    public string? Subtitle;

    public LavalandBossIntroOverlay(IResourceCache cache)
    {
        _titleFont = cache.GetFont(FontPath, 56);
        _subtitleFont = cache.GetFont(FontPath, 22);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return Bars > 0f || TitleAlpha > 0f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.ScreenHandle;
        var size = (Vector2) args.ViewportBounds.Size;
        var origin = (Vector2) args.ViewportBounds.TopLeft;

        // Cinema bars from the top and the bottom.
        var bar = size.Y * 0.12f * Math.Clamp(Bars, 0f, 1f);
        if (bar > 0f)
        {
            handle.DrawRect(new UIBox2(origin, origin + new Vector2(size.X, bar)), Color.Black);
            handle.DrawRect(new UIBox2(origin + new Vector2(0f, size.Y - bar), origin + size), Color.Black);
        }

        var alpha = Math.Clamp(TitleAlpha, 0f, 1f);
        if (alpha <= 0f || Title.Length == 0)
            return;

        var title = Title.ToUpperInvariant();
        var titleSize = handle.GetDimensions(_titleFont, title, 1f);
        var titlePos = origin + new Vector2((size.X - titleSize.X) / 2f, size.Y * 0.66f);

        // A dark band behind the name, then the name with a shadow; it slides up a little as it appears.
        titlePos.Y += (1f - alpha) * 12f;
        var bandTop = titlePos.Y - 14f;
        var bandBottom = titlePos.Y + titleSize.Y + (Subtitle != null ? 46f : 14f);
        handle.DrawRect(new UIBox2(origin.X, bandTop, origin.X + size.X, bandBottom), Color.Black.WithAlpha(0.55f * alpha));
        handle.DrawRect(new UIBox2(origin.X + size.X * 0.2f, bandTop, origin.X + size.X * 0.8f, bandTop + 2f),
            TitleColor.WithAlpha(alpha));

        handle.DrawString(_titleFont, titlePos + new Vector2(3f, 3f), title, Color.Black.WithAlpha(alpha));
        handle.DrawString(_titleFont, titlePos, title, TitleColor.WithAlpha(alpha));

        if (Subtitle is not { } subtitle)
            return;

        var subtitleSize = handle.GetDimensions(_subtitleFont, subtitle, 1f);
        var subtitlePos = new Vector2(origin.X + (size.X - subtitleSize.X) / 2f, titlePos.Y + titleSize.Y + 6f);
        handle.DrawString(_subtitleFont, subtitlePos + new Vector2(2f, 2f), subtitle, Color.Black.WithAlpha(alpha));
        handle.DrawString(_subtitleFont, subtitlePos, subtitle, SubtitleColor.WithAlpha(alpha));
    }
}
