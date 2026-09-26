using System.Collections.Generic;
using System.Linq;
using Content.Server.DeadSpace.AshWalkers;
using Content.Server.Temperature.Systems;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.DeadSpace.AshWalkers;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Maps;
using Content.Shared.Placeable;
using Content.Shared.Stacks;
using Content.Shared.Temperature.Components;
using Content.Shared.Weather;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.DeadSpace.Lavaland;

[TestFixture]
public sealed class AshWalkerSettlementTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: AshWalkerHearth
          id: TestAshWalkerHearth
          components:
          - type: AshWalkerHearth
            fuelPerItem: 2
            maxFuel: 4
            heatingPower: 1000000

        - type: entity
          parent: BaseItem
          id: TestAshWalkerThermalItem
          components:
          - type: Sprite
            sprite: Objects/Materials/ore.rsi
            state: iron
          - type: Temperature
            currentTemperature: 293
        """;

    [Test]
    public async Task HearthNeedsFuelTracksPlacedItemsAndRespectsPause()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = server.System<SharedMapSystem>();
        var map = await pair.CreateTestMap();
        Entity<AshWalkerHearthComponent> hearth = default;
        EntityUid item = default;
        float remaining = 0;

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity("TestAshWalkerHearth", map.GridCoords);
            hearth = (uid, entMan.GetComponent<AshWalkerHearthComponent>(uid));
            item = entMan.SpawnEntity("TestAshWalkerThermalItem", map.GridCoords);
        });
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<ItemPlacerComponent>(hearth).PlacedEntities, Does.Contain(item));
            Assert.That(entMan.GetComponent<TemperatureComponent>(item).CurrentTemperature, Is.EqualTo(293));
            var user = entMan.SpawnEntity("MobAshWalker", new EntityCoordinates(map.Grid, 2.5f, 0.5f));
            var fuel = entMan.SpawnEntity("MaterialFungalWood3", new EntityCoordinates(map.Grid, 2.5f, 0.5f));
            var invalidFuel = entMan.SpawnEntity("MaterialCloth1", new EntityCoordinates(map.Grid, 2.5f, 0.5f));
            entMan.EventBus.RaiseLocalEvent(hearth, new InteractUsingEvent(user, invalidFuel, hearth, map.GridCoords));
            Assert.That(hearth.Comp.FuelRemaining, Is.Zero);
            for (var i = 0; i < 3; i++)
                entMan.EventBus.RaiseLocalEvent(hearth, new InteractUsingEvent(user, fuel, hearth, map.GridCoords));
            Assert.That(entMan.GetComponent<StackComponent>(fuel).Count, Is.EqualTo(1));
            Assert.That(hearth.Comp.FuelRemaining, Is.EqualTo(4));
        });
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<TemperatureComponent>(item).CurrentTemperature, Is.EqualTo(hearth.Comp.MaxTemperature));
            Assert.That(hearth.Comp.FuelRemaining, Is.InRange(0.01f, 3.99f));
            remaining = hearth.Comp.FuelRemaining;
            maps.SetPaused(map.MapUid, true);
        });
        await pair.RunTicksSync(120);
        await server.WaitAssertion(() =>
        {
            Assert.That(hearth.Comp.FuelRemaining, Is.EqualTo(remaining));
            maps.SetPaused(map.MapUid, false);
        });
        await pair.RunTicksSync(300);
        await server.WaitAssertion(() =>
        {
            Assert.That(hearth.Comp.FuelRemaining, Is.Zero);
            Assert.That(hearth.Comp.Burning, Is.False);
            server.System<TemperatureSystem>().ForceChangeTemperature(item, 293);
        });
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() =>
            Assert.That(entMan.GetComponent<TemperatureComponent>(item).CurrentTemperature, Is.EqualTo(293)));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WildPlantHarvestCanBeReseededAndRegrows()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var trays = server.System<PlantTraySystem>();
        var plants = server.System<PlantSystem>();
        var harvests = server.System<PlantHarvestSystem>();
        var holders = server.System<PlantHolderSystem>();
        var map = await pair.CreateTestMap();

        List<EntityUid> Produce()
        {
            var result = new List<EntityUid>();
            var query = entMan.EntityQueryEnumerator<ProduceComponent>();
            while (query.MoveNext(out var uid, out var produce))
            {
                if (produce.PlantProtoId == "LavalandPolyporePlant" && !entMan.IsQueuedForDeletion(uid))
                    result.Add(uid);
            }
            return result;
        }

        await server.WaitAssertion(() =>
        {
            var wild = entMan.SpawnEntity("LavalandPolyporeWild", map.GridCoords);
            Assert.That(trays.TryGetPlant(wild, out var plant), Is.True);
            Assert.That(entMan.GetComponent<TransformComponent>(plant.Value).ParentUid, Is.EqualTo(wild));
            plants.UpdatePlant(plant.Value, force: true);
            Assert.That(entMan.GetComponent<PlantHolderComponent>(plant.Value).ReadyForHarvest, Is.True);
            var user = entMan.SpawnEntity("MobAshWalker", new EntityCoordinates(map.Grid, 2.5f, 0.5f));
            harvests.DoHarvest(plant.Value, user);
            var produce = Produce();
            Assert.That(produce.Count, Is.GreaterThan(1));
            harvests.DoHarvest(plant.Value, user);
            Assert.That(Produce(), Is.EquivalentTo(produce));

            var sieve = entMan.SpawnEntity("AshWalkerSeedSieve", map.GridCoords);
            entMan.EventBus.RaiseLocalEvent(sieve, new InteractUsingEvent(user, produce[0], sieve, map.GridCoords));
            Assert.That(entMan.IsQueuedForDeletion(produce[0]), Is.True);
            var seedQuery = entMan.EntityQueryEnumerator<SeedComponent>();
            EntityUid seed = default;
            while (seedQuery.MoveNext(out var uid, out var comp))
            {
                if (comp.PlantProtoId == "LavalandPolyporePlant")
                    seed = uid;
            }
            Assert.That(seed, Is.Not.EqualTo(default(EntityUid)));
            var garden = entMan.SpawnEntity("AshWalkerSoil", new EntityCoordinates(map.Grid, 1.5f, 0.5f));
            entMan.EventBus.RaiseLocalEvent(seed, new AfterInteractEvent(user, seed, garden, map.GridCoords, true));
            Assert.That(trays.TryGetPlant(garden, out var planted), Is.True);
            Assert.That(planted, Is.Not.EqualTo(plant));
            Assert.That(entMan.Deleted(seed), Is.True);
            var nutrition = entMan.GetComponent<PlantTrayComponent>(garden).NutritionLevel;
            entMan.EventBus.RaiseLocalEvent(produce[1], new AfterInteractEvent(user, produce[1], garden, map.GridCoords, true));
            Assert.That(entMan.GetComponent<PlantTrayComponent>(garden).NutritionLevel, Is.GreaterThan(nutrition));

            holders.AdjustsAge(plant.Value, 20);
            plants.UpdatePlant(plant.Value, force: true);
            Assert.That(entMan.GetComponent<PlantHolderComponent>(plant.Value).ReadyForHarvest, Is.True);
            var before = Produce().Count;
            harvests.DoHarvest(plant.Value, user);
            Assert.That(Produce().Count, Is.GreaterThan(before));
            Assert.That(trays.TryGetPlant(wild, out var regrown), Is.True);
            Assert.That(regrown, Is.EqualTo(plant));
        });
        await pair.RunTicksSync(5);
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RoofAndSettlementFloorsBlockWeather()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = server.System<SharedMapSystem>();
        var roofs = server.System<SharedRoofSystem>();
        var weather = server.System<SharedWeatherSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var tiles = server.ResolveDependency<ITileDefinitionManager>();
            var basalt = tiles["FloorBasalt"];
            maps.SetTile(map.Grid, map.Grid.Comp, map.Tile.GridIndices, new Tile(basalt.TileId));
            var tile = maps.GetTileRef(map.Grid, map.Grid.Comp, map.Tile.GridIndices);
            var roof = entMan.EnsureComponent<RoofComponent>(map.Grid);
            roofs.SetRoof((map.Grid, map.Grid.Comp, roof), tile.GridIndices, false);
            Assert.That(weather.CanWeatherAffect(map.Grid, map.Grid.Comp, tile), Is.True);
            roofs.SetRoof((map.Grid, map.Grid.Comp, roof), tile.GridIndices, true);
            Assert.That(weather.CanWeatherAffect(map.Grid, map.Grid.Comp, tile), Is.False);

            Assert.That(server.System<MapLoaderSystem>().TryLoadGrid(map.MapId,
                new ResPath("/Maps/Lavaland/ds_ashwalkers_base.yml"), out var settlement), Is.True);
            var grid = settlement.Value;
            var sheltered = maps.GetAllTiles(grid, grid.Comp)
                .Where(t => !((ContentTileDefinition) tiles[t.Tile.TypeId]).Weather).ToList();
            Assert.That(sheltered, Is.Not.Empty);
            foreach (var shelteredTile in sheltered)
                Assert.That(weather.CanWeatherAffect(grid, grid.Comp, shelteredTile), Is.False);

            var outdoor = maps.GetAllTiles(grid, grid.Comp).First(t => t.Tile.TypeId == basalt.TileId);
            var roofedOutdoor = entMan.EnsureComponent<RoofComponent>(grid);
            roofs.SetRoof((grid, grid.Comp, roofedOutdoor), outdoor.GridIndices, true);
            Assert.That(weather.CanWeatherAffect(grid, grid.Comp, outdoor), Is.False);
        });
        await pair.CleanReturnAsync();
    }
}
