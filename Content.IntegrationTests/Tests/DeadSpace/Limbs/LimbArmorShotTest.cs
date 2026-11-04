using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace.Limbs;

[TestFixture]
public sealed class LimbArmorShotTest
{
    [TestCase("ClothingOuterHardsuitSecurity")]
    [TestCase("ClothingOuterHardsuitDeathsquad")]
    public async Task ArmorShieldsTheChestFromRifleRounds(string worn)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var ent = server.EntMan;
        EntityUid shooter = default;
        EntityUid body = default;
        EntityUid gun = default;
        await server.WaitPost(() =>
        {
            shooter = ent.SpawnEntity("MobHuman", new MapCoordinates(new Vector2(0.5f, 0.5f), map.MapId));
            body = ent.SpawnEntity("MobHuman", new MapCoordinates(new Vector2(0.5f, 2.5f), map.MapId));
            gun = ent.SpawnEntity("WeaponRifleAsVal", new MapCoordinates(new Vector2(0.5f, 0.5f), map.MapId));
            var suit = ent.SpawnEntity(worn, ent.GetComponent<TransformComponent>(body).Coordinates);
            Assert.That(ent.System<InventorySystem>().TryEquip(body, suit, "outerClothing", silent: true, force: true), Is.True);
        });
        await pair.RunTicksSync(5);

        for (var i = 0; i < 6; i++)
        {
            await server.WaitPost(() =>
            {
                var scan = ent.SpawnEntity("BulletLightRifleTrace", new MapCoordinates(new Vector2(0.5f, 0.5f), map.MapId));
                var ev = new HitscanRaycastFiredEvent
                {
                    Data = new HitscanRaycastFiredData
                    {
                        ShotDirection = new Vector2(0, 1),
                        HitEntity = body,
                        Gun = gun,
                        Hitscan = scan,
                        Shooter = shooter,
                    },
                };
                ent.EventBus.RaiseLocalEvent(scan, ref ev);
            });
            await pair.RunTicksSync(3);
        }

        await server.WaitAssertion(() =>
        {
            var limbs = ent.GetComponent<LimbBodyComponent>(body);
            var torso = limbs.Zones[(int) LimbZone.Torso];
            Assert.That(torso.Health, Is.GreaterThan(LimbWoundRules.HealthFor(LimbZone.Torso, limbs) * 0.5f));
            Assert.That(ent.GetComponent<MobStateComponent>(body).CurrentState, Is.EqualTo(MobState.Alive));
        });
        await pair.CleanReturnAsync();
    }
}
