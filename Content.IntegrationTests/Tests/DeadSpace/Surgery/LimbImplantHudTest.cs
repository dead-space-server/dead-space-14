using System.Numerics;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Flash.Components;
using Content.Shared.Overlays;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace.Surgery;

[TestFixture]
public sealed class LimbImplantHudTest
{
    [Test]
    public async Task OcularImplantsWorkWhileSeated()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var at = new MapCoordinates(Vector2.Zero, map.MapId);
            var implants = entMan.System<LimbImplantSystem>();

            var human = entMan.SpawnEntity("MobHuman", at);
            var sec = entMan.SpawnEntity("LimbImplantSecurityHud", at);
            Assert.That(implants.TryInsert(human, LimbImplantSlots.Eyes, sec), Is.True);
            var implant = entMan.GetComponent<LimbImplantComponent>(sec);
            Assert.Multiple(() =>
            {
                Assert.That(implant.Action, Is.Null, "no toggle action");
                Assert.That(entMan.HasComponent<ShowJobIconsComponent>(human), Is.True, "on as soon as it is seated");
            });

            Assert.That(implants.TryExtract(human, LimbImplantSlots.Eyes, out _), Is.True);
            Assert.That(entMan.HasComponent<ShowJobIconsComponent>(human), Is.False, "pulled implant takes its overlay");

            var shield = entMan.SpawnEntity("LimbImplantFlashShield", at);
            Assert.That(implants.TryInsert(human, LimbImplantSlots.Eyes, shield), Is.True);
            Assert.That(entMan.HasComponent<FlashImmunityComponent>(human), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
