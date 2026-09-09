using System.Numerics;
using Content.Client.Botany;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Botany.Systems;
using Content.Shared.DeadSpace.AshWalkers;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Stacks;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;

namespace Content.IntegrationTests.Tests.DeadSpace.Lavaland;

public sealed class AshWalkerInteractionTest : InteractionTest
{
    [Test]
    public async Task PlantingSporesIsPredictedAndRejectsOccupiedSoil()
    {
        await Client.WaitPost(() => Client.ResolveDependency<IConfigurationManager>().SetCVar(CVars.NetPredictLagBias, 0.2f));
        await SpawnTarget("AshWalkerSoil");
        var seed = await PlaceInHands("LavalandPolyporeSeeds");
        var serverSeed = ToServer(seed);
        await RunTicks(5);
        await SetKey(EngineKeyFunctions.Use, BoundKeyState.Down);
        await Client.WaitAssertion(() =>
        {
            Assert.That(Client.System<PlantTraySystem>().TryGetPlant(CTarget!.Value, out var plant), Is.True);
            Assert.That(CEntMan.GetComponent<TransformComponent>(plant!.Value).ParentUid, Is.EqualTo(CTarget));
            Assert.That(Client.System<SharedHandsSystem>().GetActiveItem(CPlayer), Is.Null);
        });
        await Client.WaitAssertion(AssertPlantVisible);
        await SetKey(EngineKeyFunctions.Use, BoundKeyState.Up);
        await Client.WaitRunTicks(1);
        await Client.WaitAssertion(() => Assert.That(Client.System<SharedHandsSystem>().GetActiveItem(CPlayer), Is.Null));
        await Client.WaitAssertion(AssertPlantVisible);
        for (var i = 0; i < 10; i++)
        {
            await RunTicks(1);
            await Client.WaitAssertion(AssertPlantVisible);
        }
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<PlantTraySystem>().TryGetPlant(STarget!.Value, out _), Is.True);
            Assert.That(SEntMan.Deleted(serverSeed), Is.True);
        });
        var extra = await PlaceInHands("LavalandPolyporeSeeds");
        await RunTicks(5);
        await SetKey(EngineKeyFunctions.Use, BoundKeyState.Down);
        await Client.WaitAssertion(() =>
        {
            Assert.That(Client.System<SharedHandsSystem>().GetActiveItem(CPlayer), Is.EqualTo(ToClient(extra)));
            Assert.That(CEntMan.IsQueuedForDeletion(ToClient(extra)), Is.False);
        });
        await SetKey(EngineKeyFunctions.Use, BoundKeyState.Up);
        await RunTicks(10);
        await Server.WaitAssertion(() => Assert.That(HandSys.GetActiveItem(SPlayer), Is.EqualTo(ToServer(extra))));
    }

    private void AssertPlantVisible()
    {
        Assert.That(Client.System<PlantTraySystem>().TryGetPlant(CTarget!.Value, out var plant), Is.True);
        var xform = CEntMan.GetComponent<TransformComponent>(plant!.Value);
        Assert.That(xform.ParentUid, Is.EqualTo(CTarget));
        Assert.That(xform.LocalPosition, Is.EqualTo(Vector2.Zero));
        var sprite = CEntMan.GetComponent<SpriteComponent>(plant!.Value);
        Assert.That(sprite.Visible, Is.True);
        Assert.That(Client.System<SpriteSystem>().TryGetLayer((plant.Value, sprite), PlantLayers.Plant, out var layer, false), Is.True);
        Assert.That(layer!.Visible, Is.True);
        Client.System<SpriteTreeSystem>().UpdateTreePositions();
        Assert.That(sprite.TreeUid, Is.Not.Null);
    }

    [Test]
    public async Task RefuelingIsPredictedWithoutPlacingFuelOnTheHearth()
    {
        await SpawnTarget("AshWalkerHearth");
        var fuel = await PlaceInHands("MaterialFungalWood3", 7);
        await RunTicks(5);
        for (var i = 0; i < 6; i++)
        {
            await SetKey(EngineKeyFunctions.Use, BoundKeyState.Down);
            await Client.WaitAssertion(() =>
            {
                Assert.That(CEntMan.GetComponent<StackComponent>(ToClient(fuel)).Count, Is.EqualTo(7 - Math.Min(i + 1, 5)));
                Assert.That(Client.System<SharedHandsSystem>().GetActiveItem(CPlayer), Is.EqualTo(ToClient(fuel)));
                Assert.That(CEntMan.GetComponent<AshWalkerHearthComponent>(CTarget!.Value).FuelRemaining, Is.GreaterThan(0));
            });
            await SetKey(EngineKeyFunctions.Use, BoundKeyState.Up);
            await RunTicks(10);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.GetComponent<StackComponent>(ToServer(fuel)).Count, Is.EqualTo(7 - Math.Min(i + 1, 5)));
                Assert.That(HandSys.GetActiveItem(SPlayer), Is.EqualTo(ToServer(fuel)));
            });
        }

        var food = await PlaceInHands("FoodLavalandPolypore");
        var serverFood = ToServer(food);
        await RunTicks(5);
        await SetKey(EngineKeyFunctions.Use, BoundKeyState.Down);
        await Client.WaitAssertion(() =>
        {
            Assert.That(Client.System<SharedHandsSystem>().GetActiveItem(CPlayer), Is.Null);
            Assert.That(CEntMan.GetComponent<TransformComponent>(ToClient(food)).Coordinates,
                Is.EqualTo(CEntMan.GetComponent<TransformComponent>(CTarget!.Value).Coordinates));
        });
        await SetKey(EngineKeyFunctions.Use, BoundKeyState.Up);
        await RunTicks(10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(HandSys.GetActiveItem(SPlayer), Is.Null);
            Assert.That(SEntMan.GetComponent<TransformComponent>(serverFood).Coordinates,
                Is.EqualTo(SEntMan.GetComponent<TransformComponent>(STarget!.Value).Coordinates));
        });
    }
}
