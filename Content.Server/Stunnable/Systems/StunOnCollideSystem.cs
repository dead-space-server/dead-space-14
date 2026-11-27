using Content.Server.Ensnaring; // DS14-downed-bola
using Content.Server.Stunnable.Components;
using Content.Shared.Damage;
using Content.Shared.Ensnaring.Components; // DS14-downed-bola
using Content.Shared.Mobs.Systems; // DS14-downed-bola
using Content.Shared.Stunnable; // DS14-downed-bola
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Movement.Systems;
using JetBrains.Annotations;
using Content.Shared.Throwing;
using Robust.Shared.Physics.Events;

namespace Content.Server.Stunnable.Systems;

[UsedImplicitly]
internal sealed partial class StunOnCollideSystem : EntitySystem
{
    [Dependency] private StunSystem _stunSystem = default!;
    [Dependency] private MovementModStatusSystem _movementMod = default!;
    [Dependency] private MobStateSystem _mobState = default!; // DS14-downed-bola

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StunOnCollideComponent, StartCollideEvent>(HandleCollide);
        // DS14-downed-bola-start
        SubscribeLocalEvent<StunOnCollideComponent, ThrowDoHitEvent>(HandleThrow, after: new[] { typeof(EnsnareableSystem) });
        // DS14-downed-bola-end
        SubscribeLocalEvent<StunOnCollideComponent, HitscanRaycastFiredEvent>(HandleHitscan); // DS14
    }

    private void TryDoCollideStun(Entity<StunOnCollideComponent> ent, EntityUid target)
    {
        // DS14-downed-bola-start
        if (TryComp<EnsnaringComponent>(ent.Owner, out var ensnaring) &&
            ensnaring.SkipDownedOrDead &&
            (HasComp<KnockedDownComponent>(target) || _mobState.IsDead(target)))
            return;
        // DS14-downed-bola-end

        _stunSystem.TryKnockdown(target, ent.Comp.KnockdownAmount, ent.Comp.Refresh, ent.Comp.AutoStand, ent.Comp.Drop, true);

        if (ent.Comp.Refresh)
        {
            _stunSystem.TryUpdateStunDuration(target, ent.Comp.StunAmount);

            _movementMod.TryUpdateMovementSpeedModDuration(
                target,
                MovementModStatusSystem.TaserSlowdown,
                ent.Comp.SlowdownAmount,
                ent.Comp.WalkSpeedModifier,
                ent.Comp.SprintSpeedModifier
            );
        }
        else
        {
            _stunSystem.TryAddStunDuration(target, ent.Comp.StunAmount);
            _movementMod.TryAddMovementSpeedModDuration(
                target,
                MovementModStatusSystem.TaserSlowdown,
                ent.Comp.SlowdownAmount,
                ent.Comp.WalkSpeedModifier,
                ent.Comp.SprintSpeedModifier
            );
        }
    }

    private void HandleCollide(Entity<StunOnCollideComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != ent.Comp.FixtureID)
            return;

        TryDoCollideStun(ent, args.OtherEntity);
    }

    private void HandleThrow(Entity<StunOnCollideComponent> ent, ref ThrowDoHitEvent args)
    {
        TryDoCollideStun(ent, args.Target);
    }

    // DS14-start: allow converted hitscan tasers/crossbows to reuse projectile stun data.
    private void HandleHitscan(Entity<StunOnCollideComponent> ent, ref HitscanRaycastFiredEvent args)
    {
        if (args.Data.HitEntity == null)
            return;

        TryDoCollideStun(ent, args.Data.HitEntity.Value);

        if (HasComp<HitscanBasicDamageComponent>(ent) ||
            HasComp<HitscanStaminaDamageComponent>(ent))
        {
            return;
        }

        var damageEvent = new HitscanDamageDealtEvent
        {
            Target = args.Data.HitEntity.Value,
            DamageDealt = new DamageSpecifier(),
            Data = args.Data,
        };

        RaiseLocalEvent(ent, ref damageEvent);
    }
    // DS14-end
}
