// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Shared.Damage.Components;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace.Limbs;

[TestFixture]
public sealed class LimbReflectTest
{
    [Test]
    public async Task AReflectedBulletLeavesNoInjury()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        EntityUid target = default;

        await server.WaitAssertion(() =>
        {
            target = entities.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
            var shield = entities.SpawnEntity("TestLimbReflectShield", new MapCoordinates(Vector2.Zero, map.MapId));
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(target, shield, checkActionBlocker: false), Is.True);

            var shooter = entities.SpawnEntity("MobHuman", new MapCoordinates(new Vector2(0f, -4f), map.MapId));
            for (var i = 0; i < 5; i++)
            {
                var bullet = entities.SpawnEntity("BulletPistol", new MapCoordinates(new Vector2(0f, -2.5f + i * 0.05f), map.MapId));
                entities.System<SharedGunSystem>().ShootProjectile(bullet, new Vector2(0f, 1f), Vector2.Zero, shooter, shooter);
            }
        });

        await pair.RunTicksSync(60);

        await server.WaitAssertion(() =>
        {
            var limbs = entities.GetComponent<LimbBodyComponent>(target);
            Assert.Multiple(() =>
            {
                entities.GetComponent<DamageableComponent>(target).Damage.DamageDict.TryGetValue("Piercing", out var piercing);
                Assert.That(piercing.Float(), Is.Zero, "piercing");
                Assert.That(limbs.Organs.Sum(o => o.Damage), Is.Zero, "organ damage");
                Assert.That(limbs.Organs.Any(o => o.Ruptured), Is.False, "ruptured organ");
                Assert.That(limbs.Zones.Sum(z => z.Bleed), Is.Zero, "bleed");
                Assert.That(limbs.Zones.Sum(z => z.Bone), Is.Zero, "bone");
                Assert.That(limbs.Zones.Sum(z => z.Flesh), Is.Zero, "flesh");
                Assert.That(limbs.Zones.Any(z => z.Embedded), Is.False, "shard");
            });
        });

        await pair.CleanReturnAsync();
    }

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestLimbReflectShield
  components:
  - type: Item
  - type: Reflect
    reflectProb: 1
    spread: 0
    reflects:
    - Energy
    - NonEnergy
";
}
