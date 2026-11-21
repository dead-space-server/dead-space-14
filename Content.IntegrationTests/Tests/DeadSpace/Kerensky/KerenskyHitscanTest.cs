using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Tests.Helpers;
using Content.Shared.DeadSpace.Dodge;
using Content.Shared.Physics;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.DeadSpace.Kerensky;

[TestFixture]
public sealed class KerenskyHitscanTest
{
    public sealed class HitscanListenerSystem : TestListenerSystem<HitscanRaycastFiredEvent>;

    [Test]
    public async Task DodgedHitscanContinuesToTheNextTarget()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var ent = server.EntMan;
        EntityUid dodger = default;
        EntityUid secondTarget = default;
        EntityUid scan = default;

        await server.WaitPost(() =>
        {
            dodger = SpawnTarget(new Vector2(1.5f, 0.5f));
            secondTarget = SpawnTarget(new Vector2(2.5f, 0.5f));
            scan = ent.SpawnEntity(null, new MapCoordinates(new Vector2(0.5f, 0.5f), map.MapId));
            ent.AddComponent<HitscanBasicRaycastComponent>(scan).CollisionMask = CollisionGroup.MobMask;
            ent.EnsureComponent<TestListenerComponent>(scan);
            ent.EnsureComponent<DodgeComponent>(dodger).PassiveChance = 1f;
        });
        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var ev = new HitscanTraceEvent
            {
                FromCoordinates = ent.GetComponent<TransformComponent>(scan).Coordinates,
                ShotDirection = Vector2.UnitX,
                Gun = scan,
                Target = secondTarget,
            };
            ent.EventBus.RaiseLocalEvent(scan, ref ev);
        });

        await server.WaitAssertion(() =>
        {
            var hit = server.System<HitscanListenerSystem>().GetEvents(scan).Single();
            Assert.That(hit.Data.HitEntity, Is.EqualTo(secondTarget));
            Assert.That(hit.Data.HitEntity, Is.Not.EqualTo(dodger));
            Assert.That(hit.Data.IgnoredEntities, Does.Contain(dodger));
        });

        await server.WaitPost(() =>
        {
            ent.RemoveComponent<DodgeComponent>(dodger);
            var ev = new HitscanTraceEvent
            {
                FromCoordinates = ent.GetComponent<TransformComponent>(scan).Coordinates,
                ShotDirection = Vector2.UnitX,
                Gun = scan,
                Target = secondTarget,
            };
            ent.EventBus.RaiseLocalEvent(scan, ref ev);
        });

        await server.WaitAssertion(() =>
        {
            var hits = server.System<HitscanListenerSystem>().GetEvents(scan);
            Assert.That(hits.Count, Is.EqualTo(2));
            Assert.That(hits.Last().Data.HitEntity, Is.EqualTo(dodger));
        });
        await pair.CleanReturnAsync();

        EntityUid SpawnTarget(Vector2 position)
        {
            var uid = ent.SpawnEntity(null, new MapCoordinates(position, map.MapId));
            var body = ent.AddComponent<PhysicsComponent>(uid);
            ent.System<FixtureSystem>().TryCreateFixture(
                uid,
                new PhysShapeCircle(0.35f),
                "hit",
                collisionLayer: (int) CollisionGroup.MobMask,
                body: body);
            ent.System<SharedPhysicsSystem>().SetCanCollide(uid, true, body: body);
            return uid;
        }
    }
}
