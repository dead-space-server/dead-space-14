// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.DeadSpace.Stylesheets;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.CCCCVars;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Lobby;

/// <summary>
/// Head-and-shoulders portrait of the lobby character for the "Dead Space" lobby: the same preview dummy, scaled up and
/// cropped to its upper part, with restrained hologram interference over it - a faint scanline, a rare short flicker
/// and now and then a brief glitch that slips the picture sideways and streaks a few lines. Interference follows the
/// hologram effects strength and is off with reduced motion. No allocations per frame.
/// </summary>
public sealed partial class HologramLobbyPortrait : LayoutContainer
{
    [Dependency] private IConfigurationManager _cfg = default!;

    /// <summary>Side of the square portrait, in UI units.</summary>
    public const float PortraitSize = 128f;

    /// <summary>Sprite pixels to UI units.</summary>
    private const float Zoom = 7f;

    /// <summary>Sprite rows (of 32, from the top) the portrait starts at: the top of the head.</summary>
    private const float FirstRow = 2f;

    private const float SpriteSide = 32f * Zoom;
    private const float GlitchDuration = 0.14f;
    private const float GlitchStep = 0.045f;
    private const float FlickerDuration = 0.07f;

    private readonly SpriteView _view;
    private readonly Random _random = new();
    private readonly Action<float> _onStrength;
    private readonly Action<bool> _onReducedMotion;

    private float _strength = 0.6f;
    private bool _reducedMotion;
    private bool _subscribed;

    private float _time;
    private float _nextGlitch;
    private float _glitchLeft;
    private float _glitchStepLeft;
    private float _nextFlicker;
    private float _flickerLeft;

    // The current glitch: sideways slip of the picture and two streak lines.
    private float _slip;
    private float _streakA;
    private float _streakB;
    private float _streakHeight;

    public HologramLobbyPortrait()
    {
        IoCManager.InjectDependencies(this);

        SetSize = new Vector2(PortraitSize, PortraitSize);
        MinSize = new Vector2(PortraitSize, PortraitSize);
        RectClipContent = true;
        MouseFilter = MouseFilterMode.Ignore;

        _onStrength = value => _strength = Math.Clamp(value, 0f, 1f);
        _onReducedMotion = value => _reducedMotion = value;

        _view = new SpriteView
        {
            Stretch = SpriteView.StretchMode.None,
            Scale = new Vector2(Zoom, Zoom),
            OverrideDirection = Direction.South,
            MouseFilter = MouseFilterMode.Ignore,
        };
        AddChild(_view);
        PlaceView(0f);

        AddChild(new Interference(this));

        _nextGlitch = NextDelay(4f, 9f);
        _nextFlicker = NextDelay(5f, 12f);
    }

    /// <summary>Shows the given preview dummy; null or a deleted entity leaves the portrait empty.</summary>
    public void SetEntity(EntityUid? uid)
    {
        _view.SetEntity(uid);
    }

    /// <summary>
    /// The sprite view is the full 32x32 sprite at <see cref="Zoom"/>, centred horizontally and lifted so the head
    /// sits at the top; the portrait clips the rest.
    /// </summary>
    private void PlaceView(float slip)
    {
        var left = (PortraitSize - SpriteSide) / 2f + slip;
        var top = -FirstRow * Zoom;
        SetAnchorPreset(_view, LayoutPreset.TopLeft);
        SetMarginLeft(_view, left);
        SetMarginTop(_view, top);
        SetMarginRight(_view, left + SpriteSide);
        SetMarginBottom(_view, top + SpriteSide);
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
        var active = !_reducedMotion && _strength > 0f;
        _time += active ? dt : 0f;

        if (!active)
        {
            EndGlitch();
            _flickerLeft = 0f;
            _view.Modulate = Color.White;
            return;
        }

        // Rarer and weaker interference at lower strength.
        var rate = 0.35f + 0.65f * _strength;

        if (_glitchLeft > 0f)
        {
            _glitchLeft -= dt;
            _glitchStepLeft -= dt;
            if (_glitchLeft <= 0f)
            {
                EndGlitch();
            }
            else if (_glitchStepLeft <= 0f)
            {
                _glitchStepLeft = GlitchStep;
                RollGlitch();
            }
        }
        else
        {
            _nextGlitch -= dt * rate;
            if (_nextGlitch <= 0f)
            {
                _glitchLeft = GlitchDuration;
                _glitchStepLeft = GlitchStep;
                _nextGlitch = NextDelay(4f, 9f);
                RollGlitch();
            }
        }

        if (_flickerLeft > 0f)
        {
            _flickerLeft -= dt;
        }
        else
        {
            _nextFlicker -= dt * rate;
            if (_nextFlicker <= 0f)
            {
                _flickerLeft = FlickerDuration;
                _nextFlicker = NextDelay(5f, 12f);
            }
        }

        var alpha = _flickerLeft > 0f ? 1f - 0.35f * _strength : 1f;
        _view.Modulate = Color.White.WithAlpha(alpha);
    }

    private void RollGlitch()
    {
        _slip = (_random.NextSingle() < 0.5f ? -1f : 1f) * (1f + 3f * _strength * _random.NextSingle());
        _streakA = _random.NextSingle();
        _streakB = _random.NextSingle();
        _streakHeight = 1f + 3f * _random.NextSingle();
        PlaceView(MathF.Round(_slip));
    }

    private void EndGlitch()
    {
        _glitchLeft = 0f;
        if (_slip == 0f)
            return;

        _slip = 0f;
        PlaceView(0f);
    }

    private float NextDelay(float min, float max)
    {
        return min + (max - min) * _random.NextSingle();
    }

    /// <summary>Drawn over the sprite: the projection tint, the scanline and the glitch streaks.</summary>
    private sealed class Interference : Control
    {
        private readonly HologramLobbyPortrait _owner;

        public Interference(HologramLobbyPortrait owner)
        {
            _owner = owner;
            MouseFilter = MouseFilterMode.Ignore;
            SetAnchorPreset(this, LayoutPreset.Wide);
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);

            var width = (float) PixelSize.X;
            var height = (float) PixelSize.Y;
            if (width < 1f || height < 1f)
                return;

            var accent = DeadSpaceStylePalette.BorderHeader;
            var scale = UIScale;

            // A faint projection tint and static fine lines: always on, they don't move.
            handle.DrawRect(new UIBox2(0, 0, width, height), accent.WithAlpha(0.05f));
            var pitch = MathF.Max(2f, MathF.Round(3f * scale));
            var line = MathF.Max(1f, MathF.Round(scale));
            var faint = Color.Black.WithAlpha(0.14f);
            for (var y = 0f; y < height; y += pitch)
            {
                handle.DrawRect(new UIBox2(0, y, width, y + line), faint);
            }

            if (_owner._reducedMotion || _owner._strength <= 0f)
                return;

            // A slow band of light crawling down.
            var band = 18f * scale;
            var travel = height + band;
            var phase = _owner._time / 4.5f % 1f;
            var bottom = travel * phase;
            var top = MathF.Max(0f, bottom - band);
            if (bottom > 0f && top < height)
                handle.DrawRect(new UIBox2(0, top, width, MathF.Min(bottom, height)), accent.WithAlpha(0.06f * _owner._strength));

            if (_owner._glitchLeft <= 0f)
                return;

            // Streaks: one bright, one dark, each a thin line slipped sideways.
            var streak = MathF.Max(1f, MathF.Round(_owner._streakHeight * scale));
            var slip = _owner._slip * scale * 2f;
            var a = MathF.Round(_owner._streakA * (height - streak));
            var b = MathF.Round(_owner._streakB * (height - streak));
            handle.DrawRect(new UIBox2(slip, a, width + slip, a + streak), DeadSpaceStylePalette.CyanBright.WithAlpha(0.35f * _owner._strength));
            handle.DrawRect(new UIBox2(-slip, b, width - slip, b + streak), Color.Black.WithAlpha(0.45f * _owner._strength));
        }
    }
}
