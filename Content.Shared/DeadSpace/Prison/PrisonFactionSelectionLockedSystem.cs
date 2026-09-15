using Content.Shared.ActionBlocker;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Throwing;

namespace Content.Shared.DeadSpace.Prison;

public sealed class PrisonFactionSelectionLockedSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PrisonFactionSelectionLockedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PrisonFactionSelectionLockedComponent, ComponentShutdown>(OnComponentChanged);
        SubscribeLocalEvent<PrisonFactionSelectionLockedComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<PrisonFactionSelectionLockedComponent, UseAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<PrisonFactionSelectionLockedComponent, PickupAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<PrisonFactionSelectionLockedComponent, DropAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<PrisonFactionSelectionLockedComponent, ThrowAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<PrisonFactionSelectionLockedComponent, AttackAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<PrisonFactionSelectionLockedComponent, ChangeDirectionAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<PrisonFactionSelectionLockedComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<PrisonFactionSelectionLockedComponent, PullAttemptEvent>(OnPullAttempt);
    }

    private void OnStartup(
        EntityUid uid,
        PrisonFactionSelectionLockedComponent component,
        ComponentStartup args)
    {
        if (TryComp<PullableComponent>(uid, out var pullable))
            _pulling.TryStopPull(uid, pullable);

        _blocker.UpdateCanMove(uid);
    }

    private void OnComponentChanged(
        EntityUid uid,
        PrisonFactionSelectionLockedComponent component,
        EntityEventArgs args)
    {
        _blocker.UpdateCanMove(uid);
    }

    private void OnUpdateCanMove(
        EntityUid uid,
        PrisonFactionSelectionLockedComponent component,
        UpdateCanMoveEvent args)
    {
        if (component.LifeStage <= ComponentLifeStage.Running)
            args.Cancel();
    }

    private void OnAttempt(
        EntityUid uid,
        PrisonFactionSelectionLockedComponent component,
        CancellableEntityEventArgs args)
    {
        args.Cancel();
    }

    private void OnInteractionAttempt(
        Entity<PrisonFactionSelectionLockedComponent> ent,
        ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnPullAttempt(
        EntityUid uid,
        PrisonFactionSelectionLockedComponent component,
        PullAttemptEvent args)
    {
        args.Cancelled = true;
    }
}
