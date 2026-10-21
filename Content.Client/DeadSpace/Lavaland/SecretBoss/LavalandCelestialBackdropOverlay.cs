// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client.DeadSpace.Lavaland.SecretBoss;

/// <summary>
/// While the Celestial intro plays: the arena is hidden behind darkness with purple flames licking up from below.
/// Two instances: the darkness under the Celestial cutscene overlay (his sprite stays visible),
/// the flames over it (its own backdrop is nearly opaque).
/// </summary>
public sealed class LavalandCelestialBackdropOverlay : Overlay
{
    private const float FadeIn = 0.4f;
    private const float FadeOut = 1.5f;
    private const int Flames = 26;
    private const int Embers = 60;

    private readonly bool _flames;
    private float _time;
    private float _elapsed = float.MaxValue;
    private float _duration;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public LavalandCelestialBackdropOverlay(bool flames)
    {
        _flames = flames;
        // The cutscene overlay has the default z-index 0.
        ZIndex = flames ? 1 : -1;
    }

    public void Show(float duration)
    {
        _duration = MathF.Max(0f, duration);
        _elapsed = 0f;
    }

    public void FrameUpdate(float frameTime)
    {
        _time += frameTime;
        _elapsed += frameTime;
    }

    private float Alpha()
    {
        if (_elapsed < FadeIn)
            return _elapsed / FadeIn;
        if (_elapsed < _duration)
            return 1f;
        var t = _elapsed - _duration;
        return t < FadeOut ? 1f - t / FadeOut : 0f;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return Alpha() > 0f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;
        var origin = new Vector2(bounds.Left, bounds.Top);
        var size = new Vector2(bounds.Width, bounds.Height);
        var alpha = Alpha();

        if (!_flames)
        {
            handle.DrawRect(new UIBox2(origin, origin + size), new Color(0.02f, 0f, 0.04f, alpha));
            return;
        }

        // Flame tongues along the bottom edge.
        for (var i = 0; i < Flames; i++)
        {
            var seed = i * 0.618034f % 1f;
            var x = origin.X + (i + 0.5f) / Flames * size.X + MathF.Sin(_time * 1.7f + i) * 14f;
            var height = size.Y * (0.18f + 0.14f * (0.5f + 0.5f * MathF.Sin(_time * (2.1f + seed) + i * 1.3f)));
            const int blobs = 9;
            for (var b = 0; b < blobs; b++)
            {
                var t = b / (float) (blobs - 1);
                var y = origin.Y + size.Y - t * height;
                var sway = MathF.Sin(_time * 3.3f + i * 0.7f + t * 4f) * 10f * t;
                var radius = (1f - t * 0.8f) * size.X / Flames * 0.9f;
                var color = Color.InterpolateBetween(new Color(0.75f, 0.35f, 1f), new Color(0.25f, 0f, 0.45f), t);
                handle.DrawCircle(new Vector2(x + sway, y), radius, color.WithAlpha(alpha * 0.22f * (1f - t * 0.7f)));
            }
        }

        // Embers drifting upward.
        for (var i = 0; i < Embers; i++)
        {
            var seed = i * 0.754877f % 1f;
            var speed = 60f + seed * 120f;
            var x = origin.X + (i * 0.381966f % 1f) * size.X + MathF.Sin(_time + i) * 20f;
            var y = origin.Y + size.Y - (_time * speed + seed * size.Y) % size.Y;
            var fade = 1f - (origin.Y + size.Y - y) / size.Y;
            handle.DrawCircle(new Vector2(x, y), 1.5f + seed * 2f, new Color(0.85f, 0.55f, 1f, alpha * 0.6f * fade));
        }
    }
}
