// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.DeadSpace;

[TestFixture]
public sealed class ToggleableClothingDeleteTest
{
    [Test]
    public async Task DeletingEquippedSuitDoesNotInsertItBack()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entities = server.EntMan;
        var inventory = server.System<InventorySystem>();
        var map = server.System<SharedMapSystem>();

        var backpack = EntityUid.Invalid;
        var suit = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            var human = entities.SpawnEntity("MobHuman", testMap.GridCoords);
            backpack = entities.SpawnEntity("ClothingBackpackRIGEVA", testMap.GridCoords);
            var clothing = entities.GetComponent<ToggleableClothingComponent>(backpack).ClothingUid;
            Assert.That(clothing.HasValue, Is.True);
            suit = clothing.Value;

            Assert.That(inventory.TryEquip(human, backpack, "back", silent: true, force: true), Is.True);
            Assert.That(inventory.TryEquip(human, suit, "outerClothing", silent: true, force: true), Is.True);
            entities.QueueDeleteEntity(suit);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.Deleted(suit), Is.True);
            Assert.That(entities.Deleted(backpack), Is.False);
            map.DeleteMap(testMap.MapId);
        });

        await pair.CleanReturnAsync();
    }
}
