using Content.Shared.Alert;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Standing;
using Content.Shared.Gravity;
using Content.Shared.Stunnable;
using Robust.Shared.Timing;

namespace Content.Shared.Movement.Systems;

/// <summary>
/// This handles the worm component
/// </summary>
public sealed partial class WormSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    // DS14-start
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    // DS14-end

    public override void Initialize()
    {
        SubscribeLocalEvent<WormComponent, StandUpAttemptEvent>(OnStandAttempt);
        SubscribeLocalEvent<WormComponent, KnockedDownRefreshEvent>(OnKnockedDownRefresh);
        SubscribeLocalEvent<WormComponent, RejuvenateEvent>(OnRejuvenate);
        // DS14-start
        SubscribeLocalEvent<WormComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WormComponent, WeightlessnessChangedEvent>(OnWeightlessnessChanged);
        SubscribeLocalEvent<WormComponent, ComponentShutdown>(OnShutdown);
        // DS14-end
    }

    private void OnStartup(Entity<WormComponent> ent, ref ComponentStartup args) // DS14
    {
        EnsureComp<KnockedDownComponent>(ent, out var knocked);
        _alerts.ShowAlert(ent.Owner, SharedStunSystem.KnockdownAlert);
        _stun.SetAutoStand((ent, knocked));
        // DS14-start
        _standing.Down(ent.Owner, playSound: false, dropHeldItems: false, force: true);
        RefreshMovement(ent.Owner);
        // DS14-end
    }

    private void OnRejuvenate(Entity<WormComponent> ent, ref RejuvenateEvent args)
    {
        RemComp<WormComponent>(ent);
    }

    private void OnStandAttempt(Entity<WormComponent> ent, ref StandUpAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancelled = true;
        args.Message = (Loc.GetString("worm-component-stand-attempt"), PopupType.SmallCaution);
        args.Autostand = false;
    }

    private void OnKnockedDownRefresh(Entity<WormComponent> ent, ref KnockedDownRefreshEvent args)
    {
        args.FrictionModifier *= ent.Comp.FrictionModifier;
        args.SpeedModifier *= ent.Comp.SpeedModifier;
    }
    // DS14-start
    private void OnWeightlessnessChanged(Entity<WormComponent> ent, ref WeightlessnessChangedEvent args)
    {
        if (args.Weightless)
            return;

        if (_timing.ApplyingState)
            return;

        if (TerminatingOrDeleted(ent))
            return;

        EnsureComp<KnockedDownComponent>(ent, out var knocked);
        _stun.SetAutoStand((ent, knocked), false);
        _standing.Down(ent.Owner, playSound: false, dropHeldItems: false, force: true);
        RefreshMovement(ent.Owner);
    }

    private void RefreshMovement(EntityUid uid)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
        _movementSpeed.RefreshFrictionModifiers(uid);
    }

    private void OnShutdown(Entity<WormComponent> ent, ref ComponentShutdown args)
    {
        RemComp<KnockedDownComponent>(ent.Owner);
        _alerts.ClearAlert(ent.Owner, SharedStunSystem.KnockdownAlert);
    }
    // DS14-end
}
