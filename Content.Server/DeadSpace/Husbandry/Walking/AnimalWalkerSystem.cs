// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Husbandry.Walking;
using Content.Shared.Maps;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Husbandry.Walking;

/// <summary>
/// Walks animals along routes, see <see cref="AnimalWalkerComponent"/>. Every tick the input of the animal is set once,
/// towards the next waypoint, and the next waypoint is taken when it is close. There is no steering in between, so
/// nothing to twitch. A stuck animal, one that is dragged away or one whose target is gone fails the route.
/// </summary>
public sealed class AnimalWalkerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    /// <summary>
    /// Sends the animal along a route. The last waypoint is where it goes.
    /// </summary>
    /// <param name="waypoints">Where to go, in order, the last one is the destination.</param>
    /// <param name="arriveDistance">How close to the destination counts as being there.</param>
    /// <param name="run">Run instead of walk.</param>
    /// <param name="smooth">Cut the route short wherever a straight strip as wide as the animal is free, for routes
    /// that follow the tiles of a path.</param>
    /// <returns>False if it cannot move at all or there is no route.</returns>
    public bool StartRoute(Entity<AnimalWalkerComponent> ent, IReadOnlyList<EntityCoordinates> waypoints, float arriveDistance, bool run = false, bool smooth = false)
    {
        var walker = ent.Comp;
        if (waypoints.Count == 0 || !TryComp<InputMoverComponent>(ent, out var mover) || !mover.CanMove)
            return false;

        var points = smooth ? Smooth(ent, waypoints) : waypoints;

        walker.Route.Clear();
        foreach (var point in points)
        {
            walker.Route.Enqueue(point);
        }

        var now = _timing.CurTime;
        walker.ArriveDistance = arriveDistance;
        walker.Run = run;
        walker.StartTime = now;
        walker.ProgressTime = now;
        walker.ProgressPosition = _transform.GetWorldPosition(ent);
        walker.Status = WalkStatus.Walking;
        return true;
    }

    public WalkStatus GetStatus(Entity<AnimalWalkerComponent> ent)
    {
        return ent.Comp.Status;
    }

    /// <summary>
    /// Whether whatever keeps the animal in place (a tether) lets it go for this target, see
    /// <see cref="AnimalTargetCheckEvent"/>. Anything that picks where the animal goes asks this first.
    /// </summary>
    /// <param name="reach">How far from the target the animal stops: 0 for a place, the arrival distance for a thing.</param>
    public bool IsTargetAllowed(EntityUid animal, MapCoordinates target, float reach = 0f)
    {
        var ev = new AnimalTargetCheckEvent(target, reach);
        RaiseLocalEvent(animal, ref ev);
        return ev.Allowed;
    }

    /// <summary>
    /// Stops the animal where it is and forgets the route and how it ended.
    /// </summary>
    public void Stop(Entity<AnimalWalkerComponent> ent)
    {
        if (ent.Comp.Status == WalkStatus.Idle)
            return;

        Finish(ent, WalkStatus.Idle);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AnimalWalkerComponent, InputMoverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var walker, out var mover, out var xform))
        {
            if (walker.Status == WalkStatus.Walking)
                Step((uid, walker), mover, xform);
        }
    }

    private void Step(Entity<AnimalWalkerComponent> ent, InputMoverComponent mover, TransformComponent xform)
    {
        var walker = ent.Comp;
        var now = _timing.CurTime;

        // Somebody who drags the animal around decides where it goes, not the animal.
        if (!mover.CanMove ||
            now - walker.StartTime > walker.MaxRouteTime ||
            TryComp<PullableComponent>(ent, out var pullable) && pullable.BeingPulled ||
            !walker.Route.TryPeek(out var next) || !next.IsValid(EntityManager))
        {
            Finish(ent, WalkStatus.Failed);
            return;
        }

        var position = _transform.GetWorldPosition(xform);
        var destination = LastWaypoint(walker);
        if (!destination.IsValid(EntityManager))
        {
            Finish(ent, WalkStatus.Failed);
            return;
        }

        // Near enough is enough, but only near the end of the route: with a wall between, the way around is still ahead.
        if (walker.Route.Count <= 2 &&
            Vector2.Distance(_transform.ToMapCoordinates(destination).Position, position) <= walker.ArriveDistance)
        {
            Finish(ent, WalkStatus.Arrived);
            return;
        }

        // Close to a waypoint on the way: on to the next one.
        var diff = _transform.ToMapCoordinates(next).Position - position;
        while (walker.Route.Count > 1 && diff.Length() <= walker.WaypointTolerance)
        {
            walker.Route.Dequeue();
            next = walker.Route.Peek();
            diff = _transform.ToMapCoordinates(next).Position - position;
        }

        var distance = diff.Length();
        if (distance < 0.01f)
        {
            Finish(ent, WalkStatus.Arrived);
            return;
        }

        // Stuck: it did not get anywhere for a while, something is in the way that was not there before.
        if (now - walker.ProgressTime >= walker.StuckTime)
        {
            if (Vector2.Distance(position, walker.ProgressPosition) < walker.StuckDistance)
            {
                Finish(ent, WalkStatus.Failed);
                return;
            }

            walker.ProgressTime = now;
            walker.ProgressPosition = position;
        }

        // The input is a fraction of the speed: full on the way, less and less for the last tiles of the route.
        var speed = walker.Route.Count == 1
            ? Math.Clamp(distance / walker.SlowDistance, walker.MinSpeedFraction, 1f)
            : 1f;
        var input = (-_mover.GetParentGridAngle(mover)).RotateVec(diff / distance) * speed;
        SetInput(ent, mover, input, walker.Run, true);
    }

    private static EntityCoordinates LastWaypoint(AnimalWalkerComponent walker)
    {
        // The queue has no way to look at the last one other than going through it, routes are short.
        var last = default(EntityCoordinates);
        foreach (var point in walker.Route)
        {
            last = point;
        }

        return last;
    }

    private void Finish(Entity<AnimalWalkerComponent> ent, WalkStatus status)
    {
        ent.Comp.Status = status;
        ent.Comp.Route.Clear();

        if (TryComp<InputMoverComponent>(ent, out var mover))
            SetInput(ent, mover, Vector2.Zero, false, false);
    }

    /// <summary>
    /// The same way the AI gives the mover its input: the vector of this tick, the mover reads it after us.
    /// </summary>
    private void SetInput(EntityUid uid, InputMoverComponent mover, Vector2 input, bool run, bool moving)
    {
        mover.CurTickWalkMovement = run ? Vector2.Zero : input;
        mover.CurTickSprintMovement = run ? input : Vector2.Zero;
        mover.LastInputTick = _timing.CurTick;
        mover.LastInputSubTick = ushort.MaxValue;

        var ev = new SpriteMoveEvent(moving);
        RaiseLocalEvent(uid, ref ev);
    }

    #region Room to walk

    /// <summary>
    /// What the animal bumps into, for the rays.
    /// </summary>
    public int GetHardMask(EntityUid uid)
    {
        return _physics.GetHardCollision(uid).Mask;
    }

    /// <summary>
    /// How far a strip as wide as the animal is free from the origin in the direction, at most <paramref name="max"/>:
    /// a ray down the middle and one along each side, the nearest thing any of them hits.
    /// </summary>
    public float FreeLength(EntityUid uid, MapId mapId, Vector2 origin, Vector2 direction, float max, float clearance, int mask)
    {
        var side = new Vector2(-direction.Y, direction.X) * clearance;
        var free = max;

        foreach (var offset in new[] { Vector2.Zero, side, -side })
        {
            var ray = new CollisionRay(origin + offset, direction, mask);
            foreach (var hit in _physics.IntersectRay(mapId, ray, max, uid, returnOnFirstHit: false))
            {
                free = Math.Min(free, hit.Distance);
            }
        }

        return free;
    }

    /// <summary>
    /// Every tile on the way, and the end of it, has a floor and is not space.
    /// </summary>
    public bool IsSolidGround(Entity<MapGridComponent> grid, Vector2 origin, Vector2 direction, float distance)
    {
        for (var step = 1f; step < distance; step += 1f)
        {
            if (!IsFloor(grid, origin + direction * step))
                return false;
        }

        return IsFloor(grid, origin + direction * distance);
    }

    private bool IsFloor(Entity<MapGridComponent> grid, Vector2 worldPoint)
    {
        return _map.TryGetTileRef(grid, grid.Comp, worldPoint, out var tile) &&
               !tile.Tile.IsEmpty &&
               !_turf.IsSpace(tile);
    }

    /// <summary>
    /// Cuts a route that follows the tiles of a path short: from where the animal is, it goes straight to the farthest
    /// of the next few waypoints that it can reach over a free strip as wide as it is, and so on from there. The
    /// last waypoint always stays, it is the destination.
    /// </summary>
    private List<EntityCoordinates> Smooth(Entity<AnimalWalkerComponent> ent, IReadOnlyList<EntityCoordinates> waypoints)
    {
        var xform = Transform(ent);
        var mapId = xform.MapID;
        var mask = GetHardMask(ent);
        var clearance = ent.Comp.Clearance;
        var gridUid = xform.GridUid;
        var grid = gridUid != null ? CompOrNull<MapGridComponent>(gridUid.Value) : null;

        var world = new List<Vector2>(waypoints.Count);
        foreach (var waypoint in waypoints)
        {
            world.Add(_transform.ToMapCoordinates(waypoint).Position);
        }

        var result = new List<EntityCoordinates>();
        var from = _transform.GetWorldPosition(xform);
        var i = 0;

        while (i < waypoints.Count)
        {
            var farthest = i;

            for (var j = Math.Min(waypoints.Count - 1, i + ent.Comp.SmoothLookahead); j > i; j--)
            {
                var diff = world[j] - from;
                var distance = diff.Length();
                if (distance < 0.01f)
                    continue;

                var direction = diff / distance;
                if (FreeLength(ent, mapId, from, direction, distance, clearance, mask) < distance)
                    continue;

                if (grid != null && gridUid is { } gridEntity && !IsSolidGround((gridEntity, grid), from, direction, distance))
                    continue;

                farthest = j;
                break;
            }

            result.Add(waypoints[farthest]);
            from = world[farthest];
            i = farthest + 1;
        }

        return result;
    }

    #endregion
}
