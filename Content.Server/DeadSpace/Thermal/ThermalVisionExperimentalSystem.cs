// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DeadSpace.ThermalVision;
using Content.Shared.Examine;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.ThermalVision;

public sealed class ThermalVisionExperimentalSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float EnergyPerUse = 100f;
    private const float CooldownSeconds = 8f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ThermalVisionExperimentalComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ThermalVisionExperimentalComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<ThermalVisionExperimentalComponent, ToggleThermalVisionExperimentalActionEvent>(OnToggle);
        SubscribeLocalEvent<ThermalVisorExperimentalComponent, ExaminedEvent>(OnExamine);
    }

    private void OnStartup(EntityUid uid, ThermalVisionExperimentalComponent component, ComponentStartup args)
    {
        _actions.AddAction(uid, ref component.ActionToggleThermalVisionExperimentalEntity, component.ActionToggleThermalVisionExperimental);

        if (component.VisorUid is {} visorUid && TryComp<ThermalVisorExperimentalComponent>(visorUid, out var visorComp) && visorComp.LastToggleTime != null)
        {
            var elapsed = _timing.CurTime - visorComp.LastToggleTime.Value;
            if (elapsed.TotalSeconds < CooldownSeconds)
            {
                var remaining = CooldownSeconds - elapsed.TotalSeconds;
                _actions.SetCooldown(component.ActionToggleThermalVisionExperimentalEntity!.Value, TimeSpan.FromSeconds(remaining));
            }
        }
    }

    private void OnRemove(EntityUid uid, ThermalVisionExperimentalComponent component, ComponentRemove args)
    {
        _actions.RemoveAction(uid, component.ActionToggleThermalVisionExperimentalEntity);
    }

    private void OnExamine(EntityUid uid, ThermalVisorExperimentalComponent component, ExaminedEvent args)
    {
        if (!TryComp<BatteryComponent>(uid, out var battery))
            return;

        var charge = _battery.GetCharge((uid, battery));
        var uses = (int)(charge / EnergyPerUse);
        string color;

        if (uses == 0)
            color = "red";
        else if (uses <= 5)
            color = "yellow";
        else
            color = "green";

        args.PushMarkup(Loc.GetString("thermal-visor-experimental-examine-charges",
            ("color", color),
            ("currentUses", uses)));
    }

    private void OnToggle(EntityUid uid, ThermalVisionExperimentalComponent component, ToggleThermalVisionExperimentalActionEvent args)
    {
        if (args.Handled || component.IsActive)
            return;

        var visorUid = component.VisorUid;
        if (visorUid == null || !TryComp<BatteryComponent>(visorUid.Value, out var battery))
            return;

        if (_battery.GetCharge((visorUid.Value, battery)) < EnergyPerUse)
            return;

        args.Handled = true;
        _battery.UseCharge((visorUid.Value, battery), EnergyPerUse);
        component.IsActive = true;
        component.CurrentPulseTime = component.PulseDuration;
        Dirty(uid, component);

        if (TryComp<ThermalVisorExperimentalComponent>(visorUid.Value, out var visorComp))
        {
            visorComp.LastToggleTime = _timing.CurTime;
            Dirty(visorUid.Value, visorComp);
        }

        Timer.Spawn(TimeSpan.FromSeconds(component.PulseDuration), () =>
        {
            if (!Exists(uid))
                return;

            if (TryComp<ThermalVisionExperimentalComponent>(uid, out var comp))
            {
                comp.IsActive = false;
                Dirty(uid, comp);
            }
        });
    }
}