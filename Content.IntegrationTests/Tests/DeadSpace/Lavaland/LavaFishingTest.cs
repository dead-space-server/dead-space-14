using System.Linq;
using System.Numerics;
using Content.Client.DeadSpace.Fishing;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.DeadSpace.Fishing;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.FixedPoint;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.DeadSpace.Lavaland;

[TestFixture]
public sealed class LavaFishingTest : InteractionTest
{
    protected override string PlayerPrototype => "MobAshWalker";

    [Test]
    public async Task BaitPreservesPackagingAndRejectsClosedOrFullContainers()
    {
        var rodNet = await SpawnTarget("AshWalkerFishingRod");
        var rodUid = ToServer(rodNet);
        var food = await PlaceInHands("FoodTinBeans");
        var foodUid = ToServer(food);
        await Interact();
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<LavaFishingRodComponent>(rodUid).Bait, Is.Zero);
            Assert.That(Server.System<SharedSolutionContainerSystem>().TryGetSolution(foodUid, "food", out var solution), Is.True);
            Assert.That(solution.Value.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(15)));
            Server.System<OpenableSystem>().SetOpen(foodUid);
        });
        await RunTicks(10);
        for (var i = 0; i < 3; i++)
            await Interact();
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<LavaFishingRodComponent>(rodUid).Bait, Is.EqualTo(3));
            Assert.That(SEntMan.Deleted(foodUid), Is.True);
            Assert.That(HandSys.EnumerateHeld(SPlayer).Select(uid => SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID),
                Is.EquivalentTo(new[] { "FoodTinBeansTrash" }));
            foreach (var held in HandSys.EnumerateHeld(SPlayer).ToArray())
                SEntMan.DeleteEntity(held);
        });
        await RunTicks(10);
        food = await PlaceInHands("FoodSoupMeatball");
        foodUid = ToServer(food);
        await Server.WaitAssertion(() =>
        {
            var solutions = Server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(foodUid, "food", out var solution), Is.True);
            solutions.SplitSolution(solution.Value, solution.Value.Comp.Solution.Volume - 5);
        });
        await RunTicks(10);
        await Interact();
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<LavaFishingRodComponent>(rodUid).Bait, Is.EqualTo(4));
            Assert.That(SEntMan.Deleted(foodUid), Is.True);
            Assert.That(HandSys.EnumerateHeld(SPlayer).Select(uid => SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID),
                Is.EquivalentTo(new[] { "FoodBowlBig" }));
            foreach (var held in HandSys.EnumerateHeld(SPlayer).ToArray())
                SEntMan.DeleteEntity(held);
        });
        await RunTicks(10);
        food = await PlaceInHands("FoodSnackChips");
        foodUid = ToServer(food);
        await Interact();
        await Interact();
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<LavaFishingRodComponent>(rodUid).Bait, Is.EqualTo(5));
            Assert.That(Server.System<SharedSolutionContainerSystem>().TryGetSolution(foodUid, "food", out var solution), Is.True);
            Assert.That(solution.Value.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(5)));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task FishingPredictsInputStoresCatchAndCleansUpCancelledCasts(bool fromBoat)
    {
        await AddGravity();
        await Server.WaitPost(() =>
        {
            for (var x = -2; x <= 5; x++)
            for (var y = -2; y <= 2; y++)
            {
                MapSystem.SetTile(MapData.Grid, MapData.Grid.Comp, new Vector2i(x, y), new Tile(TileMan["Plating"].TileId));
                if (x >= 1)
                    SEntMan.SpawnEntity("FloorLavaEntity", new EntityCoordinates(MapData.Grid, x + 0.5f, y + 0.5f));
            }
            if (fromBoat)
            {
                var boat = SEntMan.SpawnEntity("LavaBoat", new EntityCoordinates(MapData.Grid, 0.5f, 0.5f));
                Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(SPlayer, SPlayer, boat), Is.True);
                Transform.SetCoordinates(boat, new EntityCoordinates(MapData.Grid, 1.5f, 0.5f));
            }
        });
        await AddAtmosphere();
        await Client.WaitPost(() => Client.ResolveDependency<IConfigurationManager>().SetCVar(CVars.NetPredictLagBias, 0.2f));
        var rodNet = await SpawnTarget("AshWalkerFishingRod");
        var food = await PlaceInHands(fromBoat ? "FoodApple" : "FoodMeat");
        await RunTicks(5);
        var foodUid = ToServer(food);
        var foodVolume = FixedPoint2.Zero;
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<SharedSolutionContainerSystem>().TryGetSolution(foodUid, "food", out var solution), Is.True);
            foodVolume = solution.Value.Comp.Solution.Volume;
            Assert.That(foodVolume, Is.GreaterThan(FixedPoint2.Zero));
        });
        await SetKey(EngineKeyFunctions.Use, BoundKeyState.Down);
        await Client.WaitRunTicks(2);
        await Client.WaitAssertion(() => Assert.That(CEntMan.GetComponent<LavaFishingRodComponent>(ToClient(rodNet)).Bait, Is.EqualTo(1)));
        await SetKey(EngineKeyFunctions.Use, BoundKeyState.Up);
        await RunTicks(10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<LavaFishingRodComponent>(ToServer(rodNet)).Bait, Is.EqualTo(1));
            Assert.That(Server.System<SharedSolutionContainerSystem>().TryGetSolution(foodUid, "food", out var solution), Is.True);
            Assert.That(solution.Value.Comp.Solution.Volume, Is.EqualTo(foodVolume - 5));
        });
        await DeleteHeldEntity();
        await Server.WaitAssertion(() => Assert.That(HandSys.TryPickupAnyHand(SPlayer, ToServer(rodNet)), Is.True));
        await RunTicks(10);
        var rodUid = ToServer(rodNet);
        var floatCoordinates = new EntityCoordinates(MapData.Grid, fromBoat ? 5.4f : 4.4f, 1.1f);
        await Server.WaitPost(() =>
        {
            var rod = SEntMan.GetComponent<LavaFishingRodComponent>(rodUid);
            rod.Rewards = [new LavaFishingReward
            {
                Table = new EntSelector { Id = fromBoat ? "AshWalkerSoulFish" : "MaterialBones1" },
                Difficulty = fromBoat ? 1.45f : 1,
                Fish = fromBoat,
            }];
            InteractSys.UserInteraction(SPlayer, floatCoordinates, null, altInteract: false);
        });
        await RunTicks(10);
        var fishingWindow = GetWindow<LavaFishingWindow>();
        var windowPosition = new Vector2(40, 60);
        await Client.WaitPost(() => LayoutContainer.SetPosition(fishingWindow, windowPosition));
        await Server.WaitAssertion(() =>
        {
            var rod = SEntMan.GetComponent<LavaFishingRodComponent>(rodUid);
            Assert.That(rod.Phase, Is.EqualTo(LavaFishingPhase.Waiting));
            Assert.That(rod.Float, Is.Not.Null);
            Assert.That(rod.Spot, Is.EqualTo(floatCoordinates));
            rod.NextPhase = STiming.CurTime;
            SEntMan.Dirty(rodUid, rod);
        });
        await RunTicks(10);
        await SetKey(ContentKeyFunctions.UseItemInHand, BoundKeyState.Down);
        await Client.WaitRunTicks(2);
        await Client.WaitAssertion(() => Assert.That(CEntMan.GetComponent<LavaFishingRodComponent>(ToClient(rodNet)).Reeling, Is.True));
        await AssertRenderPrediction(rodNet, true);
        await SetKey(ContentKeyFunctions.UseItemInHand, BoundKeyState.Up);
        await AssertRenderPrediction(rodNet, false);
        await AssertFishingWindow(fishingWindow);
        await SetKey(ContentKeyFunctions.UseItemInHand, BoundKeyState.Down);
        await RunTicks(12);
        EntityUid occupiedHand = default;
        await Server.WaitAssertion(() =>
        {
            occupiedHand = SEntMan.SpawnEntity("MaterialBones1", Transform.GetMoverCoordinates(SPlayer));
            Assert.That(HandSys.TryPickupAnyHand(SPlayer, occupiedHand), Is.True);
        });
        var pulling = true;
        for (var i = 0; i < 450; i++)
        {
            var active = false;
            var desired = pulling;
            await Client.WaitPost(() =>
            {
                var rod = CEntMan.GetComponent<LavaFishingRodComponent>(ToClient(rodNet));
                active = rod.Phase == LavaFishingPhase.Reeling;
                if (!active)
                    return;
                desired = rod.ZonePosition < rod.FishPosition;
            });
            if (!active)
                break;
            if (desired != pulling)
            {
                pulling = desired;
                await SetKey(ContentKeyFunctions.UseItemInHand, pulling ? BoundKeyState.Down : BoundKeyState.Up);
            }
            await RunTicks(5);
        }
        await SetKey(ContentKeyFunctions.UseItemInHand, BoundKeyState.Up);
        await RunTicks(15);
        await Client.WaitAssertion(() => Assert.That(fishingWindow.IsOpen, Is.False));
        EntityUid catchUid = default;
        await Server.WaitAssertion(() =>
        {
            var rod = SEntMan.GetComponent<LavaFishingRodComponent>(rodUid);
            Assert.That(rod.Phase, Is.EqualTo(LavaFishingPhase.Caught));
            Assert.That(rod.Float, Is.Null);
            var containers = Server.System<SharedContainerSystem>();
            Assert.That(containers.TryGetContainer(rodUid, rod.CatchContainer, out var container), Is.True);
            catchUid = container.ContainedEntities.Single();
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(catchUid).EntityPrototype!.ID,
                Is.EqualTo(fromBoat ? "AshWalkerSoulFish" : "MaterialBones1"));
            SEntMan.DeleteEntity(occupiedHand);
            InteractSys.InteractionActivate(SPlayer, rodUid);
            Assert.That(HandSys.IsHolding(SPlayer, catchUid), Is.True);
            InteractSys.InteractionActivate(SPlayer, rodUid);
            Assert.That(container.ContainedEntities, Is.Empty);
            SEntMan.DeleteEntity(catchUid);
            rod.Bait = 1;
            SEntMan.Dirty(rodUid, rod);
            InteractSys.UserInteraction(SPlayer, floatCoordinates, null, altInteract: false);
        });
        await RunTicks(10);
        await Client.WaitAssertion(() =>
        {
            Assert.That(GetWindow<LavaFishingWindow>(), Is.SameAs(fishingWindow));
            Assert.That(Vector2.Distance(fishingWindow.Position, windowPosition), Is.LessThan(1));
        });
        await ClickControl<LavaFishingWindow, Button>("CancelButton");
        await RunTicks(10);
        await Client.WaitAssertion(() => Assert.That(fishingWindow.IsOpen, Is.False));
        await Server.WaitAssertion(() =>
        {
            var rod = SEntMan.GetComponent<LavaFishingRodComponent>(rodUid);
            Assert.That(rod.Phase, Is.EqualTo(LavaFishingPhase.Idle));
            Assert.That(rod.Float, Is.Null);
            Assert.That(SEntMan.HasComponent<Content.Server.DeadSpace.Fishing.LavaFishingSessionComponent>(rodUid), Is.False);
            Assert.That(SEntMan.GetComponent<Content.Server.DeadSpace.Fishing.LavaFishingPoolComponent>(MapData.Grid).Areas.Values.Single().Available, Is.EqualTo(5));
            if (fromBoat)
                Assert.That(SEntMan.GetComponent<BuckleComponent>(SPlayer).BuckledTo, Is.Not.Null);
        });
        await Server.WaitPost(() =>
        {
            var rod = SEntMan.GetComponent<LavaFishingRodComponent>(rodUid);
            rod.Bait = 1;
            SEntMan.Dirty(rodUid, rod);
            InteractSys.UserInteraction(SPlayer, floatCoordinates, null, altInteract: false);
        });
        await RunTicks(10);
        await PressKey(ContentKeyFunctions.SwapHands, 10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<LavaFishingRodComponent>(rodUid).Phase, Is.EqualTo(LavaFishingPhase.Idle));
            Assert.That(SEntMan.GetComponent<Content.Server.DeadSpace.Fishing.LavaFishingPoolComponent>(MapData.Grid).Areas.Values.Single().Available, Is.EqualTo(5));
        });
        await Client.WaitAssertion(() => Assert.That(fishingWindow.IsOpen, Is.False));
    }

    private async Task AssertFishingWindow(LavaFishingWindow window)
    {
        await Client.WaitAssertion(() =>
        {
            Assert.That(window.Disposed, Is.False);
            Assert.That(window.Resizable, Is.True);
            Assert.That(Client.Resolve<IUserInterfaceManager>().KeyboardFocused, Is.Null);
            var originalSize = window.SetSize;
            var config = Client.Resolve<IConfigurationManager>();
            var originalScale = config.GetCVar(CVars.DisplayUIScale);
            var autoScale = config.GetCVar(CVars.ResAutoScaleEnabled);
            var controls = new Control[]
            {
                GetControlFromField<Label>("StatusLabel", window),
                GetControlFromField<LavaFishingBar>("ReelingBar", window),
                GetControlFromField<ProgressBar>("CatchProgress", window),
                GetControlFromField<ScrollContainer>("HintsScroll", window),
                GetControlFromField<Button>("CancelButton", window),
            };
            try
            {
                config.SetCVar(CVars.ResAutoScaleEnabled, false);
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    config.SetCVar(CVars.DisplayUIScale, scale);
                    Assert.That(window.UIScale, Is.EqualTo(scale));
                    foreach (var width in new[] { 360, 420, 600 })
                    {
                        window.SetSize = new Vector2(width, 330);
                        window.Measure(Vector2Helpers.Infinity);
                        window.Arrange(UIBox2.FromDimensions(window.Position, window.DesiredSize));
                        var bottom = window.GlobalPosition.Y;
                        foreach (var control in controls)
                        {
                            Assert.That(control.Height, Is.GreaterThan(0), control.Name);
                            Assert.That(control.GlobalPosition.Y, Is.GreaterThanOrEqualTo(bottom + 4));
                            Assert.That(control.GlobalPosition.X, Is.GreaterThan(window.GlobalPosition.X));
                            Assert.That(control.GlobalPosition.X + control.Width, Is.LessThan(window.GlobalPosition.X + window.Width));
                            bottom = control.GlobalPosition.Y + control.Height;
                            Assert.That(bottom, Is.LessThan(window.GlobalPosition.Y + window.Height));
                        }
                    }
                    window.SetSize = originalSize;
                    window.Measure(Vector2Helpers.Infinity);
                    window.Arrange(UIBox2.FromDimensions(window.Position, window.DesiredSize));
                    var hints = GetControlFromField<BoxContainer>("HintsContent", window);
                    var scroll = GetControlFromField<ScrollContainer>("HintsScroll", window);
                    Assert.That(hints.Height, Is.LessThanOrEqualTo(scroll.Height));
                    foreach (var hint in hints.Children)
                        Assert.That(hint.Height, Is.GreaterThan(0), hint.Name);
                }
            }
            finally
            {
                config.SetCVar(CVars.DisplayUIScale, originalScale);
                config.SetCVar(CVars.ResAutoScaleEnabled, autoScale);
                window.SetSize = originalSize;
                window.Measure(Vector2Helpers.Infinity);
                window.Arrange(UIBox2.FromDimensions(window.Position, window.DesiredSize));
            }
        });
        foreach (var state in new[] { BoundKeyState.Down, BoundKeyState.Up })
        {
            var args = new GUIBoundKeyEventArgs(ContentKeyFunctions.UseItemInHand, state, default, false, default, default);
            await Client.DoGuiEvent(window, args);
            Assert.That(args.Handled, Is.False);
        }
    }

    private async Task AssertRenderPrediction(NetEntity rodNet, bool pulling)
    {
        await Client.WaitAssertion(() =>
        {
            var uid = ToClient(rodNet);
            var rod = CEntMan.GetComponent<LavaFishingRodComponent>(uid);
            var fishing = Client.System<Content.Client.DeadSpace.Fishing.LavaFishingSystem>();
            var original = new LavaFishingReelState(rod.ZonePosition, rod.FishPosition, rod.Progress, rod.Elapsed);
            var remainder = CTiming.TickRemainder;
            try
            {
                CTiming.TickRemainder = TimeSpan.FromSeconds(1d / 240);
                var firstFrame = fishing.GetRenderState((uid, rod));
                CTiming.TickRemainder = TimeSpan.FromSeconds(1d / 120);
                var secondFrame = fishing.GetRenderState((uid, rod));
                Assert.That((secondFrame.ZonePosition - firstFrame.ZonePosition) * (pulling ? 1 : -1), Is.GreaterThan(0));
                Assert.That(secondFrame.Progress, Is.GreaterThan(firstFrame.Progress));
                Assert.That(new LavaFishingReelState(rod.ZonePosition, rod.FishPosition, rod.Progress, rod.Elapsed), Is.EqualTo(original));
            }
            finally
            {
                CTiming.TickRemainder = remainder;
            }
        });
    }
}
