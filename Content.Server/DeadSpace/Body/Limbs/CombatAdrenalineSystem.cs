// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Body.Limbs;

public sealed class CombatAdrenalineSystem : EntitySystem
{
    private static readonly EntProtoId SurgeEffect = "StatusEffectCombatAdrenaline";

    [Dependency] private readonly LimbWoundSystem _limbs = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LimbBodyComponent, DamageChangedEvent>(OnDamage, after: [typeof(MobThresholdSystem)]);
        SubscribeLocalEvent<CombatAdrenalineSurgeComponent, StatusEffectRemovedEvent>(OnSurgeEnded);
    }

    private void OnDamage(Entity<LimbBodyComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta == null)
            return;

        _limbs.NoteDamage(ent.Owner, ent.Comp, args);
        RefundLimb(ent, args.DamageDelta);

        var positive = 0f;
        foreach (var value in args.DamageDelta.DamageDict.Values)
        {
            if (value > 0)
                positive += value.Float();
        }

        var surging = Surging(ent);
        if (CombatAdrenalineRules.TryRefresh(surging, positive))
            _status.TryUpdateStatusEffectDuration(ent, SurgeEffect, TimeSpan.FromSeconds(CombatAdrenalineRules.SurgeSeconds));
        else
            TrySurge(ent, positive);

        if (Surging(ent))
            KeepStanding(ent);
    }

    private void RefundLimb(Entity<LimbBodyComponent> ent, DamageSpecifier delta)
    {
        if (ent.Comp.PendingRefund == null)
            return;

        var heal = new DamageSpecifier();
        foreach (var (type, value) in ent.Comp.PendingRefund.DamageDict)
            heal.DamageDict[type] = -value;

        ent.Comp.PendingRefund = null;
        foreach (var value in delta.DamageDict.Values)
        {
            if (value <= 0)
                continue;

            _damage.TryChangeDamage(ent.Owner, heal, interruptsDoAfters: false);
            return;
        }
    }

    public bool TrySurge(EntityUid uid, float positiveDamage, LimbBodyComponent? limbs = null)
    {
        if (!Resolve(uid, ref limbs, false))
            return false;

        if (!CombatAdrenalineRules.TryStart(
                HasComp<CombatAdrenalineImplantComponent>(uid),
                positiveDamage,
                Surging(uid),
                _combat.IsInCombatMode(uid)))
        {
            return false;
        }

        if (!_status.TrySetStatusEffectDuration(uid, SurgeEffect, TimeSpan.FromSeconds(CombatAdrenalineRules.SurgeSeconds)))
            return false;

        KeepStanding(uid);
        return true;
    }

    private void OnSurgeEnded(Entity<CombatAdrenalineSurgeComponent> ent, ref StatusEffectRemovedEvent args)
    {
        var target = args.Target;
        if (!TryComp<MobStateComponent>(target, out var state))
            return;

        if (state.CurrentState == MobState.PreCritical)
        {
            EnsureComp<WormComponent>(target);
            return;
        }

        if (_mobs.IsCritical(target, state))
            _standing.Down(target, playSound: false, dropHeldItems: true, force: true);
    }

    private void KeepStanding(EntityUid uid)
    {
        if (!CombatAdrenalineRules.CanStayUp(LegSevered(uid, LimbZone.LegLeft), LegSevered(uid, LimbZone.LegRight)))
        {
            _standing.Down(uid, playSound: false, dropHeldItems: false, force: true);
            return;
        }

        RemComp<WormComponent>(uid);
        RemComp<KnockedDownComponent>(uid);
        _standing.Stand(uid, force: true);
    }

    private bool LegSevered(EntityUid uid, LimbZone zone)
    {
        if (!TryComp<LimbBodyComponent>(uid, out var limbs))
            return false;

        var index = LimbWoundRules.Index(zone);
        return index < limbs.Zones.Count && limbs.Zones[index].Severed;
    }

    private bool Surging(EntityUid uid)
    {
        return _status.TryGetStatusEffect(uid, SurgeEffect, out _);
    }
}
