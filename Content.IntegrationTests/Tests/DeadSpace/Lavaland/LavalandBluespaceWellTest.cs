// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Weapons.Ranged.Upgrades;
using Content.Shared.Weapons.Ranged.Upgrades.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace.Lavaland;

[TestFixture]
public sealed class LavalandBluespaceWellTest
{
    [TestCase("WeaponShotgunSawnKinetik", false)]
    [TestCase("WeaponProtoKineticAccelerator", true)]
    public async Task ExplosiveUpgradeRespectsWeaponBlacklist(string prototype, bool allowed)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        await server.WaitAssertion(() =>
        {
            var gun = entities.SpawnEntity(prototype, MapCoordinates.Nullspace);
            var upgrade = entities.SpawnEntity("PKAUpgradeExplosiveLavaland", MapCoordinates.Nullspace);
            var ev = new AfterInteractUsingEvent(gun, upgrade, gun, entities.GetComponent<TransformComponent>(gun).Coordinates, true);
            entities.EventBus.RaiseLocalEvent(gun, ev);
            var upgrades = server.System<GunUpgradeSystem>().GetCurrentUpgrades((gun, entities.GetComponent<UpgradeableGunComponent>(gun)));
            Assert.That(upgrades.Count, Is.EqualTo(allowed ? 1 : 0));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BagTransferPreservesOreAndLeavesNonOre()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            Dirty = true,
        });
        var server = pair.Server;
        var entities = server.EntMan;
        await server.WaitAssertion(() =>
        {
            var well = entities.SpawnEntity("LavalandBluespaceWell", MapCoordinates.Nullspace);
            var bag = entities.SpawnEntity("ClothingBackpack", MapCoordinates.Nullspace);
            var ore = entities.SpawnEntity("SteelOre1", MapCoordinates.Nullspace);
            var sheet = entities.SpawnEntity("SheetSteel1", MapCoordinates.Nullspace);
            var storage = server.System<StorageSystem>();
            Assert.That(storage.Insert(bag, ore, out _, playSound: false), Is.True);
            Assert.That(storage.Insert(bag, sheet, out _, playSound: false), Is.True);
            var ev = new InteractUsingEvent(bag, bag, well, entities.GetComponent<TransformComponent>(well).Coordinates);
            entities.EventBus.RaiseLocalEvent(well, ev);
            var component = entities.GetComponent<LavalandBluespaceWellComponent>(well);
            Assert.Multiple(() =>
            {
                Assert.That(ev.Handled, Is.True);
                Assert.That(component.Ore.ContainedEntities, Does.Contain(ore));
                Assert.That(entities.GetComponent<StackComponent>(ore).Count, Is.EqualTo(1));
                Assert.That(entities.GetComponent<StorageComponent>(bag).Container.ContainedEntities, Does.Contain(sheet));
                Assert.That(component.Ore.ContainedEntities, Does.Not.Contain(sheet));
            });
        });
        await pair.CleanReturnAsync();
    }
}
