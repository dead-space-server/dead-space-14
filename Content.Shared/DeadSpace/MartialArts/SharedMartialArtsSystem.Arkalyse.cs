using Content.Server.DeadSpace.MartialArts;
using Content.Shared.Interaction.Events;
using Content.Shared.DeadSpace.MartialArts.Arkalyse;
using Content.Shared.Weapons.Melee;
using Content.Shared.DeadSpace.SmokingCarp;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Robust.Shared.Audio;
using Robust.Shared.Physics.Components;
using System.Linq;
using System.Numerics;
using Content.Shared.Speech.Muting;
using Robust.Shared.Timing;


namespace Content.Shared.DeadSpace.MartialArts;

public partial class SharedMartialArtsSystem
{
    private void InitializeArkalyse()
    {
        SubscribeLocalEvent<MartialArtsComponent, DamageAtackArkalyseActionEvent>(OnDamageAtack);
        SubscribeLocalEvent<MartialArtsComponent, StunedAtackArkalyseActionEvent>(OnStunAtack);
        SubscribeLocalEvent<MartialArtsComponent, MutedAtackArkalyseActionEvent>(OnMuteAtack);

        SubscribeLocalEvent<MartialArtsComponent, MeleeHitArkalyseEvent>(MeleeHitArkalyse);

        SubscribeLocalEvent<MartialArtsTrainingArkalyseComponent, UseInHandEvent>(UseBookArkalyse);
    }
    // Выдача способностей и превращение книги в пепел
    private void UseBookArkalyse(Entity<MartialArtsTrainingArkalyseComponent> ent, ref UseInHandEvent args)
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
    private void OnDamageAtack(Entity<MartialArtsComponent> ent, ref DamageAtackArkalyseActionEvent args)
    {
        OnActionActivated(ent);
        ent.Comp.TypeAtack = 1;
    }
    // Активация способности SmokePunchCarp
    private void OnStunAtack(Entity<MartialArtsComponent> ent, ref StunedAtackArkalyseActionEvent args)
    {
        OnActionActivated(ent);
        ent.Comp.TypeAtack = 2;
    }
    // Активация способности SmokePunchCarp
    private void OnMuteAtack(Entity<MartialArtsComponent> ent, ref MutedAtackArkalyseActionEvent args)
    {
        OnActionActivated(ent);
        ent.Comp.TypeAtack = 3;
    }
    // Реализация приемов 1, 2, 3
    private void MeleeHitArkalyse(Entity<MartialArtsComponent> ent, ref MeleeHitArkalyseEvent args)
    {
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

                            _audio.PlayPvs(protoTechTwo.HitSound, args.User, AudioParams.Default.WithVolume(0.5f));
                            _stun.TryParalyze(target, TimeSpan.FromSeconds(protoTechTwo.ParalyzeTime), true);
                            SpawnAttachedTo(protoTechTwo.EffectPunch, Transform(target).Coordinates);
                            ent.Comp.IsDamageAttack = false;
                            break;
                        case 3:
                            if (!_proto.TryIndex(ent.Comp.TechDataThree, out var protoTechThree))
                                return;

                            TryComp<MutedComponent>(target, out _);
                            Timer.Spawn(TimeSpan.FromSeconds(protoTechThree.ParalyzeTime), () => { if (Exists(target)) RemComp<MutedComponent>(target); });

                            DamageHit(ent, target, protoTechThree.DamageType, protoTechThree.HitDamage, out _);
                            _stamina.TakeStaminaDamage(target, protoTechThree.StaminaDamage);
                            break;
                    }
                }
            }
        }
    }
}
