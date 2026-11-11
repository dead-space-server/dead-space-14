using System.Numerics;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace.Surgery;

[TestFixture]
public sealed class LimbImplantSyringeTest
{
    [TestCase("SandevistanLoneImplanter", "Back")]
    [TestCase("MantisBladesLoneImplanter", "ArmLeft")]
    [TestCase("MonowireLoneImplanter", "ArmLeft")]
    public async Task ALoneSyringeSeatsItsImplant(string syringe, string slot)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var at = new MapCoordinates(Vector2.Zero, map.MapId);
            var implanters = entMan.System<SharedImplanterSystem>();
            var implants = entMan.System<LimbImplantSystem>();

            var human = entMan.SpawnEntity("MobHuman", at);
            var implanter = entMan.SpawnEntity(syringe, at);
            var comp = entMan.GetComponent<ImplanterComponent>(implanter);
            Assert.That(implanters.CanImplantLimb(human, comp, out _), Is.True);

            implanters.Implant(human, human, implanter, comp);
            Assert.That(implants.Occupied(human, slot), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
