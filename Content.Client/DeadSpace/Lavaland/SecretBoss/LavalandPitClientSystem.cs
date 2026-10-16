// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Content.Shared.DeadSpace.Lavaland.SecretBoss;
using Robust.Client.Animations;
using Robust.Client.ComponentTrees;
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
    [Dependency] private readonly SpriteTreeSystem _spriteTree = default!;

    private const string JumpKey = "lavaland-pit-jump";
    private const string LandingKey = "lavaland-pit-landing";

    /// <summary>The doll starts this high above its owner, beyond the top of the screen.</summary>
    private const float DropHeight = 9.5f;

    private readonly Dictionary<EntityUid, (Vector2 Offset, Angle Rotation, Vector2 Scale, Color Color)> _original = new();
    private readonly Dictionary<EntityUid, (Vector2 Offset, Angle Rotation, Vector2 Scale, Color Color)> _landingOriginal = new();
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
        SubscribeLocalEvent<LavalandPitLandingComponent, AfterAutoHandleStateEvent>(OnLandingState);
        SubscribeLocalEvent<LavalandPitLandingComponent, ComponentRemove>(OnLandingRemove);
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

        // The doll is drawn far from its entity: keep its place in the render tree with it,
        // or it is culled while high above the screen and only pops in near the ground.
        var landings = EntityQueryEnumerator<LavalandPitLandingComponent, SpriteComponent>();
        while (landings.MoveNext(out var uid, out _, out var sprite))
        {
            _spriteTree.QueueTreeUpdate((uid, sprite));
        }

        var jumps = EntityQueryEnumerator<LavalandPitJumpingComponent, SpriteComponent>();
        while (jumps.MoveNext(out var uid, out _, out var sprite))
        {
            _spriteTree.QueueTreeUpdate((uid, sprite));
        }
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

    private void OnLandingState(Entity<LavalandPitLandingComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_landingOriginal.ContainsKey(ent) || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        // A jump that is still being undone must not be taken for the original look.
        var original = _original.TryGetValue(ent, out var jumpOriginal)
            ? jumpOriginal
            : (sprite.Offset, sprite.Rotation, sprite.Scale, sprite.Color);
        _landingOriginal[ent] = original;

        var animation = ent.Comp.ClimbOut
            ? BuildClimbOut(ent.Comp, original.Offset, original.Rotation, original.Scale, original.Color)
            : BuildLanding(ent.Comp, original.Offset, original.Rotation, original.Scale);
        if (_animation.HasRunningAnimation(ent.Owner, LandingKey))
            _animation.Stop(ent.Owner, null, LandingKey);
        _animation.Play(ent.Owner, animation, LandingKey);
    }

    private void OnLandingRemove(Entity<LavalandPitLandingComponent> ent, ref ComponentRemove args)
    {
        if (_animation.HasRunningAnimation(ent.Owner, LandingKey))
            _animation.Stop(ent.Owner, null, LandingKey);

        if (!_landingOriginal.Remove(ent, out var original) || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var spriteEnt = (ent.Owner, sprite);
        _sprite.SetOffset(spriteEnt, original.Offset);
        _sprite.SetRotation(spriteEnt, original.Rotation);
        _sprite.SetScale(spriteEnt, original.Scale);
        _sprite.SetColor(spriteEnt, original.Color);
    }

    /// <summary>Pose the doll ends up in on impact, degrees.</summary>
    private static float LandingPose(LavalandLandingStyle style)
    {
        return style switch
        {
            LavalandLandingStyle.Back => 90f,
            LavalandLandingStyle.Head => 180f,
            LavalandLandingStyle.Face => -90f,
            LavalandLandingStyle.Side => 90f,
            _ => 0f,
        };
    }

    /// <summary>
    /// Drops from beyond the top of the screen spinning, slams into the ground in the style's pose with a squash and
    /// a little bounce, lies there wriggling, then hops up and stands in the middle of the screen.
    /// </summary>
    private static Animation BuildLanding(LavalandPitLandingComponent landing, Vector2 baseOffset, Angle baseRotation, Vector2 baseScale)
    {
        var d = MathF.Max(0.6f, landing.Duration);
        var pose = LandingPose(landing.Style);
        var slide = landing.Style == LavalandLandingStyle.Side ? new Vector2(0.45f, 0f) : Vector2.Zero;
        var squash = landing.Style == LavalandLandingStyle.Seat ? new Vector2(1.45f, 0.55f) : new Vector2(1.3f, 0.7f);

        var offset = Track(nameof(SpriteComponent.Offset),
            (baseOffset + new Vector2(0f, DropHeight), 0f),
            (baseOffset + new Vector2(0f, DropHeight), d * 0.06f),
            (baseOffset + new Vector2(0f, DropHeight * 0.7f), d * 0.1f),
            (baseOffset + new Vector2(0f, DropHeight * 0.25f), d * 0.08f),
            (baseOffset, d * 0.05f),
            (baseOffset + slide * 0.5f + new Vector2(0f, 0.32f), d * 0.07f),
            (baseOffset + slide, d * 0.07f),
            (baseOffset + slide, d * 0.27f),
            (baseOffset + slide * 0.5f + new Vector2(0f, 0.8f), d * 0.1f),
            (baseOffset, d * 0.1f),
            (baseOffset, d * 0.1f));

        var scale = Track(nameof(SpriteComponent.Scale),
            (baseScale * new Vector2(0.9f, 1.15f), 0f),
            (baseScale * new Vector2(0.9f, 1.15f), d * 0.29f),
            (baseScale * squash, d * 0.03f),
            (baseScale * new Vector2(0.92f, 1.08f), d * 0.06f),
            (baseScale, d * 0.05f),
            (baseScale, d * 0.26f),
            (baseScale * new Vector2(0.88f, 1.15f), d * 0.04f),
            (baseScale, d * 0.1f),
            (baseScale * new Vector2(1.15f, 0.85f), d * 0.06f),
            (baseScale, d * 0.08f),
            (baseScale, d * 0.03f));

        // Spin on the way down and arrive exactly in the pose; wriggle; turn back upright during the hop.
        var rotation = new AnimationTrackComponentProperty
        {
            ComponentType = typeof(SpriteComponent),
            Property = nameof(SpriteComponent.Rotation),
            InterpolationMode = AnimationInterpolationMode.Linear,
        };
        var spin = landing.Style == LavalandLandingStyle.Face ? 360f : -360f;
        rotation.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(baseRotation + Angle.FromDegrees(pose + spin), 0f));
        AddTurn(rotation, baseRotation, pose + spin, pose + spin, d * 0.06f);
        AddTurn(rotation, baseRotation, pose + spin, pose, d * 0.23f);
        AddTurn(rotation, baseRotation, pose, pose, d * 0.14f);
        AddTurn(rotation, baseRotation, pose, pose + 12f, d * 0.07f);
        AddTurn(rotation, baseRotation, pose + 12f, pose - 12f, d * 0.07f);
        AddTurn(rotation, baseRotation, pose - 12f, pose, d * 0.08f);
        AddTurn(rotation, baseRotation, pose, 0f, d * 0.14f);
        AddTurn(rotation, baseRotation, 0f, 0f, d * 0.21f);

        return new Animation
        {
            Length = TimeSpan.FromSeconds(d + 0.05f),
            AnimationTracks = { offset, scale, rotation },
        };
    }

    /// <summary>Rises out of the pit growing back to size, hops over the edge and lands beside it.</summary>
    private static Animation BuildClimbOut(LavalandPitLandingComponent landing, Vector2 baseOffset, Angle baseRotation, Vector2 baseScale, Color baseColor)
    {
        var d = MathF.Max(0.5f, landing.Duration);
        var from = landing.From;

        var offset = Track(nameof(SpriteComponent.Offset),
            (baseOffset + from, 0f),
            (baseOffset + from * 0.6f + new Vector2(0f, 0.45f), d * 0.35f),
            (baseOffset + from * 0.2f + new Vector2(0f, 0.95f), d * 0.25f),
            (baseOffset, d * 0.18f),
            (baseOffset, d * 0.22f));

        var scale = Track(nameof(SpriteComponent.Scale),
            (baseScale * 0.3f, 0f),
            (baseScale * 0.8f, d * 0.35f),
            (baseScale * new Vector2(0.92f, 1.1f), d * 0.25f),
            (baseScale * new Vector2(1.15f, 0.85f), d * 0.18f),
            (baseScale, d * 0.12f),
            (baseScale, d * 0.1f));

        var color = Track(nameof(SpriteComponent.Color),
            (baseColor.WithAlpha(0f), 0f),
            (baseColor, d * 0.3f),
            (baseColor, d * 0.7f));

        var rotation = Track(nameof(SpriteComponent.Rotation),
            (baseRotation, 0f),
            (baseRotation + Angle.FromDegrees(-15), d * 0.35f),
            (baseRotation + Angle.FromDegrees(12), d * 0.25f),
            (baseRotation, d * 0.18f),
            (baseRotation, d * 0.22f));

        return new Animation
        {
            Length = TimeSpan.FromSeconds(d + 0.05f),
            AnimationTracks = { offset, scale, color, rotation },
        };
    }

    private static AnimationTrackComponentProperty Track(string property, params (object Value, float Delay)[] frames)
    {
        var track = new AnimationTrackComponentProperty
        {
            ComponentType = typeof(SpriteComponent),
            Property = property,
            InterpolationMode = AnimationInterpolationMode.Linear,
        };

        foreach (var (value, delay) in frames)
        {
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(value, delay));
        }

        return track;
    }

    /// <summary>Turns from one angle to another over the delay in steps under a quarter turn, so it never goes the short way.</summary>
    private static void AddTurn(AnimationTrackComponentProperty track, Angle baseRotation, float fromDegrees, float toDegrees, float delay)
    {
        var steps = Math.Max(1, (int) MathF.Ceiling(MathF.Abs(toDegrees - fromDegrees) / 80f));
        for (var s = 1; s <= steps; s++)
        {
            var angle = fromDegrees + (toDegrees - fromDegrees) * s / steps;
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(baseRotation + Angle.FromDegrees(angle), delay / steps));
        }
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
