// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Friction;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.DeadSpace.Wizard.Runes;

public sealed class RuneProjectileHoldSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        var after = new[] { typeof(TileFrictionController) };
        SubscribeLocalEvent<PhysicsUpdateBeforeSolveEvent>(OnPhysics, null, after);
        SubscribeLocalEvent<PhysicsUpdateAfterSolveEvent>(OnPhysicsAfter, null, after);
    }

    private void OnPhysics(ref PhysicsUpdateBeforeSolveEvent ev)
    {
        Hold();
    }

    private void OnPhysicsAfter(ref PhysicsUpdateAfterSolveEvent ev)
    {
        Hold();
    }

    private void Hold()
    {
        var query = EntityQueryEnumerator<RuneProjectileHoldComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var hold, out var body))
        {
            _transform.SetWorldPosition(uid, hold.Position);
            if (body.BodyType != BodyType.Static)
            {
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
                _physics.SetAngularVelocity(uid, 0f, body: body);
                _physics.SetBodyType(uid, BodyType.Static, body: body);
            }
        }
    }
}
