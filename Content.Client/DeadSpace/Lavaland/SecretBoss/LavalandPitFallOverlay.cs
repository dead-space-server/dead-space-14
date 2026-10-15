// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland.SecretBoss;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;

namespace Content.Client.DeadSpace.Lavaland.SecretBoss;

/// <summary>
/// While the local player falls down the pit: darkness, rushing streaks and up to 7 tumbling dolls of the players
/// falling right now. In the last moment the own doll drops from the top of the screen into the centre.
/// Also draws the plain fade to black used when everyone falls asleep.
/// </summary>
public sealed class LavalandPitFallOverlay : Overlay
{
    private const int MaxDolls = 7;
    private const float LandingTime = 0.9f;
    private const float FadeOutTime = 0.6f;
    private const float DollScale = 4f;

    private readonly IEntityManager _entities;
    private readonly IPlayerManager _player;
    private readonly SharedTransformSystem _transform;
    private readonly List<(EntityUid Uid, LavalandPitFallingComponent Fall, SpriteComponent Sprite)> _dolls = new();

    private float _time;
    private float _fallElapsed;
    private float _fallDuration;
    private bool _falling;
    private float _afterFall = FadeOutTime;

    private float _fadeIn;
    private float _fadeHold;
    private float _fadeOut;
    private float _fadeElapsed = float.MaxValue;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public LavalandPitFallOverlay(IEntityManager entities, IPlayerManager player)
    {
        _entities = entities;
        _player = player;
        _transform = entities.System<SharedTransformSystem>();
        ZIndex = 200;
    }

    public void StartFade(float fadeIn, float hold, float fadeOut)
    {
        _fadeIn = MathF.Max(0.01f, fadeIn);
        _fadeHold = MathF.Max(0f, hold);
        _fadeOut = MathF.Max(0.01f, fadeOut);
        _fadeElapsed = 0f;
    }

    public void FrameUpdate(float frameTime)
    {
        _time += frameTime;
        _fadeElapsed += frameTime;

        if (_player.LocalEntity is { } local &&
            _entities.TryGetComponent<LavalandPitFallingComponent>(local, out var falling))
        {
            if (!_falling)
                _fallElapsed = 0f;
            _falling = true;
            _fallElapsed += frameTime;
            _fallDuration = falling.Duration;
            _afterFall = 0f;
        }
        else
        {
            _falling = false;
            _afterFall += frameTime;
        }
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _falling || _afterFall < FadeOutTime || FadeAlpha() > 0f;
    }

    private float FadeAlpha()
    {
        if (_fadeElapsed < _fadeIn)
            return _fadeElapsed / _fadeIn;
        if (_fadeElapsed < _fadeIn + _fadeHold)
            return 1f;
        var t = _fadeElapsed - _fadeIn - _fadeHold;
        return t < _fadeOut ? 1f - t / _fadeOut : 0f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;
        var size = new Vector2(bounds.Width, bounds.Height);
        var origin = new Vector2(bounds.Left, bounds.Top);

        if (_falling || _afterFall < FadeOutTime)
        {
            var alpha = _falling ? 1f : 1f - _afterFall / FadeOutTime;
            handle.DrawRect(new UIBox2(origin, origin + size), new Color(0f, 0f, 0f, alpha));
            if (_falling)
            {
                DrawStreaks(handle, origin, size);
                DrawDolls(handle, origin, size);
            }
        }

        var fade = FadeAlpha();
        if (fade > 0f)
            handle.DrawRect(new UIBox2(origin, origin + size), new Color(0f, 0f, 0f, fade));
    }

    /// <summary>Faint streaks rushing upward: the wind of the fall.</summary>
    private void DrawStreaks(DrawingHandleScreen handle, Vector2 origin, Vector2 size)
    {
        for (var i = 0; i < 40; i++)
        {
            var seed = i * 7919 % 1000 / 1000f;
            var speed = 900f + seed * 900f;
            var x = origin.X + (i * 0.61803f % 1f) * size.X;
            var y = origin.Y + size.Y - (_time * speed + seed * size.Y * 3f) % (size.Y + 200f);
            var length = 40f + seed * 120f;
            handle.DrawLine(new Vector2(x, y), new Vector2(x, y + length), new Color(0.55f, 0.5f, 0.85f, 0.08f + seed * 0.12f));
        }
    }

    private void DrawDolls(DrawingHandleScreen handle, Vector2 origin, Vector2 size)
    {
        if (_player.LocalEntity is not { } local)
            return;

        _dolls.Clear();
        if (_entities.TryGetComponent<LavalandPitFallingComponent>(local, out var ownFall) &&
            _entities.TryGetComponent<SpriteComponent>(local, out var ownSprite))
        {
            _dolls.Add((local, ownFall, ownSprite));
        }

        var query = _entities.EntityQueryEnumerator<LavalandPitFallingComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var fall, out var sprite) && _dolls.Count < MaxDolls)
        {
            if (uid != local)
                _dolls.Add((uid, fall, sprite));
        }

        var center = origin + size / 2f;
        var landing = _fallElapsed >= _fallDuration - LandingTime;
        for (var i = 0; i < _dolls.Count; i++)
        {
            var (uid, fall, sprite) = _dolls[i];
            if (_entities.Deleted(uid))
                continue;

            Vector2 position;
            Angle rotation;
            if (landing)
            {
                // Only the own doll: it drops from the top of the screen into the centre and rights itself.
                if (i != 0)
                    continue;
                var t = Math.Clamp((_fallElapsed - (_fallDuration - LandingTime)) / LandingTime, 0f, 1f);
                var eased = 1f - (1f - t) * (1f - t);
                position = new Vector2(center.X, origin.Y - 80f + (center.Y - origin.Y + 80f) * eased);
                rotation = Angle.FromDegrees(StyleAngle(fall.Style, _time + i) * (1f - t));
            }
            else
            {
                // Swinging side to side like a falling leaf, tilted towards the swing;
                // several fallers circle around each other.
                var phase = i * 1.9f;
                var swing = MathF.Sin(_time * 1.4f + phase);
                var sway = new Vector2(swing * size.X * 0.12f, MathF.Abs(MathF.Cos(_time * 1.4f + phase)) * -18f);
                position = center + Orbit(i, _dolls.Count, size) + sway;
                rotation = Angle.FromDegrees(StyleAngle(fall.Style, _time + i * 0.37f) + swing * 18f);
            }

            handle.DrawEntity(uid, position, new Vector2(DollScale), Angle.Zero, -rotation, Direction.South, sprite,
                xformSystem: _transform);
        }
    }

    /// <summary>Alone - in the middle; together - evenly spaced on a slowly turning circle around each other.</summary>
    private Vector2 Orbit(int index, int count, Vector2 size)
    {
        if (count <= 1)
            return Vector2.Zero;

        var radius = MathF.Min(size.X, size.Y) * (0.12f + 0.025f * count);
        var angle = MathF.Tau * index / count + _time * 0.6f;
        return new Vector2(MathF.Cos(angle) * radius * 1.3f, MathF.Sin(angle) * radius);
    }

    private static float StyleAngle(LavalandPitJumpStyle style, float time)
    {
        return style switch
        {
            LavalandPitJumpStyle.HeadFirst => 180f + MathF.Sin(time * 3f) * 12f,
            LavalandPitJumpStyle.Somersault => time * 300f % 360f,
            LavalandPitJumpStyle.Backflip => -(time * 300f % 360f),
            LavalandPitJumpStyle.Corkscrew => time * 720f % 360f,
            _ => MathF.Sin(time * 4f) * 35f,
        };
    }
}
