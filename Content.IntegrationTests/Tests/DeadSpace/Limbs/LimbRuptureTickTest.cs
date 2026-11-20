using System.Numerics;
using Content.Shared.DeadSpace.Body.Limbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace.Limbs;

[TestFixture]
public sealed class LimbRuptureTickTest
{
    [Test]
    public async Task AMendedHeartInAWornChestStaysWhole()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid human = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var wounds = entities.System<LimbWoundSystem>();
            human = entities.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
            var limbs = entities.GetComponent<LimbBodyComponent>(human);
            var pool = LimbWoundRules.HealthFor(LimbZone.Torso, limbs);
            Assert.That(wounds.ApplyEdit(human, LimbZone.Torso, new LimbEdit { SetHealth = true, Health = pool * 0.05f, Bleed = 1f }), Is.True);
            Assert.That(wounds.ApplyOrganEdit(human, LimbOrgan.Heart, new LimbOrganEdit { SetRuptured = true, Ruptured = false, SetDamage = true, Damage = 0f }), Is.True);
            entities.EnsureComponent<LimbActiveComponent>(human);
        });

        await server.WaitRunTicks(120);

        await server.WaitAssertion(() =>
        {
            var limbs = server.EntMan.GetComponent<LimbBodyComponent>(human);
            Assert.That(limbs.Organs[(int) LimbOrgan.Heart].Ruptured, Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
