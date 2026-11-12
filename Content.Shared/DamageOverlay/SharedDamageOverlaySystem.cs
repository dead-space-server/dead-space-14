using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.TheCircle.Dreadnought; // DS14
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;

namespace Content.Shared.DamageOverlay;

/// <summary>
/// A system that updates the damage overlay when a player's damage changes.
/// </summary>
public abstract partial class SharedDamageOverlaySystem : EntitySystem
{
    [Dependency] private MobThresholdSystem _mobThresholdSystem = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    [Dependency] private EntityQuery<InjurableComponent> _injurableQuery = default!;
    [Dependency] private EntityQuery<DamageableComponent> _damageableQuery = default!;
    [Dependency] private EntityQuery<MobStateComponent> _mobStateQuery = default!;
    [Dependency] private EntityQuery<MobThresholdsComponent> _thresholdsQuery = default!;

    [SubscribeLocalEvent]
    protected virtual void OnStartup(Entity<DamageOverlayComponent> entity, ref ComponentStartup args)
    {
        EnsureOverlay(entity);
        UpdateOverlays(entity);
    }

    [SubscribeLocalEvent]
    private void OnMobStateChanged(Entity<DamageOverlayComponent> entity, ref MobStateChangedEvent args)
    {
        UpdateOverlays(entity, args.Component);
    }

    [SubscribeLocalEvent]
    private void OnThresholdCheck(Entity<DamageOverlayComponent> entity, ref MobThresholdChecked args)
    {
        UpdateOverlays(entity, args.MobState, args.Damageable, args.Threshold);
    }

    protected void ClearOverlay(Entity<DamageOverlayComponent> entity)
    {
        entity.Comp.CurrentState = MobState.Alive;
        entity.Comp.DeadLevel = 0f;
        entity.Comp.CritLevel = 0f;
        entity.Comp.PainLevel = 0f;
        entity.Comp.OxygenLevel = 0f;
        entity.Comp.PreCriticalLevel = 0f; // DS14

        DirtyField(entity, entity.Comp, nameof(DamageOverlayComponent.CurrentState));
        DirtyField(entity, entity.Comp, nameof(DamageOverlayComponent.PainLevel));
        DirtyField(entity, entity.Comp, nameof(DamageOverlayComponent.OxygenLevel));
        DirtyField(entity, entity.Comp, nameof(DamageOverlayComponent.CritLevel));
        DirtyField(entity, entity.Comp, nameof(DamageOverlayComponent.DeadLevel));
        DirtyField(entity, entity.Comp, nameof(DamageOverlayComponent.PreCriticalLevel)); // DS14
    }

    // DS14-start
    /// <summary>
    /// Recalculates the overlay values of an entity, e.g. after a DS14 effect that changes them.
    /// </summary>
    public void RefreshOverlay(EntityUid uid)
    {
        if (TryComp<DamageOverlayComponent>(uid, out var comp))
            UpdateOverlays((uid, comp));
    }
    // DS14-end

    //TODO: Jezi: adjust oxygen and hp overlays to use appropriate systems once bodysim is implemented
    protected void UpdateOverlays(Entity<DamageOverlayComponent> entity,
        MobStateComponent? mobState = null,
        DamageableComponent? damageable = null,
        MobThresholdsComponent? thresholds = null,
        InjurableComponent? injurable = null)
    {
        // DS14-start
        if (HasComp<DreadnoughtLastStandActiveComponent>(entity))
        {
            ClearOverlay(entity);
            return;
        }
        // DS14-end

        if (mobState == null && !_mobStateQuery.TryComp(entity, out mobState) ||
            thresholds == null && !_thresholdsQuery.TryComp(entity, out thresholds) ||
            damageable == null && !_damageableQuery.TryComp(entity, out damageable) ||
            injurable == null && !_injurableQuery.TryComp(entity, out injurable))
            return;

        if (!_mobThresholdSystem.TryGetIncapThreshold(entity, out var foundThreshold, thresholds))
            return; //this entity cannot die or crit!!

        if (!thresholds.ShowOverlays)
        {
            ClearOverlay(entity);
            return; //this entity intentionally has no overlays
        }

        var damagePerGroup = _damageable.GetDamagePerGroup((entity, damageable));
        var critThreshold = foundThreshold.Value;
        entity.Comp.CurrentState = mobState.CurrentState;

        switch (mobState.CurrentState)
        {
            case MobState.Alive:
            case MobState.PreCritical: // DS14
            {
                FixedPoint2 painLevel = 0;
                entity.Comp.PainLevel = 0;

                if (!_statusEffects.TryEffectsWithComp<PainNumbnessStatusEffectComponent>(entity, out _))
                {
                    foreach (var painDamageType in injurable.PainDamageGroups)
                    {
                        damagePerGroup.TryGetValue(painDamageType, out var painDamage);
                        painLevel += painDamage;
                    }

                    entity.Comp.PainLevel = FixedPoint2.Min(1f, painLevel / critThreshold).Float();

                    if (entity.Comp.PainLevel < 0.05f) // Don't show damage overlay if they're near enough to max.
                    {
                        entity.Comp.PainLevel = 0;
                    }
                }

                entity.Comp.OxygenLevel = 0f; // DS14
                if (damagePerGroup.TryGetValue("Airloss", out var oxyDamage))
                {
                    entity.Comp.OxygenLevel = FixedPoint2.Min(1f, oxyDamage / critThreshold).Float();
                }

                // DS14-start
                entity.Comp.PreCriticalLevel = mobState.CurrentState == MobState.PreCritical
                    ? GetStateProgress(entity, MobState.PreCritical, MobState.Critical, _damageable.GetTotalDamage((entity, damageable)), thresholds)
                    : 0f;
                DirtyField(entity, entity.Comp, nameof(DamageOverlayComponent.PreCriticalLevel));
                // DS14-end

                entity.Comp.CritLevel = 0;
                entity.Comp.DeadLevel = 0;

                DirtyField(entity, entity.Comp, nameof(DamageOverlayComponent.PainLevel));
                DirtyField(entity, entity.Comp, nameof(DamageOverlayComponent.OxygenLevel));
                DirtyField(entity, entity.Comp, nameof(DamageOverlayComponent.CritLevel));
                DirtyField(entity, entity.Comp, nameof(DamageOverlayComponent.DeadLevel));

                break;
            }
            case MobState.Critical:
            {
                // DS14-start - progress from the critical threshold to the dead threshold
                entity.Comp.PreCriticalLevel = 0f;
                entity.Comp.CritLevel = GetStateProgress(
                    entity,
                    MobState.Critical,
                    MobState.Dead,
                    _damageable.GetTotalDamage((entity, damageable)),
                    thresholds);
                entity.Comp.OxygenLevel = 0f;
                DirtyField(entity, entity.Comp, nameof(DamageOverlayComponent.PreCriticalLevel));
                DirtyField(entity, entity.Comp, nameof(DamageOverlayComponent.OxygenLevel));
                // DS14-end

                entity.Comp.PainLevel = 0;
                entity.Comp.DeadLevel = 0;

                DirtyField(entity, entity.Comp, nameof(DamageOverlayComponent.PainLevel));
                DirtyField(entity, entity.Comp, nameof(DamageOverlayComponent.CritLevel));
                DirtyField(entity, entity.Comp, nameof(DamageOverlayComponent.DeadLevel));

                break;
            }
            case MobState.Dead:
            {
                entity.Comp.PainLevel = 0;
                // DS14-start
                entity.Comp.CritLevel = 1f;
                entity.Comp.OxygenLevel = 0f;
                entity.Comp.PreCriticalLevel = 0f;
                DirtyField(entity, entity.Comp, nameof(DamageOverlayComponent.OxygenLevel));
                DirtyField(entity, entity.Comp, nameof(DamageOverlayComponent.PreCriticalLevel));
                // DS14-end

                DirtyField(entity, entity.Comp, nameof(DamageOverlayComponent.PainLevel));
                DirtyField(entity, entity.Comp, nameof(DamageOverlayComponent.CritLevel));

                break;
            }
        }

        DirtyField(entity, entity.Comp, nameof(DamageOverlayComponent.CurrentState));
    }

    protected virtual void EnsureOverlay(Entity<DamageOverlayComponent> entity) { }

    // DS14-start
    private float GetStateProgress(
        EntityUid entity,
        MobState startState,
        MobState endState,
        FixedPoint2 damage,
        MobThresholdsComponent thresholds)
    {
        if (!_mobThresholdSystem.TryGetThresholdForState(entity, startState, out var start, thresholds) ||
            !_mobThresholdSystem.TryGetThresholdForState(entity, endState, out var end, thresholds))
        {
            return 0f;
        }

        var range = end.Value - start.Value;
        if (range <= 0)
            return 0f;

        return FixedPoint2.Clamp((damage - start.Value) / range, 0f, 1f).Float();
    }
    // DS14-end
}
