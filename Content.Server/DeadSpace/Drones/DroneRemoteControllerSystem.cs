using Content.Shared.DeadSpace.Drones.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.DoAfter;
using Content.Server.Popups;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Utility;
using Content.Shared.Ghost;
using Content.Shared.Timing;
using Robust.Shared.Timing;
using Content.Shared.Emp;
using Robust.Shared.Audio.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands;
using Content.Shared.Examine;
using Content.Shared.Whitelist;

namespace Content.Server.DeadSpace.Drones.Systems;

public sealed partial class DroneRemoteControllerSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private DroneSystem _drone = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private EntityWhitelistSystem _entityWhitelist = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DroneRemoteControllerComponent, AfterInteractEvent>(AfterInteract);
        SubscribeLocalEvent<DroneRemoteControllerComponent, TryDroneConnectDoAfterEvent>(OnDoAfter);

        SubscribeLocalEvent<DroneRemoteControllerComponent, UseInHandEvent>(OnUse);
        SubscribeLocalEvent<DroneRemoteControllerComponent, HandDeselectedEvent>(OnDeselect);
        SubscribeLocalEvent<DroneRemoteControllerComponent, GotUnequippedHandEvent>(OnItemLeaveHand);

        SubscribeLocalEvent<DroneRemoteControllerComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<DroneRemoteControllerComponent, EmpDisabledRemovedEvent>(OnEmpFinished);

        SubscribeLocalEvent<DroneRemoteControllerComponent, GetVerbsEvent<Verb>>(GetVerb);

        SubscribeLocalEvent<DroneHostComponent, DamageChangedEvent>(OnDamageChanged);

        SubscribeLocalEvent<DroneRemoteControllerComponent, ExaminedEvent>(OnExamine);

        SubscribeLocalEvent<DroneRemoteControllerComponent, ComponentShutdown>(OnShutdown);
    }

    private void AfterInteract(EntityUid uid, DroneRemoteControllerComponent comp, AfterInteractEvent args)
    {
        if (args.Target == null || !args.CanReach)
            return;

        if (!TryComp<DroneComponent>(args.Target, out var drone))
            return;

        _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, comp.ConnectTime, new TryDroneConnectDoAfterEvent(), uid, target: args.Target, used: uid)
        {
            BreakOnMove = true,
            NeedHand = true,
            BreakOnDamage = false,
            DuplicateCondition = DuplicateConditions.SameEvent,
            BlockDuplicate = true,
            CancelDuplicate = false
        });
    }

    private void OnDoAfter(EntityUid uid, DroneRemoteControllerComponent comp, TryDroneConnectDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target == null)
            return;

        var target = args.Args.Target.Value;
        var user = args.Args.User;

        if (!TryComp<DroneComponent>(target, out var drone))
            return;

        if (comp.IsDroneConnected)
        {
            _popupSystem.PopupEntity(Loc.GetString("drone-is-connected"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        if (drone.DroneController != null)
        {
            _popupSystem.PopupEntity(Loc.GetString("drone-is-connected-to-other"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        if (!comp.CanConnect || !drone.CanConnect || !_entityWhitelist.IsWhitelistPassOrNull(comp.ConnectWhitelist, drone.Owner))
        {
            _popupSystem.PopupEntity(Loc.GetString("drone-failed-to-connect"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        drone.DroneController = uid;
        comp.ConnectedDrone = target;
        comp.IsDroneConnected = true;
        _popupSystem.PopupEntity(Loc.GetString("drone-connected"), user, user, PopupType.Small);

        _audio.PlayPvs(comp.ConnectSound, uid);
    }

    private void OnUse(EntityUid uid, DroneRemoteControllerComponent comp, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!_useDelay.TryResetDelay(uid))
            return;

        if (comp.ConnectedDrone == null)
        {
            _popupSystem.PopupEntity(Loc.GetString("drone-not-found"), args.User, args.User, PopupType.Small);
            args.Handled = true;
            return;
        }

        var hostPos = _transform.GetMapCoordinates(uid);
        var dronePos = _transform.GetMapCoordinates(comp.ConnectedDrone.Value);
        var dist = (hostPos.Position - dronePos.Position).Length();

        if (dist > comp.Range && !comp.CanWorkOnDifferentMaps)
        {
            _popupSystem.PopupEntity(Loc.GetString("drone-out-of-range"), args.User, args.User, PopupType.Small);
            args.Handled = true;
            return;
        }

        if (!comp.ContolDrone)
        {
            _drone.StartControlDrone(comp.ConnectedDrone.Value, args.User, uid);
        }
        else
        {
            _drone.StopControlDrone(comp.ConnectedDrone.Value, args.User, uid);
        }
        args.Handled = true;
    }

    private void OnEmpPulse(Entity<DroneRemoteControllerComponent> ent, ref EmpPulseEvent args)
    {
        args.Affected = true;
        args.Disabled = true;

        ent.Comp.CanConnect = false;
        DisconectDrone(ent.Comp);
    }

    private void OnEmpFinished(Entity<DroneRemoteControllerComponent> ent, ref EmpDisabledRemovedEvent args)
    {
        ent.Comp.CanConnect = true;
    }

    private void GetVerb(EntityUid uid, DroneRemoteControllerComponent comp, GetVerbsEvent<Verb> args)
    {
        if (!comp.IsDroneConnected || !args.CanComplexInteract || !args.CanAccess || HasComp<GhostComponent>(args.User))
            return;

        args.Verbs.Add(new Verb
        {
            Act = () => DisconectDrone(comp),
            Text = Loc.GetString("drone-verb-disconnect"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
        });
    }

    public void DisconectDrone(DroneRemoteControllerComponent comp, string popup = "drone-disconnect")
    {
        if (comp.ConnectedDrone == null)
            return;

        if (!TryComp<DroneComponent>(comp.ConnectedDrone, out var drone))
            return;

        var holder = Comp<TransformComponent>(comp.Owner).ParentUid;
        if (holder.IsValid())
            _popupSystem.PopupEntity(Loc.GetString(popup), holder, holder, PopupType.Small);

        if (drone.DroneHost != null)
            _drone.StopControlDrone(comp.ConnectedDrone.Value, drone.DroneHost.Value, comp.Owner);

        _audio.PlayPvs(comp.DisconnectSound, comp.Owner);

        drone.DroneController = null;
        comp.ConnectedDrone = null;
        comp.IsDroneConnected = false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DroneHostComponent>();
        while (query.MoveNext(out var uid, out var host))
        {
            if (host.Drone == null || host.Controller == null)
                continue;

            if (!TryComp<DroneRemoteControllerComponent>(host.Controller, out var controller))
                continue;

            if (!TryComp<DroneComponent>(host.Drone, out var drone))
                continue;

            if (controller.CanWorkOnDifferentMaps)
                continue;

            var hostPos = _transform.GetMapCoordinates(uid);
            var dronePos = _transform.GetMapCoordinates(host.Drone.Value);

            if (hostPos.MapId != dronePos.MapId)
            {
                _drone.StopControlDrone(host.Drone.Value, uid, host.Controller.Value);
                _popupSystem.PopupEntity(Loc.GetString("drone-disconnect-emp"), uid, uid, PopupType.MediumCaution);
                continue;
            }

            var dist = (hostPos.Position - dronePos.Position).Length();
            if (dist > controller.Range)
            {
                _drone.StopControlDrone(host.Drone.Value, uid, host.Controller.Value);
                _popupSystem.PopupEntity(Loc.GetString("drone-disconnect-emp"), uid, uid, PopupType.MediumCaution);
            }

            if (dist >= controller.WarningRange)
            {
                if (_timing.CurTime >= controller.NextWarningTime)
                {
                    _popupSystem.PopupEntity(Loc.GetString("drone-distance-warning"), controller.IsFPV && drone.IsFPV ? host.Drone.Value : uid, uid, PopupType.MediumCaution);
                    controller.NextWarningTime = _timing.CurTime + controller.WarningPeriod;
                }
            }
        }
    }

    private void OnDamageChanged(EntityUid uid, DroneHostComponent comp, DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        if (comp.Controller != null && comp.Drone != null)
        {
            _popupSystem.PopupEntity(Loc.GetString("drone-host-got-damage"), uid, uid, PopupType.MediumCaution);
            _drone.StopControlDrone(comp.Drone.Value, uid, comp.Controller.Value);
        }
    }

    private void OnDeselect(EntityUid uid, DroneRemoteControllerComponent comp, HandDeselectedEvent args)
    {
        if (!TryComp<DroneComponent>(comp.ConnectedDrone, out var drone))
            return;

        if (drone.DroneHost != null)
            _drone.StopControlDrone(comp.ConnectedDrone.Value, drone.DroneHost.Value, comp.Owner);
    }

    private void OnItemLeaveHand(EntityUid uid, DroneRemoteControllerComponent comp, GotUnequippedHandEvent args)
    {
        if (!TryComp<DroneComponent>(comp.ConnectedDrone, out var drone))
            return;

        if (drone.DroneHost != null)
            _drone.StopControlDrone(comp.ConnectedDrone.Value, drone.DroneHost.Value, comp.Owner);
    }

    private void OnExamine(EntityUid uid, DroneRemoteControllerComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(component.IsDroneConnected ? "drone-status-connect" : "drone-status-not-connect"));
    }

    private void OnShutdown(Entity<DroneRemoteControllerComponent> ent, ref ComponentShutdown args)
    {
        DisconectDrone(ent.Comp);
    }
}