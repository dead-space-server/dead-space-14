using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.NPC.HTN;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;

namespace Content.IntegrationTests.Tests.DeadSpace.Lavaland;

public sealed class GutlunchInteractionTest : InteractionTest
{
    [Test]
    public async Task HandFeedingAndMilkingStartPredictedDoAfters()
    {
        await SpawnTarget("MobGubbuck");
        await Server.WaitPost(() => SEntMan.RemoveComponent<HTNComponent>(STarget!.Value));
        var meat = await PlaceInHands("FoodMeat");
        var serverMeat = ToServer(meat);
        await RunTicks(5);
        await ClickAndAssertPredicted();
        await RunTicks(60);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(serverMeat), Is.True);
            Assert.That(Server.System<SharedSolutionContainerSystem>().TryGetSolution(STarget!.Value, "udder", out _, out var milk), Is.True);
            Assert.That(milk.Volume, Is.EqualTo(FixedPoint2.New(18)));
        });
        var bowl = await PlaceInHands("AshWalkerMushroomBowl");
        await RunTicks(5);
        await ClickAndAssertPredicted();
        await RunTicks(120);
        await Server.WaitAssertion(() =>
        {
            var solutions = Server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(STarget!.Value, "udder", out _, out var milk), Is.True);
            Assert.That(milk.Volume, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(solutions.TryGetRefillableSolution(ToServer(bowl), out _, out var contents), Is.True);
            Assert.That(contents.Volume, Is.EqualTo(FixedPoint2.New(18)));
        });
    }

    [Test]
    public async Task MilkingCannotFinishAfterAnimalDies()
    {
        await SpawnTarget("MobGubbuck");
        await Server.WaitAssertion(() =>
        {
            SEntMan.RemoveComponent<HTNComponent>(STarget!.Value);
            var solutions = Server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(STarget!.Value, "udder", out var milk), Is.True);
            solutions.TryAddSolution(milk.Value, new Solution([new("Cream", 30), new("Saline", 20)]));
        });
        var bowl = await PlaceInHands("AshWalkerMushroomBowl");
        await RunTicks(5);
        await ClickAndAssertPredicted();
        await Server.WaitPost(() => Server.System<DamageableSystem>().TryChangeDamage(STarget!.Value,
            new DamageSpecifier { DamageDict = { ["Blunt"] = 100 } }));
        await RunTicks(120);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<SharedSolutionContainerSystem>().TryGetRefillableSolution(ToServer(bowl), out _, out var contents), Is.True);
            Assert.That(contents.Volume, Is.EqualTo(FixedPoint2.Zero));
        });
    }

    private async Task ClickAndAssertPredicted()
    {
        await SetKey(EngineKeyFunctions.Use, BoundKeyState.Down);
        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.GetComponent<DoAfterComponent>(CPlayer).DoAfters.Values.Any(doAfter => doAfter.CancelledTime == null), Is.True);
            Assert.That(Client.System<SharedHandsSystem>().GetActiveItem(CPlayer), Is.Not.Null);
        });
        await SetKey(EngineKeyFunctions.Use, BoundKeyState.Up);
    }
}
