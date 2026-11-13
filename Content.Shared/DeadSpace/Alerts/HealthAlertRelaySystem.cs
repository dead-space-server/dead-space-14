// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Events;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Alerts;

/// <summary>
/// Handles relaying the health alert of a linked entity to the entity with <see cref="HealthAlertRelayComponent"/>.
/// This runs on both client and server, so alerts update immediately alongside damage prediction.
/// </summary>
public sealed partial class HealthAlertRelaySystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HealthAlertRelayComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HealthAlertRelayComponent, AfterAutoHandleStateEvent>(OnAfterHandleState);
        SubscribeLocalEvent<MobThresholdsComponent, MobThresholdChecked>(OnMobThresholdChecked);
    }

    private void OnStartup(EntityUid uid, HealthAlertRelayComponent component, ComponentStartup args)
    {
        UpdateRelay(uid, component);
    }

    private void OnAfterHandleState(EntityUid uid, HealthAlertRelayComponent component, ref AfterAutoHandleStateEvent args)
    {
        UpdateRelay(uid, component);
    }

    private void OnMobThresholdChecked(EntityUid uid, MobThresholdsComponent component, ref MobThresholdChecked args)
    {
        var query = EntityQueryEnumerator<HealthAlertRelayComponent>();
        while (query.MoveNext(out var relayUid, out var relayComp))
        {
            if (relayComp.LinkedEntity == uid)
            {
                UpdateRelayAlert(relayUid, uid, component, args.MobState, args.Damageable);
            }
        }
    }

    /// <summary>
    /// Forces an update of the health alert relayed from the linked entity.
    /// </summary>
    public void UpdateRelay(EntityUid relayUid, HealthAlertRelayComponent relayComp)
    {
        if (relayComp.LinkedEntity == null)
            return;

        if (TryComp<MobThresholdsComponent>(relayComp.LinkedEntity.Value, out var thresholdComp) &&
            TryComp<MobStateComponent>(relayComp.LinkedEntity.Value, out var mobStateComp) &&
            TryComp<DamageableComponent>(relayComp.LinkedEntity.Value, out var damageableComp))
        {
            UpdateRelayAlert(relayUid, relayComp.LinkedEntity.Value, thresholdComp, mobStateComp, damageableComp);
        }
    }

    private void UpdateRelayAlert(EntityUid relayUid, EntityUid targetUid, MobThresholdsComponent thresholdComp, MobStateComponent mobStateComp, DamageableComponent damageComp)
    {
        if (!thresholdComp.TriggersAlerts)
            return;

        if (!thresholdComp.StateAlertDict.TryGetValue(mobStateComp.CurrentState, out var currentAlert))
            return;

        if (!_alerts.TryGet(currentAlert, out var alertPrototype))
            return;

        if (alertPrototype.SupportsSeverity)
        {
            var severity = _alerts.GetMinSeverity(currentAlert);
            var ev = new BeforeAlertSeverityCheckEvent(currentAlert, severity);
            RaiseLocalEvent(targetUid, ev);

            if (ev.CancelUpdate)
            {
                _alerts.ShowAlert(relayUid, ev.CurrentAlert, ev.Severity);
                return;
            }

            if (_mobThreshold.TryGetNextState(targetUid, mobStateComp.CurrentState, out var nextState, thresholdComp) &&
                _mobThreshold.TryGetPercentageForState(targetUid, nextState.Value, damageComp.TotalDamage, out var percentage))
            {
                percentage = FixedPoint2.Clamp(percentage.Value, 0, 1);
                severity = (short)MathF.Round(
                    MathHelper.Lerp(
                        _alerts.GetMinSeverity(currentAlert),
                        _alerts.GetMaxSeverity(currentAlert),
                        percentage.Value.Float()));
            }
            _alerts.ShowAlert(relayUid, currentAlert, severity);
        }
        else
        {
            _alerts.ShowAlert(relayUid, currentAlert);
        }
    }
}
