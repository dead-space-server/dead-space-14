// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Movement.Components;
using Content.Shared.Movement.Components;
using Robust.Shared.Physics.Components;

namespace Content.Shared.Movement.Systems;

/// <summary>
/// DS14: tile-by-tile movement for <see cref="TileStepMovementComponent"/>.
/// Each step picks the neighbouring tile in the held direction and drives the body to its center;
/// a wall (no progress) sends the mob back to the center of the tile it stands on.
/// </summary>
public abstract partial class SharedMoverController
{
    protected EntityQuery<TileStepMovementComponent> TileStepQuery;

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
                Dirty(ent);
            }
            return false;
        }

        var position = xform.LocalPosition;
        var now = Timing.CurTime;

        if (step.Stepping && step.Parent != xform.ParentUid)
            step.Stepping = false;

        if (step.Stepping)
        {
            var left = (step.Target - position).Length();
            if (left <= TileStepArriveDistance)
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
                    BeginTileStep(step, xform, TileCenter(position), now);
                }
            }
        }

        var direction = HeldTileDirection(mover);
        if (!step.Stepping && direction != Vector2.Zero)
        {
            var from = TileCenter(position);
            BeginTileStep(step, xform, from + direction, now);
            PlayTileStepSound(ent, mover, xform);
        }

        if (!step.Stepping && direction == Vector2.Zero)
        {
            // Idle: settle on the center of the current tile.
            var center = TileCenter(position);
            if ((center - position).Length() > TileStepArriveDistance)
                BeginTileStep(step, xform, center, now);
        }

        var velocity = Vector2.Zero;
        if (step.Stepping)
        {
            var delta = step.Target - position;
            var distance = delta.Length();
            var speed = GetTileStepSpeed(ent, mover);
            // Never overshoot the center within one tick.
            var stepSpeed = MathF.Min(speed, distance / frameTime);
            velocity = distance > 0f ? delta / distance * stepSpeed : Vector2.Zero;

            if (!NoRotateQuery.HasComponent(ent) && delta.LengthSquared() > 0.0001f)
            {
                var parentRotation = _transform.GetWorldRotation(xform.ParentUid);
                _transform.SetWorldRotation(xform, parentRotation.RotateVec(delta).ToWorldAngle());
            }

            velocity = _transform.GetWorldRotation(xform.ParentUid).RotateVec(velocity);
        }

        PhysicsSystem.SetLinearVelocity(ent, velocity, body: physics);
        PhysicsSystem.SetAngularVelocity(ent, 0f, body: physics);
        Dirty(ent);
        return true;
    }

    private static void BeginTileStep(TileStepMovementComponent step, TransformComponent xform, Vector2 target, TimeSpan now)
    {
        step.Stepping = true;
        step.Target = target;
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
