// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Content.Shared.DeadSpace.Lavaland.SecretBoss;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Lavaland.SecretBoss;

/// <summary>
/// While the local player falls down a pit: darkness, rushing streaks and up to 7 tumbling dolls of the players
/// falling right now. The landing itself happens in the world (the doll drops onto the own screen), so the
/// darkness just fades away at the end.
/// While the local player is pulled up out of the chromite caves: the dolls hang on their ropes and rise.
/// Also draws the plain fade to black used when everyone falls asleep.
/// </summary>
public sealed class LavalandPitFallOverlay : Overlay
{
    private const int MaxDolls = 7;
    // Short: the landing doll starts its drop right away and must be seen.
    private const float FadeOutTime = 0.15f;
    private const float DollScale = 4f;

    /// <summary>Screen pixels of one rope sprite on the hanging screen (the dolls are drawn 4x).</summary>
    private const float RopeSegment = 32f * DollScale;

    private static readonly SpriteSpecifier.Rsi DefaultRope =
        new(new ResPath("Objects/Weapons/Guns/Launchers/grappling_gun.rsi"), "rope");

    private readonly IEntityManager _entities;
    private readonly IPlayerManager _player;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprite;
    private readonly List<(EntityUid Uid, LavalandPitFallingComponent Fall, SpriteComponent Sprite)> _dolls = new();
    private readonly List<(EntityUid Uid, SpriteComponent Sprite, SpriteSpecifier? Rope)> _hangers = new();

    private float _time;
    private bool _falling;
    private bool _hanging;
    private float _hangElapsed;
    private float _hangDuration;
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
        _sprite = entities.System<SpriteSystem>();
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

        _falling = false;
        var hanging = false;
        if (_player.LocalEntity is { } local)
        {
            _falling = _entities.HasComponent<LavalandPitFallingComponent>(local);
            if (_entities.TryGetComponent<LavalandCaveClimbComponent>(local, out var climb) &&
                climb.Stage == LavalandCaveClimbStage.Hanging)
            {
                hanging = true;
                if (!_hanging)
                    _hangElapsed = 0f;
                _hangElapsed += frameTime;
                _hangDuration = MathF.Max(0.5f, climb.Duration);
            }
        }

        _hanging = hanging;
        if (_falling || _hanging)
            _afterFall = 0f;
        else
            _afterFall += frameTime;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _falling || _hanging || _afterFall < FadeOutTime || FadeAlpha() > 0f;
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

        if (_hanging)
        {
            DrawHanging(handle, origin, size);
        }
        else if (_falling || _afterFall < FadeOutTime)
        {
            var alpha = _falling ? 1f : 1f - _afterFall / FadeOutTime;
            handle.DrawRect(new UIBox2(origin, origin + size), new Color(0f, 0f, 0f, alpha));
            if (_falling)
            {
                DrawStreaks(handle, origin, size, false);
                DrawDolls(handle, origin, size);
            }
        }

        var fade = FadeAlpha();
        if (fade > 0f)
            handle.DrawRect(new UIBox2(origin, origin + size), new Color(0f, 0f, 0f, fade));
    }

    /// <summary>Faint streaks rushing past: upward while falling, downward while being pulled up.</summary>
    private void DrawStreaks(DrawingHandleScreen handle, Vector2 origin, Vector2 size, bool down)
    {
        for (var i = 0; i < 40; i++)
        {
            var seed = i * 7919 % 1000 / 1000f;
            var speed = (down ? 500f : 900f) + seed * 900f;
            var x = origin.X + (i * 0.61803f % 1f) * size.X;
            var travel = (_time * speed + seed * size.Y * 3f) % (size.Y + 200f);
            var y = down ? origin.Y - 200f + travel : origin.Y + size.Y - travel;
            var length = 40f + seed * 120f;
            var color = down
                ? new Color(0.5f, 0.75f, 1f, 0.07f + seed * 0.12f)
                : new Color(0.55f, 0.5f, 0.85f, 0.08f + seed * 0.12f);
            handle.DrawLine(new Vector2(x, y), new Vector2(x, y + length), color);
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
        for (var i = 0; i < _dolls.Count; i++)
        {
            var (uid, fall, sprite) = _dolls[i];
            if (_entities.Deleted(uid))
                continue;

            // Swinging side to side like a falling leaf, tilted towards the swing;
            // several fallers circle around each other.
            var phase = i * 1.9f;
            var swing = MathF.Sin(_time * 1.4f + phase);
            var sway = new Vector2(swing * size.X * 0.12f, MathF.Abs(MathF.Cos(_time * 1.4f + phase)) * -18f);
            var position = center + Orbit(i, _dolls.Count, size) + sway;
            var rotation = Angle.FromDegrees(StyleAngle(fall.Style, _time + i * 0.37f) + swing * 18f);

            handle.DrawEntity(uid, position, new Vector2(DollScale), Angle.Zero, -rotation, Direction.South, sprite,
                xformSystem: _transform);
        }
    }

    /// <summary>
    /// Dark shaft with streaks going down; every climber hangs on a rope from the top of the screen,
    /// swinging and rising. The last moments fade to black before the climb out on lavaland.
    /// </summary>
    private void DrawHanging(DrawingHandleScreen handle, Vector2 origin, Vector2 size)
    {
        // A deep blue shaft, lighter towards the top where lavaland is.
        const int bands = 12;
        for (var b = 0; b < bands; b++)
        {
            var t = b / (float) (bands - 1);
            var color = Color.InterpolateBetween(Color.FromHex("#3a2a24"), Color.FromHex("#04060f"), t);
            var top = origin.Y + size.Y * b / bands;
            handle.DrawRect(new UIBox2(origin.X, top, origin.X + size.X, top + size.Y / bands + 1f), color);
        }

        DrawStreaks(handle, origin, size, true);

        _hangers.Clear();
        if (_player.LocalEntity is { } local && _entities.TryGetComponent<SpriteComponent>(local, out var ownSprite) &&
            _entities.TryGetComponent<LavalandCaveClimbComponent>(local, out var ownClimb))
        {
            _hangers.Add((local, ownSprite, ownClimb.RopeSprite));
        }

        var query = _entities.EntityQueryEnumerator<LavalandCaveClimbComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var climb, out var sprite) && _hangers.Count < MaxDolls)
        {
            if (uid != _player.LocalEntity && climb.Stage == LavalandCaveClimbStage.Hanging)
                _hangers.Add((uid, sprite, climb.RopeSprite));
        }

        var progress = Math.Clamp(_hangElapsed / _hangDuration, 0f, 1f);
        // Pulled up in jerks, like hand over hand.
        var jerk = progress + MathF.Sin(progress * MathF.PI * 10f) * 0.012f;
        for (var i = 0; i < _hangers.Count; i++)
        {
            var (uid, sprite, ropeSprite) = _hangers[i];
            if (_entities.Deleted(uid))
                continue;

            var column = _hangers.Count <= 1 ? 0.5f : 0.25f + 0.5f * i / (_hangers.Count - 1);
            var anchor = new Vector2(origin.X + size.X * column, origin.Y - 10f);
            var ropeLength = size.Y * (0.72f - 0.42f * jerk);
            var swing = MathF.Sin(_time * 1.7f + i * 1.3f) * 0.22f;
            var hand = anchor + new Vector2(MathF.Sin(swing), MathF.Cos(swing)) * ropeLength;

            DrawRope(handle, _sprite.Frame0(ropeSprite ?? DefaultRope), anchor, ropeLength, swing);

            // The doll hangs by its raised hands under the end of the rope.
            var body = hand + new Vector2(MathF.Sin(swing), MathF.Cos(swing)) * 60f;
            handle.DrawEntity(uid, body, new Vector2(DollScale), Angle.Zero, new Angle(swing), Direction.South, sprite,
                xformSystem: _transform);
        }

        // Light from above floods in at the very end.
        if (progress > 0.8f)
        {
            var flash = (progress - 0.8f) / 0.2f;
            handle.DrawRect(new UIBox2(origin, origin + size), new Color(0f, 0f, 0f, flash));
        }
    }

    /// <summary>The rope sprite of the climber's own hook, tiled down from the anchor and swung with him.</summary>
    private static void DrawRope(DrawingHandleScreen handle, Texture rope, Vector2 anchor, float length, float swing)
    {
        // Rotate around the anchor: positive swing moves the end to the right, as the body does.
        var previous = handle.GetTransform();
        handle.SetTransform(Matrix3x2.CreateRotation(-swing) * Matrix3x2.CreateTranslation(anchor) * previous);
        for (var y = 0f; y < length; y += RopeSegment)
        {
            var height = MathF.Min(RopeSegment, length - y);
            var box = new UIBox2(-RopeSegment / 2f, y, RopeSegment / 2f, y + height);
            handle.DrawTextureRectRegion(rope, box, new UIBox2(0f, 0f, rope.Width, rope.Height * height / RopeSegment));
        }

        handle.SetTransform(previous);
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
