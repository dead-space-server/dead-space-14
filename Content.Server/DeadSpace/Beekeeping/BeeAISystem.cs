// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.DeadSpace.Beekeeping;
using Content.Shared.DoAfter;
using Content.Shared.Ghost;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Content.Shared.NPC.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Beekeeping;

/// <summary>
/// The brain of a worker bee, see <see cref="BeeComponent"/>. What keeps it cheap on a server with many players:
/// <list type="bullet">
/// <item>a bee thinks twice a second, not every tick;</item>
/// <item>a bee that has no player within <see cref="ActiveRange"/> does nothing, the hive is simulated instead
/// (see <see cref="BeeHiveSystem"/>);</item>
/// <item>flowers are found with a lookup around the bee, not by going through every plant of the round.</item>
/// </list>
/// </summary>
public sealed class BeeAISystem : EntitySystem
{
    /// <summary>
    /// Bees farther than this from every player stay where they are.
    /// </summary>
    public const float ActiveRange = 40f;

    private static readonly TimeSpan PlayerScanInterval = TimeSpan.FromSeconds(2);

    [Dependency] private readonly BeeHiveSystem _hive = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly PollinationSystem _pollination = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly List<EntityCoordinates> _players = new();
    private readonly HashSet<Entity<PollinationComponent>> _flowers = new();
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
        // Without this the steering does not move the bee. The usual brain is taken off: two brains would fight
        // over the steering and the bee would freeze. (The prototype of the worker still inherits HTN from the
        // base of simple mobs, and a prototype cannot take a parent component away.)
        // The order matters: taking HTN off puts the NPC to sleep (the HTN shutdown removes ActiveNPC), so the bee is
        // woken up only after that. The other way round the bee stood still and never flew.
        RemComp<HTNComponent>(ent);
        EnsureComp<ActiveNPCComponent>(ent);
        ent.Comp.NextThink = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(0f, ent.Comp.ThinkInterval));

        if (ent.Comp.HiveOwner is { } hive && TryComp<BeeHiveComponent>(hive, out var hiveComp))
            hiveComp.Bees.Add(ent);
    }

    private void OnShutdown(Entity<BeeComponent> ent, ref ComponentShutdown args)
    {
        _steering.Unregister(ent);

        if (ent.Comp.HiveOwner is { } hive && TryComp<BeeHiveComponent>(hive, out var hiveComp))
            hiveComp.Bees.Remove(ent);
    }

    /// <summary>
    /// Whether a player (not a ghost) is within the range of the coordinates. The positions of the players are
    /// taken every <see cref="PlayerScanInterval"/>, whatever the number of callers.
    /// </summary>
    public bool IsPlayerNear(EntityCoordinates coordinates, float range = ActiveRange)
    {
        var now = _timing.CurTime;
        if (now >= _nextPlayerScan)
        {
            _nextPlayerScan = now + PlayerScanInterval;
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

            // Nobody sees the bee: it stays where it is, and costs nothing.
            if (!IsPlayerNear(xform.Coordinates))
            {
                if (bee.State is BeeState.MovingToFlower or BeeState.ReturningToHive)
                    _steering.Unregister(uid);

                continue;
            }

            if (bee.IsBusy)
                continue;

            bee.StateTimer += bee.ThinkInterval;
            var ent = (uid, bee);

            switch (bee.State)
            {
                case BeeState.Idle: TickIdle(ent); break;
                case BeeState.SearchingFlower: TickSearching(ent, xform); break;
                case BeeState.MovingToFlower: TickMoving(ent, xform); break;
                case BeeState.Pollinating: TickPollinating(ent, xform); break;
                case BeeState.ReturningToHive: TickReturning(ent, xform); break;
            }
        }
    }

    private void SetState(Entity<BeeComponent> ent, BeeState state)
    {
        var bee = ent.Comp;
        if (bee.State == state)
            return;

        if (bee.State is BeeState.Pollinating)
            CancelDoAfters(ent);

        if (bee.State is BeeState.MovingToFlower or BeeState.ReturningToHive)
            _steering.Unregister(ent);

        bee.State = state;
        bee.StateTimer = 0f;

        if (state is BeeState.MovingToFlower or BeeState.ReturningToHive)
        {
            bee.LastDistanceToTarget = float.MaxValue;
            bee.NoProgressTime = 0f;
        }
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
        SetState(ent, BeeState.SearchingFlower);
        return false;
    }

    private float DistanceTo(EntityUid target, TransformComponent xform)
    {
        return (_transform.GetWorldPosition(target) - _transform.GetWorldPosition(xform)).Length();
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

    private void TickSearching(Entity<BeeComponent> ent, TransformComponent xform)
    {
        var bee = ent.Comp;
        var origin = _transform.GetWorldPosition(xform);
        EntityUid? best = null;
        var bestDistance = float.MaxValue;

        _flowers.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, bee.SearchRadius, _flowers);

        foreach (var flower in _flowers)
        {
            if (!_pollination.CanPollinate(flower))
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

    private void TickMoving(Entity<BeeComponent> ent, TransformComponent xform)
    {
        var bee = ent.Comp;
        if (!HasValidFlower(ent))
            return;

        var target = bee.TargetFlower!.Value;
        var distance = DistanceTo(target, xform);
        if (distance < bee.ArrivalThreshold)
        {
            SetState(ent, BeeState.Pollinating);
            return;
        }

        var coordinates = Transform(target).Coordinates;

        if (SteeringLostPath(ent) || ReRouteIfStuck(ent, distance, coordinates))
            return;

        if (bee.StateTimer > bee.MaxMovingTime)
        {
            bee.TargetFlower = null;
            SetState(ent, BeeState.SearchingFlower);
            return;
        }

        _steering.TryRegister(ent, coordinates);
    }

    private void TickPollinating(Entity<BeeComponent> ent, TransformComponent xform)
    {
        var bee = ent.Comp;
        if (!HasValidFlower(ent))
            return;

        var flower = bee.TargetFlower!.Value;
        if (DistanceTo(flower, xform) > bee.ArrivalThreshold * 2f ||
            !TryComp<PollinationComponent>(flower, out var pollination) ||
            !_pollination.CanPollinate((flower, pollination)))
        {
            SetState(ent, BeeState.SearchingFlower);
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
            SetState(ent, BeeState.SearchingFlower);
        }
    }

    private void OnPollinatingDoAfter(Entity<BeeComponent> ent, ref BeePollinatingDoAfterEvent args)
    {
        var bee = ent.Comp;
        bee.IsBusy = false;

        if (args.Cancelled || !HasValidFlower(ent))
        {
            SetState(ent, BeeState.SearchingFlower);
            return;
        }

        var flower = bee.TargetFlower!.Value;
        if (!TryComp<PollinationComponent>(flower, out var pollination) || !_pollination.CanPollinate((flower, pollination)))
        {
            SetState(ent, BeeState.SearchingFlower);
            return;
        }

        var pollen = Math.Min(pollination.PollenYield, bee.MaxPollenCarry - bee.PollenCarried);
        if (pollen > 0f)
        {
            bee.PollenCarried += pollen;
            var ev = new PlantPollinatedEvent(ent, pollen);
            RaiseLocalEvent(flower, ref ev);
        }

        bee.TargetFlower = null;

        // One flower, then straight home with the pollen.
        SetState(ent, BeeState.ReturningToHive);
    }

    private void TickReturning(Entity<BeeComponent> ent, TransformComponent xform)
    {
        var bee = ent.Comp;
        if (!HasValidHive(ent))
            return;

        var hive = bee.HiveOwner!.Value;
        var distance = DistanceTo(hive, xform);
        if (distance < bee.ArrivalThreshold)
        {
            // At home: the pollen is left in the hive at once and the bee rests before the next flight.
            var ev = new BeePollenDepositedEvent(ent, bee.PollenCarried);
            RaiseLocalEvent(hive, ref ev);

            bee.PollenCarried = 0f;
            bee.TargetFlower = null;
            SetState(ent, BeeState.Idle);
            return;
        }

        var coordinates = Transform(hive).Coordinates;

        if (bee.StateTimer > bee.MaxMovingTime || SteeringLostPath(ent))
        {
            bee.StateTimer = 0f;
            _steering.Unregister(ent);
            _steering.Register(ent, coordinates);
            return;
        }

        if (ReRouteIfStuck(ent, distance, coordinates))
            return;

        _steering.TryRegister(ent, coordinates);
    }

    private bool SteeringLostPath(Entity<BeeComponent> ent)
    {
        return TryComp<NPCSteeringComponent>(ent, out var steering) && steering.Status == SteeringStatus.NoPath;
    }

    /// <summary>
    /// If the bee does not get closer for a while, the path is planned again.
    /// </summary>
    /// <returns>True if it was.</returns>
    private bool ReRouteIfStuck(Entity<BeeComponent> ent, float distance, EntityCoordinates target)
    {
        var bee = ent.Comp;
        if (distance < bee.LastDistanceToTarget - bee.ProgressEpsilon)
        {
            bee.LastDistanceToTarget = distance;
            bee.NoProgressTime = 0f;
            return false;
        }

        bee.NoProgressTime += bee.ThinkInterval;
        if (bee.NoProgressTime < bee.StuckTimeout)
            return false;

        bee.NoProgressTime = 0f;
        bee.LastDistanceToTarget = float.MaxValue;
        _steering.Unregister(ent);
        _steering.Register(ent, target);
        return true;
    }

}
