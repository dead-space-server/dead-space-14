// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Clothing.ReverseRig;
using Content.Shared.Atmos.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeadSpace.Clothing.ReverseRig;
using Content.Shared.Inventory;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.DeadSpace;

[TestFixture]
[NonParallelizable]
public sealed class ReverseRigTests
{
    private const string TargetPrototype = "ReverseRigTestTarget";
    private const string RemovableBackpackPrototype = "ReverseRigTestRemovableBackpack";
    private const string UnremoveableBackpackPrototype = "ReverseRigTestUnremoveableBackpack";
    private const string SelfUnremovableBackpackPrototype = "ReverseRigTestSelfUnremovableBackpack";
    private const string RigPrototype = "ClothingOuterRIGReverse";
    private const string NitrogenTankPrototype = "NitrogenTankFilled";

    private const float Epsilon = 0.0001f;

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ReverseRigTestTarget
  components:
  - type: Inventory
  - type: ContainerContainer
  - type: MobState

- type: entity
  id: ReverseRigTestRemovableBackpack
  components:
  - type: Clothing
    slots: [BACK]

- type: entity
  id: ReverseRigTestUnremoveableBackpack
  parent: ReverseRigTestRemovableBackpack
  components:
  - type: Unremoveable
    deleteOnDrop: false

- type: entity
  id: ReverseRigTestSelfUnremovableBackpack
  parent: ReverseRigTestRemovableBackpack
  components:
  - type: SelfUnremovableClothing
";

    [Test]
    public async Task HappyPathEquipsAndStowsAttachedBackpack()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entityManager = server.EntMan;
        var inventory = server.System<InventorySystem>();
        var map = server.System<SharedMapSystem>();

        await server.WaitAssertion(() =>
        {
            var target = entityManager.SpawnEntity(TargetPrototype, testMap.GridCoords);
            var rig = entityManager.SpawnEntity(RigPrototype, testMap.GridCoords);
            var component = entityManager.GetComponent<ReverseRigComponent>(rig);
            var backpack = component.BackpackUid;

            Assert.That(backpack, Is.Not.Null);
            Assert.That(inventory.TryEquip(target, rig, "outerClothing"), Is.True);
            AssertSlot(inventory, target, "outerClothing", rig);
            AssertSlot(inventory, target, component.Slot, backpack!.Value);

            Assert.That(inventory.TryUnequip(target, "outerClothing"), Is.True);
            AssertSlotEmpty(inventory, target, "outerClothing");
            AssertSlotEmpty(inventory, target, component.Slot);
            Assert.That(component.BackpackContainer!.ContainedEntity, Is.EqualTo(backpack));

            map.DeleteMap(testMap.MapId);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovableBackpackIsDisplacedNormally()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entityManager = server.EntMan;
        var inventory = server.System<InventorySystem>();
        var map = server.System<SharedMapSystem>();

        await server.WaitAssertion(() =>
        {
            var target = entityManager.SpawnEntity(TargetPrototype, testMap.GridCoords);
            var original = entityManager.SpawnEntity(RemovableBackpackPrototype, testMap.GridCoords);
            var rig = entityManager.SpawnEntity(RigPrototype, testMap.GridCoords);
            var component = entityManager.GetComponent<ReverseRigComponent>(rig);

            Assert.That(inventory.TryEquip(target, original, component.Slot, force: true), Is.True);
            Assert.That(inventory.TryEquip(target, rig, "outerClothing"), Is.True);
            AssertSlot(inventory, target, "outerClothing", rig);
            AssertSlot(inventory, target, component.Slot, component.BackpackUid!.Value);
            Assert.That(entityManager.GetComponent<TransformComponent>(original).ParentUid, Is.Not.EqualTo(target));

            map.DeleteMap(testMap.MapId);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(UnremoveableBackpackPrototype)]
    [TestCase(SelfUnremovableBackpackPrototype)]
    public async Task NormalEquipRejectsBlockedBackpack(string blockerPrototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entityManager = server.EntMan;
        var inventory = server.System<InventorySystem>();
        var map = server.System<SharedMapSystem>();

        await server.WaitAssertion(() =>
        {
            var target = entityManager.SpawnEntity(TargetPrototype, testMap.GridCoords);
            var blocker = entityManager.SpawnEntity(blockerPrototype, testMap.GridCoords);
            var rig = entityManager.SpawnEntity(RigPrototype, testMap.GridCoords);
            var component = entityManager.GetComponent<ReverseRigComponent>(rig);

            Assert.That(inventory.TryEquip(target, blocker, component.Slot, force: true), Is.True);
            Assert.That(inventory.TryEquip(target, rig, "outerClothing"), Is.False);
            AssertSlot(inventory, target, component.Slot, blocker);
            AssertSlotEmpty(inventory, target, "outerClothing");
            Assert.That(component.BackpackContainer!.ContainedEntity, Is.EqualTo(component.BackpackUid));

            map.DeleteMap(testMap.MapId);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ForcedEquipRollsBackWhenBackpackCannotBeRemoved()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entityManager = server.EntMan;
        var inventory = server.System<InventorySystem>();
        var map = server.System<SharedMapSystem>();

        EntityUid target = default;
        EntityUid blocker = default;
        EntityUid rig = default;

        await server.WaitAssertion(() =>
        {
            target = entityManager.SpawnEntity(TargetPrototype, testMap.GridCoords);
            blocker = entityManager.SpawnEntity(UnremoveableBackpackPrototype, testMap.GridCoords);
            rig = entityManager.SpawnEntity(RigPrototype, testMap.GridCoords);
            var component = entityManager.GetComponent<ReverseRigComponent>(rig);

            Assert.That(inventory.TryEquip(target, blocker, component.Slot, force: true), Is.True);
            inventory.TryEquip(target, rig, "outerClothing", force: true);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var component = entityManager.GetComponent<ReverseRigComponent>(rig);
            AssertSlot(inventory, target, component.Slot, blocker);
            AssertSlotEmpty(inventory, target, "outerClothing");
            Assert.That(component.BackpackContainer!.ContainedEntity, Is.EqualTo(component.BackpackUid));

            map.DeleteMap(testMap.MapId);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExhaustedAndReplacedTankConservesGas()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entityManager = server.EntMan;
        var itemSlots = server.System<ItemSlotsSystem>();
        var map = server.System<SharedMapSystem>();

        EntityUid rig = default;
        EntityUid backpack = default;
        EntityUid source = default;
        float workingReserve = default;
        float replacementTotal = default;

        await server.WaitAssertion(() =>
        {
            rig = entityManager.SpawnEntity(RigPrototype, testMap.GridCoords);
            backpack = entityManager.GetComponent<ReverseRigComponent>(rig).BackpackUid!.Value;
            Assert.That(itemSlots.TryGetSlot(backpack, ReverseRigGasBridgeSystem.TankSlotId, out var slot), Is.True);
            source = slot.Item!.Value;
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var buffer = entityManager.GetComponent<GasTankComponent>(backpack).Air;
            var sourceAir = entityManager.GetComponent<GasTankComponent>(source).Air;
            var sourceTracking = entityManager.GetComponent<ReverseRigBackpackComponent>(backpack);

            workingReserve = buffer.TotalMoles;
            Assert.That(workingReserve, Is.GreaterThan(Epsilon));
            Assert.That(sourceTracking.BufferSourceUid, Is.EqualTo(source));

            var refillAmount = workingReserve / 5f;
            Assert.That(sourceAir.TotalMoles, Is.GreaterThan(refillAmount));
            buffer.Remove(refillAmount);
            sourceAir.Remove(sourceAir.TotalMoles - refillAmount);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var buffer = entityManager.GetComponent<GasTankComponent>(backpack).Air;
            var sourceAir = entityManager.GetComponent<GasTankComponent>(source).Air;

            Assert.That(sourceAir.TotalMoles, Is.LessThanOrEqualTo(Epsilon));
            Assert.That(buffer.TotalMoles, Is.EqualTo(workingReserve).Within(Epsilon));
            Assert.That(buffer.TotalMoles + sourceAir.TotalMoles,
                Is.EqualTo(workingReserve).Within(Epsilon));

            Assert.That(itemSlots.TryGetSlot(backpack, ReverseRigGasBridgeSystem.TankSlotId, out var slot), Is.True);
            Assert.That(itemSlots.TryEject(backpack, slot, null, out var ejected), Is.True);
            Assert.That(ejected, Is.EqualTo(source));
            Assert.That(buffer.TotalMoles, Is.LessThanOrEqualTo(Epsilon));
            Assert.That(sourceAir.TotalMoles, Is.EqualTo(workingReserve).Within(Epsilon));

            var replacement = entityManager.SpawnEntity(NitrogenTankPrototype, testMap.GridCoords);
            var replacementAir = entityManager.GetComponent<GasTankComponent>(replacement).Air;
            replacementTotal = replacementAir.TotalMoles;
            Assert.That(itemSlots.TryInsert(backpack, slot, replacement, null), Is.True);

            source = replacement;
            Assert.That(replacementAir.TotalMoles + buffer.TotalMoles, Is.EqualTo(replacementTotal).Within(Epsilon));
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var buffer = entityManager.GetComponent<GasTankComponent>(backpack).Air;
            var replacementAir = entityManager.GetComponent<GasTankComponent>(source).Air;
            var sourceTracking = entityManager.GetComponent<ReverseRigBackpackComponent>(backpack);

            Assert.That(buffer.TotalMoles, Is.GreaterThan(Epsilon));
            Assert.That(replacementAir.TotalMoles, Is.LessThan(replacementTotal));
            Assert.That(sourceTracking.BufferSourceUid, Is.EqualTo(source));
            Assert.That(buffer.TotalMoles + replacementAir.TotalMoles,
                Is.EqualTo(replacementTotal).Within(Epsilon));

            map.DeleteMap(testMap.MapId);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DestroyedSourceKeepsIncompatibleReserveUntilConsumed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entityManager = server.EntMan;
        var itemSlots = server.System<ItemSlotsSystem>();
        var map = server.System<SharedMapSystem>();

        EntityUid backpack = default;
        EntityUid source = default;
        float workingReserve = default;

        await server.WaitAssertion(() =>
        {
            var rig = entityManager.SpawnEntity(RigPrototype, testMap.GridCoords);
            backpack = entityManager.GetComponent<ReverseRigComponent>(rig).BackpackUid!.Value;
            Assert.That(itemSlots.TryGetSlot(backpack, ReverseRigGasBridgeSystem.TankSlotId, out var slot), Is.True);
            source = slot.Item!.Value;
        });

        await server.WaitRunTicks(1);

        float replacementTotal = default;
        EntityUid replacement = default;

        await server.WaitAssertion(() =>
        {
            var buffer = entityManager.GetComponent<GasTankComponent>(backpack).Air;
            workingReserve = buffer.TotalMoles;
            Assert.That(workingReserve, Is.GreaterThan(Epsilon));

            entityManager.DeleteEntity(source);
            Assert.That(buffer.TotalMoles, Is.EqualTo(workingReserve).Within(Epsilon));

            replacement = entityManager.SpawnEntity(NitrogenTankPrototype, testMap.GridCoords);
            var replacementAir = entityManager.GetComponent<GasTankComponent>(replacement).Air;
            replacementTotal = replacementAir.TotalMoles;
            Assert.That(itemSlots.TryGetSlot(backpack, ReverseRigGasBridgeSystem.TankSlotId, out var slot), Is.True);
            Assert.That(itemSlots.TryInsert(backpack, slot, replacement, null), Is.True);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var buffer = entityManager.GetComponent<GasTankComponent>(backpack).Air;
            var replacementAir = entityManager.GetComponent<GasTankComponent>(replacement).Air;

            Assert.That(buffer.TotalMoles, Is.EqualTo(workingReserve).Within(Epsilon));
            Assert.That(replacementAir.TotalMoles, Is.EqualTo(replacementTotal).Within(Epsilon));

            buffer.Remove(buffer.TotalMoles);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var buffer = entityManager.GetComponent<GasTankComponent>(backpack).Air;
            var replacementAir = entityManager.GetComponent<GasTankComponent>(replacement).Air;

            Assert.That(buffer.TotalMoles, Is.GreaterThan(Epsilon));
            Assert.That(replacementAir.TotalMoles, Is.LessThan(replacementTotal));
            Assert.That(buffer.TotalMoles + replacementAir.TotalMoles,
                Is.EqualTo(replacementTotal).Within(Epsilon));

            map.DeleteMap(testMap.MapId);
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertSlot(InventorySystem inventory, EntityUid target, string slot, EntityUid expected)
    {
        Assert.That(inventory.TryGetSlotEntity(target, slot, out var actual), Is.True);
        Assert.That(actual, Is.EqualTo(expected));
    }

    private static void AssertSlotEmpty(InventorySystem inventory, EntityUid target, string slot)
    {
        Assert.That(inventory.TryGetSlotEntity(target, slot, out _), Is.False);
    }
}
