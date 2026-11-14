using Content.Shared.CombatMode;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Rejuvenate;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace.Limbs;

[TestFixture]
public sealed class LimbLobotomyTest
{
    [Test]
    public async Task ADrilledBrainCannotEnterCombat()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var combat = entities.System<SharedCombatModeSystem>();
            var where = new MapCoordinates(System.Numerics.Vector2.Zero, map.MapId);
            var human = entities.SpawnEntity("MobHuman", where);

            combat.SetInCombatMode(human, true);
            Assert.That(combat.IsInCombatMode(human), Is.True);

            var effect = new SurgeryEffectEvent(human, null, "Lobotomy");
            entities.EventBus.RaiseLocalEvent(human, ref effect);

            Assert.That(entities.HasComponent<SurgeryLobotomyComponent>(human), Is.True);
            Assert.That(combat.IsInCombatMode(human), Is.False);
            combat.SetInCombatMode(human, true);
            Assert.That(combat.IsInCombatMode(human), Is.False);

            entities.EventBus.RaiseLocalEvent(human, new RejuvenateEvent());
            Assert.That(entities.HasComponent<SurgeryLobotomyComponent>(human), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
