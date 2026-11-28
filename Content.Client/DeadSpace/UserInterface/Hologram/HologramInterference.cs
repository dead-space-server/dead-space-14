// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.DeadSpace.Stylesheets;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.CCCCVars;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.UserInterface.Hologram;

/// <summary>
/// Projection interference laid over a part of the "Dead Space" interface: fine scanlines, a slow band of light
/// crawling down, a projector glow from the base, sparse flickering specks and, now and then, a short glitch with
/// streaks slipped sideways. Follows the effects strength and reduced motion; draws nothing in other styles.
/// Add it as the last child of a container so it lies over the content; it never takes the mouse.
/// </summary>
public sealed partial class HologramInterference : Control
{
    [Dependency] private IConfigurationManager _cfg = default!;

    private readonly Random _random = new();
    private readonly Action<float> _onStrength;
    private readonly Action<bool> _onReducedMotion;

    private float _strength = 0.6f;
    private bool _reducedMotion;
    private bool _subscribed;

    private float _time;
    private float _nextGlitch;
    private float _glitchLeft;
    private float _slip;
    private float _streakA;
    private float _streakB;
    private float _nextFlicker;
    private float _flickerLeft;
    private int _specksSeed;

    /// <summary>Only the band, the glow and the specks, fainter: for a large background area.</summary>
    public bool Subtle { get; set; }

    /// <summary>Draws the fine scanlines; off where the area already has a texture of its own.</summary>
    public bool Scanlines { get; set; } = true;

    public HologramInterference()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
        HorizontalExpand = true;
        VerticalExpand = true;

        _onStrength = value => _strength = Math.Clamp(value, 0f, 1f);
        _onReducedMotion = value => _reducedMotion = value;
        _nextGlitch = Next(3f, 8f);
        _nextFlicker = Next(4f, 10f);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        if (_subscribed)
            return;

        _subscribed = true;
        _cfg.OnValueChanged(CCCCVars.HologramEffects, _onStrength, true);
        _cfg.OnValueChanged(CCVars.ReducedMotion, _onReducedMotion, true);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        if (!_subscribed)
            return;

        _subscribed = false;
        _cfg.UnsubValueChanged(CCCCVars.HologramEffects, _onStrength);
        _cfg.UnsubValueChanged(CCVars.ReducedMotion, _onReducedMotion);
    }

    private bool Moving => !_reducedMotion && _strength > 0f;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (!Moving || !VisibleInTree)
            return;

        var dt = MathF.Min(args.DeltaSeconds, 0.1f);
        _time += dt;
        _specksSeed = (int) (_time * 12f);

        if (_glitchLeft > 0f)
        {
            _glitchLeft -= dt;
            if (_glitchLeft <= 0f)
                _nextGlitch = Next(4f, 10f) / MathF.Max(0.3f, _strength);
        }
        else if (!Subtle && (_nextGlitch -= dt) <= 0f)
        {
            _glitchLeft = 0.16f;
            _slip = Next(-4f, 4f);
            _streakA = Next(0.1f, 0.9f);
            _streakB = Next(0.1f, 0.9f);
        }

        if (_flickerLeft > 0f)
            _flickerLeft -= dt;
        else if ((_nextFlicker -= dt) <= 0f)
        {
            _flickerLeft = 0.07f;
            _nextFlicker = Next(5f, 12f);
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (!DeadSpaceStylePalette.Hologram)
            return;

        var width = (float) PixelSize.X;
        var height = (float) PixelSize.Y;
        if (width < 1f || height < 1f)
            return;

        var accent = DeadSpaceStylePalette.BorderHeader;
        var scale = UIScale;
        var power = Subtle ? 0.5f : 1f;

        // Projector glow rising from the base: stacked bands fading upwards.
        const int glowBands = 8;
        var glowHeight = height * (Subtle ? 0.35f : 0.5f);
        for (var i = 0; i < glowBands; i++)
        {
            var bottom = height - glowHeight * i / glowBands;
            var top = height - glowHeight * (i + 1) / glowBands;
            var alpha = 0.06f * power * (1f - (float) i / glowBands);
            handle.DrawRect(new UIBox2(0, top, width, bottom), accent.WithAlpha(alpha));
        }

        if (Scanlines && !Subtle)
        {
            var pitch = MathF.Max(2f, MathF.Round(3f * scale));
            var line = MathF.Max(1f, MathF.Round(scale));
            var faint = Color.Black.WithAlpha(0.12f);
            for (var y = 0f; y < height; y += pitch)
            {
                handle.DrawRect(new UIBox2(0, y, width, y + line), faint);
            }
        }

        if (!Moving)
            return;

        // A slow band of light crawling down.
        var band = (Subtle ? 60f : 22f) * scale;
        var travel = height + band;
        var phase = _time / (Subtle ? 9f : 4.5f) % 1f;
        var bandBottom = travel * phase;
        var bandTop = MathF.Max(0f, bandBottom - band);
        if (bandBottom > 0f && bandTop < height)
        {
            var mid = (bandTop + MathF.Min(bandBottom, height)) / 2f;
            handle.DrawRect(new UIBox2(0, bandTop, width, MathF.Min(bandBottom, height)), accent.WithAlpha(0.035f * power * _strength));
            handle.DrawRect(new UIBox2(0, mid, width, mid + MathF.Max(1f, scale)), accent.WithAlpha(0.09f * power * _strength));
        }

        // Sparse specks of signal noise, reshuffled a dozen times a second.
        var specks = new Random(_specksSeed);
        var count = (int) ((Subtle ? 10 : 16) * _strength);
        var dot = MathF.Max(1f, MathF.Round(scale * 1.5f));
        for (var i = 0; i < count; i++)
        {
            var x = MathF.Round((float) specks.NextDouble() * width);
            var y = MathF.Round((float) specks.NextDouble() * height);
            var bright = specks.Next(3) == 0;
            var color = bright ? DeadSpaceStylePalette.CyanBright : accent;
            handle.DrawRect(new UIBox2(x, y, x + dot * (bright ? 2 : 1), y + dot), color.WithAlpha(0.25f * power));
        }

        // A brief flicker of the whole projection.
        if (_flickerLeft > 0f)
            handle.DrawRect(new UIBox2(0, 0, width, height), accent.WithAlpha(0.05f * power));

        if (_glitchLeft <= 0f)
            return;

        // Glitch: one bright and one dark streak, each slipped sideways.
        var streak = MathF.Max(1f, MathF.Round(2f * scale));
        var slip = _slip * scale * 2f;
        var a = MathF.Round(_streakA * (height - streak));
        var b = MathF.Round(_streakB * (height - streak));
        handle.DrawRect(new UIBox2(slip, a, width + slip, a + streak), DeadSpaceStylePalette.CyanBright.WithAlpha(0.3f * _strength));
        handle.DrawRect(new UIBox2(-slip, b, width - slip, b + streak), Color.Black.WithAlpha(0.4f * _strength));
    }

    private float Next(float min, float max)
    {
        return min + (float) _random.NextDouble() * (max - min);
    }
}
