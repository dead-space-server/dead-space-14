// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Body.Systems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.DeadSpace;

[TestFixture]
[TestOf(typeof(InternalsSystem))]
public sealed class InternalsMixTest
{
    [Test]
    [TestCase(Gas.Plasma)]
    [TestCase(Gas.Tritium)]
    [TestCase(Gas.CarbonDioxide)]
    [TestCase(Gas.NitrousOxide)]
    [TestCase(Gas.WaterVapor)]
    [TestCase(Gas.Ammonia)]
    public async Task AnOxygenTankWithAnotherGasStaysOn(Gas extra)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entities = server.EntMan;
        EntityUid dummy = default;
        GasTankComponent gasTank = null;
        var full = 0f;

        await server.WaitAssertion(() =>
        {
            dummy = entities.Spawn("TestInternalsMixDummy", testMap.MapCoords);
            Assert.That(entities.System<InventorySystem>().TryGetSlotEntity(dummy, "suitstorage", out var tank), Is.True);
            gasTank = entities.GetComponent<GasTankComponent>(tank.Value);
            var total = gasTank.Air.TotalMoles;
            gasTank.Air.Clear();
            gasTank.Air.SetMoles(Gas.Oxygen, total * 0.95f);
            gasTank.Air.SetMoles(extra, total * 0.05f);
            full = gasTank.Air.TotalMoles;
            if (!gasTank.IsConnected)
                entities.System<SharedGasTankSystem>().ConnectToInternals((tank.Value, gasTank), dummy);
            Assert.That(gasTank.IsConnected, Is.True);
        });

        await pair.RunSeconds(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.System<InternalsSystem>().AreInternalsWorking(dummy), Is.True,
                $"an oxygen tank with {extra} was disconnected");
            Assert.That(gasTank.Air.TotalMoles, Is.LessThan(full), "nobody breathed from the tank");
        });

        await pair.CleanReturnAsync();
    }

    [TestPrototypes]
    private const string Prototypes = @"
- type: startingGear
  id: InternalsMixDummyGear
  equipment:
    mask: ClothingMaskBreath
    suitstorage: OxygenTankFilled

- type: entity
  id: TestInternalsMixDummy
  parent: MobHuman
  components:
    - type: Loadout
      prototypes: [InternalsMixDummyGear]
";
}
