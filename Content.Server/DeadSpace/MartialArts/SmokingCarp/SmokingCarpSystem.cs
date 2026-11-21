// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Server.DeadSpace.MartialArts.SmokingCarp.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Physics.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Weapons.Reflect;
using Content.Shared.DeadSpace.MartialArts.SmokingCarp;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Content.Server.Damage.Systems;
using System.Linq;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Server.GameObjects;
using System.Numerics;

namespace Content.Server.DeadSpace.MartialArts.SmokingCarp;

public sealed partial class SmokingCarpSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StaminaSystem _stamina = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private TransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _receivers = new();
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SmokingCarpComponent, SmokingCarpPowerPunchEvent>(OnPowerPunchAction);
        SubscribeLocalEvent<SmokingCarpComponent, SmokingCarpSmokePunchEvent>(OnSmokePunchAction);
        SubscribeLocalEvent<SmokingCarpComponent, MeleeHitEvent>(OnMeleeHitEvent);
        SubscribeLocalEvent<SmokingCarpComponent, ReflectCarpEvent>(SmokingCarpReflect);
        SubscribeLocalEvent<SmokingCarpTripPunchComponent, SmokingCarpTripPunchEvent>(SmokingCarpTripPunch);
    }

    private void SelectCombo(Entity<SmokingCarpComponent> ent, SmokingCarpList combo)
    {
        ent.Comp.SelectedCombo = combo;
        _popup.PopupEntity(Loc.GetString("active-martial-ability"), ent, ent);
    }

    private void OnPowerPunchAction(Entity<SmokingCarpComponent> ent, ref SmokingCarpPowerPunchEvent args)
    {
        if (args.Handled)
            return;

        SelectCombo(ent, SmokingCarpList.PowerPunch);

        args.Handled = true;
    }

    private void OnSmokePunchAction(Entity<SmokingCarpComponent> ent, ref SmokingCarpSmokePunchEvent args)
    {
        if (args.Handled)
            return;

        SelectCombo(ent, SmokingCarpList.SmokePunch);

        args.Handled = true;
    }

    private void OnMeleeHitEvent(Entity<SmokingCarpComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.HitEntities.Any())
            return;

        foreach (var hitEntity in args.HitEntities)
        {
            if (!HasComp<MobStateComponent>(hitEntity))
                continue;

            DoHitCarp(ent, hitEntity);
        }
    }
    private void DoHitCarp(Entity<SmokingCarpComponent> ent, EntityUid hitEntity)    {
        if (ent.Comp.SelectedCombo is not { } combo)
            return;

        switch (combo)
        {
            case SmokingCarpList.PowerPunch:
                DamageHit(hitEntity, ent.Comp.Params.DamageTypeForPowerPunch, ent.Comp.Params.HitDamageForPowerPunch, ent.Comp.Params.IgnoreResist, out _);
                SpawnAttachedTo(ent.Comp.Params.EffectPowerPunch, Transform(hitEntity).Coordinates);
                _audio.PlayPvs(ent.Comp.Params.HitSoundForPowerPunch, ent, AudioParams.Default.WithVolume(3.0f));

                if (ent.Comp.Params.PackMessageOnHit is { Count: > 0 } pack)
                {
                    var saying = pack[_random.Next(pack.Count)];
                    var ev = new SmokingCarpSaying(saying);
                    RaiseLocalEvent(ent, ev);
                }

                OnPowerPunch(ent, hitEntity, ent.Comp.Params.MaxPushDistance, ent.Comp.Params.PushStrength);
                break;

            case SmokingCarpList.SmokePunch:
                DamageHit(hitEntity, ent.Comp.Params.DamageTypeForSmokePunch, ent.Comp.Params.HitDamageForSmokePunch, ent.Comp.Params.IgnoreResist, out _);
                _stamina.TakeStaminaDamage(hitEntity, ent.Comp.Params.StaminaDamageSmokePunch);
                _audio.PlayPvs(ent.Comp.Params.HitSoundForSmokePunch, ent, AudioParams.Default.WithVolume(3.0f));
                SpawnAttachedTo(ent.Comp.Params.EffectSmokePunch, Transform(hitEntity).Coordinates);
                break;
        }
        ent.Comp.SelectedCombo = null;
    }

    private void SmokingCarpReflect(Entity<SmokingCarpComponent> ent, ref ReflectCarpEvent args)
    {
        if (HasComp<ReflectComponent>(ent))
        {
            _popup.PopupEntity(Loc.GetString("unreflect-smoking-carp"), ent, ent);
            RemComp<ReflectComponent>(ent);

            if (HasComp<SmokingCarpPacifiedComponent>(ent))
            {
                RemComp<PacifiedComponent>(ent);
                RemComp<SmokingCarpPacifiedComponent>(ent);
            }
            return;
        }

        if (args.Handled)
            return;

        args.Handled = true;

        if (!HasComp<PacifiedComponent>(ent))
        {
            AddComp<PacifiedComponent>(ent);
            AddComp<SmokingCarpPacifiedComponent>(ent);
        }

        var reflectComponent = EnsureComp<ReflectComponent>(ent);
        _popup.PopupEntity(Loc.GetString("reflect-smoking-carp"), ent, ent);
        reflectComponent.ReflectProb = 1.0f;
        reflectComponent.Spread = 360f;
    }

    private void SmokingCarpTripPunch(Entity<SmokingCarpTripPunchComponent> ent, ref SmokingCarpTripPunchEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var xform = Transform(args.Performer);

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
        _audio.PlayPvs(ent.Comp.TripSound, args.Performer);

        foreach (var receiver in _receivers)
        {
            if (_mobState.IsDead(receiver))
                continue;

            _stun.TryUpdateParalyzeDuration(receiver, TimeSpan.FromSeconds(ent.Comp.ParalyzeTime));
        }

        if (ent.Comp.SelfEffect is not null)
            SpawnAttachedTo(ent.Comp.SelfEffect, Transform(args.Performer).Coordinates);
    }

    private void DamageHit(EntityUid target,
    string damageType,
    int damageAmount,
    bool ignoreResist,
    out DamageSpecifier damage)
    {
        damage = new DamageSpecifier();
        damage.DamageDict.Add(damageType, damageAmount);

        _damageable.TryChangeDamage(target, damage, ignoreResist);
    }

    private void OnPowerPunch(EntityUid user, EntityUid hitEnt, float maxPushDistance, float pushStrength)
    {
        if (!TryComp<PhysicsComponent>(hitEnt, out var physicsComponent))
            return;

        var userPos = _transform.GetWorldPosition(Transform(user));
        var targetPos = _transform.GetWorldPosition(Transform(hitEnt));
        var pushDirection = targetPos - userPos;

        var distSq = pushDirection.LengthSquared();

        var distance = MathF.Sqrt(distSq);
        if (distance > maxPushDistance)
            return;

        if (distance < 0.001f)
            return;

        var dir = pushDirection / distance;

        var t = 1f - distance / maxPushDistance;
        var pushFactor = MathF.Max(t, 0f);
        pushStrength = pushStrength * pushFactor;

        if (pushStrength <= 0f)
            return;

        var impulse = dir * pushStrength;
        _physics.ApplyLinearImpulse(hitEnt, impulse, body: physicsComponent);
    }
}
