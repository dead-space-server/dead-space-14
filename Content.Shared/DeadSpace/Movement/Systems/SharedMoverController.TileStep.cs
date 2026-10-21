// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Movement.Components;
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
                TryPickTileStep(xform, physics, step.Target, direction, out var chained))
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
                    // Bumped into something: back to the center of the tile we stand on.
                    BeginTileStep(step, xform, TileCenter(position), now, TileCenter(position));
                }
            }
        }

        if (!step.Stepping && direction != Vector2.Zero)
        {
            // A wall ahead is not walked into (that only stalls and jerks): a diagonal slides along it instead,
            // a straight step into it is not taken at all.
            var from = TileCenter(position);
            if (TryPickTileStep(xform, physics, from, direction, out var target))
            {
                BeginTileStep(step, xform, target, now, from);
                PlayTileStepSound(ent, mover, xform);
            }
        }

        if (!step.Stepping && direction == Vector2.Zero)
        {
            // Idle: settle on the center of the current tile.
            var center = TileCenter(position);
            if ((center - position).Length() > TileStepArriveDistance)
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
    /// straight parts. False if every way is blocked by a wall.
    /// </summary>
    private bool TryPickTileStep(TransformComponent xform, PhysicsComponent physics, Vector2 from, Vector2 direction, out Vector2 target)
    {
        target = from + direction;
        if (!IsTileStepBlocked(xform, physics, target))
            return true;

        if (direction.X == 0f || direction.Y == 0f)
            return false;

        foreach (var part in new[] { new Vector2(direction.X, 0f), new Vector2(0f, direction.Y) })
        {
            target = from + part;
            if (!IsTileStepBlocked(xform, physics, target))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Something anchored and solid for this mob (a wall, a window, a closed door) stands on the tile.
    /// Anchored things are the same on the client and the server, so both pick the same step.
    /// </summary>
    private bool IsTileStepBlocked(TransformComponent xform, PhysicsComponent physics, Vector2 localTarget)
    {
        if (xform.ParentUid != xform.GridUid || !MapGridQuery.TryComp(xform.GridUid, out var grid))
            return false;

        var gridUid = xform.GridUid.Value;
        var tile = new Vector2i((int) MathF.Floor(localTarget.X), (int) MathF.Floor(localTarget.Y));
        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var other))
        {
            if (!PhysicsQuery.TryComp(other, out var otherBody) || !otherBody.CanCollide || !otherBody.Hard)
                continue;

            if ((otherBody.CollisionLayer & physics.CollisionMask) == 0)
                continue;

            // Only a hard fixture in our way blocks: a bear trap is hard as an item but its trigger on the
            // impassable layer is not, and the body's layer is the union of all of its fixtures.
            if (!TileStepFixturesQuery.TryComp(other, out var fixtures))
                return true;

            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (fixture.Hard && (fixture.CollisionLayer & physics.CollisionMask) != 0)
                    return true;
            }
        }

        return false;
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
