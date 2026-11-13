using Content.Shared.Damage.Components;
using Content.Shared.DeadSpace.Psychiatry; //DS-14
using Content.Shared.DeadSpace.TheCircle.Dreadnought;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Player;

namespace Content.Client.UserInterface.Systems.DamageOverlays;

[UsedImplicitly]
public sealed partial class DamageOverlayUiController : UIController
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    [UISystemDependency] private readonly MobThresholdSystem _mobThresholdSystem = default!;
    [UISystemDependency] private readonly StatusEffectsSystem _statusEffects = default!;
    private Overlays.DamageOverlay _overlay = default!;

    public override void Initialize()
    {
        _overlay = new Overlays.DamageOverlay();
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttach);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<MobThresholdChecked>(OnThresholdCheck);
    }

    private void OnPlayerAttach(LocalPlayerAttachedEvent args)
    {
        ClearOverlay();
        if (!EntityManager.TryGetComponent<MobStateComponent>(args.Entity, out var mobState))
            return;
        if (mobState.CurrentState != MobState.Dead)
            UpdateOverlays(args.Entity, mobState);
        _overlayManager.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        _overlayManager.RemoveOverlay(_overlay);
        ClearOverlay();
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.Target != _playerManager.LocalEntity)
            return;

        UpdateOverlays(args.Target, args.Component);
    }

    private void OnThresholdCheck(ref MobThresholdChecked args)
    {

        if (args.Target != _playerManager.LocalEntity)
            return;
        UpdateOverlays(args.Target, args.MobState, args.Damageable, args.Threshold);
    }

    public void RefreshLocal(EntityUid entity) //DS-14
    {
        if (!EntityManager.TryGetComponent<MobStateComponent>(entity, out var mobState))
            return;
        if (mobState.CurrentState == MobState.Dead)
        {
            ClearOverlay();
            return;
        }

        UpdateOverlays(entity, mobState);
    }

    private void ClearOverlay()
    {
        _overlay.Reset();
    }

    //TODO: Jezi: adjust oxygen and hp overlays to use appropriate systems once bodysim is implemented
    private void UpdateOverlays(EntityUid entity, MobStateComponent? mobState, DamageableComponent? damageable = null, MobThresholdsComponent? thresholds = null)
    {
        // DS14-start
        if (EntityManager.HasComponent<DreadnoughtLastStandActiveComponent>(entity))
        {
            ClearOverlay();
            return;
        }
        // DS14-end

        if (mobState == null && !EntityManager.TryGetComponent(entity, out mobState) ||
            thresholds == null && !EntityManager.TryGetComponent(entity, out thresholds) ||
            damageable == null && !EntityManager.TryGetComponent(entity, out  damageable))
            return;

        if (!_mobThresholdSystem.TryGetIncapThreshold(entity, out var foundThreshold, thresholds))
            return; //this entity cannot die or crit!!

        if (!thresholds.ShowOverlays)
        {
            ClearOverlay();
            return; //this entity intentionally has no overlays
        }

        var critThreshold = foundThreshold.Value;
        _overlay.State = mobState.CurrentState;

        switch (mobState.CurrentState)
        {
            case MobState.Alive:
            case MobState.PreCritical:
            {
                FixedPoint2 painLevel = 0;
                _overlay.PainLevel = 0;

                if (!_statusEffects.TryEffectsWithComp<PainNumbnessStatusEffectComponent>(entity, out _))
                {
                    foreach (var painDamageType in damageable.PainDamageGroups)
                    {
                        damageable.DamagePerGroup.TryGetValue(painDamageType, out var painDamage);
                        painLevel += painDamage;
                    }
                    _overlay.PainLevel = FixedPoint2.Min(1f, painLevel / critThreshold).Float();

                    if (_overlay.PainLevel < 0.05f) // Don't show damage overlay if they're near enough to max.
                    {
                        _overlay.PainLevel = 0;
                    }
                }

                _overlay.OxygenLevel = 0f;
                if (damageable.DamagePerGroup.TryGetValue("Airloss", out var oxyDamage))
                {
                    _overlay.OxygenLevel = FixedPoint2.Min(1f, oxyDamage / critThreshold).Float();
                }

                //DS-14 start
                if (EntityManager.TryGetComponent<SchizophreniaComponent>(entity, out var schizo)
                    && schizo.PhantomAmount > 0f
                    && schizo.Stage >= SchizophreniaStage.Simple)
                {
                    var extra = Math.Clamp(schizo.PhantomAmount / critThreshold.Float(), 0f, 0.4f);
                    if (schizo.PhantomType == "Asphyxiation")
                    {
                        if (_overlay.OxygenLevel < 0.85f)
                            _overlay.OxygenLevel = Math.Min(0.85f, _overlay.OxygenLevel + extra);
                    }
                    else if (_overlay.PainLevel < 0.85f)
                    {
                        _overlay.PainLevel = Math.Min(0.85f, _overlay.PainLevel + extra);
                    }
                }
                //DS-14 end

                _overlay.PreCriticalLevel = mobState.CurrentState == MobState.PreCritical
                    ? GetStateProgress(entity, MobState.PreCritical, MobState.Critical, damageable.TotalDamage, thresholds)
                    : 0f;
                _overlay.CritLevel = 0f;
                _overlay.DeadLevel = 0f;
                break;
            }
            case MobState.Critical:
            {
                _overlay.PreCriticalLevel = 0f;
                _overlay.CritLevel = GetStateProgress(
                    entity,
                    MobState.Critical,
                    MobState.Dead,
                    damageable.TotalDamage,
                    thresholds);
                _overlay.PainLevel = 0f;
                _overlay.OxygenLevel = 0f;
                _overlay.DeadLevel = 0f;
                break;
            }
            case MobState.Dead:
            {
                _overlay.PreCriticalLevel = 0f;
                _overlay.PainLevel = 0f;
                _overlay.OxygenLevel = 0f;
                _overlay.CritLevel = 1f;
                break;
            }
        }
    }

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
}
