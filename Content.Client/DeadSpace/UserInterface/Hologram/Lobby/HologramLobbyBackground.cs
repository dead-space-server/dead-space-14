// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.DeadSpace.Stylesheets;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.CCCCVars;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Lobby;

/// <summary>
/// Animated backdrop of the "Dead Space" lobby: a dark field tinted by the projection colour, a slow wandering glow,
/// soft light ribbons, and layered stars and motes drifting at different depths. The layers shift a little after the
/// mouse, deeper ones less. Everything is drawn procedurally in one batch from preallocated buffers; the colours come
/// from the palette, so they follow the player's projection colour. Motion scales with the hologram effects strength
/// and nearly stops with reduced motion.
/// </summary>
public sealed partial class HologramLobbyBackground : Control
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IInputManager _input = default!;

    private const int StarCount = 150;
    private const int MoteCount = 36;
    private const int RibbonCount = 3;
    private const int RibbonSegments = 48;
    private const int GlowSegments = 32;

    /// <summary>How far the nearest layer shifts after the mouse, in UI units.</summary>
    private const float ParallaxReach = 28f;

    /// <summary>Speed of everything with reduced motion on.</summary>
    private const float ReducedMotionSpeed = 0.08f;

    private const int VertexCapacity =
        6                                       // base gradient
        + GlowSegments * 3 * 2                  // two glows
        + RibbonCount * RibbonSegments * 12     // two soft halves per ribbon
        + StarCount * 6                         // a quad per star
        + MoteCount * 12;                       // halo and core per mote

    private readonly DrawVertexUV2DColor[] _vertices = new DrawVertexUV2DColor[VertexCapacity];
    private readonly Vector2[] _unitCircle = new Vector2[GlowSegments];
    private readonly Particle[] _stars = new Particle[StarCount];
    private readonly Particle[] _motes = new Particle[MoteCount];
    private readonly Action<float> _onStrength;
    private readonly Action<bool> _onReducedMotion;

    private int _count;
    private float _time;
    private float _strength = 0.6f;
    private bool _reducedMotion;
    private bool _subscribed;
    private Vector2 _parallax;

    public HologramLobbyBackground()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;

        _onStrength = value => _strength = Math.Clamp(value, 0f, 1f);
        _onReducedMotion = value => _reducedMotion = value;

        for (var i = 0; i < GlowSegments; i++)
        {
            var angle = MathF.Tau * i / GlowSegments;
            _unitCircle[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }

        // A fixed seed: the same sky every time, no flicker between rebuilds.
        var random = new Random(1414);
        for (var i = 0; i < StarCount; i++)
        {
            // Three depth layers: far, small and slow; near, larger and faster.
            var depth = (i % 3 + 1) / 3f;
            _stars[i] = new Particle
            {
                Position = new Vector2(random.NextSingle(), random.NextSingle()),
                Velocity = new Vector2(-0.004f - 0.006f * depth, 0.0015f * (random.NextSingle() - 0.5f)),
                Depth = depth,
                Size = 1f + depth * (random.NextSingle() < 0.25f ? 2f : 1f),
                Phase = random.NextSingle() * MathF.Tau,
                Rate = 0.6f + random.NextSingle() * 1.8f,
                Alpha = 0.25f + 0.45f * depth * random.NextSingle() + 0.15f,
            };
        }

        for (var i = 0; i < MoteCount; i++)
        {
            var depth = 0.4f + 0.6f * random.NextSingle();
            _motes[i] = new Particle
            {
                Position = new Vector2(random.NextSingle(), random.NextSingle()),
                Velocity = new Vector2(0.004f * (random.NextSingle() - 0.5f), -0.006f - 0.010f * depth),
                Depth = depth,
                Size = 2f + 3f * depth,
                Phase = random.NextSingle() * MathF.Tau,
                Rate = 0.3f + random.NextSingle() * 0.7f,
                Alpha = 0.18f + 0.30f * random.NextSingle(),
            };
        }
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

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var dt = MathF.Min(args.DeltaSeconds, 0.1f);
        var speed = _strength * (_reducedMotion ? ReducedMotionSpeed : 1f);
        var step = dt * speed;
        _time += step;

        Drift(_stars, step);
        Drift(_motes, step);

        // The mouse pulls the layers a little; with reduced motion or no effects they stay put.
        var target = Vector2.Zero;
        var size = (Vector2) PixelSize;
        if (!_reducedMotion && _strength > 0f && size.X > 1f && size.Y > 1f)
        {
            var mouse = _input.MouseScreenPosition.Position - (Vector2) GlobalPixelPosition;
            target = Vector2.Clamp((mouse - size / 2f) / (size / 2f), -Vector2.One, Vector2.One) * _strength;
        }

        _parallax += (target - _parallax) * MathF.Min(1f, dt * 1.5f);
    }

    private static void Drift(Particle[] particles, float step)
    {
        for (var i = 0; i < particles.Length; i++)
        {
            ref var p = ref particles[i];
            p.Position += p.Velocity * step;
            p.Position.X -= MathF.Floor(p.Position.X);
            p.Position.Y -= MathF.Floor(p.Position.Y);
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var width = (float) PixelSize.X;
        var height = (float) PixelSize.Y;
        if (width < 1f || height < 1f)
            return;

        var scale = UIScale;
        var accent = DeadSpaceStylePalette.BorderHeader.WithAlpha(1f);
        var bright = DeadSpaceStylePalette.CyanBright.WithAlpha(1f);
        var reach = ParallaxReach * scale;
        _count = 0;

        // Deep field: almost black, a touch of the projection colour, lighter towards the bottom.
        var top = Color.InterpolateBetween(Color.Black, accent, 0.025f);
        var bottom = Color.InterpolateBetween(Color.Black, accent, 0.09f);
        AddQuad(new Vector2(0, 0), new Vector2(width, height), top, top, bottom, bottom);

        // Two slow wandering glows.
        var glowRadius = MathF.Max(width, height) * 0.55f;
        AddGlow(
            new Vector2(width * (0.30f + 0.06f * MathF.Sin(_time * 0.11f)), height * (0.40f + 0.05f * MathF.Cos(_time * 0.09f)))
            - _parallax * reach * 0.2f,
            glowRadius,
            accent.WithAlpha(0.16f));
        AddGlow(
            new Vector2(width * (0.78f + 0.05f * MathF.Cos(_time * 0.07f)), height * (0.72f + 0.04f * MathF.Sin(_time * 0.13f)))
            - _parallax * reach * 0.25f,
            glowRadius * 0.7f,
            bright.WithAlpha(0.07f));

        // Light ribbons: soft bands of light slowly rippling across the screen.
        for (var r = 0; r < RibbonCount; r++)
        {
            var color = r == 1 ? bright : accent;
            AddRibbon(
                width,
                height,
                baseY: height * (0.52f + 0.13f * r) - _parallax.Y * reach * (0.3f + 0.15f * r),
                amplitude: height * (0.06f + 0.025f * r),
                thickness: height * (0.05f + 0.02f * r),
                frequency: 1.4f + 0.55f * r,
                phase: _time * (0.18f + 0.07f * r) + r * 2.1f,
                shiftX: -_parallax.X * reach * (0.3f + 0.15f * r),
                color: color.WithAlpha(0.10f - 0.02f * r));
        }

        // Stars: pixel squares, twinkling, the near ones larger and moving more with the mouse.
        foreach (ref var star in _stars.AsSpan())
        {
            var position = Place(star, width, height, reach);
            var twinkle = 0.65f + 0.35f * MathF.Sin(_time * 4f * star.Rate + star.Phase);
            var size = MathF.Max(1f, MathF.Round(star.Size * scale));
            var color = (star.Depth > 0.9f ? bright : Color.InterpolateBetween(accent, bright, 0.5f))
                .WithAlpha(star.Alpha * twinkle);
            AddQuad(position, position + new Vector2(size, size), color, color, color, color);
        }

        // Motes: larger soft particles rising slowly, a faint halo around a small core.
        foreach (ref var mote in _motes.AsSpan())
        {
            var position = Place(mote, width, height, reach);
            var pulse = 0.6f + 0.4f * MathF.Sin(_time * 1.5f * mote.Rate + mote.Phase);
            var core = MathF.Max(1f, MathF.Round(mote.Size * scale));
            var halo = core * 3f;
            var haloColor = accent.WithAlpha(mote.Alpha * 0.18f * pulse);
            var coreColor = bright.WithAlpha(mote.Alpha * pulse);
            var haloOrigin = position - new Vector2(halo - core, halo - core) / 2f;
            AddQuad(haloOrigin, haloOrigin + new Vector2(halo, halo), haloColor, haloColor, haloColor, haloColor);
            AddQuad(position, position + new Vector2(core, core), coreColor, coreColor, coreColor, coreColor);
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, _vertices.AsSpan(0, _count));
    }

    private Vector2 Place(in Particle particle, float width, float height, float reach)
    {
        var x = particle.Position.X * width - _parallax.X * reach * particle.Depth;
        var y = particle.Position.Y * height - _parallax.Y * reach * particle.Depth;
        x -= MathF.Floor(x / width) * width;
        y -= MathF.Floor(y / height) * height;
        return new Vector2(MathF.Round(x), MathF.Round(y));
    }

    private void AddRibbon(
        float width,
        float height,
        float baseY,
        float amplitude,
        float thickness,
        float frequency,
        float phase,
        float shiftX,
        Color color)
    {
        var clear = color.WithAlpha(0f);
        var previousCenter = Vector2.Zero;
        var previousHalf = 0f;

        for (var i = 0; i <= RibbonSegments; i++)
        {
            var t = (float) i / RibbonSegments;
            var x = t * width;
            var wave = (t * frequency * MathF.Tau) + phase;
            var y = baseY
                    + amplitude * MathF.Sin(wave + shiftX / MathF.Max(width, 1f) * MathF.Tau)
                    + amplitude * 0.35f * MathF.Sin(wave * 2.3f - phase * 0.6f);
            // The band breathes along its length.
            var half = thickness * (0.55f + 0.45f * MathF.Sin(wave * 0.7f + phase * 0.4f)) / 2f;
            var center = new Vector2(x, y);

            if (i > 0)
            {
                // Upper half: bright at the centre line, clear at the edge; then the lower half.
                AddQuad(
                    previousCenter - new Vector2(0, previousHalf),
                    center - new Vector2(0, half),
                    previousCenter,
                    center,
                    clear,
                    clear,
                    color,
                    color);
                AddQuad(
                    previousCenter,
                    center,
                    previousCenter + new Vector2(0, previousHalf),
                    center + new Vector2(0, half),
                    color,
                    color,
                    clear,
                    clear);
            }

            previousCenter = center;
            previousHalf = half;
        }
    }

    private void AddGlow(Vector2 center, float radius, Color color)
    {
        var clear = color.WithAlpha(0f);
        var inner = Color.FromSrgb(color);
        var outer = Color.FromSrgb(clear);
        var previous = center + _unitCircle[GlowSegments - 1] * radius;
        for (var i = 0; i < GlowSegments; i++)
        {
            var next = center + _unitCircle[i] * radius;
            _vertices[_count++] = new DrawVertexUV2DColor(center, inner);
            _vertices[_count++] = new DrawVertexUV2DColor(previous, outer);
            _vertices[_count++] = new DrawVertexUV2DColor(next, outer);
            previous = next;
        }
    }

    /// <summary>An axis-aligned quad with a colour per corner.</summary>
    private void AddQuad(Vector2 topLeft, Vector2 bottomRight, Color topLeftColor, Color topRightColor, Color bottomLeftColor, Color bottomRightColor)
    {
        AddQuad(
            topLeft,
            new Vector2(bottomRight.X, topLeft.Y),
            new Vector2(topLeft.X, bottomRight.Y),
            bottomRight,
            topLeftColor,
            topRightColor,
            bottomLeftColor,
            bottomRightColor);
    }

    /// <summary>A quad from four corners (top-left, top-right, bottom-left, bottom-right) with a colour per corner.</summary>
    private void AddQuad(
        Vector2 a,
        Vector2 b,
        Vector2 c,
        Vector2 d,
        Color colorA,
        Color colorB,
        Color colorC,
        Color colorD)
    {
        if (_count + 6 > _vertices.Length)
            return;

        // Vertex colours are in linear space.
        var la = Color.FromSrgb(colorA);
        var lb = Color.FromSrgb(colorB);
        var lc = Color.FromSrgb(colorC);
        var ld = Color.FromSrgb(colorD);
        _vertices[_count++] = new DrawVertexUV2DColor(a, la);
        _vertices[_count++] = new DrawVertexUV2DColor(b, lb);
        _vertices[_count++] = new DrawVertexUV2DColor(c, lc);
        _vertices[_count++] = new DrawVertexUV2DColor(b, lb);
        _vertices[_count++] = new DrawVertexUV2DColor(d, ld);
        _vertices[_count++] = new DrawVertexUV2DColor(c, lc);
    }

    private struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Depth;
        public float Size;
        public float Phase;
        public float Rate;
        public float Alpha;
    }
}
