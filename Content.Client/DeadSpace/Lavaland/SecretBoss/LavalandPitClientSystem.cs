// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland.SecretBoss;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Animations;

namespace Content.Client.DeadSpace.Lavaland.SecretBoss;

/// <summary>
/// Dive animations into the Celestial pit (seen by everyone around), the dark falling screen
/// and the fade to black before the Celestial.
/// </summary>
public sealed class LavalandPitClientSystem : EntitySystem
{
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const string JumpKey = "lavaland-pit-jump";

    private readonly Dictionary<EntityUid, (Vector2 Offset, Angle Rotation, Vector2 Scale, Color Color)> _original = new();
    private LavalandPitFallOverlay _overlay = default!;
    private LavalandCelestialBackdropOverlay _backdrop = default!;
    private LavalandCelestialBackdropOverlay _flames = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new LavalandPitFallOverlay(EntityManager, _player);
        _overlays.AddOverlay(_overlay);
        _backdrop = new LavalandCelestialBackdropOverlay(false);
        _flames = new LavalandCelestialBackdropOverlay(true);
        _overlays.AddOverlay(_backdrop);
        _overlays.AddOverlay(_flames);

        SubscribeLocalEvent<LavalandPitJumpingComponent, AfterAutoHandleStateEvent>(OnJumpState);
        SubscribeLocalEvent<LavalandPitJumpingComponent, ComponentRemove>(OnJumpRemove);
        SubscribeNetworkEvent<LavalandSecretBossFadeEvent>(OnFade);
        SubscribeNetworkEvent<LavalandCelestialBackdropEvent>(OnBackdrop);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
        _overlays.RemoveOverlay(_backdrop);
        _overlays.RemoveOverlay(_flames);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        _overlay.FrameUpdate(frameTime);
        _backdrop.FrameUpdate(frameTime);
        _flames.FrameUpdate(frameTime);
    }

    private void OnBackdrop(LavalandCelestialBackdropEvent ev)
    {
        _backdrop.Show(ev.Duration);
        _flames.Show(ev.Duration);
    }

    private void OnFade(LavalandSecretBossFadeEvent ev)
    {
        _overlay.StartFade(ev.FadeIn, ev.Hold, ev.FadeOut);
    }

    // Started from the state, not from ComponentInit: when the server adds the component, the fields
    // (target, style) arrive with the state after the component is created.
    private void OnJumpState(Entity<LavalandPitJumpingComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_original.ContainsKey(ent) || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _original[ent] = (sprite.Offset, sprite.Rotation, sprite.Scale, sprite.Color);
        _animation.Play(ent.Owner, BuildJump(ent.Comp, sprite), JumpKey);
    }

    private void OnJumpRemove(Entity<LavalandPitJumpingComponent> ent, ref ComponentRemove args)
    {
        if (_animation.HasRunningAnimation(ent.Owner, JumpKey))
            _animation.Stop(ent.Owner, null, JumpKey);

        if (!_original.Remove(ent, out var original) || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var spriteEnt = (ent.Owner, sprite);
        _sprite.SetOffset(spriteEnt, original.Offset);
        _sprite.SetRotation(spriteEnt, original.Rotation);
        _sprite.SetScale(spriteEnt, original.Scale);
        _sprite.SetColor(spriteEnt, original.Color);
    }

    /// <summary>Hop up, arc into the pit centre and shrink away, spinning according to the style.</summary>
    private static Animation BuildJump(LavalandPitJumpingComponent jump, SpriteComponent sprite)
    {
        var d = MathF.Max(0.3f, jump.Duration);
        var target = jump.Target;
        var scale = sprite.Scale;
        var color = sprite.Color;

        var offset = new AnimationTrackComponentProperty
        {
            ComponentType = typeof(SpriteComponent),
            Property = nameof(SpriteComponent.Offset),
            InterpolationMode = AnimationInterpolationMode.Cubic,
            KeyFrames =
            {
                new AnimationTrackProperty.KeyFrame(sprite.Offset, 0f),
                new AnimationTrackProperty.KeyFrame(sprite.Offset + target * 0.45f + new Vector2(0f, 0.9f), d * 0.4f),
                new AnimationTrackProperty.KeyFrame(sprite.Offset + target, d * 0.4f),
                new AnimationTrackProperty.KeyFrame(sprite.Offset + target - new Vector2(0f, 0.35f), d * 0.2f),
            },
        };

        var scaleTrack = new AnimationTrackComponentProperty
        {
            ComponentType = typeof(SpriteComponent),
            Property = nameof(SpriteComponent.Scale),
            InterpolationMode = AnimationInterpolationMode.Linear,
            KeyFrames =
            {
                new AnimationTrackProperty.KeyFrame(scale, 0f),
                new AnimationTrackProperty.KeyFrame(scale * 1.15f, d * 0.4f),
                new AnimationTrackProperty.KeyFrame(scale * 0.7f, d * 0.4f),
                new AnimationTrackProperty.KeyFrame(scale * 0.15f, d * 0.2f),
            },
        };

        var fade = new AnimationTrackComponentProperty
        {
            ComponentType = typeof(SpriteComponent),
            Property = nameof(SpriteComponent.Color),
            InterpolationMode = AnimationInterpolationMode.Linear,
            KeyFrames =
            {
                new AnimationTrackProperty.KeyFrame(color, 0f),
                new AnimationTrackProperty.KeyFrame(color, d * 0.75f),
                new AnimationTrackProperty.KeyFrame(color.WithAlpha(0f), d * 0.25f),
            },
        };

        var rotation = new AnimationTrackComponentProperty
        {
            ComponentType = typeof(SpriteComponent),
            Property = nameof(SpriteComponent.Rotation),
            InterpolationMode = AnimationInterpolationMode.Linear,
        };

        // Keyframes in quarter turns so the rotation never takes the short way round.
        var degrees = jump.Style switch
        {
            LavalandPitJumpStyle.HeadFirst => new[] { 0f, 0f, 90f, 180f, 180f },
            LavalandPitJumpStyle.Somersault => new[] { 0f, 90f, 180f, 270f, 360f },
            LavalandPitJumpStyle.Backflip => new[] { 0f, -90f, -180f, -270f, -360f },
            LavalandPitJumpStyle.Corkscrew => new[] { 0f, 180f, 360f, 540f, 720f },
            _ => new[] { 0f, 30f, -30f, 30f, 0f },
        };

        var previous = 0f;
        for (var i = 0; i < degrees.Length; i++)
        {
            var time = d * i / (degrees.Length - 1);
            var steps = Math.Max(1, (int) MathF.Ceiling(MathF.Abs(degrees[i] - previous) / 90f));
            // Split long turns into quarter turns.
            for (var s = 1; s <= steps; s++)
            {
                var angle = previous + (degrees[i] - previous) * s / steps;
                var stepDelay = i == 0 ? 0f : d / (degrees.Length - 1) / steps;
                rotation.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(sprite.Rotation + Angle.FromDegrees(angle),
                    i == 0 && s == 1 ? 0f : stepDelay));
            }

            previous = degrees[i];
        }

        return new Animation
        {
            Length = TimeSpan.FromSeconds(d + 0.05f),
            AnimationTracks = { offset, scaleTrack, fade, rotation },
        };
    }
}
