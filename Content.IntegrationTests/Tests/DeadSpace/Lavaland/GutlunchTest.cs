using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.DeadSpace.AshWalkers;
using Content.Server.Gravity;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Nutrition.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.AshWalkers;
using Content.Shared.FixedPoint;
using Content.Shared.Gravity;
using Content.Shared.Nutrition.AnimalHusbandry;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.DeadSpace.Lavaland;

[TestFixture]
public sealed class GutlunchTest
{
    [Test]
    public async Task HungryGutlunchFindsAndEatsMeatOnTheGround()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = server.System<SharedMapSystem>();
        var map = await pair.CreateTestMap();
        EntityUid animal = default;
        EntityUid meat = default;
        EntityUid cooked = default;

        await server.WaitPost(() =>
        {
            var gravity = entMan.EnsureComponent<GravityComponent>(map.Grid);
            server.System<GravitySystem>().EnableGravity(map.Grid, gravity);
            for (var x = -1; x <= 5; x++)
            for (var y = -1; y <= 1; y++)
                maps.SetTile(map.Grid, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);

            animal = entMan.SpawnEntity("MobGubbuck", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            cooked = entMan.SpawnEntity("FoodMeatCooked", new EntityCoordinates(map.Grid, 1.5f, 0.5f));
            meat = entMan.SpawnEntity("FoodMeat", new EntityCoordinates(map.Grid, 3.5f, 0.5f));
        });
        await pair.RunTicksSync(240);
        await server.WaitAssertion(() =>
        {
            var htn = entMan.GetComponent<HTNComponent>(animal);
            var npc = server.System<NPCSystem>();
            var chosen = server.System<NPCUtilitySystem>().GetEntities(htn.Blackboard, "NearbyGutlunchFood").GetHighest();
            Assert.That(entMan.Deleted(meat), Is.True,
                $"enabled={npc.Enabled}, awake={npc.IsAwake(animal, htn)}, position={entMan.GetComponent<TransformComponent>(animal).Coordinates}, " +
                $"chosen={chosen}, food={meat}, task={htn.Plan?.CurrentOperator.GetType().Name}, planning={htn.PlanningJob?.Status}");
            Assert.That(entMan.Deleted(cooked), Is.False);
            Assert.That(server.System<GutlunchSystem>().TryGetMilk(animal, out var milk), Is.True);
            Assert.That(milk.Value.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(18)));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FeedingConservesNutrientsAndRequiresFreshFood()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var gutlunches = server.System<GutlunchSystem>();
        var ingestion = server.System<IngestionSystem>();
        var solutions = server.System<SharedSolutionContainerSystem>();
        var map = await pair.CreateTestMap();
        EntityUid first = default;
        EntityUid second = default;
        EntityUid meat = default;

        await server.WaitAssertion(() =>
        {
            first = entMan.SpawnEntity("MobGubbuck", map.GridCoords);
            second = entMan.SpawnEntity("MobGubbuck", map.GridCoords);
            entMan.RemoveComponent<HTNComponent>(first);
            entMan.RemoveComponent<HTNComponent>(second);
            meat = entMan.SpawnEntity("FoodMeat", map.GridCoords);
            Assert.That(ingestion.TryIngest(first, meat), Is.True);
            Assert.That(ingestion.TryIngest(second, meat), Is.True);
        });
        await pair.RunTicksSync(70);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(meat), Is.True);
            AssertMilk();
            var empty = entMan.SpawnEntity("FoodMeat", map.GridCoords);
            Assert.That(solutions.TryGetSolution(empty, "food", out var food), Is.True);
            solutions.RemoveAllSolution(food.Value);
            Assert.That(ingestion.TryIngest(first, empty), Is.False);
            var cooked = entMan.SpawnEntity("FoodMeatCooked", map.GridCoords);
            Assert.That(ingestion.TryIngest(first, cooked), Is.False);
        });
        await pair.RunTicksSync(1300);
        await server.WaitAssertion(AssertMilk);
        await pair.CleanReturnAsync();

        void AssertMilk()
        {
            Assert.That(gutlunches.TryGetMilk(first, out var firstMilk), Is.True);
            Assert.That(gutlunches.TryGetMilk(second, out var secondMilk), Is.True);
            var total = firstMilk.Value.Comp.Solution.Clone();
            total.AddSolution(secondMilk.Value.Comp.Solution, null);
            Assert.That(total.Volume, Is.EqualTo(FixedPoint2.New(18)));
            Assert.That(total.GetTotalPrototypeQuantity("Cream"), Is.EqualTo(FixedPoint2.New(10.8)));
            Assert.That(total.GetTotalPrototypeQuantity("Saline"), Is.EqualTo(FixedPoint2.New(7.2)));
        }
    }

    [Test]
    public async Task BreedingSpendsMilkReservesCapacityAndRespectsPauseAndDeath()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var husbandry = server.System<AnimalHusbandrySystem>();
        var gutlunches = server.System<GutlunchSystem>();
        var solutions = server.System<SharedSolutionContainerSystem>();
        var maps = server.System<SharedMapSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await pair.CreateTestMap();
        EntityUid female = default;
        EntityUid male = default;
        EntityUid otherFemale = default;
        EntityUid overflow = default;
        ReproductiveComponent pregnancy = default!;
        TimeSpan? birthTime = default;

        await server.WaitAssertion(() =>
        {
            female = SpawnAdult("MobGuthen");
            male = SpawnAdult("MobGubbuck");
            otherFemale = SpawnAdult("MobGuthen");
            SpawnAdult("MobGubbuck");
            SpawnAdult("MobGubbuck");
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            var transform = server.System<SharedTransformSystem>();
            transform.SetCoordinates(female, map.GridCoords);
            transform.SetCoordinates(male, map.GridCoords);
            transform.SetCoordinates(otherFemale, map.GridCoords);
            Assert.That(husbandry.TryReproduce(female, male), Is.False);
            Fill(female);
            Assert.That(husbandry.TryReproduce(female, male), Is.False);
            Fill(male);
            Fill(otherFemale);
            pregnancy = entMan.GetComponent<ReproductiveComponent>(female);
            pregnancy.GestationDuration = TimeSpan.FromSeconds(2);
            Assert.That(husbandry.TryReproduce(female, otherFemale), Is.False);
            Assert.That(husbandry.TryReproduce(female, male), Is.True);
            Assert.That(gutlunches.TryGetMilk(female, out var femaleMilk), Is.True);
            Assert.That(femaleMilk.Value.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(gutlunches.TryGetMilk(male, out var maleMilk), Is.True);
            Assert.That(maleMilk.Value.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(25)));
            Assert.That(husbandry.TryReproduce(otherFemale, male), Is.False);
            overflow = SpawnAdult("MobGubbuck");
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            husbandry.Birth(female);
            Assert.That(pregnancy.Gestating, Is.True);
            Assert.That(Animals().Length, Is.EqualTo(6));
            entMan.DeleteEntity(overflow);
            pregnancy.GestationEndTime = timing.CurTime + TimeSpan.FromSeconds(2);
            birthTime = pregnancy.GestationEndTime;
            maps.SetPaused(map.MapUid, true);
        });
        await pair.RunSeconds(4);
        await server.WaitAssertion(() =>
        {
            Assert.That(pregnancy.Gestating, Is.True);
            Assert.That(Animals().Length, Is.EqualTo(5));
            maps.SetPaused(map.MapUid, false);
            Assert.That(pregnancy.GestationEndTime, Is.GreaterThan(birthTime));
        });
        await pair.RunSeconds(1);
        await server.WaitAssertion(() => Assert.That(pregnancy.Gestating, Is.True));
        await pair.RunSeconds(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(pregnancy.Gestating, Is.False);
            Assert.That(Animals().Length, Is.EqualTo(6));
            var infant = Animals().Single(entMan.HasComponent<InfantComponent>);
            Assert.That(husbandry.CanReproduce(infant), Is.False);
            entMan.DeleteEntity(infant);
            Fill(female);
            Fill(male);
            server.System<SharedTransformSystem>().SetCoordinates(male, entMan.GetComponent<TransformComponent>(female).Coordinates);
            Assert.That(husbandry.TryReproduce(female, male), Is.True);
            server.System<DamageableSystem>().TryChangeDamage(female, new DamageSpecifier { DamageDict = { ["Blunt"] = 100 } });
            Assert.That(pregnancy.Gestating, Is.False);
            husbandry.Birth(female);
            Assert.That(Animals().Length, Is.EqualTo(5));
        });
        await pair.CleanReturnAsync();

        EntityUid SpawnAdult(string prototype)
        {
            var uid = entMan.SpawnEntity(prototype, map.GridCoords);
            entMan.RemoveComponent<HTNComponent>(uid);
            if (entMan.TryGetComponent<ReproductiveComponent>(uid, out var reproductive))
                reproductive.NextBreedAttempt = timing.CurTime + TimeSpan.FromHours(1);
            return uid;
        }

        void Fill(EntityUid uid)
        {
            Assert.That(gutlunches.TryGetMilk(uid, out var milk), Is.True);
            solutions.RemoveAllSolution(milk.Value);
            solutions.TryAddSolution(milk.Value, new Solution([new("Cream", 30), new("Saline", 20)]));
        }

        EntityUid[] Animals()
        {
            var animals = new List<EntityUid>();
            var query = entMan.AllEntityQueryEnumerator<GutlunchComponent>();
            while (query.MoveNext(out var uid, out _))
                animals.Add(uid);
            return animals.ToArray();
        }
    }
}
