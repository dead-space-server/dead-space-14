// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Movement.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Shared.Movement.Systems;

/// <summary>
/// DS14: tile-by-tile movement for <see cref="TileStepMovementComponent"/>.
/// Each step picks the neighbouring tile in the held direction and drives the body to its center;
/// a wall (no progress) sends the mob back to the center of the tile it stands on.
/// </summary>
public abstract partial class SharedMoverController
{
    [Dependency] private readonly INetManager _tileStepNet = default!;

    protected EntityQuery<TileStepMovementComponent> TileStepQuery;
    protected EntityQuery<FixturesComponent> TileStepFixturesQuery;

    /// <summary>Close enough to a tile center to count as standing on it.</summary>
    private const float TileStepArriveDistance = 0.02f;

    /// <summary>Returns true when the movement of this tick was done here.</summary>
    protected bool HandleTileStepMovement(
        Entity<TileStepMovementComponent> ent,
        InputMoverComponent mover,
        PhysicsComponent physics,
        TransformComponent xform,
        bool freeFloating,
        float frameTime)
    {
        var step = ent.Comp;

        // In space or in the air the usual movement takes over; start over once back on the ground.
        if (freeFloating || frameTime <= 0f)
        {
            if (step.Stepping)
            {
                step.Stepping = false;
                DirtyTileStep(ent);
            }
            return false;
        }

        // Only the step itself is networked; the progress counters are local.
        var wasStepping = step.Stepping;
        var oldTarget = step.Target;
        var oldParent = step.Parent;

        var position = xform.LocalPosition;
        var now = Timing.CurTime;

        if (step.Stepping && step.Parent != xform.ParentUid)
            step.Stepping = false;

        var direction = HeldTileDirection(mover);
        var speed = GetTileStepSpeed(ent, mover);

        if (step.Stepping)
        {
            var left = (step.Target - position).Length();
            var stepVector = step.Target - step.From;

            // Turned right back mid-step: straight back to the tile it came from, no finishing the step first.
            if (direction != Vector2.Zero && stepVector.LengthSquared() > 0.01f &&
                Vector2.Dot(Vector2.Normalize(direction), Vector2.Normalize(stepVector)) < -0.9f &&
                left > TileStepArriveDistance)
            {
                BeginTileStep(step, xform, step.From, now, step.Target);
                left = (step.Target - position).Length();
            }

            // Arriving within this tick with a direction held: the next step starts right now, from the tile it is
            // about to reach - no tick spent creeping onto the center and no hitch on every tile.
            if (direction != Vector2.Zero && left <= speed * frameTime + TileStepArriveDistance &&
                TryPickTileStep(ent, xform, physics, step.Target, direction, out var chained))
            {
                BeginTileStep(step, xform, chained, now, step.Target);
                PlayTileStepSound(ent, mover, xform);
            }
            else if (left <= TileStepArriveDistance)
            {
                step.Stepping = false;
            }
            else
            {
                if (left < step.LastDistance - 0.01f)
                {
                    step.LastDistance = left;
                    step.LastProgress = now;
                }
                else if (now - step.LastProgress > step.StuckTime)
                {
                    // Bumped into something: back to the tile the step came from - unless it was already the way
                    // back that got stuck, or the body cannot stand there (a crate pushed onto it, a railing along
                    // the edge): then stop where we are, and a held direction starts a fresh step below. Never
                    // pushing at the same spot for good: that held the mob in place and swallowed the input.
                    var returning = step.Target == step.From;
                    if (!returning && CanStandOnTileCenter(ent, xform, step.From))
                        BeginTileStep(step, xform, step.From, now, step.From);
                    else
                        step.Stepping = false;
                }
            }
        }

        if (!step.Stepping && direction != Vector2.Zero)
        {
            // A wall ahead is not walked into (that only stalls and jerks): a diagonal slides along it instead,
            // a straight step into it is not taken at all.
            var from = TileCenter(position);
            if (TryPickTileStep(ent, xform, physics, from, direction, out var target))
            {
                BeginTileStep(step, xform, target, now, from);
                PlayTileStepSound(ent, mover, xform);
            }
        }

        if (!step.Stepping && direction == Vector2.Zero)
        {
            // Idle: settle on the center of the current tile, if the body fits there.
            var center = TileCenter(position);
            if ((center - position).Length() > TileStepArriveDistance && CanStandOnTileCenter(ent, xform, center))
                BeginTileStep(step, xform, center, now, center);
        }

        var velocity = Vector2.Zero;
        if (step.Stepping)
        {
            var delta = step.Target - position;
            var distance = delta.Length();
            // Never overshoot the center within one tick.
            var stepSpeed = MathF.Min(speed, distance / frameTime);
            velocity = distance > 0f ? delta / distance * stepSpeed : Vector2.Zero;

            if (!NoRotateQuery.HasComponent(ent) && delta.LengthSquared() > 0.0001f)
            {
                // Only when it really turns: setting the same rotation every tick only dirties the transform.
                var parentRotation = _transform.GetWorldRotation(xform.ParentUid);
                var facing = parentRotation.RotateVec(delta).ToWorldAngle();
                if (!facing.EqualsApprox(_transform.GetWorldRotation(xform), 0.02))
                    _transform.SetWorldRotation(xform, facing);
            }

            velocity = _transform.GetWorldRotation(xform.ParentUid).RotateVec(velocity);
        }

        PhysicsSystem.SetLinearVelocity(ent, velocity, body: physics);
        PhysicsSystem.SetAngularVelocity(ent, 0f, body: physics);
        if (step.Stepping != wasStepping || step.Target != oldTarget || step.Parent != oldParent)
            DirtyTileStep(ent);
        return true;
    }

    /// <summary>
    /// Marks the step as changed. The client only does it while predicting: dirtying outside of prediction
    /// breaks the rollback of predicted entities.
    /// </summary>
    private void DirtyTileStep(Entity<TileStepMovementComponent> ent)
    {
        if (_tileStepNet.IsClient && !Timing.InPrediction)
            return;

        Dirty(ent);
    }

    /// <summary>
    /// The neighbouring tile to step to: the held direction if it is free, otherwise for a diagonal one of its
    /// straight parts. A diagonal is only taken with both of its straight parts free too: cutting the corner of a
    /// wall the body bumps into it and is sent back (it would not let one into a passage). False if every way is
    /// blocked.
    /// </summary>
    private bool TryPickTileStep(EntityUid uid, TransformComponent xform, PhysicsComponent physics, Vector2 from,
        Vector2 direction, out Vector2 target)
    {
        target = from + direction;
        if (direction.X == 0f || direction.Y == 0f)
            return !IsTileStepBlocked(uid, xform, physics, target);

        var horizontal = from + new Vector2(direction.X, 0f);
        var vertical = from + new Vector2(0f, direction.Y);
        var horizontalFree = !IsTileStepBlocked(uid, xform, physics, horizontal);
        var verticalFree = !IsTileStepBlocked(uid, xform, physics, vertical);
        if (horizontalFree && verticalFree && !IsTileStepBlocked(uid, xform, physics, target))
            return true;

        if (horizontalFree)
        {
            target = horizontal;
            return true;
        }

        if (verticalFree)
        {
            target = vertical;
            return true;
        }

        return false;
    }

    /// <summary>Half the size of the middle of a tile a step goes through.</summary>
    private const float TileStepCenterHalfSize = 0.2f;

    /// <summary>
    /// Something solid for this mob (a wall, a window, a crate, a cactus) fills the middle of the tile.
    /// Not only anchored things: walking into a loose crate held the mob against it.
    /// </summary>
    private bool IsTileStepBlocked(EntityUid uid, TransformComponent xform, PhysicsComponent physics, Vector2 localTarget)
    {
        if (xform.ParentUid != xform.GridUid || !MapGridQuery.TryComp(xform.GridUid, out _))
            return false;

        var middle = Box2.CenteredAround(TileCenter(localTarget), new Vector2(TileStepCenterHalfSize * 2f));
        return TileStepObstacleOverlaps(uid, xform.GridUid.Value, physics, middle);
    }

    private readonly HashSet<EntityUid> _tileStepObstacles = new();

    /// <summary>
    /// Something hard for this mob overlaps the box (grid-local). Doors and turnstiles do not count: a door is
    /// stepped into (the bump opens it, a locked one stops the step like any obstacle), a turnstile lets one through
    /// from its side. Nor do mobs: they pass through one another.
    /// </summary>
    private bool TileStepObstacleOverlaps(EntityUid uid, EntityUid gridUid, PhysicsComponent physics, Box2 box)
    {
        _tileStepObstacles.Clear();
        _lookup.GetLocalEntitiesIntersecting(gridUid, box, _tileStepObstacles, LookupFlags.Static | LookupFlags.Dynamic);
        foreach (var other in _tileStepObstacles)
        {
            if (other == uid || HasComp<DoorComponent>(other) || HasComp<TurnstileComponent>(other) ||
                HasComp<MobStateComponent>(other) ||
                !PhysicsQuery.TryComp(other, out var otherBody) || !otherBody.CanCollide || !otherBody.Hard ||
                (otherBody.CollisionLayer & physics.CollisionMask) == 0 ||
                !TileStepFixturesQuery.TryComp(other, out var fixtures) || !XformQuery.TryComp(other, out var otherXform) ||
                otherXform.ParentUid != gridUid)
            {
                continue;
            }

            // Only a hard fixture in our way blocks: a bear trap is hard as an item but its trigger on the
            // impassable layer is not, and the body's layer is the union of all of its fixtures. And only one
            // across the box, overlapping it, not touching: a wall lamp, a camera or a railing along an edge leaves
            // the way free.
            var transform = new Transform(otherXform.LocalPosition, otherXform.LocalRotation);
            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (!fixture.Hard || (fixture.CollisionLayer & physics.CollisionMask) == 0)
                    continue;

                var overlap = fixture.Shape.ComputeAABB(transform, 0).Intersect(box);
                if (overlap.Width > 0.01f && overlap.Height > 0.01f)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The mob's own body fits on the tile center: nothing solid there (a railing along the edge, a crate pushed
    /// halfway onto it) would overlap it.
    /// </summary>
    private bool CanStandOnTileCenter(EntityUid uid, TransformComponent xform, Vector2 center)
    {
        if (xform.ParentUid != xform.GridUid || !MapGridQuery.TryComp(xform.GridUid, out _) ||
            !TileStepFixturesQuery.TryComp(uid, out var ownFixtures) || !PhysicsQuery.TryComp(uid, out var physics))
        {
            return true;
        }

        var bodyTransform = new Transform(center, xform.LocalRotation);
        Box2? body = null;
        foreach (var fixture in ownFixtures.Fixtures.Values)
        {
            if (!fixture.Hard)
                continue;

            var box = fixture.Shape.ComputeAABB(bodyTransform, 0);
            body = body is { } so ? so.Union(box) : box;
        }

        return body is not { } bodyBox || !TileStepObstacleOverlaps(uid, xform.GridUid.Value, physics, bodyBox);
    }

    private static void BeginTileStep(TileStepMovementComponent step, TransformComponent xform, Vector2 target, TimeSpan now,
        Vector2 from)
    {
        step.Stepping = true;
        step.Target = target;
        step.From = from;
        step.Parent = xform.ParentUid;
        step.LastDistance = (target - xform.LocalPosition).Length();
        step.LastProgress = now;
    }

    /// <summary>Held direction as a whole-tile offset (diagonals allowed), in the parent's coordinates.</summary>
    private Vector2 HeldTileDirection(InputMoverComponent mover)
    {
        var raw = DirVecForButtons(mover.HeldMoveButtons & ~MoveButtons.Walk);
        if (raw == Vector2.Zero)
            return Vector2.Zero;

        var rotated = mover.TargetRelativeRotation.RotateVec(raw);
        return new Vector2(MathF.Round(rotated.X), MathF.Round(rotated.Y));
    }

    private static Vector2 TileCenter(Vector2 local)
    {
        return new Vector2(MathF.Floor(local.X) + 0.5f, MathF.Floor(local.Y) + 0.5f);
    }

    private float GetTileStepSpeed(EntityUid uid, InputMoverComponent mover)
    {
        var modifier = ModifierQuery.CompOrNull(uid);
        if (mover.Sprinting)
            return modifier?.CurrentSprintSpeed ?? MovementSpeedModifierComponent.DefaultBaseSprintSpeed;

        return modifier?.CurrentWalkSpeed ?? MovementSpeedModifierComponent.DefaultBaseWalkSpeed;
    }

    private void PlayTileStepSound(EntityUid uid, InputMoverComponent mover, TransformComponent xform)
    {
        if (!MobMoverQuery.TryGetComponent(uid, out var mobMover) ||
            !TryGetSound(false, uid, mover, mobMover, xform, out var sound))
        {
            return;
        }

        var audioParams = sound.Params.WithVariation(sound.Params.Variation ?? mobMover.FootstepVariation);
        var source = RelayTargetQuery.TryComp(uid, out var relay) ? relay.Source : uid;
        _audio.PlayPredicted(sound, uid, source, audioParams);
    }
}
