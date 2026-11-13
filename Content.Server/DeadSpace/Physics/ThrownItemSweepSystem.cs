// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Server.DeadSpace.Physics;

public sealed partial class ThrownItemSweepSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrownItemSystem _thrown = default!;

    private const float MinDistance = 0.25f;
    private const float StopBackoff = 0.35f;
    private const float EndSkip = 0.5f;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(SharedPhysicsSystem));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ThrownItemComponent, PhysicsComponent, FixturesComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var thrown, out var body, out var fixtures, out var xform))
        {
            if (!body.CanCollide)
                continue;

            if (SweepPath.TryGet(xform, body, frameTime, MinDistance, _transform, out var mapId, out var from, out var to))
                Sweep(uid, thrown, body, fixtures, mapId, from, to);
        }
    }

    private void Sweep(
        EntityUid uid,
        ThrownItemComponent thrown,
        PhysicsComponent body,
        FixturesComponent fixtures,
        MapId mapId,
        Vector2 from,
        Vector2 to)
    {
        var delta = to - from;
        var distance = delta.Length();
        if (distance < MinDistance)
            return;

        Fixture? ourFixture = null;
        var hardMask = 0;
        var mask = 0;
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            ourFixture ??= fixture;
            mask |= fixture.CollisionMask;
            if (fixture.Hard)
                hardMask |= fixture.CollisionMask;
        }

        if (ourFixture == null || mask == 0)
            return;

        var direction = delta / distance;
        var ray = new CollisionRay(from, direction, mask);
        foreach (var hit in _physics.IntersectRay(mapId, ray, distance, uid, false))
        {
            if (hit.Distance > distance - EndSkip)
                break;

            if (hit.HitEntity == thrown.Thrower || !CanHit(uid, body, ourFixture, mask, hit.HitEntity))
                continue;

            if ((hardMask & BlockingLayer(hit.HitEntity)) == 0)
            {
                _thrown.ThrowCollideInteraction(thrown, uid, hit.HitEntity);
                continue;
            }

            var stop = hit.HitPos - direction * MathF.Min(StopBackoff, hit.Distance);
            _transform.SetWorldPosition(uid, stop);
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
            _thrown.ThrowCollideInteraction(thrown, uid, hit.HitEntity);
            return;
        }
    }

    private int BlockingLayer(EntityUid target)
    {
        var layer = 0;
        if (!TryComp<FixturesComponent>(target, out var fixtures))
            return layer;

        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (fixture.Hard)
                layer |= fixture.CollisionLayer;
        }

        return layer;
    }

    private bool CanHit(EntityUid uid, PhysicsComponent body, Fixture ourFixture, int mask, EntityUid target)
    {
        if (target == uid ||
            !TryComp<PhysicsComponent>(target, out var otherBody) ||
            !otherBody.CanCollide ||
            !TryComp<FixturesComponent>(target, out var otherFixtures))
        {
            return false;
        }

        Fixture? otherFixture = null;
        foreach (var fixture in otherFixtures.Fixtures.Values)
        {
            if (fixture.Hard && (fixture.CollisionLayer & mask) != 0)
            {
                otherFixture = fixture;
                break;
            }
        }

        if (otherFixture == null)
            return false;

        var ourEv = new PreventCollideEvent(uid, target, body, otherBody, ourFixture, otherFixture);
        RaiseLocalEvent(uid, ref ourEv);
        if (ourEv.Cancelled)
            return false;

        var otherEv = new PreventCollideEvent(target, uid, otherBody, body, otherFixture, ourFixture);
        RaiseLocalEvent(target, ref otherEv);
        return !otherEv.Cancelled;
    }
}
