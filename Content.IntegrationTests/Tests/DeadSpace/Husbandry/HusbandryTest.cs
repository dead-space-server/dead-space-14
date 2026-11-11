// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.DeadSpace.Husbandry.Feeding;
using Content.Server.DeadSpace.Husbandry.Growth;
using Content.Shared.Botany.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DeadSpace.Beekeeping;
using Content.Shared.DeadSpace.Husbandry.Breeding;
using Content.Shared.DeadSpace.Husbandry.Core;
using Content.Shared.DeadSpace.Husbandry.Feeding;
using Content.Shared.DeadSpace.Husbandry.Growth;
using Content.Shared.DeadSpace.Husbandry.Needs;
using Content.Shared.DeadSpace.Husbandry.Sex;
using Content.Shared.Nutrition.AnimalHusbandry;
using Content.Shared.Nutrition.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.DeadSpace.Husbandry;

/// <summary>
/// The husbandry module on the real prototypes: livestock has the module and not the vanilla hunger, eats, drinks,
/// grows up and breeds, and the bees pollinate.
/// </summary>
[TestFixture]
public sealed class HusbandryTest
{
    private static readonly string[] Livestock =
    {
        "MobCow", "MobGoat", "MobSheep", "MobPig", "MobChicken", "MobDuckMallard",
    };

    [Test]
    public async Task LivestockLivesByTheHusbandryRules()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            foreach (var id in Livestock)
            {
                var animal = server.EntMan.SpawnEntity(id, map.GridCoords);

                Assert.Multiple(() =>
                {
                    Assert.That(server.EntMan.HasComponent<AnimalNeedsComponent>(animal), Is.True, $"{id} has needs");
                    Assert.That(server.EntMan.HasComponent<AnimalSexComponent>(animal), Is.True, $"{id} has a sex");
                    Assert.That(server.EntMan.HasComponent<DietComponent>(animal), Is.True, $"{id} has a diet");
                    Assert.That(server.EntMan.HasComponent<GrowthComponent>(animal), Is.True, $"{id} grows");
                    Assert.That(server.EntMan.HasComponent<AnimalBreedingComponent>(animal), Is.True, $"{id} breeds");
                    Assert.That(server.EntMan.HasComponent<SatiationComponent>(animal), Is.False, $"{id} has no vanilla satiation");
                    Assert.That(server.EntMan.HasComponent<ReproductiveComponent>(animal), Is.False, $"{id} has no vanilla breeding");
                });
            }
        });

        await pair.CleanReturnAsync();
    }


    [Test]
    public async Task AnimalEatsThroughTheStomach()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid cow = default;
        EntityUid apple = default;

        await server.WaitPost(() =>
        {
            var ents = server.EntMan;
            cow = ents.SpawnEntity("MobChicken", map.GridCoords);
            // The brain is off: this tests the eating itself, not where the animal walks.
            ents.RemoveComponent<Content.Server.NPC.HTN.HTNComponent>(cow);
            var needs = ents.GetComponent<AnimalNeedsComponent>(cow);
            needs.Satiety.Value = 10f;
            needs.Satiety.DecayPerMinute = 0f;
            needs.Hydration.DecayPerMinute = 0f;

            apple = ents.SpawnEntity("FoodApple", map.GridCoords);
            var feeding = server.System<AnimalFeedingSystem>();
            Assert.That(feeding.CanEat((cow, ents.GetComponent<DietComponent>(cow)), apple), Is.True);
            Assert.That(feeding.TryEat(cow, apple, out _), Is.True, "the animal starts eating by the usual ingestion");
        });

        // The bites, then the stomach digests: the satiety goes up gradually, not at once.
        await pair.RunTicksSync(30 * 30);

        await server.WaitAssertion(() =>
        {
            var needs = server.EntMan.GetComponent<AnimalNeedsComponent>(cow);
            Assert.That(needs.Satiety.Value, Is.GreaterThan(10f), "what was eaten was digested into satiety");
            Assert.That(server.EntMan.Deleted(apple), Is.True, "the apple was eaten up");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AnimalDrinksAnyOpenContainerThroughTheStomach()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid cow = default;
        EntityUid trough = default;
        EntityUid bucket = default;

        await server.WaitPost(() =>
        {
            var ents = server.EntMan;
            var feeding = server.System<AnimalFeedingSystem>();
            var solutions = server.System<SharedSolutionContainerSystem>();

            cow = ents.SpawnEntity("MobCow", map.GridCoords);
            ents.RemoveComponent<Content.Server.NPC.HTN.HTNComponent>(cow);
            var needs = ents.GetComponent<AnimalNeedsComponent>(cow);
            needs.Hydration.Value = 10f;
            needs.Satiety.DecayPerMinute = 0f;
            needs.Hydration.DecayPerMinute = 0f;

            trough = ents.SpawnEntity("AnimalTrough", map.GridCoords);
            Assert.That(feeding.CanDrink(trough), Is.False, "an empty trough");

            Assert.That(solutions.TryGetSolution(trough, "tank", out var soln, out _), Is.True);
            solutions.TryAddReagent(soln.Value, "Water", 50);
            Assert.That(feeding.CanDrink(trough), Is.True);

            // Any open container with a liquid in it, not only a trough.
            bucket = ents.SpawnEntity("Bucket", map.GridCoords);
            var drainable = ents.GetComponent<DrainableSolutionComponent>(bucket);
            Assert.That(solutions.TryGetSolution(bucket, drainable.Solution, out var bucketSoln, out _), Is.True);
            solutions.TryAddReagent(bucketSoln.Value, "Water", 30);
            Assert.That(feeding.CanDrink(bucket), Is.True);

            Assert.That(feeding.TryDrink(cow, trough, out _), Is.True);
        });

        await pair.RunTicksSync(30 * 30);

        await server.WaitAssertion(() =>
        {
            var needs = server.EntMan.GetComponent<AnimalNeedsComponent>(cow);
            Assert.That(needs.Hydration.Value, Is.GreaterThan(10f), "the water in the stomach was digested into hydration");

            var solutions = server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(trough, "tank", out _, out var sol), Is.True);
            Assert.That((float) sol.Volume, Is.LessThan(50f), "the sip came out of the trough");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AnimalStarvesWithoutFood()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid cow = default;
        await server.WaitPost(() =>
        {
            cow = server.EntMan.SpawnEntity("MobCow", map.GridCoords);
            var needs = server.EntMan.GetComponent<AnimalNeedsComponent>(cow);
            needs.Satiety.Value = 1f;
            needs.Satiety.DecayPerMinute = 6000f;
            needs.UpdateRate = TimeSpan.FromSeconds(0.1);
        });

        await pair.RunTicksSync(1 * 60);

        await server.WaitAssertion(() =>
        {
            var needs = server.EntMan.GetComponent<AnimalNeedsComponent>(cow);
            // The stomach of a new animal holds a little food, so the satiety may not be exactly zero.
            Assert.That(needs.Satiety.Value, Is.LessThan(2f));
            Assert.That(needs.SatietyLevel, Is.Not.EqualTo(NeedLevel.Satisfied));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BabyGrowsUp()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var growth = server.System<GrowthSystem>();
            var cow = server.EntMan.SpawnEntity("MobCow", map.GridCoords);
            var comp = server.EntMan.GetComponent<GrowthComponent>(cow);

            Assert.That(growth.IsGrownUp(cow, "adult"), Is.True, "a spawned cow is an adult");

            growth.SetStageById((cow, comp), "baby");
            Assert.That(growth.IsGrownUp(cow, "adult"), Is.False);
            Assert.That(comp.StageEndTime, Is.Not.Null, "a baby grows up by itself");

            growth.SetStageById((cow, comp), "adult");
            Assert.That(growth.IsGrownUp(cow, "adult"), Is.True);
            Assert.That(comp.StageEndTime, Is.Null);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PairBreedsAndTheBabyIsBorn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid mother = default;
        EntityUid father = default;

        await server.WaitPost(() =>
        {
            var ents = server.EntMan;
            mother = ents.SpawnEntity("MobCow", map.GridCoords);
            father = ents.SpawnEntity("MobCow", map.GridCoords);

            SetSex(ents, mother, AnimalSexKind.Female);
            SetSex(ents, father, AnimalSexKind.Male);

            foreach (var cow in new[] { mother, father })
            {
                var needs = ents.GetComponent<AnimalNeedsComponent>(cow);
                needs.Satiety.Value = needs.Satiety.Max;
                needs.Hydration.Value = needs.Hydration.Max;
                needs.Satiety.DecayPerMinute = 0f;
                needs.Hydration.DecayPerMinute = 0f;
            }

            var breeding = ents.GetComponent<AnimalBreedingComponent>(mother);
            breeding.BreedChance = 1f;
            breeding.NextAttempt = timing.CurTime;
            breeding.GestationDuration = TimeSpan.FromSeconds(1);
        });

        await pair.RunTicksSync(2 * 60);

        await server.WaitAssertion(() =>
        {
            var breeding = server.EntMan.GetComponent<AnimalBreedingComponent>(mother);
            Assert.That(breeding.Gestating || breeding.NextAllowed > TimeSpan.Zero, Is.True, "the mother got pregnant");
        });

        await pair.RunTicksSync(3 * 60);

        await server.WaitAssertion(() =>
        {
            var cows = server.EntMan.AllEntities<AnimalBreedingComponent>().ToList();
            Assert.That(cows, Has.Count.EqualTo(3), "a calf was born");

            var calf = cows.Single(c => c.Owner != mother && c.Owner != father);
            Assert.That(server.System<GrowthSystem>().IsGrownUp(calf.Owner, "adult"), Is.False, "the calf is a baby");

            var mom = server.EntMan.GetComponent<AnimalNeedsComponent>(mother);
            Assert.That(mom.Satiety.Value, Is.LessThan(mom.Satiety.Max), "a pregnancy costs satiety");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HungryAnimalsDoNotBreed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid mother = default;
        await server.WaitPost(() =>
        {
            var ents = server.EntMan;
            mother = ents.SpawnEntity("MobCow", map.GridCoords);
            var father = ents.SpawnEntity("MobCow", map.GridCoords);
            SetSex(ents, mother, AnimalSexKind.Female);
            SetSex(ents, father, AnimalSexKind.Male);

            var needs = ents.GetComponent<AnimalNeedsComponent>(mother);
            needs.Satiety.Value = 5f;
            needs.Satiety.DecayPerMinute = 0f;

            var breeding = ents.GetComponent<AnimalBreedingComponent>(mother);
            breeding.BreedChance = 1f;
            breeding.NextAttempt = timing.CurTime;
        });

        await pair.RunTicksSync(2 * 60);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<AnimalBreedingComponent>(mother).Gestating, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BeePollinatesAPlantOnlyOncePerCooldown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ents = server.EntMan;
            var plant = ents.SpawnEntity("WheatPlants", map.GridCoords);
            var bee = ents.SpawnEntity("MobBeeWorker", map.GridCoords);

            Assert.That(ents.HasComponent<PollinationComponent>(plant), Is.True, "every plant can be pollinated");

            var pollination = ents.GetComponent<PollinationComponent>(plant);
            var age = ents.GetComponent<PlantHolderComponent>(plant).Age;

            var ev = new PlantPollinatedEvent(bee, 10f);
            ents.EventBus.RaiseLocalEvent(plant, ref ev);

            Assert.That(pollination.NextAvailable, Is.GreaterThan(TimeSpan.Zero));
            Assert.That(ents.GetComponent<PlantHolderComponent>(plant).Age, Is.EqualTo(age + pollination.AgeBonus));

            // The second visit during the cooldown changes nothing.
            ents.EventBus.RaiseLocalEvent(plant, ref ev);
            Assert.That(ents.GetComponent<PlantHolderComponent>(plant).Age, Is.EqualTo(age + pollination.AgeBonus));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WorkerBeeHasNoOtherBrainAndCanFly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var bee = server.EntMan.SpawnEntity("MobBeeWorker", map.GridCoords);

            Assert.Multiple(() =>
            {
                // Two brains would fight over the movement.
                Assert.That(server.EntMan.HasComponent<Content.Server.NPC.HTN.HTNComponent>(bee), Is.False);
                Assert.That(server.EntMan.HasComponent<Content.Shared.DeadSpace.Husbandry.Walking.AnimalWalkerComponent>(bee), Is.True, "the bee flies by the walker");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WorkerBeeFliesToAPointInAStraightLine()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid bee = default;
        System.Numerics.Vector2 start = default;

        await server.WaitPost(() =>
        {
            var ents = server.EntMan;
            var xforms = server.System<SharedTransformSystem>();
            bee = ents.SpawnEntity("MobBeeWorker", map.GridCoords);
            start = xforms.GetWorldPosition(bee);

            var target = map.GridCoords.Offset(new System.Numerics.Vector2(4f, 0f));
            var walker = ents.GetComponent<Content.Shared.DeadSpace.Husbandry.Walking.AnimalWalkerComponent>(bee);
            Assert.That(server.System<Content.Server.DeadSpace.Husbandry.Walking.AnimalWalkerSystem>().StartRoute((bee, walker), new[] { target }, 0.5f, run: true), Is.True);
        });

        await pair.RunTicksSync(3 * 60);

        await server.WaitAssertion(() =>
        {
            var end = server.System<SharedTransformSystem>().GetWorldPosition(bee);
            Assert.That((end - start).Length(), Is.GreaterThan(2f), "the bee got going towards the point");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BeekeepingItemsCanBePickedUp()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            foreach (var id in new[] { "MaterialHoneycomb", "HiveFrame", "QueenBee" })
            {
                var item = server.EntMan.SpawnEntity(id, map.GridCoords);
                Assert.Multiple(() =>
                {
                    Assert.That(server.EntMan.HasComponent<Content.Shared.Item.ItemComponent>(item), Is.True, $"{id} is an item");
                    Assert.That(server.EntMan.HasComponent<Robust.Shared.Physics.Components.PhysicsComponent>(item), Is.True, $"{id} has a body");
                    Assert.That(server.EntMan.HasComponent<Content.Shared.Movement.Pulling.Components.PullableComponent>(item), Is.True, $"{id} can be pulled");
                });
            }

            var bee = server.EntMan.SpawnEntity("MobBeeWorker", map.GridCoords);
            var fixtures = server.EntMan.GetComponent<Robust.Shared.Physics.FixturesComponent>(bee);
            Assert.That(fixtures.Fixtures.Values.Any(f => f.Hard), Is.False, "a worker bee has no hard collider");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DebugAnalyzerCanBeSpawned()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var analyzer = server.EntMan.SpawnEntity("DebugAnalyzer", map.GridCoords);
            Assert.That(server.EntMan.HasComponent<Content.Shared.DeadSpace.DebugAnalyzer.DebugAnalyzerComponent>(analyzer), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    private static void SetSex(IEntityManager ents, EntityUid animal, AnimalSexKind sex)
    {
        var comp = ents.GetComponent<AnimalSexComponent>(animal);
        comp.Randomize = false;
        comp.Sex = sex;
    }
}
