using System.Numerics;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;

namespace Content.Client.DeadSpace.Halloween;

/// <summary>
/// Shows the name of the candy effect in a band across the top of the screen, smoothly fading in and out.
/// </summary>
public sealed class CandyAnnounceOverlay : Overlay
{
    [Dependency] private readonly IResourceCache _resources = default!;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private const string FontPath = "/Fonts/NotoSansDisplay/NotoSansDisplay-Bold.ttf";
    private const int FontSize = 48;
    private const float FadeIn = 0.6f;
    private const float FadeOut = 1.2f;

    private static readonly Color TextColor = Color.FromHex("#ff9a2e");
    private static readonly Color ShadowColor = Color.FromHex("#2a0f00");

    private readonly Font _font;

    private string _text = string.Empty;
    private float _duration;
    private float _elapsed;

    public CandyAnnounceOverlay()
    {
        IoCManager.InjectDependencies(this);
        _font = _resources.GetFont(FontPath, FontSize);
        ZIndex = 200;
    }

    public void Show(string text, float duration)
    {
        _text = text;
        _duration = MathF.Max(1f, duration);
        _elapsed = 0f;
    }

    public void FrameUpdate(float frameTime)
    {
        if (_elapsed >= _duration)
            return;

        _elapsed += frameTime;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _elapsed < _duration && _text.Length > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;
        var width = (float) bounds.Width;
        var height = (float) bounds.Height;
        var origin = new Vector2(bounds.Left, bounds.Top);

        // Smoothly fades in at the top of the screen and fades out at the end.
        var envelope = MathF.Min(1f, _elapsed / FadeIn) * MathF.Min(1f, (_duration - _elapsed) / FadeOut);
        envelope = Math.Clamp(envelope, 0f, 1f);

        var scale = Math.Clamp(height / 1080f, 0.55f, 1.6f);
        var size = handle.GetDimensions(_font, _text, scale);
        var bandHeight = size.Y * 1.6f;
        var bandTop = origin.Y + height * 0.08f;

        var band = new UIBox2(origin.X, bandTop, origin.X + width, bandTop + bandHeight);
        handle.DrawRect(band, ShadowColor.WithAlpha(0.35f * envelope));

        var pos = new Vector2(origin.X + (width - size.X) / 2f, bandTop + (bandHeight - size.Y) / 2f);
        handle.DrawString(_font, pos + new Vector2(2f, 2f), _text, scale, ShadowColor.WithAlpha(0.9f * envelope));
        handle.DrawString(_font, pos, _text, scale, TextColor.WithAlpha(envelope));
    }
}
