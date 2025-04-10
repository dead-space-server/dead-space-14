using Content.Server.DeadSpace.MartialArts;
using Content.Shared.DeadSpace.SmokingCarp;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Physics.Components;
using System.Numerics;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Weapons.Reflect;
using Content.Shared.DeadSpace.MartialArts.SmokingCarp;
using System.Linq;
using Content.Shared.DeadSpace.MartialArts.Arkalyse;

namespace Content.Shared.DeadSpace.MartialArts;

public partial class SharedMartialArtsSystem
{
    private void InitializeSmokingCarp()
    {
        SubscribeLocalEvent<SmokingCarpComponent, SmokingCarpActionEvent>(OnSmokingCarpAction);
        SubscribeLocalEvent<SmokingCarpComponent, MeleeHitEvent>(OnMeleeHitEvent);

        SubscribeLocalEvent<SmokingCarpComponent, ReflectCarpEvent>(SmokingCarpReflect);
        SubscribeLocalEvent<SmokingCarpTripPunchComponent, SmokingCarpTripPunchEvent>(SmokingCarpTripPunch);

        SubscribeLocalEvent<MartialArtsTrainingCarpComponent, UseInHandEvent>(UseInjectorSmokingCarp);
    }
    // Используем в руках инжектор, для получения умений + базовых показателей сущности
    private void UseInjectorSmokingCarp(Entity<MartialArtsTrainingCarpComponent> ent, ref UseInHandEvent args)
    {
        if (!_net.IsServer)
            return;

        if (args.Handled)
            return;

        if (TryComp<ArkalyseComponent>(args.User, out _))
            return;

        EnsureComp<SmokingCarpTripPunchComponent>(args.User);
        var userSmokingCarp = EnsureComp<SmokingCarpComponent>(args.User);
        foreach (var actionId in userSmokingCarp.BaseSmokingCarp)
        {
            var actions = _action.AddAction(args.User, actionId);
            if (actions != null)
                userSmokingCarp.SmokeCarpActionEntities.Add(actions.Value);
        }

        TransformToItem(ent, ent.Comp.ItemAfterLerning);

        var meleeWeaponComponent = EnsureComp<MeleeWeaponComponent>(args.User);
        meleeWeaponComponent.AttackRate = ent.Comp.AddAtackRate;

        args.Handled = true;
    }
    // Активация события
    private void OnSmokingCarpAction(Entity<SmokingCarpComponent> ent, ref SmokingCarpActionEvent args)
    {
        if (!_net.IsServer)
            return;

        var actionEnt = args.Action.Owner;
        if (!TryComp<SmokingCarpActionComponent>(actionEnt, out var carpActionComp))
            return;

        if (args.Handled)
            return;

        args.Handled = true;

        _popup.PopupEntity(Loc.GetString("active-martial-ability"), ent, ent);

        ent.Comp.SelectedCombo = carpActionComp.List;
        ent.Comp.SelectedComboComp = carpActionComp;
    }
    private void OnMeleeHitEvent(Entity<SmokingCarpComponent> ent, ref MeleeHitEvent args)
    {
        if (_net.IsClient)
            return;

        if (args.HitEntities.Count <= 0)
            return;

        foreach (var hitEntity in args.HitEntities)
        {
            if (!HasComp<MobStateComponent>(hitEntity))
                continue;

            DoHitCarp(ent, hitEntity);
        }
    }
    private void DoHitCarp(Entity<SmokingCarpComponent> ent, EntityUid hitEntity)
    {
        if (ent.Comp.SelectedComboComp == null)
            return;

        var comboComp = ent.Comp.SelectedComboComp;

        switch (ent.Comp.SelectedCombo)
        {
            case SmokingCarpList.PowerPunch:
                if (_net.IsClient)
                    return;
                DamageHit(ent, hitEntity, comboComp.DamageType, comboComp.HitDamage, comboComp.IgnoreResist, out _);
                SpawnAttachedTo(comboComp.EffectPunch, Transform(hitEntity).Coordinates);
                _audio.PlayPvs(comboComp.HitSound, ent, AudioParams.Default.WithVolume(3.0f));

                var saying =
                Enumerable.ElementAt<LocId>(comboComp.PackMessageOnHit, (int)_random.Next(comboComp.PackMessageOnHit.Count));
                var ev = new SmokingCarpSaying(saying);
                RaiseLocalEvent(ent, ev);

                if (TryComp<PhysicsComponent>(hitEntity, out var physicsComponent))
                {
                    var userTransform = Transform(ent);
                    var targetTransform = Transform(hitEntity);
                    var pushDirection = targetTransform.WorldPosition - userTransform.WorldPosition;

                    if (!pushDirection.Equals(Vector2.Zero))
                    {
                        var distance = pushDirection.Length();

                        if (distance <= comboComp.MaxPushDistance)
                        {
                            pushDirection = pushDirection.Normalized();
                            var pushStrength = comboComp.PushStrength;

                            pushStrength *= 10f - distance / comboComp.MaxPushDistance;

                            var impulse = pushDirection * pushStrength;
                            _physics.ApplyLinearImpulse(hitEntity, impulse, body: physicsComponent);
                        }
                    }
                }
                break;
            case SmokingCarpList.SmokePunch:
                if (_net.IsClient)
                    return;
                DamageHit(ent, hitEntity, comboComp.DamageType, comboComp.HitDamage, comboComp.IgnoreResist, out _);
                _stamina.TakeStaminaDamage(hitEntity, comboComp.StaminaDamage);
                SpawnAttachedTo(comboComp.EffectPunch, Transform(hitEntity).Coordinates);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        ent.Comp.SelectedCombo = null;
        ent.Comp.SelectedComboComp = null;
    }
    private void SmokingCarpReflect(Entity<SmokingCarpComponent> ent, ref ReflectCarpEvent args)
    {
        if (_net.IsClient)
            return;

        if (HasComp<ReflectComponent>(ent))
        {
            _popup.PopupEntity(Loc.GetString("unreflect-smoking-carp"), ent, ent);
            RemComp<ReflectComponent>(ent);
            RemComp<PacifiedComponent>(ent);
            return;
        }

        if (args.Handled)
            return;

        args.Handled = true;

        AddComp<PacifiedComponent>(ent);
        var reflectComponent = EnsureComp<ReflectComponent>(ent);
        _popup.PopupEntity(Loc.GetString("reflect-smoking-carp"), ent, ent);
        reflectComponent.ReflectProb = 1.0f;
        reflectComponent.Spread = 360f;
    }
    private void SmokingCarpTripPunch(Entity<SmokingCarpTripPunchComponent> ent, ref SmokingCarpTripPunchEvent args)
    {
        if (_net.IsClient)
            return;

        if (args.Handled)
            return;

        args.Handled = true;

        if (!TryComp<TransformComponent>(args.Performer, out var xform))
            return;

        _receivers.Clear();

        foreach (var target in _entityLookup.GetEntitiesInRange(xform.Coordinates, ent.Comp.Range))
        {
            if (target == args.Performer)
                continue;

            if (HasComp<SmokingCarpComponent>(target))
                continue;

            if (!HasComp<MobStateComponent>(target))
                continue;

            _receivers.Add(target);
        }

        if (_net.IsServer)
            _audio.PlayPvs(ent.Comp.TripSound, args.Performer);

        foreach (var receiver in _receivers)
        {
            if (_mobState.IsDead(receiver))
                continue;

            _stun.TryParalyze(receiver, TimeSpan.FromSeconds(ent.Comp.ParalyzeTime), true);
        }

        if (_net.IsServer && ent.Comp.SelfEffect is not null)
            SpawnAttachedTo(ent.Comp.SelfEffect, Transform(args.Performer).Coordinates);
    }
}
