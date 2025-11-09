using System.Numerics;
using Robust.Client.Animations;
using Robust.Shared.Animations;
using Robust.Shared.GameStates;
using Robust.Client.GameObjects;
using Content.Shared.DeadSpace.Abilities.JumpAbility.Components;

namespace Content.Client.DeadSpace.Abilities.JumpAbility;

public sealed class JumpAbilitySystem : EntitySystem
{
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    private const string AnimationKey = "jumpAnimationKeyId";
    public override void Initialize()
    {
        SubscribeLocalEvent<JumpAbilityComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnGetState(EntityUid uid, JumpAbilityComponent component, ref ComponentGetState args)
    {
        args.State = new JumpAnimationComponentState();
    }
    private void OnHandleState(EntityUid uid, JumpAbilityComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not JumpAnimationComponentState state)
            return;

        Jump(uid, component);
    }

    public void Jump(EntityUid uid, JumpAbilityComponent component)
    {
        if (_animation.HasRunningAnimation(uid, AnimationKey))
            return;

        var animation = new Animation
        {
            Length = TimeSpan.FromSeconds(component.JumpDuration),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Cubic,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(0, 1), component.JumpDuration / 2),
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero,  component.JumpDuration / 2),
                    }
                }
            }
        };

        _animation.Play(uid, animation, AnimationKey);
    }

}
