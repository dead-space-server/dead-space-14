using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Sirena.Animations;
using Content.Server.Sirena.Animations;

namespace Content.Server.DeadSpace.Weapons.Melee
{
    public sealed class AnimationOnMeleeSystem : EntitySystem
    {
        [Dependency] private readonly EmoteAnimationSystem _animationSystem = default!;
        public override void Initialize()
        {
            SubscribeLocalEvent<AnimationOnMeleeComponent, MeleeHitEvent>(OnMeleeHit);
        }
        private void OnMeleeHit(EntityUid uid, AnimationOnMeleeComponent component, MeleeHitEvent args)
        {
            if (TryComp<EmoteAnimationComponent>(args.User, out var emote))
            {
                _animationSystem.PlayEmoteAnimation(args.User, emote, component.Emote);
            }
        }
    }
}
