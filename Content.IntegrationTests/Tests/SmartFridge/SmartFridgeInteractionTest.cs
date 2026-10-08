using System.Collections.Generic;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.SmartFridge;
using Robust.Client.Timing;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.SmartFridge;

public sealed class SmartFridgeInteractionTest : InteractionTest
{
    private const string SmartFridgeProtoId = "SmartFridge";
    private const string SampleItemProtoId = "FoodAmbrosiaVulgaris";
    private const string SampleDumpableAndInsertableId = "PillCanisterSomething";
    private const int SampleDumpableCount = 5;
    private const string SampleDumpableId = "ChemBagSomething";

    [TestPrototypes]
    private const string TestPrototypes = $@"
- type: entity
  parent: PillCanister
  id: {SampleDumpableAndInsertableId}
  components:
  - type: EntityTableContainerFill
    containers:
      storagebase:
        id: PillCopper
        amount: 5

- type: entity
  parent: ChemBag
  id: {SampleDumpableId}
  components:
  - type: EntityTableContainerFill
    containers:
      storagebase:
        id: PillCopper
        amount: 5
";

    [Test]
    public async Task InsertAndDispenseItemTest()
    {
        await PlaceInHands(SampleItemProtoId);

        await SpawnTarget(SmartFridgeProtoId);
        var fridge = SEntMan.GetEntity(Target.Value);
        var component = SEntMan.GetComponent<SmartFridgeComponent>(fridge);

        await SpawnEntity("APCBasic", SEntMan.GetCoordinates(TargetCoords));
        await RunTicks(1);

        // smartfridge spawns with nothing
        Assert.That(component.Entries, Is.Empty);
        await InteractUsing(SampleItemProtoId);

        // smartfridge now has items
        Assert.That(component.Entries, Is.Not.Empty);
        Assert.That(component.ContainedEntries[component.Entries[0]], Is.Not.Empty);

        // open the UI
        await Activate();
        Assert.That(IsUiOpen(SmartFridgeUiKey.Key));

        // dispense an item
        await SendBui(SmartFridgeUiKey.Key, new SmartFridgeDispenseItemMessage(component.Entries[0]));

        // assert that the listing is still there
        Assert.That(component.Entries, Is.Not.Empty);
        // but empty
        Assert.That(component.ContainedEntries[component.Entries[0]], Is.Empty);

        // and that the thing we dispensed is actually around
        await AssertEntityLookup(
            ("APCBasic", 1),
            (SampleItemProtoId, 1)
        );
    }

    [Test]
    public async Task InsertDumpableInsertableItemTest()
    {
        await PlaceInHands(SampleItemProtoId);

        await SpawnTarget(SmartFridgeProtoId);
        var fridge = SEntMan.GetEntity(Target.Value);
        var component = SEntMan.GetComponent<SmartFridgeComponent>(fridge);

        await SpawnEntity("APCBasic", SEntMan.GetCoordinates(TargetCoords));
        await RunTicks(1);

        await InteractUsing(SampleDumpableAndInsertableId);

        // smartfridge now has one item only
        Assert.That(component.Entries, Is.Not.Empty);
        Assert.That(component.ContainedEntries[component.Entries[0]].Count, Is.EqualTo(1));
    }

    [Test]
    public async Task InsertDumpableItemTest()
    {
        await PlaceInHands(SampleItemProtoId);

        await SpawnTarget(SmartFridgeProtoId);
        var fridge = SEntMan.GetEntity(Target.Value);
        var component = SEntMan.GetComponent<SmartFridgeComponent>(fridge);

        await SpawnEntity("APCBasic", SEntMan.GetCoordinates(TargetCoords));
        await RunTicks(1);

        await InteractUsing(SampleDumpableId);

        // smartfridge now has N items
        Assert.That(component.Entries, Is.Not.Empty);
        Assert.That(component.ContainedEntries[component.Entries[0]].Count, Is.EqualTo(SampleDumpableCount));
    }

    //DS-14 start
    [Test]
    public async Task StateApplicationRemovalKeepsClientListing()
    {
        await PlaceInHands(SampleItemProtoId);
        await SpawnTarget(SmartFridgeProtoId);
        await SpawnEntity("APCBasic", SEntMan.GetCoordinates(TargetCoords));
        await RunTicks(1);
        await InteractUsing(SampleItemProtoId);
        await RunTicks(10);

        var clientFridge = CEntMan.GetEntity(Target.Value);
        var clientComp = CEntMan.GetComponent<SmartFridgeComponent>(clientFridge);
        Assert.That(CountContained(clientComp), Is.EqualTo(1));

        var timing = Client.ResolveDependency<IClientGameTiming>();
        await Client.WaitPost(() =>
        {
            var containers = CEntMan.System<SharedContainerSystem>();
            Assert.That(containers.TryGetContainer(clientFridge, clientComp.Container, out var container));
            var inside = new List<EntityUid>(container.ContainedEntities);
            Assert.That(inside, Is.Not.Empty);

            using (timing.StartStateApplicationArea())
            {
                foreach (var item in inside)
                {
                    containers.Remove(
                        (item, CEntMan.GetComponent<TransformComponent>(item), CEntMan.GetComponent<MetaDataComponent>(item)),
                        container,
                        reparent: false,
                        force: true);
                }
            }
        });

        Assert.That(CountContained(clientComp), Is.EqualTo(1));
    }

    private static int CountContained(SmartFridgeComponent component)
    {
        var count = 0;
        foreach (var set in component.ContainedEntries.Values)
            count += set.Count;
        return count;
    }
    //DS-14 end
}
