using System.Numerics;
using Content.Server.Speech.Components;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Inventory;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace.Limbs;

[TestFixture]
public sealed class LimbMmiBellyTest
{
    [Test]
    public async Task ClothesOverTheBellyGagAndBlindTheMmi()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var containers = entities.System<SharedContainerSystem>();
            var inventory = entities.System<InventorySystem>();
            var where = new MapCoordinates(Vector2.Zero, map.MapId);
            var human = entities.SpawnEntity("MobHuman", where);
            var mmi = entities.SpawnEntity("MMI", where);
            var shoes = entities.SpawnEntity("ClothingShoesColorBlack", where);
            var suit = entities.SpawnEntity("ClothingUniformJumpsuitColorGrey", where);

            Assert.That(containers.TryGetContainer(human, SurgeryCavityComponent.Abdomen, out var belly), Is.True);
            Assert.That(containers.Insert(mmi, belly), Is.True);
            Assert.That(entities.HasComponent<SurgeryMmiBellyComponent>(human), Is.True);
            Assert.That(entities.HasComponent<MMIComponent>(mmi), Is.True);
            Assert.That(entities.HasComponent<MumbleAccentComponent>(mmi), Is.False);
            Assert.That(entities.HasComponent<TemporaryBlindnessComponent>(mmi), Is.False);

            Assert.That(inventory.TryEquip(human, shoes, "shoes", silent: true, force: true), Is.True);
            Assert.That(entities.HasComponent<MumbleAccentComponent>(mmi), Is.False);

            Assert.That(inventory.TryEquip(human, suit, "jumpsuit", silent: true, force: true), Is.True);
            Assert.That(entities.HasComponent<MumbleAccentComponent>(mmi), Is.True);
            Assert.That(entities.HasComponent<TemporaryBlindnessComponent>(mmi), Is.True);

            Assert.That(inventory.TryUnequip(human, "jumpsuit", silent: true, force: true), Is.True);
            Assert.That(entities.HasComponent<MumbleAccentComponent>(mmi), Is.False);
            Assert.That(entities.HasComponent<TemporaryBlindnessComponent>(mmi), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ALodgedMmiKeepsItsBrain()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid human = default;
        EntityUid mmi = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var where = new MapCoordinates(Vector2.Zero, map.MapId);
            human = entities.SpawnEntity("MobHuman", where);
            mmi = entities.SpawnEntity("MMIFilled", where);
            var suit = entities.SpawnEntity("ClothingUniformJumpsuitColorGrey", where);
            Assert.That(entities.GetComponent<MMIComponent>(mmi).BrainSlot.Item, Is.Not.Null);
            Assert.That(entities.System<Content.Server.DeadSpace.Surgery.SurgeryCavitySystem>().TryLodge(human, Content.Shared.DeadSpace.Body.Limbs.LimbZone.Groin, mmi), Is.True);
            Assert.That(entities.System<InventorySystem>().TryEquip(human, suit, "jumpsuit", silent: true, force: true), Is.True);
        });

        await server.WaitRunTicks(60);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<MMIComponent>(mmi).BrainSlot.Item, Is.Not.Null);
        });

        await pair.CleanReturnAsync();
    }
}
