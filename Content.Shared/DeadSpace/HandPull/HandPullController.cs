using System.Numerics;
using Content.Shared.Friction;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.DeadSpace.HandPull;

public sealed class HandPullController : VirtualController
{
    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(TileFrictionController));

        base.Initialize();
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        var query = EntityQueryEnumerator<HandPulledComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var pulled, out var body))
        {
            if (prediction && !body.Predict)
                continue;

            if (!TryComp<PhysicsComponent>(pulled.Puller, out var pullerBody) ||
                !TryComp(uid, out TransformComponent? xform) ||
                !TryComp(pulled.Puller, out TransformComponent? pullerXform) ||
                xform.MapID != pullerXform.MapID)
            {
                continue;
            }

            var factor = Math.Clamp(body.LinearDamping * frameTime, 0f, 1f);
            var pullerVelocity = pullerBody.LinearVelocity;

            if (factor <= 0f || pullerVelocity == Vector2.Zero)
                continue;

            var toPuller = TransformSystem.GetWorldPosition(pullerXform) - TransformSystem.GetWorldPosition(xform);
            if (toPuller.LengthSquared() < 0.0001f)
                continue;

            var direction = Vector2.Normalize(toPuller);
            var followSpeed = Vector2.Dot(pullerVelocity, direction);

            if (followSpeed <= 0f)
                continue;

            PhysicsSystem.SetLinearVelocity(uid, body.LinearVelocity + direction * followSpeed * factor, body: body);
        }
    }
}
