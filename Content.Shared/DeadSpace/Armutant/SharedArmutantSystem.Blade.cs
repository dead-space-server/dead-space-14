using Content.Shared.Damage;
using Robust.Shared.Physics.Components;

namespace Content.Shared.DeadSpace.Armutant;

public partial class SharedArmutantSystem
{
    private void InitializeBlade()
    {
        SubscribeLocalEvent<ArmutantComponent, BladeDashActionEvent>(OnDashAction);
        SubscribeLocalEvent<ArmutantComponent, CreateTalonBladeEvent>(OnCreateTalonBladeAction);
    }

    private void OnDashAction(Entity<ArmutantComponent> ent, ref BladeDashActionEvent args)
    {
        if (!_net.IsServer)
            return;

        if (args.Handled)
            return;

        var coordinatesEnt = _transform.GetMapCoordinates(ent);
        var target = args.Target.ToMap(EntityManager, _transform);

        HandlePullingInteractions(ent);

        var userTransform = Transform(ent);
        _transform.SetCoordinates(ent, userTransform, args.Target);
        _transform.AttachToGridOrMap(ent, userTransform);

        if (!_examine.InRangeUnOccluded(coordinatesEnt, target, args.MaxDashRange, null))
        {
            _popup.PopupEntity(Loc.GetString("claws-dash-ability-cant-see", ("item", ent)), ent, ent);
            return;
        }

        foreach (var entity in _lookup.GetEntitiesInRange(ent, args.CollisionRadiusDash))
        {
            if (entity == args.Performer || !TryComp<PhysicsComponent>(entity, out var physics))
                continue;

            if (TryComp<DamageableComponent>(entity, out var damageable))
            {
                _damage.TryChangeDamage(entity, ConverDamageSpecifier(args.TypeDamage, args.AmountDamage, out _), true);
                _audio.PlayPvs(args.SoundEffectDash, entity);
            }

            var dashDirection = (_transform.GetWorldPosition(entity) - _transform.GetWorldPosition(ent)).Normalized();
            _physics.SetLinearVelocity(entity, dashDirection * args.KnockbackForce, body: physics);

            var effect = Spawn(args.SelfEffect, Transform(ent).Coordinates);
            _transform.SetParent(effect, ent);
        }
        args.Handled = true;
    }

    private void OnCreateTalonBladeAction(Entity<ArmutantComponent> ent, ref CreateTalonBladeEvent args)
    {
        if (!_net.IsServer)
            return;

        if (args.Handled)
            return;

        var shard = Spawn(args.TalonBladePrototype, Transform(ent).Coordinates);
        _hands.TryPickupAnyHand(ent, shard);

        _audio.PlayPvs(args.SoundEffectSpawn, ent);

        args.Handled = true;
    }
}
