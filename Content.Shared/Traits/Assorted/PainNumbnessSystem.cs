using Content.Shared.DeadSpace.Damage.Components; // DS14
using Content.Shared.Damage.Events;
using Content.Shared.FixedPoint; // DS14
using Content.Shared.Mobs; // DS14
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems; // DS14
using Content.Shared.StatusEffectNew;
// DS14-start
using Content.Shared.Alert;
using Robust.Shared.Prototypes;
// DS14-end

namespace Content.Shared.Traits.Assorted;

public sealed partial class PainNumbnessSystem : EntitySystem
{
    [Dependency] private MobThresholdSystem _mobThresholdSystem = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeedModifier = default!; // DS14

    // DS14-start
    [Dependency] private AlertsSystem _alerts = default!;
    private static readonly ProtoId<AlertCategoryPrototype> HealthAlertCategory = "Health";
    // DS14-end

    public override void Initialize()
    {
        SubscribeLocalEvent<PainNumbnessStatusEffectComponent, StatusEffectAppliedEvent>(OnEffectApplied);
        SubscribeLocalEvent<PainNumbnessStatusEffectComponent, StatusEffectRemovedEvent>(OnEffectRemoved);
        SubscribeLocalEvent<PainNumbnessStatusEffectComponent, StatusEffectRelayedEvent<BeforeForceSayEvent>>(OnChangeForceSay);
        SubscribeLocalEvent<PainNumbnessStatusEffectComponent, StatusEffectRelayedEvent<BeforeAlertSeverityCheckEvent>>(OnAlertSeverityCheck);
    }

    private void OnEffectApplied(Entity<PainNumbnessStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (!HasComp<MobThresholdsComponent>(args.Target))
            return;

        // DS14-Start
        EnsureComp<PainNumbnessSlowImmunityComponent>(args.Target);
        _movementSpeedModifier.RefreshMovementSpeedModifiers(args.Target);

        SaveAndOverrideMobThresholds(args.Target);
// DS14-End
        _mobThresholdSystem.VerifyThresholds(args.Target);
    }

    private void OnEffectRemoved(Entity<PainNumbnessStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (!HasComp<MobThresholdsComponent>(args.Target))
            return;

        // DS14-Start
        RemComp<PainNumbnessSlowImmunityComponent>(args.Target);
        _movementSpeedModifier.RefreshMovementSpeedModifiers(args.Target);

        RestoreMobThresholds(args.Target);
		// DS14-End
        _mobThresholdSystem.VerifyThresholds(args.Target);
    }

    // DS14-Start
	private void SaveAndOverrideMobThresholds(EntityUid target)
    {
        if (HasComp<PainNumbnessMobThresholdComponent>(target))
            return;

        if (!TryComp<MobThresholdsComponent>(target, out var thresholds))
            return;

        var saved = new PainNumbnessMobThresholdComponent();
        foreach (var (damage, state) in thresholds.Thresholds)
        {
            saved.OriginalThresholds[damage] = state;
        }
        AddComp(target, saved);

        var newThresholds = new SortedDictionary<FixedPoint2, MobState>();

        foreach (var (damage, state) in thresholds.Thresholds)
        {
            if (state == MobState.PreCritical)
                continue;

            if (state == MobState.Critical)
            {
                var preCritThreshold = FixedPoint2.Zero;
                foreach (var (d, s) in saved.OriginalThresholds)
                {
                    if (s == MobState.PreCritical)
                    {
                        preCritThreshold = d;
                        break;
                    }
                }

                if (preCritThreshold > FixedPoint2.Zero)
                    newThresholds[90] = MobState.Critical;
                else
                    newThresholds[damage - 20] = MobState.Critical;
                continue;
            }

            if (state == MobState.Dead)
            {
                newThresholds[damage - 30] = MobState.Dead;
                continue;
            }

            newThresholds[damage] = state;
        }

        thresholds.Thresholds = newThresholds;
        Dirty(target, thresholds);
    }

    private void RestoreMobThresholds(EntityUid target)
    {
        if (!TryComp<PainNumbnessMobThresholdComponent>(target, out var saved))
            return;

        if (TryComp<MobThresholdsComponent>(target, out var thresholds))
        {
            thresholds.Thresholds = saved.OriginalThresholds;
            Dirty(target, thresholds);
        }

        RemComp<PainNumbnessMobThresholdComponent>(target);
    }
	// DS14-End
	
    private void OnChangeForceSay(Entity<PainNumbnessStatusEffectComponent> ent, ref StatusEffectRelayedEvent<BeforeForceSayEvent> args)
    {
        if (ent.Comp.ForceSayNumbDataset != null)
            args.Args.Prefix = ent.Comp.ForceSayNumbDataset.Value;
    }

    private void OnAlertSeverityCheck(Entity<PainNumbnessStatusEffectComponent> ent, ref StatusEffectRelayedEvent<BeforeAlertSeverityCheckEvent> args)
    {
        if (_alerts.TryGet(args.Args.CurrentAlert, out var alert) && alert.Category == HealthAlertCategory) // DS14
            args.Args.CancelUpdate = true;
    }
}
