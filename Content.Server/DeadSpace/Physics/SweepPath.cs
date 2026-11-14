// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Server.DeadSpace.Physics;

public static class SweepPath
{
    public static bool TryGet(
        TransformComponent xform,
        PhysicsComponent body,
        float frameTime,
        float minDistance,
        SharedTransformSystem transforms,
        out MapId mapId,
        out Vector2 from,
        out Vector2 to,
        float minSpeed = 0f)
    {
        mapId = xform.MapID;
        from = default;
        to = default;

        if (!body.Awake || mapId == MapId.Nullspace)
            return false;

        var velocity = body.LinearVelocity;
        if (minSpeed > 0f)
        {
            if (velocity.LengthSquared() < minSpeed * minSpeed)
                return false;
        }
        else
        {
            var step = velocity * frameTime;
            if (step.LengthSquared() < minDistance * minDistance)
                return false;
        }

        var moved = velocity * frameTime;
        to = transforms.GetWorldPosition(xform);
        var parentRotation = xform.ParentUid.IsValid() ? transforms.GetWorldRotation(xform.ParentUid) : Angle.Zero;
        from = to - parentRotation.RotateVec(moved);
        return true;
    }
}
