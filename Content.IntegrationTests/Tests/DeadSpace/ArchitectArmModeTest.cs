// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.ComponentModeSwitcher;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace;

[TestFixture]
[NonParallelizable]
public sealed class ArchitectArmModeTest
{
    [Test]
    public async Task RcdIsInitializedAfterReturningFromCombatMode()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var switcherSystem = server.System<ComponentModeSwitcherSystem>();
        EntityUid arm = default;

        await server.WaitPost(() =>
        {
            arm = entMan.SpawnEntity("ArchitectIntegratedArm", MapCoordinates.Nullspace);

            var initialRcd = entMan.GetComponent<RCDComponent>(arm);
            Assert.That(initialRcd.ProtoId.Id, Is.Not.EqualTo("Invalid"));
            entMan.EventBus.RaiseLocalEvent(arm, new RCDSystemMessage("FloorSteel"));
            Assert.That(entMan.HasComponent<MeleeWeaponComponent>(arm), Is.False);

            var switcher = entMan.GetComponent<ComponentModeSwitcherComponent>(arm);
            Assert.That(switcherSystem.TryCycleMode((arm, switcher)), Is.True);
            Assert.That(entMan.HasComponent<RCDComponent>(arm), Is.False);
            Assert.That(entMan.HasComponent<MeleeWeaponComponent>(arm), Is.True);

            Assert.That(switcherSystem.TryCycleMode((arm, switcher)), Is.True);
            Assert.That(entMan.HasComponent<MeleeWeaponComponent>(arm), Is.False);

            var restoredRcd = entMan.GetComponent<RCDComponent>(arm);
            Assert.That(restoredRcd.ProtoId.Id, Is.Not.EqualTo("Invalid"));
            Assert.That(restoredRcd.ProtoId.Id, Is.EqualTo("FloorSteel"));
        });

        await server.WaitPost(() => entMan.DeleteEntity(arm));
        await pair.CleanReturnAsync();
    }
}