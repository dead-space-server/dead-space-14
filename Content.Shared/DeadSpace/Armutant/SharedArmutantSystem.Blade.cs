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

        var coordinatesEnt = _transform.GetMapCoordinates(ent); // Логика для совершения рывка
        var target = args.Target.ToMap(EntityManager, _transform);

        HandlePullingInteractions(ent);

        var userTransform = Transform(ent);
        _transform.SetCoordinates(ent, userTransform, args.Target);
        _transform.AttachToGridOrMap(ent, userTransform);

        var actionEnt = args.Action.Owner;
        if (!TryComp<ArmutantBladeActionComponent>(actionEnt, out var armutantActionComp)) // Вводим переменную для использования данных с компонента
            return;

        if (!_examine.InRangeUnOccluded(coordinatesEnt, target, armutantActionComp.MaxDashRange, null)) // Если человек указывает слишком далекое расстояние или что-то стоит на пути телепортации
        {
            _popup.PopupEntity(Loc.GetString("claws-dash-ability-cant-see", ("item", ent)), ent, ent);
            return;
        }

        foreach (var entity in _lookup.GetEntitiesInRange(ent, armutantActionComp.CollisionRadiusDash)) // Все кто попадают под способность в радиусе CollisionRadiusDash
        {
            if (entity == args.Performer || !TryComp<PhysicsComponent>(entity, out var physics))
                continue;

            if (TryComp<DamageableComponent>(entity, out var damageable))
            {
                _damage.TryChangeDamage(entity, ConverDamageSpecifier(armutantActionComp.TypeDamage, armutantActionComp.AmountDamage, out _), true); // Наносим урон
                _audio.PlayPvs(armutantActionComp.SoundEffect, entity);
            }

            var dashDirection = (_transform.GetWorldPosition(entity) - _transform.GetWorldPosition(ent)).Normalized(); // Происходит эффект отталкивания
            _physics.SetLinearVelocity(entity, dashDirection * armutantActionComp.KnockbackForce, body: physics);

            var effect = Spawn(armutantActionComp.SelfEffect, Transform(ent).Coordinates); // Спавним эффект при приземлении
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

        var actionEnt = args.Action.Owner;
        if (!TryComp<ArmutantBladeActionComponent>(actionEnt, out var armutantActionComp))
            return;

        var shard = Spawn(armutantActionComp.TalonBladePrototype, Transform(ent).Coordinates); // Просто создаем сущность в руках
        _hands.TryPickupAnyHand(ent, shard);

        _audio.PlayPvs(armutantActionComp.SoundEffect, ent);

        args.Handled = true;
    }
}
