using System.Linq;
using Content.Server.DeadSpace.MartialArts;
using Content.Shared.DeadSpace.SmokingCarp;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Physics.Components;
using System.Numerics;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Weapons.Reflect;

namespace Content.Shared.DeadSpace.MartialArts;

public partial class SharedMartialArtsSystem
{
    private void InitializeSmokingCarp()
    {
        SubscribeLocalEvent<MartialArtsComponent, PowerPunchCarpEvent>(SmokingCarpPowerPunch);
        SubscribeLocalEvent<MartialArtsComponent, SmokePunchCarpEvent>(SmokingCarpSmokePunch);
        SubscribeLocalEvent<MartialArtsComponent, MeleeHitSmokingCarpEvent>(MeleeHitSmokingCarp);
        SubscribeLocalEvent<MartialArtsComponent, TripPunchCarpEvent>(SmokingCarpTripPunch);
        SubscribeLocalEvent<MartialArtsComponent, ReflectCarpEvent>(SmokingCarpReflect);

        SubscribeLocalEvent<MartialArtsTrainingCarpComponent, UseInHandEvent>(UseInjectorSmokingCarp);
    }
    // Используем в руках инжектор, для получения умений + базовых показателей сущности
    private void UseInjectorSmokingCarp(Entity<MartialArtsTrainingCarpComponent> ent, ref UseInHandEvent args)
    {
        if (!_net.IsServer)
            return;

        if (args.Handled)
            return;

        var actions = new[]
        {
            ent.Comp.ActionAtackOne,
            ent.Comp.ActionAtackTwo,
            ent.Comp.ActionAtackThree,
            ent.Comp.ActionAtackFour
        };

        foreach (var action in actions)
        {
            AddAbility(args.User, action, args.User);
        }

        TransformToItem(ent, ent.Comp.ItemAfterLerning);

        var userMartialArts = EnsureComp<MartialArtsComponent>(args.User);
        userMartialArts.MartialArtsForm = ent.Comp.MartialArtsForm;
        var meleeWeaponComponent = EnsureComp<MeleeWeaponComponent>(args.User);
        meleeWeaponComponent.AttackRate = ent.Comp.AddAtackRate;

        args.Handled = true;
    }
    // Активация способности PowerPunchCarp
    private void SmokingCarpPowerPunch(Entity<MartialArtsComponent> ent, ref PowerPunchCarpEvent args)
    {
        OnActionActivated(ent);
        ent.Comp.TypeAtack = 1;
    }
    // Активация способности SmokePunchCarp
    private void SmokingCarpSmokePunch(Entity<MartialArtsComponent> ent, ref SmokePunchCarpEvent args)
    {
        OnActionActivated(ent);
        ent.Comp.TypeAtack = 2;
    }
    // Логика ударов, смена ударов реализована благодаря свитчу
    private void MeleeHitSmokingCarp(Entity<MartialArtsComponent> ent, ref MeleeHitSmokingCarpEvent args)
    {
        if (!_net.IsServer)
            return;

        SelectEnterTechique(ent); // Вызываем метод и присваиеваем приемам их ID прототипов

        if (ent.Comp.IsDamageAttack && args.HitEntities.Any())
        {
            foreach (var target in args.HitEntities)
            {
                if (args.User == target)
                    continue;

                if (!TryComp<MobStateComponent>(target, out var mobState))
                    continue;

                if (mobState.CurrentState != MobState.Dead)
                {
                    switch (ent.Comp.TypeAtack)
                    {
                        case 1:
                            if (!_proto.TryIndex(ent.Comp.TechDataOne, out var protoTechOne))
                                return;

                            DamageHit(ent, target, protoTechOne.DamageType, protoTechOne.HitDamage, out _);
                            SpawnAttachedTo(protoTechOne.EffectPunch, Transform(target).Coordinates);
                            _audio.PlayPvs(protoTechOne.HitSound, args.User, AudioParams.Default.WithVolume(3.0f));

                            var ev = new SmokingCarpSaying(protoTechOne.PackMessageOnHit);
                            RaiseLocalEvent(ent, ev);
                            if (TryComp<PhysicsComponent>(target, out var physicsComponent))
                            {
                                var userTransform = Transform(args.User);
                                var targetTransform = Transform(target);
                                var pushDirection = targetTransform.WorldPosition - userTransform.WorldPosition;

                                if (!pushDirection.Equals(Vector2.Zero))
                                {
                                    var distance = pushDirection.Length();

                                    if (distance <= protoTechOne.MaxPushDistance)
                                    {
                                        pushDirection = pushDirection.Normalized();
                                        var pushStrength = protoTechOne.PushStrength;

                                        pushStrength *= 10f - distance / protoTechOne.MaxPushDistance;

                                        var impulse = pushDirection * pushStrength;
                                        _physics.ApplyLinearImpulse(target, impulse, body: physicsComponent);
                                    }
                                }
                            }
                            ent.Comp.IsDamageAttack = false;
                            break;
                        case 2:
                            if (!_proto.TryIndex(ent.Comp.TechDataTwo, out var protoTechTwo))
                                return;

                            DamageHit(ent, target, protoTechTwo.DamageType, protoTechTwo.HitDamage, out _);
                            _stamina.TakeStaminaDamage(target, protoTechTwo.StaminaDamage);
                            SpawnAttachedTo(protoTechTwo.EffectPunch, Transform(target).Coordinates);
                            ent.Comp.IsDamageAttack = false;
                            break;
                    }
                }
            }
        }
    }
    // Стан по площади
    private void SmokingCarpTripPunch(Entity<MartialArtsComponent> ent, ref TripPunchCarpEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.TechDataThree, out var protoTechThree))
            return;

        if (!TryComp<TransformComponent>(ent, out var xform) || _mobState.IsDead(ent))
            return;

        args.Handled = true;

        _receivers.Clear();

        foreach (var target in _entityLookup.GetEntitiesInRange(xform.Coordinates, protoTechThree.Range))
        {
            if (protoTechThree.MartialArtsForm == MartialArtsForms.SmokingCarp)
                continue;

            if (!HasComp<MobStateComponent>(target))
                continue;

            _receivers.Add(target);
        }

        if (_net.IsServer)
            _audio.PlayPvs(protoTechThree.HitSound, ent);

        foreach (var receiver in _receivers)
        {
            if (_mobState.IsDead(receiver))
                continue;

            _stun.TryParalyze(receiver, TimeSpan.FromSeconds(protoTechThree.ParalyzeTime), true);
        }

        if (_net.IsServer && protoTechThree.SelfEffect is not null)
            SpawnAttachedTo(protoTechThree.SelfEffect, Transform(ent).Coordinates);
    }
    private void SmokingCarpReflect(Entity<MartialArtsComponent> ent, ref ReflectCarpEvent args)
    {
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
}
