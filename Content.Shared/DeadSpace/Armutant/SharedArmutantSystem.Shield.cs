using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Armutant;

public partial class SharedArmutantSystem
{
    private void InitializeShield()
    {
        SubscribeLocalEvent<ArmutantComponent, CreateArmorShieldToggleEvent>(OnCreateArmorShield);
        SubscribeLocalEvent<ArmutantComponent, StunShieldToggleEvent>(OnStunShieldAction);
        SubscribeLocalEvent<ArmutantComponent, VoidShieldToggleEvent>(OnToggleShield);
    }

    private void OnCreateArmorShield(Entity<ArmutantComponent> ent, ref CreateArmorShieldToggleEvent args)
    {
        if (!_net.IsServer)
            return;

        if (args.Handled)
            return;

        _audio.PlayPvs(args.SoundEffect, ent);

        if (ent.Comp.EquipmentArmor.Count > 0)
        {
            RemoveAllArmor(ent, ent.Comp.EquipmentArmor);
            _popup.PopupEntity(Loc.GetString("armutant-armor-removed"), ent, ent);
        }
        else
        {
            var success = TryEquipArmor(ent, args.ArmorPrototype, "outerClothing", ent.Comp.EquipmentArmor) &
                          TryEquipArmor(ent, args.ArmorHelmetPrototype, "head", ent.Comp.EquipmentArmor);

            if (!success)
                _popup.PopupEntity(Loc.GetString("armutant-equip-armor-fail"), ent, ent);
            else
                _popup.PopupEntity(Loc.GetString("armutant-armor-equipped"), ent, ent);
        }
        args.Handled = true;
    }

    private void OnStunShieldAction(Entity<ArmutantComponent> ent, ref StunShieldToggleEvent args)
    {
        if (!_net.IsServer)
            return;

        if (args.Handled)
            return;

        var ev = new StunShieldAttemptEvent();
        RaiseLocalEvent(ent, ref ev);

        if (ev.Cancelled)
            return;

        if (!TryComp<TransformComponent>(ent, out var xform) || _mobState.IsDead(ent))
            return;

        args.Handled = true;

        _receivers.Clear();
        foreach (var entity in _lookup.GetEntitiesInRange(xform.Coordinates, args.Range))
        {
            if (entity == args.Performer)
                continue;

            if (!HasComp<MobStateComponent>(entity))
                continue;

            _receivers.Add(entity);
        }

        if (_net.IsServer)
            _audio.PlayPvs(args.SoundEffect, ent);

        foreach (var receiver in _receivers)
        {
            if (!TryComp<PhysicsComponent>(receiver, out var physics))
                continue;

            if (_mobState.IsDead(receiver))
                continue;

            _stun.TryUpdateParalyzeDuration(receiver, args.ParalyzeTime);

            var dashDirection = (_transform.GetWorldPosition(receiver) - _transform.GetWorldPosition(ent)).Normalized();
            _physics.SetLinearVelocity(receiver, dashDirection * args.KnockbackForce, body: physics);

            if (_net.IsServer && args.EffectTarget is not null)
                SpawnAttachedTo(args.EffectTarget, Transform(receiver).Coordinates);

            if (xform.Coordinates.TryDistance(EntityManager, Transform(receiver).Coordinates, out var distance) &&
                distance <= args.ShortRange)
            {
                if (!_standing.IsDown(receiver))
                    continue;
            }
        }
        SpawnAttachedTo(args.SelfEffectStun, Transform(ent).Coordinates);
    }

    private void OnToggleShield(Entity<ArmutantComponent> ent, ref VoidShieldToggleEvent args) // Метод вызова щита с отражением на 15 секунд
    {
        if (!_net.IsServer)
            return;

        if (args.Handled)
            return;

        var reflectComponent = EnsureComp<ReflectComponent>(ent);
        reflectComponent.ReflectProb = 0.99f;
        reflectComponent.Spread = 180f;
        reflectComponent.SoundOnReflect = args.ReflectSound;

        if (_net.IsServer && args.SelfEffectShield is not null)
        {
            var effect = Spawn(args.SelfEffectShield, Transform(ent).Coordinates);
            _transform.SetParent(effect, ent);
        }

        Timer.Spawn(args.ActiveTime, () =>
        {
            RemComp<ReflectComponent>(ent);
        });
        args.Handled = true;
    }
}
