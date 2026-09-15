// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Physics.Components;
using Content.Shared.Damage.Systems;
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
using Robust.Shared.Prototypes;
using Content.Server.DeadSpace.MartialArts.CQC.Components;
using Content.Shared.DeadSpace.MartialArts.CQC;
using Content.Shared.Actions;
using Content.Shared.StatusEffectNew;
using Content.Shared.DeadSpace.MartialArts.CQC.Components;

namespace Content.Server.DeadSpace.MartialArts.CQC;

public sealed class CQCSystem : CQCSharedSystem
{
    private static readonly EntProtoId MutedEffect = "StatusEffectCQCMuted";

    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    private readonly HashSet<EntityUid> _receivers = new();
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CQCComponent, CQCPowerPunchEvent>(OnPowerPunchAction);
        SubscribeLocalEvent<CQCComponent, CQCMuteEvent>(OnMutePunchAction);
        SubscribeLocalEvent<CQCComponent, CQCRelaxEvent>(OnRelaxAction);
        SubscribeLocalEvent<CQCComponent, MeleeHitEvent>(OnMeleeHitEvent);
        SubscribeLocalEvent<CQCComponent, CQCConcentrationEvent>(CQCConcentration);
        SubscribeLocalEvent<CQCStepPunchComponent, CQCStepPunchEvent>(CQCStepPunch);
    }

    private void SelectCombo(Entity<CQCComponent> ent, CQCList combo)
    {
        ent.Comp.SelectedCombo = combo;
        _popup.PopupEntity(Loc.GetString("active-martial-ability"), ent, ent);
    }

    private void OnPowerPunchAction(Entity<CQCComponent> ent, ref CQCPowerPunchEvent args)
    {
        if (args.Handled)
            return;

        SelectCombo(ent, CQCList.PowerPunch);

        args.Handled = true;
    }

    private void OnMutePunchAction(Entity<CQCComponent> ent, ref CQCMuteEvent args)
    {
        if (args.Handled)
            return;

        SelectCombo(ent, CQCList.MuteAttack);

        args.Handled = true;
    }

    private void OnRelaxAction(Entity<CQCComponent> ent, ref CQCRelaxEvent args)
    {
        if (args.Handled)
            return;

        ent.Comp.SelectedCombo = null;
        _popup.PopupEntity(Loc.GetString("relax-martial-ability"), ent, ent);

        args.Handled = true;
    }

    private void OnMeleeHitEvent(Entity<CQCComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.HitEntities.Any())
            return;

        foreach (var hitEntity in args.HitEntities)
        {
            if (!HasComp<MobStateComponent>(hitEntity))
                continue;

            DoCQCHit(ent, hitEntity);
        }
    }
    private void DoCQCHit(Entity<CQCComponent> ent, EntityUid hitEntity)
    {
        if (ent.Comp.SelectedCombo is not { } combo)
            return;

        switch (combo)
        {
            case CQCList.PowerPunch:
                DamageHit(hitEntity, ent.Comp.Params.DamageTypeForPowerPunch, ent.Comp.Params.HitDamageForPowerPunch, ent.Comp.Params.IgnoreResist, out _);
                SpawnAttachedTo(ent.Comp.Params.EffectPowerPunch, Transform(hitEntity).Coordinates);
                _audio.PlayPvs(ent.Comp.Params.HitSoundForPowerPunch, ent, AudioParams.Default.WithVolume(3.0f));

                if (ent.Comp.Params.PackMessageOnHit is { Count: > 0 } pack)
                {
                    var saying = pack[_random.Next(pack.Count)];
                    var ev = new CQCSaying(saying);
                    RaiseLocalEvent(ent, ev);
                }

                OnPowerPunch(ent, hitEntity, ent.Comp.Params.MaxPushDistance, ent.Comp.Params.PushStrength);
                break;

            case CQCList.MuteAttack:
                _statusEffects.TrySetStatusEffectDuration(hitEntity, MutedEffect, ent.Comp.Params.ParalyzeTimeMuteAtack);
                DamageHit(hitEntity, ent.Comp.Params.DamageTypeForMuteAtack, ent.Comp.Params.HitDamageForMuteAtack, ent.Comp.Params.IgnoreResist, out _);
                _stamina.TakeStaminaDamage(hitEntity, ent.Comp.Params.StaminaDamageMuteAtack);
                break;
        }
        ent.Comp.SelectedCombo = null;
    }

    private void CQCConcentration(Entity<CQCComponent> ent, ref CQCConcentrationEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.Concentrated)
        {
            RemComp<CQCCantUseWeaponComponent>(ent);
            ent.Comp.SelectedCombo = null;

            foreach (var actionEnt in ent.Comp.BaseCQCActionEntities)
            {
                _action.RemoveAction(ent.Owner, actionEnt);
                QueueDel(actionEnt);
            }

            ent.Comp.BaseCQCActionEntities.Clear();
        }
        else
        {
            EnsureComp<CQCCantUseWeaponComponent>(ent);
            foreach (var action in ent.Comp.BaseCQC)
            {
                EntityUid? actionEnt = null;
                _action.AddAction(ent.Owner, ref actionEnt, action);

                if (actionEnt != null)
                    ent.Comp.BaseCQCActionEntities.Add(actionEnt.Value);
            }
        }
        ent.Comp.Concentrated = !ent.Comp.Concentrated;
        _action.SetToggled(ent.Comp.CQCConcentrationActionEntity, ent.Comp.Concentrated);
    }

    private void CQCStepPunch(Entity<CQCStepPunchComponent> ent, ref CQCStepPunchEvent args)
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

            if (HasComp<CQCComponent>(target))
                continue;

            if (!HasComp<MobStateComponent>(target))
                continue;

            _receivers.Add(target);
        }
        _audio.PlayPvs(ent.Comp.StepSound, args.Performer);

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
