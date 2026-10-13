using System.Linq;
using Content.IntegrationTests.Tests.Movement;
using Content.Server.DeadSpace.Smokables.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DeadSpace.Smokables;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Stacks;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.DeadSpace.Shisha;

public sealed class ShishaFuelTest : MovementTest
{
    private Entity<ShishaComponent> Base => (STarget!.Value, Comp<ShishaComponent>());
    private SharedSolutionContainerSystem Solutions => Server.System<SharedSolutionContainerSystem>();

    private Solution Bowl()
    {
        Assert.That(Solutions.TryGetSolution(Base.Owner, Base.Comp.Solution, out _, out var solution), Is.True);
        return solution!;
    }

    private async Task Load(string prototype, int amount = 1)
    {
        await PlaceInHands(prototype, amount);
        await Interact();
    }

    private async Task Prepare(bool coal = true, bool filler = true)
    {
        await SpawnTarget("Shisha", PlayerCoords);
        if (coal)
            await Load("Coal1");
        if (filler)
            await Load("GroundTobacco");
    }

    private async Task Light()
    {
        await Load("CheapLighter");
        await DeleteHeldEntity();
    }

    private async Task TakeHose()
    {
        await DeleteHeldEntity();
        await Interact();
        Assert.That(HandSys.IsHolding(SPlayer, Base.Comp.Hose!.Value), Is.True);
    }

    private async Task Puff(bool awaitCompletion = true)
    {
        await Interact(Player, NetPosition(Player), awaitDoAfters: awaitCompletion);
    }

    private async Task Sprite(string expected)
    {
        await RunTicks(5);
        await Client.WaitAssertion(() =>
        {
            var sprites = Client.System<SpriteSystem>();
            var layer = sprites.LayerMapGet(CTarget!.Value, ShishaVisuals.Base);
            Assert.That(sprites.LayerGetRsiState(CTarget.Value, layer).ToString(), Is.EqualTo(expected));
        });
    }

    [TestCase("GroundTobacco", "Nicotine", 10)]
    [TestCase("GroundCannabis", "THC", 20)]
    public async Task FillerConsumesOneStackItemAndPreservesItsChemistry(string prototype, string reagent, int dose)
    {
        await SpawnTarget("Shisha", PlayerCoords);
        var ingredient = await PlaceInHands(prototype, 3);
        await Interact();
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<StackComponent>(ToServer(ingredient)).Count, Is.EqualTo(2));
            Assert.That(Bowl().GetTotalPrototypeQuantity(reagent), Is.EqualTo(FixedPoint2.New(dose)));
            Solutions.TryGetSolution(ToServer(ingredient), "food", out _, out var remaining);
            Assert.That(remaining!.GetTotalPrototypeQuantity(reagent), Is.EqualTo(FixedPoint2.New(dose)));
            Assert.That(Base.Comp.Lit, Is.False);
        });
    }

    [Test]
    public async Task CoalConsumesOneItemAndRejectsASecondPiece()
    {
        await SpawnTarget("Shisha", PlayerCoords);
        var coal = await PlaceInHands("Coal", 3);
        await Interact();
        await Sprite("icon-coal");
        await Interact();
        await RunSeconds(1);
        Assert.That(SEntMan.GetComponent<StackComponent>(ToServer(coal)).Count, Is.EqualTo(2));
        Assert.That(Base.Comp.FuelRemaining, Is.EqualTo(Base.Comp.FuelPerItem));
        Assert.That(Bowl().Volume, Is.EqualTo(FixedPoint2.Zero));
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task LightingRequiresBothCoalAndFiller(bool coal, bool filler)
    {
        await Prepare(coal, filler);
        await Light();
        Assert.That(Base.Comp.Lit, Is.EqualTo(coal && filler));
    }

    [Test]
    public async Task UnlitLighterCannotLightAndUnlitBowlCannotSmoke()
    {
        await Prepare();
        await PlaceInHands("CheapLighter", enableToggleable: false);
        await Interact();
        Assert.That(Base.Comp.Lit, Is.False);
        await TakeHose();
        await Puff();
        Assert.That(ActiveDoAfters, Is.Empty);
        Assert.That(Bowl().Volume, Is.EqualTo(FixedPoint2.New(10)));
    }

    [Test]
    public async Task HeldBaseCanBeLitAgainstAFlame()
    {
        await Prepare();
        var lighter = await SpawnEntity("CheapLighter", ToServer(PlayerCoords));
        await Server.WaitAssertion(() => Assert.That(ItemToggleSys.TryActivate(lighter, user: SPlayer), Is.True));
        await DeleteHeldEntity();
        await DragDrop(Target!.Value, Player);
        await Interact(lighter, ToServer(PlayerCoords));
        Assert.That(Base.Comp.Lit, Is.True);
    }

    [Test]
    public async Task LitStateUpdatesWorldAndCarriedSprites()
    {
        await Prepare();
        await Sprite("icon-coal");
        await Light();
        await Sprite("icon-lit");
        await TakeHose();
        await Sprite("icon-lit-no-hose");
        await Interact();
        await Sprite("icon-lit");
        await DragDrop(Target!.Value, Player);
        await Client.WaitAssertion(() =>
        {
            var ev = new GetInhandVisualsEvent(CPlayer, HandLocation.Left);
            CEntMan.EventBus.RaiseLocalEvent(CTarget!.Value, ev);
            Assert.That(ev.Layers, Is.Not.Empty);
            Assert.That(ev.Layers[0].Item2.State, Is.EqualTo("icon-lit-no-hose"));
        });
    }

    [Test]
    public async Task FullBowlRejectsFillerWithoutConsumingIt()
    {
        await SpawnTarget("Shisha", PlayerCoords);
        await Server.WaitPost(() =>
        {
            Solutions.TryGetSolution(Base.Owner, Base.Comp.Solution, out var reservoir, out var bowl);
            Solutions.TryAddReagent(reservoir!.Value, "THC", bowl!.MaxVolume - FixedPoint2.New(10), out _);
        });
        var volume = Bowl().Volume;
        var filler = await PlaceInHands("GroundCannabis", 2);
        await Interact();
        Assert.That(Bowl().Volume, Is.EqualTo(volume));
        Assert.That(SEntMan.GetComponent<StackComponent>(ToServer(filler)).Count, Is.EqualTo(2));
    }

    [Test]
    public async Task IdleHeatBurnsCoalButOnlyPuffsConsumeFiller()
    {
        await Prepare();
        await Light();
        var fuel = Base.Comp.FuelRemaining;
        await RunSeconds(1);
        Assert.That(Base.Comp.FuelRemaining, Is.LessThan(fuel));
        Assert.That(Bowl().Volume, Is.EqualTo(FixedPoint2.New(10)));
        await TakeHose();
        await Puff();
        Assert.That(Bowl().Volume, Is.EqualTo(FixedPoint2.New(4)));
        await Puff();
        Assert.That(Bowl().Volume, Is.EqualTo(FixedPoint2.Zero));
        await Puff(); // Empty puffs still complete without adding reagents.
        Assert.That(Bowl().Volume, Is.EqualTo(FixedPoint2.Zero));
    }

    [Test]
    public async Task FuelExpiryCancelsPendingPuffAndCanBeRefuelled()
    {
        await Prepare();
        await Light();
        await TakeHose();
        await Puff(false);
        Assert.That(ActiveDoAfters.Count(), Is.EqualTo(1));
        await Server.WaitPost(() => Base.Comp.FuelRemaining = 0.1f);
        await RunSeconds(1);
        Assert.That(Base.Comp.Lit, Is.False);
        Assert.That(Base.Comp.FuelRemaining, Is.Zero);
        Assert.That(ActiveDoAfters, Is.Empty);
        Assert.That(Bowl().Volume, Is.EqualTo(FixedPoint2.New(10)));
        await Sprite("icon-no-hose");
        await Interact(); // Return the owned hose before loading coal.
        await Load("Coal1");
        await Light();
        Assert.That(Base.Comp.Lit, Is.True);
    }

    [Test]
    public async Task CanAddFillerWhileLit()
    {
        await Prepare();
        await Light();
        var fuel = Base.Comp.FuelRemaining;
        var filler = await PlaceInHands("GroundTobacco", 2);
        await Interact();
        Assert.That(SEntMan.GetComponent<StackComponent>(ToServer(filler)).Count, Is.EqualTo(1));
        Assert.That(Bowl().Volume, Is.EqualTo(FixedPoint2.New(20)));
        Assert.That(Base.Comp.Lit, Is.True);
        Assert.That(Base.Comp.FuelRemaining, Is.LessThanOrEqualTo(fuel));
        await Sprite("icon-lit");
        await TakeHose();
        await Puff();
        Assert.That(Bowl().Volume, Is.EqualTo(FixedPoint2.New(14)));
    }

    [Test]
    public async Task BowlHoldsEnoughFillerForOneCoal()
    {
        await Prepare();
        var puffs = (int) Math.Ceiling(Base.Comp.FuelPerItem / Base.Comp.PuffDuration.TotalSeconds);
        Assert.That(Bowl().MaxVolume, Is.GreaterThanOrEqualTo(Base.Comp.Dose * puffs));
    }
}
