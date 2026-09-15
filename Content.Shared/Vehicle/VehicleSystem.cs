/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Content.Shared.Access.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Vehicle.Components;
using Content.Shared.Stunnable;
using Content.Shared.Whitelist;
using Content.Shared.Weapons.Melee.Events;
using JetBrains.Annotations;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Content.Shared.Popups;

namespace Content.Shared.Vehicle;

/// <summary>
/// Handles logic relating to vehicles.
/// </summary>
public sealed partial class VehicleSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityWhitelistSystem _entityWhitelist = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private IGameTiming _timing = default!;
    // DS14-start
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;
    // DS14-end

    private EntityQuery<VehicleComponent> _vehicleQuery;
    private EntityQuery<VehicleOperatorComponent> _operatorQuery;
    private EntityQuery<AppearanceComponent> _appearanceQuery;
    private EntityQuery<InputMoverComponent> _inputMoverQuery;
    private EntityQuery<HandsComponent> _handsQuery;
    private EntityQuery<VehicleHandBlockerComponent> _handBlockerQuery;
    private EntityQuery<InteractionRelayComponent> _interactionRelayQuery;
    private EntityQuery<MovementRelayTargetComponent> _relayTargetQuery;
    private EntityQuery<RelayInputMoverComponent> _relayQuery;
    private readonly HashSet<(EntityUid Vehicle, EntityUid Operator)> _operatorMeleeVehicleHits = new(); // DS14

    /// <inheritdoc/>
    public override void Initialize()
    {
        _vehicleQuery = GetEntityQuery<VehicleComponent>();
        _operatorQuery = GetEntityQuery<VehicleOperatorComponent>();
        _appearanceQuery = GetEntityQuery<AppearanceComponent>();
        _inputMoverQuery = GetEntityQuery<InputMoverComponent>();
        _handsQuery = GetEntityQuery<HandsComponent>();
        _handBlockerQuery = GetEntityQuery<VehicleHandBlockerComponent>();
        _interactionRelayQuery = GetEntityQuery<InteractionRelayComponent>();
        _relayTargetQuery = GetEntityQuery<MovementRelayTargetComponent>();
        _relayQuery = GetEntityQuery<RelayInputMoverComponent>();

        InitializeOperator();
        InitializeKey();

        SubscribeLocalEvent<VehicleComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<VehicleComponent, UpdateCanMoveEvent>(OnVehicleUpdateCanMove);
        SubscribeLocalEvent<VehicleComponent, ComponentShutdown>(OnVehicleShutdown);
        SubscribeLocalEvent<VehicleComponent, GetAdditionalAccessEvent>(OnVehicleGetAdditionalAccess);

        SubscribeLocalEvent<VehicleComponent, UnstrapAttemptEvent>(OnVehicleUnstrapAttempt); // DS14

        SubscribeLocalEvent<VehicleOperatorComponent, ComponentShutdown>(OnOperatorShutdown);
        // DS14-start - Current engine uses explicit event subscriptions.
        SubscribeLocalEvent<VehicleOperatorComponent, CanAttackFromContainerEvent>(OnOperatorCanAttackFromContainer);
        SubscribeLocalEvent<VehicleOperatorComponent, AttackAttemptEvent>(OnOperatorAttackAttempt);
        // DS14-end
        // DS14-start
        SubscribeLocalEvent<VehicleOperatorComponent, MobStateChangedEvent>(OnOperatorMobStateChanged);
        SubscribeLocalEvent<VehicleOperatorComponent, UpdateCanMoveEvent>(OnOperatorUpdateCanMove);
        SubscribeLocalEvent<VehicleComponent, VehicleOperatorSetEvent>(OnVehicleOperatorSet);
        SubscribeLocalEvent<VehicleComponent, AttackedEvent>(OnVehicleAttacked);
        SubscribeLocalEvent<VehicleComponent, StartCollideEvent>(OnStartCollide);
        // DS14-end
    }

    /// <remarks>
    /// We subscribe to BeforeDamageChangedEvent so that we can access the damage value before the container is applied.
    /// </remarks>
    private void OnBeforeDamageChanged(Entity<VehicleComponent> ent, ref BeforeDamageChangedEvent args)
    {
        // DS14-start
        if (args.Damage.AnyPositive() &&
            args.Origin is { } origin &&
            ent.Comp.Operator == origin &&
            _operatorMeleeVehicleHits.Remove((ent.Owner, origin)))
        {
            args.Cancelled = true;
            return;
        }
        // DS14-end

        if (!ent.Comp.TransferDamage || !args.Damage.AnyPositive() || ent.Comp.Operator is not { } operatorUid)
            return;

        var damage = DamageSpecifier.GetPositive(args.Damage);

        if (ent.Comp.TransferDamageModifier is { } modifierSet)
        {
            // Reduce damage to the operator via the specified modifier, if provided.
            damage = DamageSpecifier.ApplyModifierSet(damage, modifierSet);
        }

        _damageable.TryChangeDamage(operatorUid, damage, origin: args.Origin);
    }

    private void OnVehicleUpdateCanMove(Entity<VehicleComponent> ent, ref UpdateCanMoveEvent args)
    {
        // DS14-start
        if (ent.Comp.RequiresOperator && ent.Comp.Operator is null)
        {
            args.Cancel();
            return;
        }
        // DS14-end

        if (!CanVehicleRun(ent))
            args.Cancel();
    }

    private void OnOperatorCanAttackFromContainer(Entity<VehicleOperatorComponent> ent, ref CanAttackFromContainerEvent args)
    {
        if (ent.Comp.Vehicle is not { } vehicleUid ||
            !_vehicleQuery.TryComp(vehicleUid, out var vehicle) ||
            !vehicle.CanAttack)
            return;

        args.CanAttack = true;
    }

    private void OnOperatorAttackAttempt(Entity<VehicleOperatorComponent> ent, ref AttackAttemptEvent args)
    {
        if (ent.Comp.Vehicle is not { } vehicleUid ||
            !_vehicleQuery.TryComp(vehicleUid, out var vehicle) ||
            !vehicle.CanAttack ||
            args.Target != vehicleUid)
            return;

        args.Cancel();
    }

    private void OnVehicleShutdown(Entity<VehicleComponent> ent, ref ComponentShutdown args)
    {
        if (_timing.ApplyingState)
            return;

        TryRemoveOperator(ent);
    }

    private void OnVehicleGetAdditionalAccess(Entity<VehicleComponent> ent, ref GetAdditionalAccessEvent args)
    {
        // Vehicles inherit access from whoever is driving them
        if (ent.Comp.Operator is { } operatorUid && Exists(operatorUid))
            args.Entities.Add(operatorUid);
    }

    // DS14-start
    private void OnVehicleUnstrapAttempt(Entity<VehicleComponent> ent, ref UnstrapAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (TryBlockProtectedOperatorUnstrap(ent, args.Buckle.Owner, args.User, args.Popup))
            args.Cancelled = true;
    }

    private bool TryBlockProtectedOperatorUnstrap(Entity<VehicleComponent> ent, EntityUid buckleOwner, EntityUid? user, bool popup)
    {
        if (!ent.Comp.ProtectOperatorUnstrap || user is not { } userUid)
            return false;

        if (ent.Comp.Operator is not { } operatorUid ||
            buckleOwner != operatorUid ||
            userUid == operatorUid)
            return false;

        if (CanForceUnstrapOperator(operatorUid))
            return false;

        if (popup && _net.IsServer)
            _popup.PopupPredicted(Loc.GetString("vehicle-operator-unstrap-protected"), ent.Owner, userUid, PopupType.SmallCaution);

        return true;
    }

    private bool CanForceUnstrapOperator(EntityUid operatorUid)
    {
        if (_net.IsServer && !HasComp<ActorComponent>(operatorUid))
            return true;

        if (TryComp<MobStateComponent>(operatorUid, out var mobState) &&
            mobState.CurrentState is MobState.PreCritical or MobState.Critical or MobState.Dead)
        {
            return true;
        }

        return HasComp<StunnedComponent>(operatorUid) || HasComp<KnockedDownComponent>(operatorUid);
    }
    // DS14-end

    private void OnOperatorShutdown(Entity<VehicleOperatorComponent> ent, ref ComponentShutdown args)
    {
        if (_timing.ApplyingState)
            return;

        if (ent.Comp.Vehicle is { } vehicleUid &&
            _vehicleQuery.TryComp(vehicleUid, out var vehicle))
        {
            ClearOperator((vehicleUid, vehicle), removeOperatorComponent: false, raiseSetEvent: true);
            return;
        }

        if (ent.Comp.Vehicle is { } missingVehicle)
            UnblockHands(missingVehicle, ent.Owner);

        CleanupOperatorRelays(ent.Owner, ent.Comp.Vehicle);
    }

    /// <summary>
    /// Set the operator for a given vehicle
    /// </summary>
    /// <param name="entity">The vehicle</param>
    /// <param name="uid">The new operator. If null, it will only remove the operator.</param>
    /// <param name="removeExisting">If true, will remove the current operator when setting the new one.</param>
    /// <returns>If the new operator was successfully able to be set</returns>
    public bool TrySetOperator(Entity<VehicleComponent> entity, EntityUid? uid, bool removeExisting = true)
    {
        var oldOperator = entity.Comp.Operator;

        if (oldOperator == null && uid is null)
            return false;

        if (!removeExisting && oldOperator is not null && oldOperator != uid)
            return false;

        if (uid is { } newOperator)
        {
            if (!CanOperate(entity.AsNullable(), newOperator))
                return false;

            if (_operatorQuery.TryComp(newOperator, out var existingOperator) &&
                existingOperator.Vehicle is { } existingVehicle &&
                existingVehicle != entity.Owner)
            {
                if (!removeExisting)
                    return false;

                if (_vehicleQuery.TryComp(existingVehicle, out var existingVehicleComponent) &&
                    existingVehicleComponent.Operator == newOperator)
                {
                    ClearOperator((existingVehicle, existingVehicleComponent), removeOperatorComponent: false, raiseSetEvent: true);
                }
                else
                {
                    UnblockHands(existingVehicle, newOperator);
                    CleanupOperatorRelays(newOperator, existingVehicle);
                    existingOperator.Vehicle = null;
                    Dirty(newOperator, existingOperator);
                }
            }

            if (!CanUseOperatorRelays(newOperator, entity.Owner))
                return false;

            if (oldOperator != newOperator && !TryBlockHands(entity, newOperator))
                return false;
        }

        if (oldOperator == uid)
        {
            if (uid is { } sameOperator)
            {
                SetOperatorComponent(sameOperator, entity.Owner);
                EnsureOperatorRelays(sameOperator, entity.Owner);
                RefreshCanRun((entity, entity.Comp));
            }

            return true;
        }

        if (oldOperator is not null)
            ClearOperator(entity, removeOperatorComponent: true, raiseSetEvent: false);

        entity.Comp.Operator = uid;

        if (uid is { } operatorUid)
        {
            SetOperatorComponent(operatorUid, entity.Owner);
            EnsureOperatorRelays(operatorUid, entity.Owner);

            var enterEvent = new OnVehicleEnteredEvent(entity, operatorUid);
            RaiseLocalEvent(operatorUid, ref enterEvent);
        }

        RefreshCanRun((entity, entity.Comp));

        var setEvent = new VehicleOperatorSetEvent(uid, oldOperator);
        RaiseLocalEvent(entity, ref setEvent);

        Dirty(entity);
        return true;
    }

    private void ClearOperator(Entity<VehicleComponent> entity, bool removeOperatorComponent, bool raiseSetEvent)
    {
        if (entity.Comp.Operator is not { } oldOperator)
            return;

        if (Exists(oldOperator))
        {
            var exitEvent = new OnVehicleExitedEvent(entity, oldOperator);
            RaiseLocalEvent(oldOperator, ref exitEvent);

            if (_operatorQuery.TryComp(oldOperator, out var operatorComponent) &&
                operatorComponent.Vehicle == entity.Owner)
            {
                operatorComponent.Vehicle = null;

                if (removeOperatorComponent)
                    RemCompDeferred<VehicleOperatorComponent>(oldOperator);
                else
                    Dirty(oldOperator, operatorComponent);
            }
        }

        UnblockHands(entity.Owner, oldOperator);
        CleanupOperatorRelays(oldOperator, entity.Owner);

        entity.Comp.Operator = null;
        RefreshCanRun((entity, entity.Comp));
        Dirty(entity);

        if (raiseSetEvent)
        {
            var setEvent = new VehicleOperatorSetEvent(null, oldOperator);
            RaiseLocalEvent(entity, ref setEvent);
        }
    }

    private void SetOperatorComponent(EntityUid operatorUid, EntityUid vehicleUid)
    {
        var vehicleOperator = EnsureComp<VehicleOperatorComponent>(operatorUid);
        vehicleOperator.Vehicle = vehicleUid;
        Dirty(operatorUid, vehicleOperator);
    }

    private bool CanUseOperatorRelays(EntityUid operatorUid, EntityUid vehicleUid)
    {
        if (operatorUid == vehicleUid)
            return false;

        if (_relayQuery.TryComp(operatorUid, out var relay) &&
            relay.RelayEntity != vehicleUid &&
            relay.RelayEntity != EntityUid.Invalid &&
            Exists(relay.RelayEntity))
        {
            return false;
        }

        if (_relayTargetQuery.TryComp(vehicleUid, out var relayTarget) &&
            relayTarget.Source != operatorUid &&
            relayTarget.Source != EntityUid.Invalid &&
            Exists(relayTarget.Source))
        {
            return false;
        }

        if (_interactionRelayQuery.TryComp(operatorUid, out var interactionRelay) &&
            interactionRelay.RelayEntity is { } interactionTarget &&
            interactionTarget != vehicleUid &&
            Exists(interactionTarget))
        {
            return false;
        }

        return true;
    }

    private void EnsureOperatorRelays(EntityUid operatorUid, EntityUid vehicleUid)
    {
        _mover.SetRelay(operatorUid, vehicleUid);

        if (!HasComp<ContainerVehicleComponent>(vehicleUid))
        {
            CleanupOperatorInteractionRelay(operatorUid, vehicleUid, cleanupDeletedRelays: true);
            return;
        }

        var interactionRelay = EnsureComp<InteractionRelayComponent>(operatorUid);
        _interaction.SetRelay(operatorUid, vehicleUid, interactionRelay);
    }

    private void CleanupOperatorRelays(EntityUid operatorUid, EntityUid? vehicleUid)
    {
        if (vehicleUid is not { } vehicle)
            return;

        if (_relayQuery.TryComp(operatorUid, out var relay) &&
            relay.RelayEntity == vehicle)
        {
            RemComp<RelayInputMoverComponent>(operatorUid);
        }

        CleanupOperatorInteractionRelay(operatorUid, vehicle);

        if (_relayTargetQuery.TryComp(vehicle, out var relayTarget) &&
            relayTarget.Source == operatorUid)
        {
            RemComp<MovementRelayTargetComponent>(vehicle);
        }
    }

    private void CleanupOperatorInteractionRelay(EntityUid operatorUid, EntityUid vehicleUid, bool cleanupDeletedRelays = false)
    {
        if (!_interactionRelayQuery.TryComp(operatorUid, out var interactionRelay) ||
            interactionRelay.RelayEntity is not { } relayEntity)
        {
            return;
        }

        if (relayEntity != vehicleUid && (!cleanupDeletedRelays || Exists(relayEntity)))
            return;

        _interaction.SetRelay(operatorUid, null, interactionRelay);
        RemComp<InteractionRelayComponent>(operatorUid);
    }

    /// <summary>
    /// Attempts to remove the current operator from a vehicle
    /// </summary>
    /// <param name="entity">The vehicle whose operator is being removed.</param>
    /// <returns>If the operator was removed successfully</returns>
    [PublicAPI]
    public bool TryRemoveOperator(Entity<VehicleComponent> entity)
    {
        return TrySetOperator(entity, null, removeExisting: true);
    }

    /// <summary>
    /// From an operator, removes it from the vehicle
    /// </summary>
    /// <param name="operatorEntity">The operator who is riding a vehicle</param>
    /// <returns>If the operator was removed successfully, or if the entity was not operating a vehicle.</returns>
    [PublicAPI]
    public bool TryRemoveOperator(Entity<VehicleOperatorComponent?> operatorEntity)
    {
        if (!Resolve(operatorEntity, ref operatorEntity.Comp, false))
            return true;

        if (!_vehicleQuery.TryComp(operatorEntity.Comp.Vehicle, out var vehicle))
        {
            if (operatorEntity.Comp.Vehicle is { } vehicleUid)
                UnblockHands(vehicleUid, operatorEntity.Owner);

            CleanupOperatorRelays(operatorEntity.Owner, operatorEntity.Comp.Vehicle);
            RemCompDeferred<VehicleOperatorComponent>(operatorEntity.Owner);
            return true;
        }

        return TrySetOperator((operatorEntity.Comp.Vehicle.Value, vehicle), null, removeExisting: true);
    }

    /// <summary>
    /// Attempts to get the current operator of a vehicle
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="operatorEnt"></param>
    [PublicAPI]
    public bool TryGetOperator(Entity<VehicleComponent?> entity, [NotNullWhen(true)] out Entity<VehicleOperatorComponent>? operatorEnt)
    {
        operatorEnt = null;
        if (!Resolve(entity, ref entity.Comp))
            return false;

        if (entity.Comp.Operator is not { } operatorUid)
            return false;

        if (!_operatorQuery.TryComp(operatorUid, out var operatorComponent))
            return false;

        operatorEnt = (operatorUid, operatorComponent);
        return true;
    }

    /// <summary>
    /// Returns the operator of the vehicle or none if there isn't one present
    /// </summary>
    public EntityUid? GetOperatorOrNull(Entity<VehicleComponent?> entity)
    {
        TryGetOperator(entity, out var operatorEnt);
        return operatorEnt;
    }

    /// <summary>
    /// Checks if the current vehicle has an operator.
    /// </summary>
    [PublicAPI]
    public bool HasOperator(Entity<VehicleComponent?> entity)
    {
        return TryGetOperator(entity, out _);
    }

    /// <summary>
    /// Checks if a given entity is capable of operating a vehicle.
    /// Note that the general ability for a vehicle to run (keys, fuel, etc.) is not checked here.
    /// This is *only* for checks on the user.
    /// </summary>
    public bool CanOperate(Entity<VehicleComponent?> entity, EntityUid uid)
    {
        if (!Exists(uid))
            return false;

        if (!Resolve(entity, ref entity.Comp))
            return false;

        if (_entityWhitelist.IsWhitelistFail(entity.Comp.OperatorWhitelist, uid))
            return false;

        if (entity.Comp.RequiresHands && (!_handsQuery.HasComp(uid) || !_actionBlocker.CanInteract(uid, entity)))
            return false;

        // DS14-start
        if (entity.Comp.RequiresHands &&
            _handBlockerQuery.TryComp(entity, out var handBlocker) &&
            (!_handsQuery.TryComp(uid, out var hands) ||
             _hands.GetHandCount((uid, hands)) < handBlocker.BlockedHands))
            return false;
        // DS14-end

        return _actionBlocker.CanConsciouslyPerformAction(uid);
    }

    /// <summary>
    /// Attempts to occupy the configured number of the operator's hands.
    /// </summary>
    private bool TryBlockHands(Entity<VehicleComponent> vehicle, EntityUid operatorUid)
    {
        if (!vehicle.Comp.RequiresHands || !_handBlockerQuery.TryComp(vehicle, out var handBlocker)) // DS14
            return true;

        if (_virtualItem.TrySpawnUnremoveableVirtualItemInHand(
                vehicle.Owner,
                operatorUid,
                handBlocker.BlockedHands,
                dropOthers: true))
            return true;

        UnblockHands(vehicle.Owner, operatorUid);
        return false;
    }

    /// <summary>
    /// Removes hand blockers owned by the specified vehicle.
    /// </summary>
    private void UnblockHands(EntityUid vehicleUid, EntityUid operatorUid)
    {
        _virtualItem.DeleteInHandsMatching(operatorUid, vehicleUid);
    }

    /// <summary>
    /// Checks if the vehicle is capable of running (has keys, fuel, etc.) and caches the value.
    /// Updates the appearance data.
    /// </summary>
    public void RefreshCanRun(Entity<VehicleComponent?> entity)
    {
        if (TerminatingOrDeleted(entity))
            return;

        if (!Resolve(entity, ref entity.Comp, false))
            return;

        _actionBlocker.UpdateCanMove(entity);
        UpdateAppearance((entity, entity.Comp));
    }

    private void UpdateAppearance(Entity<VehicleComponent> entity)
    {
        if (!_appearanceQuery.TryComp(entity, out var appearance))
            return;

        if (_inputMoverQuery.TryComp(entity, out var inputMover))
        {
            _appearance.SetData(entity, VehicleVisuals.CanRun, inputMover.CanMove, appearance);
        }

        _appearance.SetData(entity, VehicleVisuals.HasOperator, entity.Comp.Operator is not null, appearance);
    }
    // DS14-start
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _operatorMeleeVehicleHits.Clear();

        var query = EntityQueryEnumerator<VehicleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var vehicle, out var xform))
        {
            if (vehicle.Operator is not { } operatorUid)
                continue;

            if (!_inputMoverQuery.TryComp(uid, out var mover))
                continue;

            var currentPos = xform.Coordinates;
            var wishDir = _mover.GetWishDir((uid, mover));

            if (wishDir == Vector2.Zero)
            {
                vehicle.MovementSoundLastPosition = currentPos;
                continue;
            }

            if (!CanVehicleRun((uid, vehicle)))
            {
                TryPopupCannotRun((uid, vehicle), operatorUid);
                vehicle.MovementSoundLastPosition = currentPos;
                continue;
            }

            if (vehicle.MovementSound == null)
                continue;

            // Only accumulate distance when the operator is actively giving input.
            if (vehicle.MovementSoundLastPosition != null
                && currentPos.TryDistance(EntityManager, vehicle.MovementSoundLastPosition.Value, out var distance))
            {
                vehicle.MovementSoundAccumulatedDistance += distance;

                if (vehicle.MovementSoundAccumulatedDistance >= vehicle.MovementSoundDistance)
                {
                    vehicle.MovementSoundAccumulatedDistance -= vehicle.MovementSoundDistance;
                    _audio.PlayPredicted(vehicle.MovementSound, uid, vehicle.Operator.Value);
                }
            }

            vehicle.MovementSoundLastPosition = currentPos;
        }
    }
    private void OnVehicleOperatorSet(Entity<VehicleComponent> ent, ref VehicleOperatorSetEvent args)
    {
        ent.Comp.MovementSoundLastPosition = null;
        ent.Comp.MovementSoundAccumulatedDistance = 0f;
    }

    private void OnVehicleAttacked(Entity<VehicleComponent> ent, ref AttackedEvent args)
    {
        if (ent.Comp.Operator == args.User)
            _operatorMeleeVehicleHits.Add((ent.Owner, args.User));
    }

    private bool CanVehicleRun(Entity<VehicleComponent> ent)
    {
        var ev = new VehicleCanRunEvent(ent);
        RaiseLocalEvent(ent, ref ev);
        return ev.CanRun;
    }

    private void TryPopupCannotRun(Entity<VehicleComponent> ent, EntityUid operatorUid)
    {
        if (!IsMissingRequiredKey(ent.Owner))
            return;

        if (_timing.CurTime < ent.Comp.NextNoKeyPopup)
            return;

        ent.Comp.NextNoKeyPopup = _timing.CurTime + TimeSpan.FromSeconds(2);
        _popup.PopupPredicted(
            Loc.GetString("vehicle-no-key"),
            null,
            ent.Owner,
            operatorUid,
            PopupType.SmallCaution
        );
    }

    private void OnOperatorUpdateCanMove(Entity<VehicleOperatorComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.Vehicle is not { } vehicleUid)
            return;

        if (!_vehicleQuery.TryComp(vehicleUid, out var vehicle))
        {
            if (!_timing.ApplyingState)
            {
                CleanupOperatorRelays(ent.Owner, ent.Comp.Vehicle);
                RemCompDeferred<VehicleOperatorComponent>(ent.Owner);
            }

            return;
        }

        if (!CanVehicleRun((vehicleUid, vehicle)))
            args.Cancel();
    }

    private void OnOperatorMobStateChanged(Entity<VehicleOperatorComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            TryRemoveOperator(ent.AsNullable());
    }

    private void OnStartCollide(Entity<VehicleComponent> ent, ref StartCollideEvent args)
    {
        if (!_net.IsServer)
            return;

        if (TerminatingOrDeleted(ent))
            return;

        if (ent.Comp.BreakOnCollideWith.Count == 0)
            return;

        var otherProto = MetaData(args.OtherEntity).EntityPrototype?.ID;
        if (string.IsNullOrEmpty(otherProto))
            return;

        foreach (var breakProto in ent.Comp.BreakOnCollideWith)
        {
            if (breakProto.Id == otherProto)
            {
                QueueDel(ent.Owner);
                return;
            }
        }
    }
    // DS14-end
}
