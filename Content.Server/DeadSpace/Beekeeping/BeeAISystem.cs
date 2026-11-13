// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Husbandry.Walking;
using Content.Server.NPC.HTN;
using Content.Shared.DeadSpace.Beekeeping;
using Content.Shared.DeadSpace.Husbandry.Walking;
using Content.Shared.CCVar;
using Content.Shared.DoAfter;
using Content.Shared.Ghost;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Beekeeping;

/// <summary>
/// The brain of a worker bee, see <see cref="BeeComponent"/>. The cycle: once a second the bee looks for a plant to
/// pollinate within <see cref="BeeComponent.SearchRadius"/>, flies to it, pollinates it and flies straight home with
/// the pollen, which it leaves in the hive.
///
/// The bee flies in a straight line (the <c>AnimalWalker</c> of the husbandry module sets its input), there is no
/// pathfinding and no steering, nothing that could leave it standing. What keeps it cheap on a server with many players:
/// <list type="bullet">
/// <item>a bee thinks twice a second, not every tick;</item>
/// <item>a bee that has no player within <see cref="BeeComponent.ActiveRange"/> does nothing at all (its brain is off), and
/// the hive makes up for it while the farm is on, see <see cref="BeeHiveSystem"/>;</item>
/// <item>plants are found with a lookup around the bee, not by going through every plant of the round.</item>
/// </list>
/// </summary>
public sealed class BeeAISystem : EntitySystem
{
    [Dependency] private readonly AnimalWalkerSystem _walker = default!;
    [Dependency] private readonly BeeHiveSystem _hive = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PollinationSystem _pollination = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<PollinationComponent>> _flowers = new();
    private readonly List<EntityCoordinates> _players = new();
    private TimeSpan _nextPlayerScan;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BeeComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BeeComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BeeComponent, BeePollinatingDoAfterEvent>(OnPollinatingDoAfter);
    }

    private void OnStartup(Entity<BeeComponent> ent, ref ComponentStartup args)
    {
        // The usual brain is taken off: two brains would fight over the movement. (The prototype of the worker still
        // inherits HTN from the base of simple mobs, and a prototype cannot take a parent component away.)
        RemComp<HTNComponent>(ent);
        ent.Comp.NextThink = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(0f, ent.Comp.ThinkInterval));

        if (ent.Comp.HiveOwner is { } hive && TryComp<BeeHiveComponent>(hive, out var hiveComp))
            hiveComp.Bees.Add(ent);
    }

    private void OnShutdown(Entity<BeeComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.HiveOwner is { } hive && TryComp<BeeHiveComponent>(hive, out var hiveComp))
            hiveComp.Bees.Remove(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BeeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var bee, out var xform))
        {
            if (now < bee.NextThink)
                continue;

            bee.NextThink = now + TimeSpan.FromSeconds(bee.ThinkInterval);

            if (_mobState.IsIncapacitated(uid))
                continue;

            // Nobody sees the bee: its brain is off, it stays where it is and costs nothing. The hive makes up for what it
            // would have brought, see BeeHiveSystem.
            if (!IsPlayerNear(xform.Coordinates, bee.ActiveRange))
            {
                StopFlying((uid, bee));
                continue;
            }

            if (bee.IsBusy)
                continue;

            bee.StateTimer += bee.ThinkInterval;
            var ent = (uid, bee);

            switch (bee.State)
            {
                case BeeState.Idle: TickIdle(ent); break;
                case BeeState.SearchingFlower: TickSearching(ent); break;
                case BeeState.MovingToFlower: TickMoving(ent); break;
                case BeeState.Pollinating: TickPollinating(ent, xform); break;
                case BeeState.ReturningToHive: TickReturning(ent); break;
            }
        }
    }

    /// <summary>
    /// Whether a player (not a ghost) is within the range of the coordinates. The positions of the players are taken
    /// every <c>husbandry.wander.player_scan_interval</c> seconds, whatever the number of callers.
    /// </summary>
    public bool IsPlayerNear(EntityCoordinates coordinates, float range)
    {
        var now = _timing.CurTime;
        if (now >= _nextPlayerScan)
        {
            _nextPlayerScan = now + TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.HusbandryPlayerScanInterval));
            _players.Clear();

            var players = EntityQueryEnumerator<ActorComponent, TransformComponent>();
            while (players.MoveNext(out var uid, out _, out var xform))
            {
                if (!HasComp<GhostComponent>(uid))
                    _players.Add(xform.Coordinates);
            }
        }

        foreach (var player in _players)
        {
            if (coordinates.TryDistance(EntityManager, player, out var distance) && distance <= range)
                return true;
        }

        return false;
    }

    private void StopFlying(Entity<BeeComponent> ent)
    {
        if (TryComp<AnimalWalkerComponent>(ent, out var walker))
            _walker.Stop((ent, walker));
    }

    private void SetState(Entity<BeeComponent> ent, BeeState state)
    {
        var bee = ent.Comp;
        if (bee.State == state)
            return;

        if (bee.State == BeeState.Pollinating)
            CancelDoAfters(ent);

        // A new state starts a new flight, or none.
        StopFlying(ent);

        bee.State = state;
        bee.StateTimer = 0f;
    }

    /// <summary>
    /// Tells the bee to go home at once, for a hive that lost its queen.
    /// </summary>
    public void RecallToHive(Entity<BeeComponent> ent)
    {
        if (ent.Comp.State is BeeState.Idle or BeeState.ReturningToHive)
            return;

        ent.Comp.TargetFlower = null;
        SetState(ent, BeeState.ReturningToHive);
    }

    private void CancelDoAfters(Entity<BeeComponent> ent)
    {
        if (TryComp<DoAfterComponent>(ent, out var doAfters))
        {
            foreach (var doAfter in doAfters.DoAfters.Values)
            {
                if (!doAfter.Cancelled && !doAfter.Completed)
                    _doAfter.Cancel(doAfter.Id);
            }
        }

        ent.Comp.IsBusy = false;
    }

    private bool HasValidHive(Entity<BeeComponent> ent)
    {
        if (ent.Comp.HiveOwner is { } hive && !TerminatingOrDeleted(hive))
            return true;

        SetState(ent, BeeState.Idle);
        return false;
    }

    private bool HasValidFlower(Entity<BeeComponent> ent)
    {
        if (ent.Comp.TargetFlower is { } flower && !TerminatingOrDeleted(flower))
            return true;

        ent.Comp.TargetFlower = null;
        SetState(ent, BeeState.ReturningToHive);
        return false;
    }

    private float DistanceTo(EntityUid target, TransformComponent xform)
    {
        return (_transform.GetWorldPosition(target) - _transform.GetWorldPosition(xform)).Length();
    }

    /// <summary>
    /// Sends the bee to the target in a straight line, or lets it go on if it is on its way.
    /// </summary>
    /// <returns>The state of the flight: arrived, still flying, or failed (it did not get anywhere).</returns>
    private WalkStatus Fly(Entity<BeeComponent> ent, EntityUid target)
    {
        if (!TryComp<AnimalWalkerComponent>(ent, out var walker))
            return WalkStatus.Failed;

        if (walker.Status == WalkStatus.Walking)
            return WalkStatus.Walking;

        if (walker.Status is WalkStatus.Arrived or WalkStatus.Failed)
        {
            var status = walker.Status;
            _walker.Stop((ent, walker));
            return status;
        }

        var destination = new EntityCoordinates(target, Vector2.Zero);
        return _walker.StartRoute((ent, walker), new[] { destination }, ent.Comp.ArrivalThreshold, run: true)
            ? WalkStatus.Walking
            : WalkStatus.Failed;
    }

    private void TickIdle(Entity<BeeComponent> ent)
    {
        if (ent.Comp.StateTimer < ent.Comp.IdleCooldown || !HasValidHive(ent))
            return;

        // The bees stay at home while the hive has no queen.
        if (!_hive.IsActive(ent.Comp.HiveOwner!.Value))
            return;

        SetState(ent, BeeState.SearchingFlower);
    }

    private void TickSearching(Entity<BeeComponent> ent)
    {
        var bee = ent.Comp;
        if (!HasValidHive(ent))
            return;

        // Everything is counted from the hive, not from where the bee is: the bees of a hive all work the same
        // ground, whatever the order they set out in.
        var hive = bee.HiveOwner!.Value;
        var hiveCoordinates = Transform(hive).Coordinates;
        var origin = _transform.GetWorldPosition(hive);
        EntityUid? best = null;
        var bestDistance = float.MaxValue;

        _flowers.Clear();
        _lookup.GetEntitiesInRange(hiveCoordinates, bee.SearchRadius, _flowers);

        foreach (var flower in _flowers)
        {
            // A plant that another bee of the hive is already on its way to is left to it.
            if (!_pollination.CanPollinate(flower) || IsClaimed(ent, flower))
                continue;

            var distance = (_transform.GetWorldPosition(flower) - origin).Length();
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = flower;
            }
        }

        if (best == null)
        {
            SetState(ent, BeeState.Idle);
            return;
        }

        bee.TargetFlower = best;
        SetState(ent, BeeState.MovingToFlower);
    }

    private bool IsClaimed(Entity<BeeComponent> ent, EntityUid flower)
    {
        if (!TryComp<BeeHiveComponent>(ent.Comp.HiveOwner, out var hive))
            return false;

        foreach (var other in hive.Bees)
        {
            if (other != ent.Owner && TryComp<BeeComponent>(other, out var otherBee) && otherBee.TargetFlower == flower)
                return true;
        }

        return false;
    }

    private void TickMoving(Entity<BeeComponent> ent)
    {
        var bee = ent.Comp;
        if (!HasValidFlower(ent))
            return;

        // The flower may have been pollinated by another bee while this one was on its way.
        var flower = bee.TargetFlower!.Value;
        if (!TryComp<PollinationComponent>(flower, out var pollination) || !_pollination.CanPollinate((flower, pollination)))
        {
            bee.TargetFlower = null;
            SetState(ent, BeeState.ReturningToHive);
            return;
        }

        switch (Fly(ent, flower))
        {
            case WalkStatus.Arrived:
                SetState(ent, BeeState.Pollinating);
                break;
            case WalkStatus.Failed:
                bee.TargetFlower = null;
                SetState(ent, BeeState.ReturningToHive);
                break;
            default:
                if (bee.StateTimer > bee.MaxMovingTime)
                {
                    bee.TargetFlower = null;
                    SetState(ent, BeeState.ReturningToHive);
                }

                break;
        }
    }

    private void TickPollinating(Entity<BeeComponent> ent, TransformComponent xform)
    {
        var bee = ent.Comp;
        if (!HasValidFlower(ent))
            return;

        var flower = bee.TargetFlower!.Value;
        if (DistanceTo(flower, xform) > bee.ArrivalThreshold * 3f ||
            !TryComp<PollinationComponent>(flower, out var pollination) ||
            !_pollination.CanPollinate((flower, pollination)))
        {
            SetState(ent, BeeState.ReturningToHive);
            return;
        }

        bee.IsBusy = true;

        var args = new DoAfterArgs(EntityManager, ent, TimeSpan.FromSeconds(bee.PollinatingDuration), new BeePollinatingDoAfterEvent(), ent, flower)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            BlockDuplicate = true,
        };

        if (!_doAfter.TryStartDoAfter(args))
        {
            bee.IsBusy = false;
            SetState(ent, BeeState.ReturningToHive);
        }
    }

    private void OnPollinatingDoAfter(Entity<BeeComponent> ent, ref BeePollinatingDoAfterEvent args)
    {
        var bee = ent.Comp;
        bee.IsBusy = false;

        if (args.Cancelled || !HasValidFlower(ent))
        {
            SetState(ent, BeeState.ReturningToHive);
            return;
        }

        var flower = bee.TargetFlower!.Value;
        if (!TryComp<PollinationComponent>(flower, out var pollination) || !_pollination.CanPollinate((flower, pollination)))
        {
            SetState(ent, BeeState.ReturningToHive);
            return;
        }

        bee.PollenCarried = Math.Min(pollination.PollenYield, bee.MaxPollenCarry);
        var ev = new PlantPollinatedEvent(ent, bee.PollenCarried);
        RaiseLocalEvent(flower, ref ev);

        // One flower, then straight home with the pollen.
        bee.TargetFlower = null;
        SetState(ent, BeeState.ReturningToHive);
    }

    private void TickReturning(Entity<BeeComponent> ent)
    {
        var bee = ent.Comp;
        if (!HasValidHive(ent))
            return;

        var hive = bee.HiveOwner!.Value;
        switch (Fly(ent, hive))
        {
            case WalkStatus.Arrived:
                // At home: the pollen is left in the hive at once and the bee rests before the next flight.
                var ev = new BeePollenDepositedEvent(ent, bee.PollenCarried);
                RaiseLocalEvent(hive, ref ev);

                bee.PollenCarried = 0f;
                SetState(ent, BeeState.Idle);
                break;
            case WalkStatus.Failed:
                // Stuck on the way: try again from where it is.
                StopFlying(ent);
                break;
        }
    }
}
