using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Botany.Systems;
using Content.Shared.DeadSpace.AshWalkers;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;

namespace Content.IntegrationTests.Tests.DeadSpace.Lavaland;

public sealed class AshWalkerInteractionTest : InteractionTest
{
    [Test]
    public async Task PlantingSporesIsPredictedAndRejectsOccupiedSoil()
    {
        await SpawnTarget("AshWalkerSoil");
        var seed = await PlaceInHands("LavalandPolyporeSeeds");
        var serverSeed = ToServer(seed);
        await RunTicks(5);
        await SetKey(EngineKeyFunctions.Use, BoundKeyState.Down);
        await Client.WaitAssertion(() =>
        {
            Assert.That(Client.System<PlantTraySystem>().TryGetPlant(CTarget!.Value, out var plant), Is.True);
            Assert.That(CEntMan.GetComponent<TransformComponent>(plant!.Value).ParentUid, Is.EqualTo(CTarget));
            Assert.That(CEntMan.IsQueuedForDeletion(ToClient(seed)), Is.True);
        });
        await SetKey(EngineKeyFunctions.Use, BoundKeyState.Up);
        await Client.WaitRunTicks(1);
        await Client.WaitAssertion(() => Assert.That(Client.System<SharedHandsSystem>().GetActiveItem(CPlayer), Is.Null));
        await RunTicks(10);
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
