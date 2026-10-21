// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using System.Text;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;

namespace Content.Client.DeadSpace.Lavaland;

/// <summary>
/// "ABSOLUTE RAGE" of the megafauna: red text on a dark red band across the upper screen, broken by interference -
/// colour split, jitter, tearing bars, static grain, scrambled letters and dropouts, with a red flash at the start.
/// </summary>
public sealed class LavalandBossRageAnnouncementOverlay : Overlay
{
    [Dependency] private readonly IResourceCache _resources = default!;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private const string FontPath = "/Fonts/NotoSansDisplay/NotoSansDisplay-Bold.ttf";
    private const int FontSize = 72;
    private const float GlitchTick = 0.06f;
    private const float FadeIn = 0.15f;
    private const float FadeOut = 0.45f;
    private const float FlashTime = 0.25f;
    private const string ScrambleGlyphs = "#%@&$!?/\\<>=+*";

    private static readonly Color TextColor = Color.FromHex("#ff2020");
    private static readonly Color ShadowColor = Color.FromHex("#5a0000");
    private static readonly Color SplitColor = Color.FromHex("#ff9a9a");
    private static readonly Color BandColor = Color.FromHex("#1a0000");

    private readonly Font _font;
    private readonly Random _random = new();
    private readonly List<(float Y, float Height, float X, float Width, Color Color)> _bars = new();
    private readonly List<(float X, float Y, float Alpha)> _grain = new();

    private string _text = string.Empty;
    private string _shown = string.Empty;
    private float _duration;
    private float _elapsed;
    private float _tickTimer;
    private Vector2 _jitter;
    private float _split;
    private float _flicker = 1f;
    private bool _dropout;

    public LavalandBossRageAnnouncementOverlay()
    {
        IoCManager.InjectDependencies(this);
        _font = _resources.GetFont(FontPath, FontSize);
        ZIndex = 200;
    }

    public void Show(string text, float duration)
    {
        _text = text;
        _shown = text;
        _duration = MathF.Max(1f, duration);
        _elapsed = 0f;
        _tickTimer = 0f;
    }

    public void FrameUpdate(float frameTime)
    {
        if (_elapsed >= _duration)
            return;

        _elapsed += frameTime;
        _tickTimer -= frameTime;
        if (_tickTimer <= 0f)
        {
            _tickTimer = GlitchTick;
            Reglitch();
        }
    }

    private void Reglitch()
    {
        // Calmer in the middle, wild at the start and at the end.
        var heavy = _elapsed < 0.5f || _elapsed > _duration - FadeOut;
        var bigJump = _random.NextDouble() < (heavy ? 0.35 : 0.12);
        var reach = bigJump ? 34f : 5f;
        _jitter = new Vector2(NextRange(-reach, reach), NextRange(-reach, reach) * 0.25f);
        _split = NextRange(2f, heavy ? 12f : 6f);
        _flicker = _random.NextDouble() < 0.12 ? NextRange(0.25f, 0.6f) : 1f;
        _dropout = _random.NextDouble() < (heavy ? 0.12 : 0.04);

        var builder = new StringBuilder(_text.Length);
        foreach (var c in _text)
        {
            builder.Append(!char.IsWhiteSpace(c) && _random.NextDouble() < (heavy ? 0.14 : 0.05)
                ? ScrambleGlyphs[_random.Next(ScrambleGlyphs.Length)]
                : c);
        }
        _shown = builder.ToString();

        _bars.Clear();
        var bars = _random.Next(heavy ? 8 : 4, heavy ? 16 : 9);
        for (var i = 0; i < bars; i++)
        {
            var roll = _random.NextDouble();
            var color = roll < 0.5 ? BandColor : roll < 0.8 ? TextColor : Color.White;
            _bars.Add((NextRange(0f, 1f), NextRange(0.01f, 0.07f), NextRange(-0.2f, 0.9f), NextRange(0.1f, 0.8f),
                color.WithAlpha(NextRange(0.2f, 0.7f))));
        }

        _grain.Clear();
        for (var i = 0; i < 160; i++)
            _grain.Add((NextRange(0f, 1f), NextRange(0f, 1f), NextRange(0.1f, 0.6f)));
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

        var envelope = MathF.Min(1f, _elapsed / FadeIn) * MathF.Min(1f, (_duration - _elapsed) / FadeOut);
        envelope = Math.Clamp(envelope, 0f, 1f);

        // Red flash with static at the very start.
        if (_elapsed < FlashTime)
        {
            var flash = 1f - _elapsed / FlashTime;
            handle.DrawRect(new UIBox2(origin, origin + new Vector2(width, height)), TextColor.WithAlpha(0.28f * flash));
            for (var i = 0; i < _grain.Count; i++)
            {
                var (gx, gy, ga) = _grain[i];
                var p = origin + new Vector2(gx * width, (gy + i * 0.37f % 1f) % 1f * height);
                handle.DrawRect(new UIBox2(p, p + new Vector2(3f, 3f)), Color.White.WithAlpha(ga * flash));
            }
        }

        var scale = Math.Clamp(height / 1080f, 0.55f, 1.6f);
        var size = handle.GetDimensions(_font, _text, scale);
        var bandHeight = size.Y * 1.7f;
        var bandTop = origin.Y + height * 0.2f - bandHeight / 2f;
        var band = new UIBox2(origin.X, bandTop, origin.X + width, bandTop + bandHeight);

        // Band, scanlines, grain.
        handle.DrawRect(band, BandColor.WithAlpha(0.6f * envelope));
        for (var y = band.Top; y < band.Bottom; y += 3f)
            handle.DrawRect(new UIBox2(band.Left, y, band.Right, y + 1f), Color.Black.WithAlpha(0.3f * envelope));
        foreach (var (gx, gy, ga) in _grain)
        {
            var p = new Vector2(band.Left + gx * width, band.Top + gy * bandHeight);
            handle.DrawRect(new UIBox2(p, p + new Vector2(2f, 2f)), TextColor.WithAlpha(ga * 0.5f * envelope));
        }

        // Text: colour split around the red core.
        if (!_dropout)
        {
            var alpha = envelope * _flicker;
            var pos = new Vector2(origin.X + (width - size.X) / 2f, band.Top + (bandHeight - size.Y) / 2f) + _jitter;
            handle.DrawString(_font, pos + new Vector2(-_split, 1.5f), _shown, scale, ShadowColor.WithAlpha(0.9f * alpha));
            handle.DrawString(_font, pos + new Vector2(_split, -1.5f), _shown, scale, SplitColor.WithAlpha(0.55f * alpha));
            handle.DrawString(_font, pos, _shown, scale, TextColor.WithAlpha(alpha));
        }

        // Tearing bars over everything in the band.
        foreach (var (by, bh, bx, bw, color) in _bars)
        {
            var top = band.Top + by * bandHeight;
            var left = band.Left + bx * width;
            handle.DrawRect(new UIBox2(left, top, left + bw * width, top + bh * bandHeight),
                color.WithAlpha(color.A * envelope));
        }
    }

    private float NextRange(float min, float max)
    {
        return min + (float) _random.NextDouble() * (max - min);
    }
}
