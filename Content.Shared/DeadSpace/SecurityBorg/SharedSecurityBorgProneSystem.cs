using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Rotation;
using Content.Shared.Standing;
using Robust.Shared.Network;

namespace Content.Shared.DeadSpace.SecurityBorg;

public sealed partial class SharedSecurityBorgProneSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SecurityBorgProneComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SecurityBorgProneComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SecurityBorgProneComponent, SecurityBorgProneActionEvent>(OnAction);
        SubscribeLocalEvent<SecurityBorgProneComponent, DownedEvent>(OnDowned);
        SubscribeLocalEvent<SecurityBorgProneComponent, StoodEvent>(OnStood);
        SubscribeLocalEvent<SecurityBorgProneComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<SecurityBorgProneComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
    }

    private void OnInit(EntityUid uid, SecurityBorgProneComponent component, ComponentInit args)
    {
        if (_net.IsServer)
        {
            var actions = EnsureComp<ActionsComponent>(uid);
            _actions.AddAction(uid, ref component.ActionEntity, component.Action, uid, actions);
            _actions.SetToggled(component.ActionEntity, component.Enabled);
        }

        SetProneVisuals(uid, component.Enabled && !IsIncapacitated(uid));

        if (component.Enabled && !IsIncapacitated(uid))
        {
            _standing.Down(uid, playSound: false, dropHeldItems: false);
            SetRotationState(uid, RotationState.Vertical);
        }
    }

    private void OnShutdown(EntityUid uid, SecurityBorgProneComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.ActionEntity);
    }

    private void OnAction(EntityUid uid, SecurityBorgProneComponent component, SecurityBorgProneActionEvent args)
    {
        if (args.Handled)
            return;

        if (IsIncapacitated(uid))
        {
            args.Handled = true;
            return;
        }

        if (component.Enabled)
        {
            if (_standing.Stand(uid))
            {
                component.Enabled = false;
                Dirty(uid, component);
                _actions.SetToggled(component.ActionEntity, false);
                SetRotationState(uid, RotationState.Vertical);
                SetProneVisuals(uid, false);
                _movementSpeed.RefreshMovementSpeedModifiers(uid);
            }
        }
        else
        {
            if (_standing.Down(uid, playSound: false, dropHeldItems: false))
            {
                component.Enabled = true;
                Dirty(uid, component);
                _actions.SetToggled(component.ActionEntity, true);
                SetRotationState(uid, RotationState.Vertical);
                SetProneVisuals(uid, true);
                _movementSpeed.RefreshMovementSpeedModifiers(uid);
            }
        }

        args.Handled = true;
    }

    private void OnDowned(EntityUid uid, SecurityBorgProneComponent component, DownedEvent args)
    {
        SetProneVisuals(uid, component.Enabled);
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    private void OnStood(EntityUid uid, SecurityBorgProneComponent component, StoodEvent args)
    {
        SetRotationState(uid, RotationState.Vertical);
        SetProneVisuals(uid, false);
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    private void OnMobStateChanged(EntityUid uid, SecurityBorgProneComponent component, MobStateChangedEvent args)
    {
        if (!component.Enabled)
            return;

        if (args.NewMobState == MobState.Alive)
        {
            _standing.Down(uid, playSound: false, dropHeldItems: false);
            SetRotationState(uid, RotationState.Vertical);
        }
    }

    private void SetRotationState(EntityUid uid, RotationState state)
    {
        if (TryComp<AppearanceComponent>(uid, out var appearance))
            _appearance.SetData(uid, RotationVisuals.RotationState, state, appearance);
    }

    private void OnRefreshMovementSpeed(EntityUid uid, SecurityBorgProneComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (component.Enabled && !IsIncapacitated(uid) && _standing.IsDown(uid))
            args.ModifySpeed(component.SpeedModifier);
    }

    private void SetProneVisuals(EntityUid uid, bool prone)
    {
        if (TryComp<AppearanceComponent>(uid, out var appearance))
            _appearance.SetData(uid, SecurityBorgProneVisuals.Prone, prone, appearance);
    }

    private bool IsIncapacitated(EntityUid uid)
    {
        return TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState is MobState.Critical or MobState.Dead;
    }
}
