using Content.Shared.Actions;
using Content.Server.DeadSpace.SmokingCarp.Abilities.Components;
using Content.Shared.Weapons.Melee.Events;
using System.Linq;
using Content.Shared.Damage;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics.Components;
using System.Numerics;
using Content.Shared.DeadSpace.SmokingCarp;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Content.Shared.Popups;
using Content.Server.Chat.Systems;
using Robust.Shared.Random;
using JetBrains.FormatRipper.Elf;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.SmokingCarp.Abilities;

public sealed class ArkalyseDamageSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actionSystem = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PowerPunchComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<PowerPunchComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<PowerPunchComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<PowerPunchComponent, PowerPunchCarpEvent>(OnActionActivated);
    }
    private void OnComponentInit(EntityUid uid, PowerPunchComponent component, ComponentInit args)
    {
        _actionSystem.AddAction(uid, ref component.ActionPowerPunchCarpAttackEntity, component.ActionPowerPunchCarpAttack, uid);
    }
    private void OnComponentShutdown(EntityUid uid, PowerPunchComponent component, ComponentShutdown args)
    {
        _actionSystem.RemoveAction(uid, component.ActionPowerPunchCarpAttackEntity);
    }
    private void OnActionActivated(EntityUid uid, PowerPunchComponent component, PowerPunchCarpEvent args)
    {
        if (args.Handled)
            return;

        if (component.IsDamageAttack)
            _popup.PopupEntity(Loc.GetString("non-active-smoking-carp"), uid, uid);

        if (!component.IsDamageAttack)
            _popup.PopupEntity(Loc.GetString("active-smoking-carp"), uid, uid);

        component.IsDamageAttack = !component.IsDamageAttack;

        args.Handled = true;
    }
    private void OnMeleeHit(EntityUid uid, PowerPunchComponent component, MeleeHitEvent args)
    {
        if (component.IsDamageAttack && args.HitEntities.Any())
        {
            foreach (var target in args.HitEntities)
            {
                if (args.User == target)
                    continue;

                if (!TryComp<MobStateComponent>(target, out var mobState))
                    continue;

                if (mobState.CurrentState != MobState.Dead)
                {
                    _damageable.TryChangeDamage(target, component.Damage, true, false);
                    SpawnAttachedTo(component.EffectPunch, Transform(target).Coordinates);
                    _audio.PlayPvs(component.HitSound, args.User, AudioParams.Default.WithVolume(3.0f));

                    if (!_prototypeManager.TryIndex(component.PackMessage, out var messagePack))
                        return;

                    var message = Loc.GetString(_random.Pick(messagePack.Values));
                    _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Speak, true);
                }
                if (TryComp<PhysicsComponent>(target, out var physicsComponent))
                {
                    var userTransform = Transform(args.User);
                    var targetTransform = Transform(target);
                    var pushDirection = targetTransform.WorldPosition - userTransform.WorldPosition;

                    if (!pushDirection.Equals(Vector2.Zero))
                    {
                        var distance = pushDirection.Length();

                        if (distance <= component.MaxPushDistance)
                        {
                            pushDirection = pushDirection.Normalized();
                            var pushStrength = component.PushStrength;

                            if (component.UseDistanceScaling)
                            {
                                pushStrength *= 10f - (distance / component.MaxPushDistance);
                            }
                            var impulse = pushDirection * pushStrength;
                            _physics.ApplyLinearImpulse(target, impulse, body: physicsComponent);
                        }
                    }
                }
                component.IsDamageAttack = false;
            }
        }
    }
}
