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
    public async Task OcularImplantsToggleTheirOverlay()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var at = new MapCoordinates(Vector2.Zero, map.MapId);
            var implants = entMan.System<LimbImplantSystem>();
            var hud = entMan.System<LimbImplantHudSystem>();

            var human = entMan.SpawnEntity("MobHuman", at);
            var sec = entMan.SpawnEntity("LimbImplantSecurityHud", at);
            Assert.That(implants.TryInsert(human, LimbImplantSlots.Eyes, sec), Is.True);
            var implant = entMan.GetComponent<LimbImplantComponent>(sec);
            Assert.That(implant.Action, Is.Not.Null, "the toggle action is granted");
            Assert.That(entMan.HasComponent<ShowJobIconsComponent>(human), Is.False, "off until toggled");

            hud.SetToggled((sec, implant), human, true);
            Assert.That(entMan.HasComponent<ShowJobIconsComponent>(human), Is.True);
            hud.SetToggled((sec, implant), human, false);
            Assert.That(entMan.HasComponent<ShowJobIconsComponent>(human), Is.False);

            hud.SetToggled((sec, implant), human, true);
            Assert.That(implants.TryExtract(human, LimbImplantSlots.Eyes, out _), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<ShowJobIconsComponent>(human), Is.False, "pulled implant takes its overlay");
                Assert.That(implant.Toggled, Is.False);
            });

            var shield = entMan.SpawnEntity("LimbImplantFlashShield", at);
            Assert.That(implants.TryInsert(human, LimbImplantSlots.Eyes, shield), Is.True);
            hud.SetToggled((shield, entMan.GetComponent<LimbImplantComponent>(shield)), human, true);
            Assert.That(entMan.HasComponent<FlashImmunityComponent>(human), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
