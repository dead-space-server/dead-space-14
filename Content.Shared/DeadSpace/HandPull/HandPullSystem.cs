using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;

namespace Content.Shared.DeadSpace.HandPull;

public sealed class HandPullSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandPullerComponent, ComponentStartup>(OnPullerStartup);
        SubscribeLocalEvent<HandPullerComponent, ComponentShutdown>(OnPullerShutdown);
        SubscribeLocalEvent<HandPullerComponent, PullStoppedMessage>(OnPullerPullStopped);
        SubscribeLocalEvent<HandPullerComponent, MobStateChangedEvent>(OnPullerMobStateChanged);

        SubscribeLocalEvent<HandPulledComponent, PullStoppedMessage>(OnPulledPullStopped);
        SubscribeLocalEvent<HandPulledComponent, MobStateChangedEvent>(OnPulledMobStateChanged);
    }

    private void OnPullerStartup(Entity<HandPullerComponent> ent, ref ComponentStartup args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(ent);
    }

    private void OnPullerShutdown(Entity<HandPullerComponent> ent, ref ComponentShutdown args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(ent);
    }

    private void OnPullerPullStopped(EntityUid uid, HandPullerComponent component, PullStoppedMessage args)
    {
        Stop((uid, component));
    }

    private void OnPulledPullStopped(EntityUid uid, HandPulledComponent component, PullStoppedMessage args)
    {
        Stop(component.Puller);
    }

    private void OnPullerMobStateChanged(Entity<HandPullerComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            Stop(ent.AsNullable());
    }

    private void OnPulledMobStateChanged(Entity<HandPulledComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            Stop(ent.Comp.Puller);
    }

    public bool IsHandPulling(EntityUid puller, EntityUid target)
    {
        return TryComp<HandPullerComponent>(puller, out var comp) && comp.Target == target;
    }

    public bool CanHandPull(EntityUid puller, EntityUid target)
    {
        if (puller == target ||
            _mobState.IsIncapacitated(puller) ||
            _mobState.IsIncapacitated(target) ||
            !HasComp<PullableComponent>(target))
        {
            return false;
        }

        if (TryComp<PullerComponent>(target, out var targetPuller) && targetPuller.Pulling == puller)
            return false;

        return _pulling.CanPull(puller, target);
    }

    public bool TryStart(EntityUid puller, EntityUid target)
    {
        if (IsHandPulling(puller, target))
            return true;

        if (!CanHandPull(puller, target) ||
            !TryComp<PullableComponent>(target, out var pullable))
        {
            return false;
        }

        if (pullable.Puller != puller &&
            !_pulling.TryStartPull(puller, target, pullableComp: pullable))
        {
            return false;
        }

        Stop(puller);
        if (TryComp<HandPulledComponent>(target, out var oldPulled))
            Stop(oldPulled.Puller);

        var pulled = EnsureComp<HandPulledComponent>(target);
        pulled.Puller = puller;
        Dirty(target, pulled);

        var handPuller = EnsureComp<HandPullerComponent>(puller);
        handPuller.Target = target;
        Dirty(puller, handPuller);

        _movementSpeed.RefreshMovementSpeedModifiers(puller);
        return true;
    }

    public void Stop(Entity<HandPullerComponent?> puller)
    {
        if (!Resolve(puller, ref puller.Comp, false))
            return;

        var target = puller.Comp.Target;
        RemComp(puller, puller.Comp);

        if (TryComp<HandPulledComponent>(target, out var pulled) && pulled.Puller == puller.Owner)
            RemComp(target, pulled);
    }
}
