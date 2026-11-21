using Content.Server.Popups;
using Content.Shared.DeadSpace.Drones.Components;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Robust.Server.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Content.Shared.Actions;

namespace Content.Server.DeadSpace.Drones.Systems;

public sealed partial class DroneSystem : EntitySystem
{
    [Dependency] private EyeSystem _eye = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private DroneRemoteControllerSystem _controller = default!;
    [Dependency] private SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DroneComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DroneComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<DroneComponent, EmpDisabledRemovedEvent>(OnEmpFinished);
        SubscribeLocalEvent<DroneComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<DroneComponent, ExaminedEvent>(OnExamine);
    }

    public void StartControlDrone(EntityUid drone, EntityUid host, EntityUid controller)
    {
        if (!TryComp<DroneComponent>(drone, out var droneComp))
            return;
        if (!TryComp<DroneRemoteControllerComponent>(controller, out var controllerComp))
            return;

        if (HasComp<DroneHostComponent>(host))
            return;

        if (!TryComp<PhysicsComponent>(host, out var hostPhysics) ||
            hostPhysics.BodyType != BodyType.KinematicController)
        {
            _popupSystem.PopupEntity(Loc.GetString("drone-failed-to-connect"), host, host, PopupType.MediumCaution);
            return;
        }

        if (!_powerCell.TryGetBatteryFromSlotOrEntity(drone, out var battery) ||
            _battery.GetCharge(battery.Value.AsNullable()) <= droneComp.Wattage)
        {
            _popupSystem.PopupEntity(Loc.GetString("drone-no-power"), host, host, PopupType.MediumCaution);
            return;
        }

        var chargeLevel = _battery.GetChargeLevel(battery.Value.AsNullable());

        foreach (var warning in droneComp.ChargeWarnings)
            warning.Triggered = chargeLevel <= warning.Threshold;

        droneComp.DroneHost = host;
        var hostComp = EnsureComp<DroneHostComponent>(host);
        hostComp.Drone = drone;
        hostComp.Controller = droneComp.DroneController;
        controllerComp.ContolDrone = true;

        _mover.SetRelay(host, drone);

        if (TryComp<EyeComponent>(host, out var eye) && droneComp.IsFPV && controllerComp.IsFPV)
        {
            _eye.SetDrawFov(host, true, eye);
            _eye.SetTarget(host, drone, eye);
        }

        foreach (var action in droneComp.ActionEntities)
        {
            _actions.AddAction(host, action, drone);
        }
    }

    public void StopControlDrone(EntityUid drone, EntityUid host, EntityUid controller)
    {
        if (!TryComp<DroneComponent>(drone, out var droneComp))
            return;
        if (!TryComp<DroneHostComponent>(host, out var hostComp))
            return;
        if (!TryComp<DroneRemoteControllerComponent>(controller, out var controllerComp))
            return;

        if (TryComp<EyeComponent>(host, out var eye))
        {
            _eye.SetDrawFov(host, true, eye);
            _eye.SetTarget(host, null, eye);
        }

        RemComp<RelayInputMoverComponent>(host);
        RemComp<DroneHostComponent>(host);

        droneComp.DroneHost = null;
        hostComp.Controller = null;
        controllerComp.ContolDrone = false;

        _actions.RemoveProvidedActions(host, drone);
    }

    private void OnMapInit(Entity<DroneComponent> ent, ref MapInitEvent args)
    {
        foreach (var action in ent.Comp.ActionsToDrone)
        {
            EntityUid? actionEnt = null;
            _actions.AddAction(ent.Owner, ref actionEnt, action);

            if (actionEnt != null)
                ent.Comp.ActionEntities.Add(actionEnt.Value);
        }
    }

    private void OnEmpPulse(Entity<DroneComponent> ent, ref EmpPulseEvent args)
    {
        args.Affected = true;
        args.Disabled = true;

        ent.Comp.CanConnect = false;

        if (TryComp<DroneRemoteControllerComponent>(ent.Comp.DroneController, out var controlller))
            _controller.DisconectDrone(controlller, "drone-disconnect-emp");
    }

    private void OnEmpFinished(Entity<DroneComponent> ent, ref EmpDisabledRemovedEvent args)
    {
        ent.Comp.CanConnect = true;
    }

    private void OnShutdown(Entity<DroneComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<DroneRemoteControllerComponent>(ent.Comp.DroneController, out var controller))
            _controller.DisconectDrone(controller, "drone-disconnect-destroyed");
    }

    private void OnExamine(EntityUid uid, DroneComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(component.DroneController != null ? "drone-status-connect" : "drone-status-not-connect"));
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DroneHostComponent>();
        while (query.MoveNext(out var uid, out var host))
        {
            if (host.Drone == null || host.Controller == null)
                continue;

            if (!TryComp<DroneComponent>(host.Drone, out var drone))
                continue;

            if (!TryComp<DroneRemoteControllerComponent>(host.Controller, out var controller))
                continue;

            if (!_powerCell.TryGetBatteryFromSlotOrEntity(host.Drone.Value, out var battery))
            {
                _controller.DisconectDrone(controller, "drone-disconnect-no-power");
                continue;
            }

            if (!_battery.TryUseCharge(battery.Value.AsNullable(), drone.Wattage * frameTime))
            {
                _controller.DisconectDrone(controller, "drone-disconnect-no-power");
            }

            var chargeLevel = _battery.GetChargeLevel(battery.Value.AsNullable());

            foreach (var warning in drone.ChargeWarnings)
            {
                if (!warning.Triggered && chargeLevel <= warning.Threshold)
                {
                    warning.Triggered = true;
                    _popupSystem.PopupEntity(Loc.GetString(warning.String), drone.IsFPV && controller.IsFPV ? host.Drone.Value : uid, uid, PopupType.MediumCaution);
                }
            }
        }
    }
}