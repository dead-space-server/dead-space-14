// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Robust.Client.Animations;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Animations;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Lavaland.ChromiteCaves;

/// <summary>
/// Looks of the chromite caves: the pit shimmering blue and purple, the pulsing beam of the exit,
/// hopping under the beam without a hook, the hook with its rope flying up and the doll pulled up after it.
/// </summary>
public sealed class LavalandChromiteCavesClientSystem : EntitySystem
{
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SpriteTreeSystem _spriteTree = default!;

    private const string HopKey = "lavaland-cave-hop";
    private const string ClimbKey = "lavaland-cave-climb";

    /// <summary>The doll is pulled this high, beyond the top of the screen.</summary>
    private const float FlyHeight = 11f;

    private readonly Dictionary<EntityUid, (Vector2 Offset, Angle Rotation, Vector2 Scale)> _original = new();
    private readonly Dictionary<EntityUid, float> _baseEnergy = new();
    private LavalandCaveRopeOverlay _rope = default!;
    private LavalandPitJumperAlertOverlay _alerts = default!;

    public override void Initialize()
    {
        base.Initialize();

        _rope = new LavalandCaveRopeOverlay(EntityManager, _sprite);
        _overlays.AddOverlay(_rope);
        _alerts = new LavalandPitJumperAlertOverlay(EntityManager, _sprite);
        _overlays.AddOverlay(_alerts);

        SubscribeLocalEvent<LavalandCaveHopComponent, AfterAutoHandleStateEvent>(OnHopState);
        SubscribeLocalEvent<LavalandCaveHopComponent, ComponentRemove>(OnHopRemove);
        SubscribeLocalEvent<LavalandCaveClimbComponent, AfterAutoHandleStateEvent>(OnClimbState);
        SubscribeLocalEvent<LavalandCaveClimbComponent, ComponentRemove>(OnClimbRemove);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_rope);
        _overlays.RemoveOverlay(_alerts);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var time = (float) _timing.RealTime.TotalSeconds;

        var pits = EntityQueryEnumerator<LavalandChromitePitComponent, SpriteComponent>();
        while (pits.MoveNext(out var uid, out var pit, out var sprite))
        {
            var t = (MathF.Sin(time * MathF.Tau / MathF.Max(0.5f, pit.Period)) + 1f) / 2f;
            var color = Color.InterpolateBetween(pit.ColorA, pit.ColorB, t);
            if (pit.SpriteTint > 0f)
                _sprite.SetColor((uid, sprite), Color.InterpolateBetween(Color.White, color, pit.SpriteTint));
            if (_pointLight.TryGetLight(uid, out var light))
                _pointLight.SetColor(uid, color, light);
        }

        var hops = EntityQueryEnumerator<LavalandCaveHopComponent, SpriteComponent>();
        while (hops.MoveNext(out var uid, out _, out var sprite))
        {
            _spriteTree.QueueTreeUpdate((uid, sprite));
        }

        // The beam has no sprite: it is light and rising motes.
        var exits = EntityQueryEnumerator<LavalandChromiteExitComponent>();
        while (exits.MoveNext(out var uid, out var exit))
        {
            var pulse = 1f - exit.PulseDepth * (MathF.Sin(time * MathF.Tau / MathF.Max(0.5f, exit.PulsePeriod)) + 1f) / 2f;
            if (!_pointLight.TryGetLight(uid, out var light))
                continue;

            if (!_baseEnergy.TryGetValue(uid, out var energy))
                _baseEnergy[uid] = energy = light.Energy;
            _pointLight.SetEnergy(uid, energy * pulse, light);
        }

        var alerts = EntityQueryEnumerator<LavalandPitJumperAlertComponent>();
        while (alerts.MoveNext(out _, out var alert))
        {
            alert.Elapsed += frameTime;
        }

        var climbs = EntityQueryEnumerator<LavalandCaveClimbComponent, SpriteComponent>();
        while (climbs.MoveNext(out var uid, out var climb, out var sprite))
        {
            // Pulled far above its entity: keep the render tree up with the sprite or it is culled.
            _spriteTree.QueueTreeUpdate((uid, sprite));

            if (climb.SeenStage != climb.Stage)
            {
                climb.SeenStage = climb.Stage;
                climb.Elapsed = 0f;
            }

            climb.Elapsed += frameTime;
        }
    }

    #region Hop

    private void OnHopState(Entity<LavalandCaveHopComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!Remember(ent, out var original))
            return;

        _animation.Play(ent.Owner, BuildHops(ent.Comp, original), HopKey);
    }

    private void OnHopRemove(Entity<LavalandCaveHopComponent> ent, ref ComponentRemove args)
    {
        Restore(ent, HopKey);
    }

    /// <summary>Jumps higher and higher at the light, then a tired slump.</summary>
    private static Animation BuildHops(LavalandCaveHopComponent hop, (Vector2 Offset, Angle Rotation, Vector2 Scale) original)
    {
        var d = MathF.Max(0.6f, hop.Duration);
        var hops = Math.Max(1, hop.Hops);
        var slump = d * 0.2f;
        var each = (d - slump) / hops;

        var offset = NewTrack(nameof(SpriteComponent.Offset));
        var scale = NewTrack(nameof(SpriteComponent.Scale));
        var rotation = NewTrack(nameof(SpriteComponent.Rotation));
        offset.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Offset, 0f));
        scale.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Scale, 0f));
        rotation.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Rotation, 0f));

        for (var i = 0; i < hops; i++)
        {
            var height = 0.45f + 0.25f * i;
            offset.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Offset, each * 0.15f));
            offset.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Offset + new Vector2(0f, height), each * 0.4f));
            offset.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Offset, each * 0.45f));

            scale.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Scale * new Vector2(1.15f, 0.85f), each * 0.15f));
            scale.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Scale * new Vector2(0.9f, 1.15f), each * 0.4f));
            scale.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Scale, each * 0.45f));

            // Reaching up at the light with a little wobble.
            var tilt = i % 2 == 0 ? 8f : -8f;
            rotation.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Rotation, each * 0.15f));
            rotation.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Rotation + Angle.FromDegrees(tilt), each * 0.4f));
            rotation.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Rotation, each * 0.45f));
        }

        // Shoulders down.
        scale.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Scale * new Vector2(1.08f, 0.88f), slump * 0.5f));
        scale.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Scale * new Vector2(1.08f, 0.88f), slump * 0.5f));
        offset.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Offset, slump));
        rotation.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Rotation, slump));

        return new Animation
        {
            Length = TimeSpan.FromSeconds(d + 0.05f),
            AnimationTracks = { offset, scale, rotation },
        };
    }

    #endregion

    #region Climb

    private void OnClimbState(Entity<LavalandCaveClimbComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        switch (ent.Comp.Stage)
        {
            case LavalandCaveClimbStage.Rope:
                if (Remember(ent, out var ropeOriginal))
                    _animation.Play(ent.Owner, BuildBrace(ent.Comp, ropeOriginal), ClimbKey);
                break;

            case LavalandCaveClimbStage.FlyUp:
                Remember(ent, out var flyOriginal);
                if (_animation.HasRunningAnimation(ent.Owner, ClimbKey))
                    _animation.Stop(ent.Owner, null, ClimbKey);
                _animation.Play(ent.Owner, BuildFlyUp(ent.Comp, flyOriginal), ClimbKey);
                break;

            case LavalandCaveClimbStage.Hanging:
                // The hanging screen draws the doll itself, in its normal look.
                Restore(ent.Owner, ClimbKey);
                break;
        }
    }

    private void OnClimbRemove(Entity<LavalandCaveClimbComponent> ent, ref ComponentRemove args)
    {
        Restore(ent, ClimbKey);
    }

    /// <summary>Braces with a squat while the hook flies up.</summary>
    private static Animation BuildBrace(LavalandCaveClimbComponent climb, (Vector2 Offset, Angle Rotation, Vector2 Scale) original)
    {
        var d = MathF.Max(0.3f, climb.Duration);
        var scale = NewTrack(nameof(SpriteComponent.Scale));
        scale.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Scale, 0f));
        scale.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Scale * new Vector2(1.1f, 0.88f), d * 0.3f));
        scale.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Scale * new Vector2(1.1f, 0.88f), d * 0.7f));

        return new Animation
        {
            Length = TimeSpan.FromSeconds(d),
            AnimationTracks = { scale },
        };
    }

    /// <summary>Yanked up by the rope, faster and faster, out of the top of the screen.</summary>
    private static Animation BuildFlyUp(LavalandCaveClimbComponent climb, (Vector2 Offset, Angle Rotation, Vector2 Scale) original)
    {
        var d = MathF.Max(0.3f, climb.Duration);
        var offset = NewTrack(nameof(SpriteComponent.Offset));
        offset.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Offset, 0f));
        offset.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Offset + new Vector2(0f, 0.6f), d * 0.25f));
        offset.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Offset + new Vector2(0f, FlyHeight * 0.35f), d * 0.35f));
        offset.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Offset + new Vector2(0f, FlyHeight), d * 0.4f));

        var scale = NewTrack(nameof(SpriteComponent.Scale));
        scale.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Scale * new Vector2(1.1f, 0.88f), 0f));
        scale.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Scale * new Vector2(0.85f, 1.2f), d * 0.25f));
        scale.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Scale * new Vector2(0.8f, 1.25f), d * 0.75f));

        var rotation = NewTrack(nameof(SpriteComponent.Rotation));
        rotation.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Rotation, 0f));
        rotation.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Rotation + Angle.FromDegrees(10), d * 0.4f));
        rotation.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Rotation + Angle.FromDegrees(-10), d * 0.3f));
        rotation.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(original.Rotation, d * 0.3f));

        return new Animation
        {
            Length = TimeSpan.FromSeconds(d + 0.5f),
            AnimationTracks = { offset, scale, rotation },
        };
    }

    #endregion

    private bool Remember(EntityUid uid, out (Vector2 Offset, Angle Rotation, Vector2 Scale) original)
    {
        if (_original.TryGetValue(uid, out original))
            return true;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return false;

        original = (sprite.Offset, sprite.Rotation, sprite.Scale);
        _original[uid] = original;
        return true;
    }

    private void Restore(EntityUid uid, string key)
    {
        if (_animation.HasRunningAnimation(uid, key))
            _animation.Stop(uid, null, key);

        if (!_original.Remove(uid, out var original) || !TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var spriteEnt = (uid, sprite);
        _sprite.SetOffset(spriteEnt, original.Offset);
        _sprite.SetRotation(spriteEnt, original.Rotation);
        _sprite.SetScale(spriteEnt, original.Scale);
    }

    private static AnimationTrackComponentProperty NewTrack(string property)
    {
        return new AnimationTrackComponentProperty
        {
            ComponentType = typeof(SpriteComponent),
            Property = property,
            InterpolationMode = AnimationInterpolationMode.Linear,
        };
    }
}
