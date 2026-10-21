// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.DeadSpace.Movement.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using System.Numerics;

namespace Content.Shared.DeadSpace.Abilities.Slide;

public sealed class SpeedSlidingSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public bool TrySlide(EntityUid uid)
    {
        if (!TryComp<SpeedSlidingComponent>(uid, out var slide))
            return false;

        // Tile-by-tile walkers do not slide.
        if (HasComp<TileStepMovementComponent>(uid))
            return false;

        if (!TryComp<PhysicsComponent>(uid, out var physics))
            return false;

        var velocity = physics.LinearVelocity;
        var speed = velocity.Length();
        if (speed < slide.MinSlideSpeed)
            return false;

        var direction = velocity / speed;
        var slideImpulse = direction * (slide.SlideSpeed * slide.SlideDistance * physics.Mass);

        _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
        _physics.ApplyLinearImpulse(uid, slideImpulse, body: physics);

        _audio.PlayPredicted(slide.SlideSound, uid, uid);
        return true;
    }
}
