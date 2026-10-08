using System.Numerics;
using Content.Shared.Chat.Prototypes; //DS-14
using Content.Shared.Sirena.Animations;
using Robust.Client.Animations;
using Robust.Shared.Animations;
using Robust.Shared.GameStates;
using Robust.Client.GameObjects;
using Robust.Client.Graphics; //DS-14
using static Content.Shared.Sirena.Animations.EmoteAnimationComponent;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths; //DS-14
using System.ComponentModel;

namespace Content.Client.Sirena.Animations;

public sealed class EmoteAnimationSystem : SharedEmoteAnimationSystem
{
    private const string AnimationKey = "emoteAnimationKeyId"; //DS-14

    [Dependency] private readonly AnimationPlayerSystem AnimationSystem = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!; //DS-14

    private readonly Dictionary<EntityUid, EmoteFrameRestore> _frames = new(); //DS-14

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EmoteAnimationComponent, ComponentHandleState>(OnHandleState);
        SubscribeLocalEvent<EmoteAnimationComponent, AnimationCompletedEvent>(OnAnimationCompleted); //DS-14
        SubscribeLocalEvent<EmoteAnimationComponent, ComponentShutdown>(OnShutdown); //DS-14
    }

    private void OnHandleState(EntityUid uid, EmoteAnimationComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not EmoteAnimationComponentState state)
            return;

        component.AnimationId = state.AnimationId;

        switch (component.AnimationId)
        {
            case EmoteStopTailActionPrototype:
                PlayEmoteStopTail(uid);
                break;
            case EmoteStartTailActionPrototype:
                PlayEmoteStartTail(uid);
                break;
            default:
                PlayFromPrototype(uid, component.AnimationId); //DS-14
                break;
        }
    }

    //DS-14 start
    private void OnAnimationCompleted(EntityUid uid, EmoteAnimationComponent component, AnimationCompletedEvent args)
    {
        if (args.Key != AnimationKey)
            return;

        RestoreFrame(uid);
    }

    private void OnShutdown(EntityUid uid, EmoteAnimationComponent component, ComponentShutdown args)
    {
        RestoreFrame(uid);
    }

    private void PlayFromPrototype(EntityUid uid, string id)
    {
        if (string.IsNullOrEmpty(id) || !_proto.TryIndex<EmotePrototype>(id, out var emote))
            return;

        if (emote.Steps == null || emote.Steps.Count == 0)
            return;

        if (!EmoteAnimation.TryResolve(emote.Steps, out var plan, out var error))
        {
            Log.Warning($"emote {id}: {error}");
            return;
        }

        if (AnimationSystem.HasRunningAnimation(uid, AnimationKey))
            return;

        var spriteRotation = Angle.Zero;
        var spriteScale = Vector2.One;
        var spriteColor = Color.White;
        SpriteComponent? sprite = null;
        if (TryComp(uid, out sprite) && sprite != null)
        {
            spriteRotation = sprite.Rotation;
            spriteScale = sprite.Scale;
            spriteColor = sprite.Color;
        }

        var bodyRotation = Angle.Zero;
        if (TryComp(uid, out TransformComponent? xform) && xform != null)
            bodyRotation = xform.LocalRotation;

        var animation = new Animation();
        var length = plan.Poses[^1].At;

        if (plan.Shift && sprite != null)
            AddVector(animation, nameof(SpriteComponent.Offset), typeof(SpriteComponent), AnimationInterpolationMode.Cubic, plan, pose => pose.Shift);

        if (plan.Size && sprite != null)
            AddVector(animation, nameof(SpriteComponent.Scale), typeof(SpriteComponent), AnimationInterpolationMode.Cubic, plan, pose => spriteScale * pose.Size);

        if (plan.Tilt && sprite != null)
            AddAngle(animation, nameof(SpriteComponent.Rotation), typeof(SpriteComponent), plan, pose => spriteRotation + Angle.FromDegrees(pose.Tilt), spriteRotation, plan.Poses[^1].Tilt);

        if (plan.Face)
            AddAngle(animation, nameof(TransformComponent.LocalRotation), typeof(TransformComponent), plan, pose => bodyRotation + Angle.FromDegrees(pose.Face), bodyRotation, plan.Poses[^1].Face);

        if (plan.Tint && sprite != null)
            AddColor(animation, plan, spriteColor);

        if (plan.Frame && sprite != null)
            TryFlick(uid, sprite, plan, animation);

        if (animation.AnimationTracks.Count == 0)
            return;

        animation.Length = TimeSpan.FromSeconds(length);
        AnimationSystem.Play(uid, animation, AnimationKey);
    }

    private static void AddVector(Animation animation, string property, Type component, AnimationInterpolationMode mode, EmoteAnimationPlan plan, Func<EmoteAnimationPose, Vector2> value)
    {
        var track = new AnimationTrackComponentProperty
        {
            ComponentType = component,
            Property = property,
            InterpolationMode = mode,
        };

        var previous = 0f;
        for (var i = 0; i < plan.Poses.Count; i++)
        {
            var at = plan.Poses[i].At;
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(value(plan.Poses[i]), i == 0 ? at : at - previous));
            previous = at;
        }

        animation.AnimationTracks.Add(track);
    }

    private static void AddAngle(Animation animation, string property, Type component, EmoteAnimationPlan plan, Func<EmoteAnimationPose, Angle> value, Angle home, float endDegrees)
    {
        var track = new AnimationTrackComponentProperty
        {
            ComponentType = component,
            Property = property,
            InterpolationMode = AnimationInterpolationMode.Linear,
        };

        var previous = 0f;
        for (var i = 0; i < plan.Poses.Count; i++)
        {
            var at = plan.Poses[i].At;
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(value(plan.Poses[i]), i == 0 ? at : at - previous));
            previous = at;
        }

        if (EmoteAnimation.IsMultiple(endDegrees, 360f) && MathF.Abs(endDegrees) > 0.001f)
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(home, 0f));

        animation.AnimationTracks.Add(track);
    }

    private static void AddColor(Animation animation, EmoteAnimationPlan plan, Color home)
    {
        var track = new AnimationTrackComponentProperty
        {
            ComponentType = typeof(SpriteComponent),
            Property = nameof(SpriteComponent.Color),
            InterpolationMode = AnimationInterpolationMode.Linear,
        };

        var previous = 0f;
        for (var i = 0; i < plan.Poses.Count; i++)
        {
            var pose = plan.Poses[i];
            var at = pose.At;
            var color = pose.RestoreTint ? home : PoseColor(pose, home);
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(color, i == 0 ? at : at - previous));
            previous = at;
        }

        animation.AnimationTracks.Add(track);
    }

    private static Color PoseColor(EmoteAnimationPose pose, Color home)
    {
        var rgb = pose.HasColor ? pose.Color : home;
        var alpha = pose.HasFade ? pose.Fade : pose.HasColor ? pose.Color.A : home.A;
        return new Color(rgb.R, rgb.G, rgb.B, alpha);
    }

    private bool TryFlick(EntityUid uid, SpriteComponent sprite, EmoteAnimationPlan plan, Animation animation)
    {
        if (!TryGetLayer((uid, sprite), plan.Layer, out var layer) || layer == null || layer.ActualRsi is not { } rsi)
            return false;

        var track = new AnimationTrackSpriteFlick
        {
            LayerKey = plan.Layer,
        };

        string? previousFrame = null;
        var previous = 0f;
        var started = false;
        foreach (var pose in plan.Poses)
        {
            if (string.IsNullOrEmpty(pose.Frame) || pose.Frame == previousFrame)
                continue;

            if (!rsi.TryGetState(pose.Frame, out _))
            {
                Log.Warning($"emoteAnimation: кадра {pose.Frame} нет на слое");
                continue;
            }

            var delta = started ? pose.At - previous : pose.At;
            track.KeyFrames.Add(new AnimationTrackSpriteFlick.KeyFrame(pose.Frame, delta));
            previousFrame = pose.Frame;
            previous = pose.At;
            started = true;
        }

        if (track.KeyFrames.Count == 0)
            return false;

        _frames[uid] = new EmoteFrameRestore
        {
            Layer = plan.Layer,
            State = layer.State,
            AutoAnimated = layer.AutoAnimated,
        };

        animation.AnimationTracks.Add(track);
        return true;
    }

    private bool TryGetLayer(Entity<SpriteComponent?> sprite, object layerKey, out SpriteComponent.Layer? layer)
    {
        if (layerKey is int index)
            return _sprite.TryGetLayer(sprite, index, out layer, false);

        if (layerKey is string key)
            return _sprite.TryGetLayer(sprite, key, out layer, false);

        layer = null;
        return false;
    }

    private void RestoreFrame(EntityUid uid)
    {
        if (!_frames.Remove(uid, out var restore) || !TryComp(uid, out SpriteComponent? sprite) || sprite == null)
            return;

        if (restore.Layer is int index)
        {
            _sprite.LayerSetRsiState((uid, sprite), index, restore.State);
            _sprite.LayerSetAutoAnimated((uid, sprite), index, restore.AutoAnimated);
            return;
        }

        if (restore.Layer is string key)
        {
            _sprite.LayerSetRsiState((uid, sprite), key, restore.State);
            _sprite.LayerSetAutoAnimated((uid, sprite), key, restore.AutoAnimated);
        }
    }

    private struct EmoteFrameRestore
    {
        public object Layer;
        public RSI.StateId State;
        public bool AutoAnimated;
    }
    public void PlayEmoteStopTail(EntityUid uid)
    {
        var animationKey = "emoteAnimationKeyId";

        if (AnimationSystem.HasRunningAnimation(uid, animationKey))
            return;
        if (!TryComp<SpriteComponent>(uid, out var sprite))
        { return; }

        //sprite.NetSyncEnabled = true;
        foreach (var item in sprite.AllLayers)
        {
            if (item.RsiState.Name != null)
                if (item.RsiState.Name.ToLower().Contains("tail"))
                {
                    item.AutoAnimated = false;
                    item.AnimationTime = 0;
                }
        }

        //foreach (var component in _entityManager.GetComponents(uid))
        //{
        //    _entityManager.Dirty((Robust.Shared.GameObjects.Component)component);
        //}
    }
    public void PlayEmoteStartTail(EntityUid uid)
    {
        var animationKey = "emoteAnimationKeyId";

        if (AnimationSystem.HasRunningAnimation(uid, animationKey))
            return;
        if (!TryComp<SpriteComponent>(uid, out var sprite))
        { return; }

        //sprite.NetSyncEnabled = true;
        foreach (var item in sprite.AllLayers)
        {
            if (item.RsiState.Name != null)
                if (item.RsiState.Name.ToLower().Contains("tail"))
                {
                    item.AnimationTime = 0;
                    item.AutoAnimated = true;
                }
        }

        //foreach (var component in _entityManager.GetComponents(uid))
        //{
        //    _entityManager.Dirty((Robust.Shared.GameObjects.Component) component);
        //}
    }

}
