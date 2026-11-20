// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.EntityFilterBarrier;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using System.Numerics;

namespace Content.Server.DeadSpace.EntityFilterBarrier;

public sealed partial class EntityFilterBarrierSystem : SharedEntityFilterBarrierSystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    protected override void OnPreventCollide(EntityUid uid, EntityFilterBarrierComponent component, ref PreventCollideEvent args)
    {
        base.OnPreventCollide(uid, component, ref args);

        if (args.Cancelled)
            return;

        var barrierPos = _transform.GetWorldPosition(uid);
        var otherPos = _transform.GetWorldPosition(args.OtherEntity);
        var relativePos = otherPos - barrierPos;

        if (Math.Abs(relativePos.X) < 0.5f && Math.Abs(relativePos.Y) < 0.5f)
        {
            var pushDir = relativePos == Vector2.Zero ? new Vector2(0, 1) : Vector2.Normalize(relativePos);
            _transform.SetWorldPosition(args.OtherEntity, barrierPos + pushDir * 0.7f);
            if (TryComp<PhysicsComponent>(args.OtherEntity, out var physics))
            {
                _physics.SetLinearVelocity(args.OtherEntity, pushDir * 5f, body: physics);
                _physics.ApplyLinearImpulse(args.OtherEntity, pushDir * 15f, body: physics);
            }
        }
    }
}