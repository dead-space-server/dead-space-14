// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Movement.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Server.DeadSpace.Physics;

public sealed class MobSweepSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const float MinSpeed = 9f;
    private const float DefaultRadius = 0.35f;
    private const float Skin = 0.02f;

    private readonly Dictionary<EntityUid, (EntityUid Parent, Vector2 Local)> _safe = new();
    private readonly HashSet<EntityUid> _seen = new();
    private readonly List<EntityUid> _stale = new();

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(SharedPhysicsSystem));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _seen.Clear();
        var query = EntityQueryEnumerator<InputMoverComponent, PhysicsComponent, FixturesComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var body, out var fixtures, out var xform))
        {
            if (!body.CanCollide || body.BodyType == BodyType.Static)
                continue;

            if (!SweepPath.TryGet(xform, body, frameTime, 0f, _transform, out var mapId, out var from, out var to, MinSpeed))
                continue;

            _seen.Add(uid);
            if (_safe.TryGetValue(uid, out var safe) && safe.Parent == xform.ParentUid && xform.ParentUid.IsValid())
            {
                var safeWorld = Vector2.Transform(safe.Local, _transform.GetWorldMatrix(xform.ParentUid));
                if ((to - safeWorld).Length() <= (to - from).Length() * 2f + 1f)
                    from = safeWorld;
            }

            Sweep(uid, body, fixtures, mapId, from, to);
            _safe[uid] = (xform.ParentUid, xform.LocalPosition);
        }

        foreach (var uid in _safe.Keys)
        {
            if (!_seen.Contains(uid))
                _stale.Add(uid);
        }

        foreach (var uid in _stale)
        {
            _safe.Remove(uid);
        }

        _stale.Clear();
    }

    private void Sweep(
        EntityUid uid,
        PhysicsComponent body,
        FixturesComponent fixtures,
        MapId mapId,
        Vector2 from,
        Vector2 to)
    {
        var delta = to - from;
        var distance = delta.Length();
        if (distance < 0.001f)
            return;

        Fixture? ourFixture = null;
        var mask = 0;
        var radius = DefaultRadius;
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard)
                continue;

            ourFixture ??= fixture;
            mask |= fixture.CollisionMask;
            if (fixture.Shape is PhysShapeCircle circle)
                radius = circle.Radius;
        }

        if (ourFixture == null || mask == 0)
            return;

        var direction = delta / distance;
        var ray = new CollisionRay(from, direction, mask);
        foreach (var hit in _physics.IntersectRay(mapId, ray, distance, uid, false))
        {
            if (!Blocks(uid, body, ourFixture, mask, hit.HitEntity))
                continue;

            _transform.SetWorldPosition(uid, hit.HitPos - direction * MathF.Min(radius + Skin, hit.Distance));

            var velocity = body.LinearVelocity;
            var into = Vector2.Dot(velocity, direction);
            if (into > 0f)
                _physics.SetLinearVelocity(uid, velocity - direction * into, body: body);

            return;
        }
    }

    private bool Blocks(EntityUid uid, PhysicsComponent body, Fixture ourFixture, int mask, EntityUid target)
    {
        if (target == uid ||
            !TryComp<PhysicsComponent>(target, out var otherBody) ||
            !otherBody.CanCollide ||
            otherBody.BodyType != BodyType.Static ||
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
