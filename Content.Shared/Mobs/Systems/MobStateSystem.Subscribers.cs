using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle.Components;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Cuffs;
using Content.Shared.Damage;
using Content.Shared.Damage.ForceSay;
using Content.Shared.Damage.Systems;
using Content.Shared.Emoting;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.DeadSpace.Movement.Events;
using Content.Shared.Gravity;
using Content.Shared.Movement.Components; //DS14
using Content.Shared.DeadSpace.Movement.Components; // DS14
using Content.Shared.Stunnable;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Pointing;
using Content.Shared.Pulling.Events;
using Content.Shared.Speech;
using Content.Shared.Standing;
using Content.Shared.Strip.Components;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Misc;
using Robust.Shared.Physics.Components;

namespace Content.Shared.Mobs.Systems;

public partial class MobStateSystem
{
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly SharedJetpackSystem _jetpack = default!;

    //General purpose event subscriptions. If you can avoid it register these events inside their own systems
    private void SubscribeEvents()
    {
        SubscribeLocalEvent<MobStateComponent, BeforeGettingStrippedEvent>(OnGettingStripped);
        SubscribeLocalEvent<MobStateComponent, ChangeDirectionAttemptEvent>(CheckAct);
        SubscribeLocalEvent<MobStateComponent, UseAttemptEvent>(CheckAct);
        SubscribeLocalEvent<MobStateComponent, AttackAttemptEvent>(CheckAct);
        SubscribeLocalEvent<MobStateComponent, ConsciousAttemptEvent>(CheckConcious);
        SubscribeLocalEvent<MobStateComponent, ThrowAttemptEvent>(CheckAct);
        SubscribeLocalEvent<MobStateComponent, SpeakAttemptEvent>(OnSpeakAttempt);
        SubscribeLocalEvent<MobStateComponent, IsEquippingAttemptEvent>(OnEquipAttempt);
        SubscribeLocalEvent<MobStateComponent, EmoteAttemptEvent>(CheckAct);
        SubscribeLocalEvent<MobStateComponent, IsUnequippingAttemptEvent>(OnUnequipAttempt);
        SubscribeLocalEvent<MobStateComponent, DropAttemptEvent>(CheckAct);
        SubscribeLocalEvent<MobStateComponent, PickupAttemptEvent>(CheckAct);
        SubscribeLocalEvent<MobStateComponent, StartPullAttemptEvent>(CheckAct);
        SubscribeLocalEvent<MobStateComponent, UpdateCanMoveEvent>(CheckAct);
        SubscribeLocalEvent<MobStateComponent, StandAttemptEvent>(CheckAct);
        SubscribeLocalEvent<MobStateComponent, PointAttemptEvent>(CheckAct);
        SubscribeLocalEvent<MobStateComponent, TryingToSleepEvent>(OnSleepAttempt);
        SubscribeLocalEvent<MobStateComponent, CombatModeShouldHandInteractEvent>(OnCombatModeShouldHandInteract);
        SubscribeLocalEvent<MobStateComponent, AttemptPacifiedAttackEvent>(OnAttemptPacifiedAttack);
        SubscribeLocalEvent<MobStateComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<MobStateComponent, AttemptActivateJetpackHandledEvent>(OnJetpackAttempt);
        SubscribeLocalEvent<MobStateComponent, MoveInputEvent>(OnMoveInput);
        SubscribeLocalEvent<MobStateComponent, WeightlessnessChangedEvent>(OnWeightlessnessChanged);
        SubscribeLocalEvent<MobStateComponent, CanWeightlessMoveEvent>(OnCanWeightlessMove,
            after: [typeof(SharedJetpackSystem), typeof(MovementIgnoreGravitySystem), typeof(SharedGrapplingGunSystem)]);

        SubscribeLocalEvent<MobStateComponent, UnbuckleAttemptEvent>(OnUnbuckleAttempt);
        SubscribeLocalEvent<MobStateComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState); // DS14
        // DS14 - current engine baseline requires explicit event subscriptions.
        SubscribeLocalEvent<MobStateComponent, CheckIncapacitatedCuffEvent>(OnIncapCuffCheck);
    }

    private void OnUnbuckleAttempt(Entity<MobStateComponent> ent, ref UnbuckleAttemptEvent args)
    {
        // TODO is this necessary?
        // Shouldn't the interaction have already been blocked by a general interaction check?
        if (args.User == ent.Owner && IsIncapacitated(ent))
            args.Cancelled = true;
    }

    private void Down(EntityUid target)
    {
        _standing.Down(target);
        var ev = new DropHandItemsEvent();
        RaiseLocalEvent(target, ref ev);
    }

    // DS14-start
    private bool IsBuckledToDownStrap(EntityUid target)
    {
        if (!TryComp<BuckleComponent>(target, out var buckle) ||
            buckle.BuckledTo is not { } strapUid ||
            !TryComp<StrapComponent>(strapUid, out var strap))
        {
            return false;
        }

        return strap.Position == StrapPosition.Down;
    }
    // DS14-end

    private void CheckConcious(Entity<MobStateComponent> ent, ref ConsciousAttemptEvent args)
    {
        switch (ent.Comp.CurrentState)
        {
            case MobState.Dead:
            case MobState.Critical:
                args.Cancelled = true;
                break;
        }
    }

    private void OnStateExitSubscribers(EntityUid target, MobStateComponent component, MobState state)
    {
        switch (state)
        {
            case MobState.Alive:
                //unused
                break;
            case MobState.Critical:
                _standing.Stand(target);
                break;
            case MobState.Dead:
                RemComp<CollisionWakeComponent>(target);
                _standing.Stand(target);
                break;
            case MobState.Invalid:
                //unused
                break;
            // DS14-start
            case MobState.PreCritical:
                if (!HasComp<WheelchairUserComponent>(target))
                {
                    RemComp<WormComponent>(target);
                    RemComp<KnockedDownComponent>(target);
                    if (IsBuckledToDownStrap(target))
                        _standing.Down(target, playSound: false, dropHeldItems: false, force: true);
                    else
                        _standing.Stand(target, force: true);
                }
                break;
            // DS14-end
            default:
                throw new NotImplementedException();
        }
    }

    private void OnStateEnteredSubscribers(EntityUid target, MobStateComponent component, MobState state)
    {
        // All of the state changes here should already be networked, so we do nothing if we are currently applying a
        // server state.
        if (_timing.ApplyingState)
            return;

        _blocker.UpdateCanMove(target); //update movement anytime a state changes
        switch (state)
        {
            case MobState.Alive:
            {
                if (!HasComp<WormComponent>(target))
                    _standing.Stand(target);
                _appearance.SetData(target, MobStateVisuals.State, MobState.Alive);
                break;
            }
            case MobState.Critical:
            {
                Down(target);
                _appearance.SetData(target, MobStateVisuals.State, MobState.Critical);
                break;
            }
            case MobState.Dead:
            {
                EnsureComp<CollisionWakeComponent>(target);
                Down(target);
                _appearance.SetData(target, MobStateVisuals.State, MobState.Dead);
                break;
            }
            case MobState.Invalid:
            {
                //unused;
                break;
            }
            // DS14-start
            case MobState.PreCritical:
                DisableJetpack(target);
                ClearWeightlessMoveInput(target);
                EnsureComp<WormComponent>(target);
                _standing.Down(target);
                _appearance.SetData(target, MobStateVisuals.State, MobState.PreCritical);
                break;
            // DS14-end
            default:
            {
                throw new NotImplementedException();
            }
        }
    }

    #region Event Subscribers

    // DS14-start
    // Adapted to the explicit subscription model used by this branch.
    private void OnAfterAutoHandleState(Entity<MobStateComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.LastReceivedState == ent.Comp.CurrentState)
            return;

        var ev = new MobStateChangedEvent(ent, ent.Comp, ent.Comp.LastReceivedState, ent.Comp.CurrentState);
        OnStateChanged(ent, ent.Comp, ent.Comp.LastReceivedState, ent.Comp.CurrentState);
        RaiseLocalEvent(ent, ev, true);
        ent.Comp.LastReceivedState = ent.Comp.CurrentState;
    }
    // DS14-end

    private void OnSleepAttempt(EntityUid target, MobStateComponent component, ref TryingToSleepEvent args)
    {
        if (IsDead(target, component))
            args.Cancelled = true;
    }

    private void OnGettingStripped(EntityUid target, MobStateComponent component, BeforeGettingStrippedEvent args)
    {
        // Incapacitated or dead targets get stripped two or three times as fast. Makes stripping corpses less tedious.
        if (IsDead(target, component))
            args.Multiplier /= 3;
        else if (IsCritical(target, component))
            args.Multiplier /= 2;
    }

    private void OnSpeakAttempt(EntityUid uid, MobStateComponent component, SpeakAttemptEvent args)
    {
        if (HasComp<AllowNextCritSpeechComponent>(uid))
        {
            RemCompDeferred<AllowNextCritSpeechComponent>(uid);
            return;
        }

        CheckAct(uid, component, args);
    }

    private void CheckAct(EntityUid target, MobStateComponent component, CancellableEntityEventArgs args)
    {
        switch (component.CurrentState)
        {
            case MobState.Dead:
            case MobState.Critical:
                args.Cancel();
                break;
        }
    }

    private void OnEquipAttempt(EntityUid target, MobStateComponent component, IsEquippingAttemptEvent args)
    {
        // is this a self-equip, or are they being stripped?
        if (args.Equipee == target)
            CheckAct(target, component, args);
    }

    private void OnUnequipAttempt(EntityUid target, MobStateComponent component, IsUnequippingAttemptEvent args)
    {
        // is this a self-equip, or are they being stripped?
        if (args.Unequipee == target)
            CheckAct(target, component, args);
    }

    private void OnCombatModeShouldHandInteract(EntityUid uid, MobStateComponent component, ref CombatModeShouldHandInteractEvent args)
    {
        // Disallow empty-hand-interacting in combat mode
        // for non-dead mobs
        if (!IsDead(uid, component))
            args.Cancelled = true;
    }

    private void OnAttemptPacifiedAttack(Entity<MobStateComponent> ent, ref AttemptPacifiedAttackEvent args)
    {
        args.Cancelled = true;
    }

    private void OnDamageModify(Entity<MobStateComponent> ent, ref DamageModifyEvent args)
    {
        args.Damage *= _damageable.UniversalMobDamageModifier;
    }

    private void OnJetpackAttempt(Entity<MobStateComponent> ent, ref AttemptActivateJetpackHandledEvent args)
    {
        if (!args.Enabled || ent.Comp.CurrentState != MobState.PreCritical)
            return;

        args.Handled = true;
    }

    private void OnMoveInput(Entity<MobStateComponent> ent, ref MoveInputEvent args)
    {
        if (ent.Comp.CurrentState != MobState.PreCritical ||
            !_gravity.IsWeightless(ent.Owner))
        {
            return;
        }

        ClearDirectionalMoveInput(args.Entity);
    }

    private void OnWeightlessnessChanged(Entity<MobStateComponent> ent, ref WeightlessnessChangedEvent args)
    {
        // DS14-start
        if (ent.Comp.CurrentState != MobState.PreCritical)
            return;

        if (_timing.ApplyingState)
            return;

        if (args.Weightless)
        {
            DisableJetpack(ent.Owner);
            ClearWeightlessMoveInput(ent.Owner);
        }
        else if (HasComp<WormComponent>(ent.Owner))
        {
            _standing.Down(ent.Owner);
        }
        // DS14-end
    }

    private void OnCanWeightlessMove(Entity<MobStateComponent> ent, ref CanWeightlessMoveEvent args)
    {
        if (ent.Comp.CurrentState == MobState.PreCritical)
            args.CanMove = false;
    }

    private void DisableJetpack(EntityUid uid)
    {
        if (!TryComp<JetpackUserComponent>(uid, out var user) ||
            !TryComp<JetpackComponent>(user.Jetpack, out var jetpack))
        {
            return;
        }

        _jetpack.SetEnabled(user.Jetpack, jetpack, false, uid);
    }

    private void ClearWeightlessMoveInput(EntityUid uid)
    {
        if (!_gravity.IsWeightless(uid) ||
            !TryComp<InputMoverComponent>(uid, out var mover))
        {
            return;
        }

        ClearDirectionalMoveInput((uid, mover));
    }

    private void ClearDirectionalMoveInput(Entity<InputMoverComponent> ent)
    {
        var movement = ent.Comp.HeldMoveButtons;
        var newMovement = movement & ~MoveButtons.AnyDirection;

        if (movement == newMovement)
            return;

        ent.Comp.HeldMoveButtons = newMovement;
        Dirty(ent.Owner, ent.Comp);
    }

    private void OnIncapCuffCheck(Entity<MobStateComponent> ent, ref CheckIncapacitatedCuffEvent args)
    {
        if (IsIncapacitated(ent, ent.Comp))
            args.Incapacitated = true;
    }

    #endregion
}
