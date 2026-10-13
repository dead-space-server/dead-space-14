using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Tests.Movement;
using Content.Server.DeadSpace.Smokables.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DeadSpace.Smokables;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Interaction.Events;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Audio.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.DeadSpace.Shisha;

/// <summary>Uses real humans, hands, interactions and movement rather than a mock smoking system.</summary>
public sealed class ShishaTest : MovementTest
{
    protected override int Tiles => 7;

    // Explicitly empty groups prevent the fork's RemoveEmpty metabolism from removing test doses.
    [TestPrototypes]
    private const string Prototypes = """
        - type: reagent
          id: ShishaTestReagentA
          name: reagent-name-nothing
          desc: reagent-desc-nothing
          physicalDesc: reagent-physical-desc-nothing
          metabolisms: {}
        - type: reagent
          id: ShishaTestReagentB
          name: reagent-name-nothing
          desc: reagent-desc-nothing
          physicalDesc: reagent-physical-desc-nothing
          metabolisms: {}
        """;

    private const string ReagentA = "ShishaTestReagentA";
    private const string ReagentB = "ShishaTestReagentB";
    private ShishaSystem Shisha => Server.System<ShishaSystem>();
    private SharedSolutionContainerSystem Solutions => Server.System<SharedSolutionContainerSystem>();

    private Entity<ShishaComponent> Base => (STarget!.Value, Comp<ShishaComponent>());
    private EntityUid Hose => Base.Comp.Hose!.Value;

    private async Task CreateShisha(int amount = 10)
    {
        await SpawnTarget("Shisha", PlayerCoords);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Base.Comp.Hose, Is.Not.Null);
            Assert.That(Shisha.IsDocked(Base), Is.True);
            Assert.That(Solutions.TryGetSolution(Base.Owner, "shisha", out var sol, out _), Is.True);
            Assert.That(Solutions.TryAddSolution(sol!.Value, new Solution(ReagentA, Math.Max(amount, 1))), Is.True);
        });
        await PlaceInHands("Coal1");
        await Interact();
        await PlaceInHands("CheapLighter");
        await Interact();
        await DeleteHeldEntity();
        Assert.That(Base.Comp.Lit, Is.True);
        if (amount == 0)
        {
            await Server.WaitPost(() =>
            {
                Solutions.TryGetSolution(Base.Owner, "shisha", out var sol, out var solution);
                Solutions.SplitSolution(sol!.Value, solution!.Volume);
            });
        }
    }

    private async Task TakeHose()
    {
        await Server.WaitAssertion(() =>
        {
            var verbs = Server.System<SharedVerbSystem>();
            var verb = verbs.GetLocalVerbs(Base, SPlayer, typeof(AlternativeVerb))
                .Single(v => v.Text == Robust.Shared.Localization.Loc.GetString("shisha-take-hose"));
            verbs.ExecuteVerb(verb, SPlayer, Base);
            Assert.That(HandSys.IsHolding(SPlayer, Hose), Is.True);
        });
        await RunTicks(5);
    }

    private async Task BeginPuff()
    {
        await Interact(Player, NetPosition(Player), awaitDoAfters: false);
        Assert.That(ActiveDoAfters.Count(), Is.EqualTo(1));
    }

    private FixedPoint2 Volume()
    {
        Assert.That(Solutions.TryGetSolution(Base.Owner, "shisha", out _, out var solution), Is.True);
        return solution!.Volume;
    }

    [Test]
    public async Task DragOntoSelfPicksUpWholeBase()
    {
        await CreateShisha();
        await DragDrop(Target!.Value, Player);
        Assert.That(HandSys.IsHolding(SPlayer, Base), Is.True);
        Assert.That(Shisha.IsDocked(Base), Is.True);
    }

    [Test]
    public async Task ClickTakesAndReturnsHoseWithoutPickingUpBase()
    {
        await CreateShisha();
        await Interact();
        Assert.That(HandSys.IsHolding(SPlayer, Hose), Is.True);
        Assert.That(HandSys.IsHolding(SPlayer, Base), Is.False);
        await Interact();
        Assert.That(Shisha.IsDocked(Base), Is.True);
        Assert.That(HandSys.IsHolding(SPlayer, Base), Is.False);
    }

    [TestCase(0)]
    [TestCase(2)]
    public async Task PartialAndEmptyPuffs(int amount)
    {
        await CreateShisha(amount);
        await TakeHose();
        await BeginPuff();
        await AwaitDoAfters();
        Assert.That(Volume(), Is.EqualTo(FixedPoint2.Zero));
        await Server.WaitAssertion(() =>
        {
            Solutions.TryGetSolution(SPlayer, BloodstreamComponent.DefaultBloodSolutionName, out _, out var blood);
            Assert.That(blood!.GetTotalPrototypeQuantity(ReagentA), Is.EqualTo(FixedPoint2.New(amount)));
            if (amount == 0)
            {
                Assert.That(SEntMan.EntityQuery<AudioComponent>().Any(audio =>
                    audio.FileName == "/Audio/Effects/Chemistry/bubbles.ogg"), Is.True);
            }
        });
    }

    [Test]
    public async Task UseInHandConsumesOneDose()
    {
        await CreateShisha();
        await TakeHose();
        await UseInHand();
        await RunTicks(1);
        Assert.That(ActiveDoAfters.Count(), Is.EqualTo(1));
        await AwaitDoAfters();
        Assert.That(Volume(), Is.EqualTo(FixedPoint2.New(4)));
    }

    [Test]
    public async Task HoseCanBePassedToAnotherSmoker()
    {
        await CreateShisha();
        await TakeHose();
        await BeginPuff();
        var other = await Spawn("MobHuman", PlayerCoords);
        await Server.WaitAssertion(() =>
        {
            Assert.That(HandSys.TryPickupAnyHand(ToServer(other), Hose), Is.True);
            // Start through the same in-hand event used by the interaction system.
            var use = new UseInHandEvent(ToServer(other));
            SEntMan.EventBus.RaiseLocalEvent(Hose, use);
            Assert.That(use.Handled, Is.True);
        });
        await RunSeconds(3);
        Assert.That(Volume(), Is.EqualTo(FixedPoint2.New(4)));
        await Server.WaitAssertion(() =>
        {
            Solutions.TryGetSolution(SPlayer, BloodstreamComponent.DefaultBloodSolutionName, out _, out var original);
            Solutions.TryGetSolution(ToServer(other), BloodstreamComponent.DefaultBloodSolutionName, out _, out var receiver);
            Assert.That(original!.GetTotalPrototypeQuantity(ReagentA), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(receiver!.GetTotalPrototypeQuantity(ReagentA), Is.EqualTo(FixedPoint2.New(6)));
        });
    }

    [Test]
    public async Task WallBetweenEndpointsRetractsHose()
    {
        await CreateShisha();
        await TakeHose();
        await BeginPuff();
        await Server.WaitPost(() =>
        {
            // Isolate obstruction from range with enough room to place a wall between endpoints.
            Base.Comp.HoseLength = 2f;
            Transform.SetCoordinates(Base.Owner, ToServer(PlayerCoords).Offset(new Vector2(1.9f, 0)));
        });
        await Spawn("WallSolid", FromServer(ToServer(PlayerCoords).Offset(new Vector2(0.95f, 0))));
        await RunSeconds(3);
        Assert.That(Shisha.IsDocked(Base), Is.True);
        Assert.That(Volume(), Is.EqualTo(FixedPoint2.New(10)));
    }

    [Test]
    public async Task MapChangeRetractsHoseEvenAtMatchingWorldPosition()
    {
        await CreateShisha();
        await TakeHose();
        await BeginPuff();
        MapId otherMap = default;
        try
        {
            await Server.WaitPost(() =>
            {
                var map = MapSystem.CreateMap(out otherMap);
                Transform.SetCoordinates(Base.Owner, new EntityCoordinates(map, Transform.GetWorldPosition(SPlayer)));
            });
            await RunSeconds(3);
            Assert.That(Shisha.IsDocked(Base), Is.True);
            Assert.That(Volume(), Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(HandSys.IsHolding(SPlayer, Hose), Is.False);
        }
        finally
        {
            await Server.WaitPost(() => MapSystem.DeleteMap(otherMap));
        }
    }

    [Test]
    public async Task TakeReturnPreservesIdentityAndReplicates()
    {
        await CreateShisha();
        var hose = Hose;
        for (var i = 0; i < 3; i++)
        {
            await TakeHose();
            Assert.That(Hose, Is.EqualTo(hose));
            var cBase = CEntMan.GetComponent<ShishaComponent>(CTarget!.Value);
            Assert.That(cBase.Hose, Is.EqualTo(ToClient(hose)));
            Assert.That(CEntMan.GetComponent<ShishaHoseComponent>(ToClient(hose)).Base, Is.EqualTo(CTarget));
            Assert.That(CEntMan.HasComponent<ShishaHoseVisualsComponent>(ToClient(hose)), Is.True);
            await AssertBaseSprite("icon-lit-no-hose");
            await Interact();
            await RunTicks(5);
            Assert.That(Shisha.IsDocked(Base), Is.True);
            Assert.That(HandSys.IsHolding(SPlayer, hose), Is.False);
            Assert.That(CEntMan.HasComponent<ShishaHoseVisualsComponent>(ToClient(hose)), Is.False);
            await AssertBaseSprite("icon-lit");
        }
    }

    private async Task AssertBaseSprite(string state)
    {
        await Client.WaitAssertion(() =>
        {
            var sprites = Client.System<SpriteSystem>();
            var layer = sprites.LayerMapGet(CTarget!.Value, ShishaVisuals.Base);
            Assert.That(sprites.LayerGetRsiState(CTarget.Value, layer).ToString(), Is.EqualTo(state));
        });
    }

    [Test]
    public async Task FullHandsLeaveHoseDocked()
    {
        await CreateShisha();
        await Server.WaitAssertion(() =>
        {
            foreach (var hand in Hands!.SortedHands)
            {
                var item = SEntMan.SpawnEntity("Beaker", ToServer(PlayerCoords));
                Assert.That(HandSys.TryPickup(SPlayer, item, hand), Is.True);
            }
            Assert.That(Shisha.TryTakeHose(Base, SPlayer), Is.False);
            Assert.That(Shisha.IsDocked(Base), Is.True);
        });
        await DragDrop(Target!.Value, Player);
        Assert.That(HandSys.IsHolding(SPlayer, Base), Is.False);
        Assert.That(Shisha.IsDocked(Base), Is.True);
    }

    [Test]
    public async Task ForeignBaseRejectsHose()
    {
        await CreateShisha();
        await TakeHose();
        var foreign = await Spawn("Shisha", TargetCoords);
        await Interact(foreign, TargetCoords);
        Assert.That(HandSys.IsHolding(SPlayer, Hose), Is.True);
        Assert.That(Comp<ShishaComponent>(foreign).Hose, Is.Not.EqualTo(Hose));
        Assert.That(Comp<ShishaHoseComponent>(FromServer(Hose)).Base, Is.EqualTo(STarget));
    }

    [Test]
    public async Task PuffConsumesOneMixedDoseAndIdleConsumesNothing()
    {
        await CreateShisha(5);
        await Server.WaitAssertion(() =>
        {
            Solutions.TryGetSolution(Base.Owner, "shisha", out var sol, out _);
            Assert.That(Solutions.TryAddSolution(sol!.Value, new Solution(ReagentB, 5)), Is.True);
        });
        await RunSeconds(4);
        Assert.That(Volume(), Is.EqualTo(FixedPoint2.New(10)));
        await TakeHose();
        await BeginPuff();
        // Repeated clicks must not add a second puff or cancel the first.
        await Interact(Player, NetPosition(Player), awaitDoAfters: false);
        Assert.That(ActiveDoAfters.Count(), Is.EqualTo(1));
        await AwaitDoAfters();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Volume(), Is.EqualTo(FixedPoint2.New(4)));
            Assert.That(Solutions.TryGetSolution(SPlayer, BloodstreamComponent.DefaultBloodSolutionName, out _, out var blood), Is.True);
            Assert.That(blood!.GetTotalPrototypeQuantity(ReagentA), Is.EqualTo(FixedPoint2.New(3)));
            Assert.That(blood.GetTotalPrototypeQuantity(ReagentB), Is.EqualTo(FixedPoint2.New(3)));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CancelledPuffConsumesNothing(bool returnHose)
    {
        await CreateShisha();
        await TakeHose();
        await BeginPuff();
        if (returnHose)
            await Interact();
        else
            await CancelDoAfters();
        await RunSeconds(3);
        Assert.That(Volume(), Is.EqualTo(FixedPoint2.New(10)));
        Assert.That(ActiveDoAfters.Any(), Is.False);
    }

    [Test]
    public async Task ReservoirIsRecheckedAtCompletion()
    {
        await CreateShisha();
        await TakeHose();
        await BeginPuff();
        await Server.WaitPost(() =>
        {
            Solutions.TryGetSolution(Base.Owner, "shisha", out var sol, out _);
            Solutions.SplitSolution(sol!.Value, FixedPoint2.New(9.5));
        });
        await AwaitDoAfters();
        Assert.That(Volume(), Is.EqualTo(FixedPoint2.Zero));
        await Server.WaitAssertion(() =>
        {
            Solutions.TryGetSolution(SPlayer, BloodstreamComponent.DefaultBloodSolutionName, out _, out var blood);
            Assert.That(blood!.GetTotalPrototypeQuantity(ReagentA), Is.EqualTo(FixedPoint2.New(0.5)));
        });
    }

    [Test]
    public async Task WalkingBeyondRangeReturnsHoseAndCancelsPuff()
    {
        await CreateShisha();
        await TakeHose();
        await BeginPuff();
        await Move(DirectionFlag.West, 1);
        await RunSeconds(3);
        Assert.That(Shisha.IsDocked(Base), Is.True);
        Assert.That(HandSys.IsHolding(SPlayer, Hose), Is.False);
        Assert.That(Volume(), Is.EqualTo(FixedPoint2.New(10)));
    }

    [Test]
    public async Task BaseAndHoseCanBeCarriedTogether()
    {
        await CreateShisha();
        await TakeHose();
        await Server.WaitAssertion(() => Assert.That(HandSys.TryPickupAnyHand(SPlayer, Base), Is.True));
        await Move(DirectionFlag.West, 1);
        Assert.That(HandSys.IsHolding(SPlayer, Base), Is.True);
        Assert.That(HandSys.IsHolding(SPlayer, Hose), Is.True);
        Assert.That(Shisha.IsDocked(Base), Is.False);
    }

    [Test]
    public async Task DroppingHoseReturnsIt()
    {
        await CreateShisha();
        await TakeHose();
        await BeginPuff();
        await Drop();
        await RunSeconds(0.3f);
        Assert.That(Shisha.IsDocked(Base), Is.True);
        Assert.That(Volume(), Is.EqualTo(FixedPoint2.New(10)));
        Assert.That(ActiveDoAfters.Any(), Is.False);
    }

    [Test]
    public async Task ThrowingRetractsInsteadOfLaunchingHose()
    {
        await CreateShisha();
        await TakeHose();
        Assert.That(await ThrowItem(), Is.False);
        await RunTicks(5);
        Assert.That(Shisha.IsDocked(Base), Is.True);
        Assert.That(HandSys.IsHolding(SPlayer, Hose), Is.False);
    }

    [Test]
    public async Task DamageCancelsPuff()
    {
        await CreateShisha();
        await TakeHose();
        await BeginPuff();
        await Server.WaitPost(() => Server.System<DamageableSystem>().TryChangeDamage(SPlayer,
            new DamageSpecifier { DamageDict = { ["Blunt"] = 5 } }, true));
        await RunSeconds(3);
        Assert.That(ActiveDoAfters.Any(), Is.False);
        Assert.That(Volume(), Is.EqualTo(FixedPoint2.New(10)));
    }

    [Test]
    public async Task CoveredMouthAtCompletionConsumesNothing()
    {
        await CreateShisha();
        await TakeHose();
        await BeginPuff();
        await Server.WaitAssertion(() =>
        {
            var mask = SEntMan.SpawnEntity("ClothingMaskGas", ToServer(PlayerCoords));
            Assert.That(Server.System<InventorySystem>().TryEquip(SPlayer, mask, "mask", force: true), Is.True);
        });
        await AwaitDoAfters();
        Assert.That(Volume(), Is.EqualTo(FixedPoint2.New(10)));
        await Interact(Player, NetPosition(Player), awaitDoAfters: false);
        Assert.That(ActiveDoAfters.Any(), Is.False);
    }

    [Test]
    public async Task LiquidsCannotReplaceFiller()
    {
        await CreateShisha(0);
        var beaker = await PlaceInHands("Beaker");
        await Server.WaitAssertion(() =>
        {
            Solutions.TryGetSolution(ToServer(beaker), "beaker", out var sol, out _);
            Assert.That(Solutions.TryAddSolution(sol!.Value, new Solution(ReagentA, 10)), Is.True);
        });
        await Interact();
        Assert.That(Volume(), Is.EqualTo(FixedPoint2.Zero));
        await Server.WaitAssertion(() =>
        {
            Solutions.TryGetSolution(ToServer(beaker), "beaker", out _, out var solution);
            Assert.That(solution!.Volume + Volume(), Is.EqualTo(FixedPoint2.New(10)));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task StoringEitherEndpointRetractsHose(bool storeBase)
    {
        await CreateShisha();
        await TakeHose();
        var backpack = await Spawn("ClothingBackpack", TargetCoords);
        await Server.WaitAssertion(() =>
        {
            var containers = Server.System<SharedContainerSystem>();
            Assert.That(containers.TryGetContainer(ToServer(backpack), "storagebase", out var storage), Is.True);
            Assert.That(containers.Insert(storeBase ? Base.Owner : Hose, storage!), Is.True);
        });
        await RunSeconds(0.3f);
        Assert.That(Shisha.IsDocked(Base), Is.True);
        Assert.That(HandSys.IsHolding(SPlayer, Hose), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DeletingEndpointCancelsPuffAndClearsOwnership(bool deleteBase)
    {
        await CreateShisha();
        await TakeHose();
        await BeginPuff();
        var hose = Hose;
        await Delete(deleteBase ? Base.Owner : hose);
        await RunSeconds(3);
        Assert.That(SEntMan.EntityExists(hose), Is.False);
        Assert.That(ActiveDoAfters.Any(), Is.False);
        if (!deleteBase)
        {
            Assert.That(Base.Comp.Hose, Is.Null);
            Assert.That(Volume(), Is.EqualTo(FixedPoint2.New(10)));
        }
    }
}
