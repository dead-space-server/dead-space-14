using Content.Server.Pinpointer;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.Components;
using Content.Shared.Construction;
using Content.Shared.Destructible;
using Content.Shared.Lock;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Radio.EntitySystems;

/// <summary>
/// System for sending radio notification upon entity becoming
/// non-functioning - unpowered / deconstructed / destroyed.
/// </summary>
// DS14 - pre-v288 engine
public sealed class NotifyOnNonFunctioningSystem : EntitySystem
{
    // DS14-start: readonly dependencies and explicit subscriptions for the pre-v288 engine.
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly PowerStateSystem _powerState = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NotifyOnNonFunctioningComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<NotifyOnNonFunctioningComponent, MachineDeconstructedEvent>(OnDeconstructed);
        SubscribeLocalEvent<NotifyOnNonFunctioningComponent, LockToggledEvent>(OnLockToggled);
        SubscribeLocalEvent<NotifyOnNonFunctioningComponent, PowerStateChanged>(OnIsWorkingChanges);
        SubscribeLocalEvent<NotifyOnNonFunctioningComponent, AnchorStateChangedEvent>(OnAnchorStateChanged);
        SubscribeLocalEvent<NotifyOnNonFunctioningComponent, PowerConsumerReceivedChanged>(ReceivedChanged);
    }
    // DS14-end

    /// <summary> Notify on entity destruction. </summary>
    private void OnDestruction(Entity<NotifyOnNonFunctioningComponent> ent, ref DestructionEventArgs args)
    {
        if (ent.Comp.LocDestroyed.HasValue)
            AlertRadioIfWasWorking(ent, ent.Comp.LocDestroyed);
    }

    /// <summary> Notify on deconstruction. </summary>
    private void OnDeconstructed(Entity<NotifyOnNonFunctioningComponent> ent, ref MachineDeconstructedEvent args)
    {
        if (ent.Comp.LocDeconstructed.HasValue)
            AlertRadioIfWasWorking(ent, ent.Comp.LocDeconstructed);
    }

    /// <summary> Notify on unlocking already locked entity. </summary>
    private void OnLockToggled(Entity<NotifyOnNonFunctioningComponent> ent, ref LockToggledEvent args)
    {
        if (args.Locked || !ent.Comp.LocUnlocked.HasValue)
            return;

        AlertRadioIfWasWorking(ent, ent.Comp.LocUnlocked);
    }

    /// <summary> Notify on turning off. </summary>
    private void OnIsWorkingChanges(Entity<NotifyOnNonFunctioningComponent> ent, ref PowerStateChanged args)
    {
        // deleted entity is working change should be handled during other events
        if (args.IsWorking || !ent.Comp.LocTurnedOff.HasValue || TerminatingOrDeleted(ent))
            return;

        AlertRadio(ent, ent.Comp.LocTurnedOff);
    }

    /// <summary> Notify on unanchoring. </summary>
    private void OnAnchorStateChanged(Entity<NotifyOnNonFunctioningComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored || !ent.Comp.LocUnanchored.HasValue || TerminatingOrDeleted(ent))
            return;

        AlertRadioIfWasWorking(ent, ent.Comp.LocUnanchored);
    }

    private void ReceivedChanged(Entity<NotifyOnNonFunctioningComponent> ent, ref PowerConsumerReceivedChanged args)
    {
        if (!ent.Comp.LocUnpowered.HasValue || !_powerState.GetWorkingState(ent.Owner))
            return;

        if (args.ReceivedPower >= args.DrawRate || _gameTiming.CurTime < ent.Comp.NextUnpoweredAlert)
            return;

        // DS14-start - unstable power must not spam the engineering radio.
        ent.Comp.NextUnpoweredAlert = _gameTiming.CurTime + ent.Comp.UnpoweredAlertCooldown;
        AlertRadioIfWasWorking(ent, ent.Comp.LocUnpowered, true);
        // DS14-end
    }

    private void AlertRadioIfWasWorking(Entity<NotifyOnNonFunctioningComponent> ent, string locString, bool ignorePower = false)
    {

        if (!_powerState.GetWorkingState(ent.Owner))
            return;

        AlertRadio(ent, locString, ignorePower);
    }

    private void AlertRadio(Entity<NotifyOnNonFunctioningComponent> ent, string locString, bool ignorePower = false)
    {
        if (ent.Comp.RequirePowered && !ignorePower)
        {
            if (TryComp<ApcPowerReceiverComponent>(ent, out var apc) && !apc.Powered)
                return;

            if (TryComp<PowerConsumerComponent>(ent, out var consumer) && consumer.DrawRate > consumer.ReceivedPower)
                return;
        }

        var locationInfo = FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString(ent.Owner));
        var message = Loc.GetString(locString, ("location", locationInfo));
        _radio.SendRadioMessage(ent.Owner, message, ent.Comp.RadioChannel, ent.Owner);
    }
}
