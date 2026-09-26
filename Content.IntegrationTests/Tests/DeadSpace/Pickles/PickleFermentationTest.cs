// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DeadSpace.Pickles;
using Content.Shared.DeadSpace.Pickles.Components;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Interaction;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.DeadSpace.Pickles;

public sealed class PickleFermentationTest : InteractionTest
{
    private static readonly EntProtoId WoodBarrel = "PickleBarrelWood";
    private static readonly EntProtoId FoodJar = "FoodPickleJar";
    private static readonly EntProtoId Cucumber = "FoodCucumber";
    private static readonly EntProtoId Cabbage = "FoodCabbage";
    private static readonly EntProtoId Tomato = "FoodTomato";
    private static readonly EntProtoId Grape = "FoodGrape";
    private static readonly EntProtoId GlassShard = "ShardGlass";

    private static readonly ProtoId<ReagentPrototype> Vinegar = "Vinegar";
    private static readonly ProtoId<ReagentPrototype> TableSalt = "TableSalt";
    private static readonly ProtoId<ReagentPrototype> Sugar = "Sugar";
    private static readonly ProtoId<ReagentPrototype> Toxin = "Toxin";
    private static readonly ProtoId<ReagentPrototype> Bacteria = "PickleBacteria";
    private static readonly ProtoId<ReagentPrototype> VinegarBrine = "PickleVinegarBrine";
    private static readonly ProtoId<ReagentPrototype> SaltBrine = "PickleSaltBrine";
    private static readonly ProtoId<ReagentPrototype> PickleWine = "PickleWine";

    private static readonly ProtoId<PickleRecipePrototype> TomatoVinegarRecipe = "PickleTomatoVinegar";
    private static readonly ProtoId<PickleRecipePrototype> CabbageVinegarRecipe = "PickleCabbageVinegar";

    private FermentationBarrelComponent Barrel => Comp<FermentationBarrelComponent>();
    private SharedSolutionContainerSystem Solutions => Server.System<SharedSolutionContainerSystem>();
    private SharedContainerSystem Containers => Server.System<SharedContainerSystem>();

    [Test]
    public async Task ReadyBatchStaysInBarrelUntilHandOrJar()
    {
        await PrepareBarrel(Vinegar, Cucumber, 3);
        await StartFermentation();
        await FinishFermentation();

        await Server.WaitAssertion(() =>
        {
            Assert.That(Barrel.State, Is.EqualTo(FermentationState.Ready));
            Assert.That(Containers.TryGetContainer(STarget!.Value, FermentationBarrelComponent.ProduceContainerId, out var container), Is.True);
            Assert.That(container!.ContainedEntities, Has.Count.EqualTo(3));
            Assert.That(container.ContainedEntities.Count(uid => SEntMan.HasComponent<PickledProduceComponent>(uid)), Is.EqualTo(3));
        });
        await FindEntity(FoodJar, shouldSucceed: false);
    }

    [Test]
    public async Task EmptyHandTakesOnePickledPiece()
    {
        await PrepareBarrel(Vinegar, Cucumber, 3);
        await StartFermentation();
        await FinishFermentation();

        await DeleteHeldEntity();
        await Interact();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Barrel.State, Is.EqualTo(FermentationState.Ready));
            var held = HandSys.GetActiveItem((SPlayer, Hands));
            Assert.That(held, Is.Not.Null);
            Assert.That(SEntMan.HasComponent<PickledProduceComponent>(held!.Value), Is.True);
            Assert.That(Containers.TryGetContainer(STarget!.Value, FermentationBarrelComponent.ProduceContainerId, out var container), Is.True);
            Assert.That(container!.ContainedEntities, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task EmptyJarPacksFromBarrel()
    {
        await PrepareBarrel(Vinegar, Cucumber, 3);
        await StartFermentation();
        await FinishFermentation();

        await InteractUsing(FoodJar);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Barrel.State, Is.EqualTo(FermentationState.Idle));
            Assert.That(Containers.TryGetContainer(STarget!.Value, FermentationBarrelComponent.ProduceContainerId, out var container), Is.True);
            Assert.That(container!.ContainedEntities, Is.Empty);
        });

        var jar = await FindEntity(FoodJar);
        await Server.WaitAssertion(() =>
        {
            var jarComp = SEntMan.GetComponent<PickleJarComponent>(jar);
            Assert.That(jarComp.RemainingPieces, Is.EqualTo(3));
            Assert.That(jarComp.PiecePrototype, Is.EqualTo(Cucumber));
            Assert.That(Solutions.TryGetSolution(jar, PickleJarComponent.SolutionName, out _, out var drink), Is.True);
            Assert.That(drink!.GetTotalPrototypeQuantity(VinegarBrine), Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(Solutions.TryGetSolution(STarget!.Value, FermentationBarrelComponent.SolutionName, out _, out var tank), Is.True);
            Assert.That(tank!.Volume, Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task LeftoverRawProduceStaysInTheBarrel()
    {
        await PrepareBarrel(Vinegar, Cucumber, 4);
        await StartFermentation();
        await FinishFermentation();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Barrel.State, Is.EqualTo(FermentationState.Ready));
            Assert.That(Containers.TryGetContainer(STarget!.Value, FermentationBarrelComponent.ProduceContainerId, out var container), Is.True);
            Assert.That(container!.ContainedEntities, Has.Count.EqualTo(4));
            Assert.That(container.ContainedEntities.Count(uid => SEntMan.HasComponent<PickledProduceComponent>(uid)), Is.EqualTo(3));
            Assert.That(container.ContainedEntities.Count(uid => !SEntMan.HasComponent<PickledProduceComponent>(uid)), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task BacteriaShortensFermentation()
    {
        await PrepareBarrel(Vinegar, Cucumber, 3);
        await StartFermentation();
        var plain = Barrel.TargetDuration;
        await Server.WaitPost(() => Barrel.State = FermentationState.Idle);
        await AddReagent(Bacteria, 5);
        await StartFermentation();
        Assert.That(Barrel.TargetDuration.TotalSeconds,
            Is.EqualTo(plain.TotalSeconds / Barrel.BacteriaSpeedMultiplier).Within(0.05));
    }

    [Test]
    public async Task ExtraChemicalsCarryIntoPickledProduce()
    {
        await PrepareBarrel(Vinegar, Cucumber, 3);
        await AddReagent(Toxin, 10);
        await StartFermentation();
        await FinishFermentation();

        await Server.WaitAssertion(() =>
        {
            Assert.That(Containers.TryGetContainer(STarget!.Value, FermentationBarrelComponent.ProduceContainerId, out var container), Is.True);
            var total = FixedPoint2.Zero;
            foreach (var uid in container!.ContainedEntities)
            {
                Assert.That(Solutions.TryGetSolution(uid, "food", out _, out var food), Is.True);
                total += food!.GetTotalPrototypeQuantity(Toxin);
            }

            Assert.That(total.Float(), Is.EqualTo(10f).Within(0.05f));
        });
    }

    [Test]
    public async Task GrapeSugarMakesPickleWineBottledWithJar()
    {
        await PrepareBarrel(Sugar, Grape, 3);
        await StartFermentation();
        await FinishFermentation();

        await Server.WaitAssertion(() =>
        {
            Assert.That(Barrel.State, Is.EqualTo(FermentationState.Ready));
            Assert.That(Barrel.ReadyMethod, Is.EqualTo(PickleMethod.Alcohol));
        });

        await InteractUsing(FoodJar);
        var jar = await FindEntity(FoodJar);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Solutions.TryGetSolution(jar, PickleJarComponent.SolutionName, out _, out var drink), Is.True);
            Assert.That(drink!.GetTotalPrototypeQuantity(PickleWine), Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(Barrel.State, Is.EqualTo(FermentationState.Idle));
        });
    }

    [Test]
    public async Task AlcoholEmptyHandShowsHint()
    {
        await PrepareBarrel(Sugar, Grape, 3);
        await StartFermentation();
        await FinishFermentation();
        await DeleteHeldEntity();
        await Interact();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Barrel.State, Is.EqualTo(FermentationState.Ready));
            Assert.That(HandSys.GetActiveItem((SPlayer, Hands)), Is.Null);
        });
    }

    [Test]
    public async Task SaltedCabbagePacksALowBrineJar()
    {
        await PrepareBarrel(TableSalt, Cabbage, 3);
        await StartFermentation();
        await FinishFermentation();
        await InteractUsing(FoodJar);
        var jar = await FindEntity(FoodJar);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Solutions.TryGetSolution(jar, PickleJarComponent.SolutionName, out _, out var drink), Is.True);
            Assert.That(drink!.GetTotalPrototypeQuantity(SaltBrine), Is.GreaterThanOrEqualTo(FixedPoint2.New(6)));
        });
    }

    [Test]
    public async Task VinegarWithoutSugarDoesNotStart()
    {
        await AddAtmosphere();
        await SpawnTarget(WoodBarrel, PlayerCoords);
        await EnsureRoomTemperature();
        await AddReagent(Vinegar, 30);
        for (var i = 0; i < 3; i++)
            await InteractUsing(Cucumber);
        await StartFermentation(shouldSucceed: false);
        Assert.That(Barrel.State, Is.EqualTo(FermentationState.Idle));
    }

    [Test]
    public async Task TooFewProduceDoesNotStart()
    {
        await PrepareBarrel(Vinegar, Cucumber, 1);
        await StartFermentation(shouldSucceed: false);
        Assert.That(Barrel.State, Is.EqualTo(FermentationState.Idle));
    }

    [Test]
    public async Task DisabledCVarBlocksFermentation()
    {
        await PrepareBarrel(Vinegar, Cucumber, 3);
        var cfg = Server.CfgMan;
        var previous = cfg.GetCVar(PicklesCVars.Enabled);
        try
        {
            await Server.WaitPost(() => cfg.SetCVar(PicklesCVars.Enabled, false));
            await StartFermentation(shouldSucceed: false);
            Assert.That(Barrel.State, Is.EqualTo(FermentationState.Idle));
        }
        finally
        {
            await Server.WaitPost(() => cfg.SetCVar(PicklesCVars.Enabled, previous));
        }
    }

    [Test]
    public async Task OverheatingBurstsTheBatch()
    {
        await PrepareBarrel(Vinegar, Cucumber, 3);
        await StartFermentation();
        await Server.WaitPost(() =>
        {
            Barrel.MaxTemperature = 250f;
            Barrel.InvalidTempTime = TimeSpan.FromSeconds(6);
        });
        await RunSeconds(1);
        await FindEntity(FoodJar, shouldSucceed: false);
        await FindEntity(GlassShard);
        Assert.That(Barrel.State, Is.EqualTo(FermentationState.Idle));
    }

    [Test]
    public async Task TooMuchBacteriaBurstsTheBatch()
    {
        await PrepareBarrel(Vinegar, Cucumber, 3);
        await AddReagent(Bacteria, 40);
        await StartFermentation();
        await RunSeconds(1);
        await FindEntity(FoodJar, shouldSucceed: false);
        await FindEntity(GlassShard);
        Assert.That(Barrel.State, Is.EqualTo(FermentationState.Idle));
    }

    [Test]
    public async Task EmptyHandTakesAPieceFromAnOpenJar()
    {
        await PrepareBarrel(Vinegar, Cucumber, 3);
        await StartFermentation();
        await FinishFermentation();
        await InteractUsing(FoodJar);

        var jar = await FindEntity(FoodJar);
        Target = SEntMan.GetNetEntity(jar);
        await Server.WaitPost(() =>
        {
            Server.System<OpenableSystem>().SetOpen(jar);
            SEntMan.GetComponent<PickleJarComponent>(jar).SlipChance = 0f;
        });
        await Drop();
        await Interact();
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<PickleJarComponent>(jar).RemainingPieces, Is.EqualTo(2));
            var held = HandSys.GetActiveItem((SPlayer, Hands));
            Assert.That(held, Is.Not.Null);
            Assert.That(SEntMan.HasComponent<PickledProduceComponent>(held!.Value), Is.True);
        });
    }

    [Test]
    public async Task EatingJarEatsPiecesOneByOneThenLeavesBrine()
    {
        await PrepareBarrel(Vinegar, Cucumber, 3);
        await StartFermentation();
        await FinishFermentation();
        await InteractUsing(FoodJar);

        var jar = await FindEntity(FoodJar);
        await Server.WaitPost(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            var eater = SEntMan.SpawnEntity("MobHuman", coords);
            Assert.That(HandSys.TryPickupAnyHand(eater, jar), Is.True, "human should hold the jar");

            Server.System<OpenableSystem>().SetOpen(jar);
            Assert.That(SEntMan.GetComponent<PickleJarComponent>(jar).RemainingPieces, Is.EqualTo(3));

            for (var i = 0; i < 3; i++)
            {
                var before = SEntMan.GetComponent<PickleJarComponent>(jar).RemainingPieces;
                var ev = new Content.Shared.Interaction.Events.UseInHandEvent(eater);
                SEntMan.EventBus.RaiseLocalEvent(jar, ev);
                Assert.That(ev.Handled, Is.True);
                Assert.That(SEntMan.GetComponent<PickleJarComponent>(jar).RemainingPieces, Is.EqualTo(before - 1));
            }

            Assert.That(SEntMan.GetComponent<PickleJarComponent>(jar).RemainingPieces, Is.EqualTo(0));
            Assert.That(Solutions.TryGetSolution(jar, PickleJarComponent.SolutionName, out _, out var drink), Is.True);
            Assert.That(drink!.Volume, Is.GreaterThan(FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task TipVerbSpillsBarrelSolution()
    {
        await PrepareBarrel(Vinegar, Cucumber, 1);
        await Server.WaitAssertion(() =>
        {
            var verbs = Server.System<SharedVerbSystem>();
            var tip = verbs.GetLocalVerbs(STarget!.Value, SPlayer, typeof(AlternativeVerb), force: true)
                .Single(verb => verb.Text == Loc.GetString("pickle-barrel-verb-tip"));
            verbs.ExecuteVerb(tip, SPlayer, STarget.Value, forced: true);
            Assert.That(Solutions.TryGetSolution(STarget.Value, FermentationBarrelComponent.SolutionName, out _, out var tank), Is.True);
            Assert.That(tank!.Volume, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(SEntMan.EntityQuery<PuddleComponent>().Any(), Is.True);
        });
    }

    [Test]
    public async Task TipVerbDumpsProduceOntoTheFloor()
    {
        await PrepareBarrel(Vinegar, Cucumber, 3);
        await StartFermentation();
        await FinishFermentation();

        await Server.WaitAssertion(() =>
        {
            Assert.That(Containers.TryGetContainer(STarget!.Value, FermentationBarrelComponent.ProduceContainerId, out var before), Is.True);
            Assert.That(before!.ContainedEntities, Has.Count.EqualTo(3));

            var verbs = Server.System<SharedVerbSystem>();
            var tip = verbs.GetLocalVerbs(STarget.Value, SPlayer, typeof(AlternativeVerb), force: true)
                .Single(verb => verb.Text == Loc.GetString("pickle-barrel-verb-tip"));
            verbs.ExecuteVerb(tip, SPlayer, STarget.Value, forced: true);

            Assert.That(Containers.TryGetContainer(STarget.Value, FermentationBarrelComponent.ProduceContainerId, out var after), Is.True);
            Assert.That(after!.ContainedEntities, Is.Empty);
            Assert.That(Barrel.State, Is.EqualTo(FermentationState.Idle));
            Assert.That(Solutions.TryGetSolution(STarget.Value, FermentationBarrelComponent.SolutionName, out _, out var tank), Is.True);
            Assert.That(tank!.Volume, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(SEntMan.EntityQuery<PickledProduceComponent>().Count(), Is.EqualTo(3));
            if (SEntMan.TryGetComponent(SPlayer, out BlindableComponent blind))
                Assert.That(blind.IsBlind, Is.False);
        });
    }

    [Test]
    public async Task JarDefaultsMatchSlipAndWineBlindChances()
    {
        var jarNet = await Spawn(FoodJar.Id, PlayerCoords);
        var jar = ToServer(jarNet);
        await Server.WaitAssertion(() =>
        {
            var jarComp = SEntMan.GetComponent<PickleJarComponent>(jar);
            Assert.That(jarComp.SlipChance, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(jarComp.WineBlindChance, Is.EqualTo(0.05f).Within(0.001f));
        });
    }

    [Test]
    public async Task TakingPieceCanSlipAndSpillBrine()
    {
        await PrepareBarrel(Vinegar, Cucumber, 3);
        await StartFermentation();
        await FinishFermentation();
        await InteractUsing(FoodJar);

        var jar = await FindEntity(FoodJar);
        Target = SEntMan.GetNetEntity(jar);
        FixedPoint2 brineBefore = default;
        await Server.WaitPost(() =>
        {
            Server.System<OpenableSystem>().SetOpen(jar);
            var jarComp = SEntMan.GetComponent<PickleJarComponent>(jar);
            jarComp.SlipChance = 1f;
            Assert.That(Solutions.TryGetSolution(jar, PickleJarComponent.SolutionName, out _, out var drink), Is.True);
            brineBefore = drink!.Volume;
        });

        await Drop();
        await Interact();
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<PickleJarComponent>(jar).RemainingPieces, Is.EqualTo(2));
            Assert.That(HandSys.GetActiveItem((SPlayer, Hands)), Is.Null);
            Assert.That(SEntMan.EntityQuery<PickledProduceComponent>().Any(), Is.True);
            Assert.That(Solutions.TryGetSolution(jar, PickleJarComponent.SolutionName, out _, out var drink), Is.True);
            Assert.That(drink!.Volume, Is.LessThan(brineBefore));
            Assert.That(brineBefore - drink.Volume, Is.GreaterThanOrEqualTo(FixedPoint2.New(8)));
            Assert.That(SEntMan.EntityQuery<PuddleComponent>().Any(), Is.True);
        });
    }

    [Test]
    public async Task TakingPieceDoesNotSpillWhenSlipChanceIsZero()
    {
        await PrepareBarrel(Vinegar, Cucumber, 3);
        await StartFermentation();
        await FinishFermentation();
        await InteractUsing(FoodJar);

        var jar = await FindEntity(FoodJar);
        Target = SEntMan.GetNetEntity(jar);
        FixedPoint2 brineBefore = default;
        await Server.WaitPost(() =>
        {
            Server.System<OpenableSystem>().SetOpen(jar);
            SEntMan.GetComponent<PickleJarComponent>(jar).SlipChance = 0f;
            Assert.That(Solutions.TryGetSolution(jar, PickleJarComponent.SolutionName, out _, out var drink), Is.True);
            brineBefore = drink!.Volume;
        });

        await Drop();
        await Interact();
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<PickleJarComponent>(jar).RemainingPieces, Is.EqualTo(2));
            var held = HandSys.GetActiveItem((SPlayer, Hands));
            Assert.That(held, Is.Not.Null);
            Assert.That(SEntMan.HasComponent<PickledProduceComponent>(held!.Value), Is.True);
            Assert.That(Solutions.TryGetSolution(jar, PickleJarComponent.SolutionName, out _, out var drink), Is.True);
            Assert.That(drink!.Volume, Is.LessThan(brineBefore)); // piece takes a brine share
            var spilled = brineBefore - drink.Volume;
            Assert.That(spilled, Is.LessThanOrEqualTo(FixedPoint2.New(4)));
        });
    }

    [Test]
    public async Task TomatoJarGetsDistinctContentsStyle()
    {
        await PrepareBarrel(Vinegar, Tomato, 3);
        await StartFermentation();
        await FinishFermentation();
        await InteractUsing(FoodJar);

        var jar = await FindEntity(FoodJar);
        await Server.WaitAssertion(() =>
        {
            var jarComp = SEntMan.GetComponent<PickleJarComponent>(jar);
            Assert.That(jarComp.PiecePrototype, Is.EqualTo(Tomato));
            Assert.That(jarComp.ContentsStyle, Is.EqualTo("tomato"));

            var proto = Server.ProtoMan;
            var tomatoRecipe = proto.Index(TomatoVinegarRecipe);
            var cabbageRecipe = proto.Index(CabbageVinegarRecipe);
            Assert.That(SharedPickleJarSystem.ContentsStyleFor(tomatoRecipe), Is.EqualTo("tomato"));
            Assert.That(SharedPickleJarSystem.ContentsStyleFor(cabbageRecipe), Is.EqualTo("cabbage"));
            Assert.That(SharedPickleJarSystem.ContentsStyleFor(null, "FoodUnknown"), Is.EqualTo("cucumber"));
        });
    }

    [Test]
    public async Task WineJarCanBlindOnIngest()
    {
        await PrepareBarrel(Sugar, Grape, 3);
        await StartFermentation();
        await FinishFermentation();
        await InteractUsing(FoodJar);

        var jar = await FindEntity(FoodJar);
        await Server.WaitPost(() =>
        {
            Server.System<OpenableSystem>().SetOpen(jar);
            var jarComp = SEntMan.GetComponent<PickleJarComponent>(jar);
            Assert.That(jarComp.IsWine, Is.True);
            jarComp.WineBlindChance = 1f;

            var blindable = SEntMan.EnsureComponent<BlindableComponent>(SPlayer);
            Assert.That(blindable.IsBlind, Is.False);

            var ingested = new IngestedEvent(SPlayer, SPlayer, new Solution(), false);
            SEntMan.EventBus.RaiseLocalEvent(jar, ref ingested);

            Assert.That(SEntMan.GetComponent<BlindableComponent>(SPlayer).IsBlind, Is.True);
        });
    }

    [Test]
    public async Task BrineJarDoesNotBlindEvenAtFullChance()
    {
        await PrepareBarrel(Vinegar, Cucumber, 3);
        await StartFermentation();
        await FinishFermentation();
        await InteractUsing(FoodJar);

        var jar = await FindEntity(FoodJar);
        await Server.WaitPost(() =>
        {
            Server.System<OpenableSystem>().SetOpen(jar);
            var jarComp = SEntMan.GetComponent<PickleJarComponent>(jar);
            Assert.That(jarComp.IsWine, Is.False);
            jarComp.WineBlindChance = 1f;

            var blindable = SEntMan.EnsureComponent<BlindableComponent>(SPlayer);
            Assert.That(blindable.IsBlind, Is.False);

            var ingested = new IngestedEvent(SPlayer, SPlayer, new Solution(), false);
            SEntMan.EventBus.RaiseLocalEvent(jar, ref ingested);

            Assert.That(SEntMan.GetComponent<BlindableComponent>(SPlayer).IsBlind, Is.False);
        });
    }

    [Test]
    public async Task DrinkingWineDryClearsIsWineFlag()
    {
        await PrepareBarrel(Sugar, Grape, 3);
        await StartFermentation();
        await FinishFermentation();
        await InteractUsing(FoodJar);

        var jar = await FindEntity(FoodJar);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<PickleJarComponent>(jar).IsWine, Is.True);
            Assert.That(Solutions.TryGetSolution(jar, PickleJarComponent.SolutionName, out var soln, out var drink), Is.True);
            Solutions.SplitSolution(soln!.Value, drink!.Volume);
            Assert.That(SEntMan.GetComponent<PickleJarComponent>(jar).IsWine, Is.False);
        });
    }

    [Test]
    public async Task EmptyJarAfterInteractIsSilentlyHandled()
    {
        await SpawnTarget(WoodBarrel, PlayerCoords);
        await EnsureRoomTemperature();
        var jarNet = await Spawn(FoodJar.Id, PlayerCoords);
        var jar = ToServer(jarNet);
        await Server.WaitAssertion(() =>
        {
            Server.System<OpenableSystem>().SetOpen(jar);
            Assert.That(SEntMan.GetComponent<PickleJarComponent>(jar).RemainingPieces, Is.EqualTo(0));
            Assert.That(Solutions.TryGetSolution(jar, PickleJarComponent.SolutionName, out _, out var drink), Is.True);
            Assert.That(drink!.Volume, Is.EqualTo(FixedPoint2.Zero));

            var coords = SEntMan.GetComponent<TransformComponent>(STarget!.Value).Coordinates;
            var ev = new AfterInteractEvent(SPlayer, jar, STarget, coords, true);
            SEntMan.EventBus.RaiseLocalEvent(jar, ev);
            Assert.That(ev.Handled, Is.True);

            var ingestible = new IngestibleEvent();
            SEntMan.EventBus.RaiseLocalEvent(jar, ref ingestible);
            Assert.That(ingestible.Cancelled, Is.True);

            // Idle barrel + empty jar must still claim InteractUsing so packing/empty paths stay silent.
            var interact = new InteractUsingEvent(SPlayer, jar, STarget.Value, coords);
            SEntMan.EventBus.RaiseLocalEvent(STarget.Value, interact);
            Assert.That(interact.Handled, Is.True);
        });
    }

    [Test]
    public async Task ClosedJarCannotPackFromBarrel()
    {
        await PrepareBarrel(Vinegar, Cucumber, 3);
        await StartFermentation();
        await FinishFermentation();

        await PlaceInHands(FoodJar);
        await Server.WaitPost(() =>
        {
            var held = HandSys.GetActiveItem((SPlayer, Hands));
            Assert.That(held, Is.Not.Null);
            Server.System<OpenableSystem>().SetOpen(held!.Value, false);
        });
        await Interact();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Barrel.State, Is.EqualTo(FermentationState.Ready));
            var held = HandSys.GetActiveItem((SPlayer, Hands));
            Assert.That(held, Is.Not.Null);
            Assert.That(SEntMan.GetComponent<PickleJarComponent>(held!.Value).RemainingPieces, Is.EqualTo(0));
            Assert.That(Containers.TryGetContainer(STarget!.Value, FermentationBarrelComponent.ProduceContainerId, out var container), Is.True);
            Assert.That(container!.ContainedEntities, Has.Count.EqualTo(3));
        });
    }

    private async Task PrepareBarrel(ProtoId<ReagentPrototype> reagent, EntProtoId produce, int count)
    {
        await AddAtmosphere();
        await SpawnTarget(WoodBarrel, PlayerCoords);
        await EnsureRoomTemperature();
        await AddReagent(reagent, 30);
        // Vinegar recipes also require a little sugar.
        if (reagent == Vinegar)
            await AddReagent(Sugar, 15);
        for (var i = 0; i < count; i++)
            await InteractUsing(produce);
    }

    private async Task EnsureRoomTemperature()
    {
        await Server.WaitAssertion(() =>
        {
            var mix = Server.System<AtmosphereSystem>().GetTileMixture(STarget!.Value, excite: true);
            Assert.That(mix, Is.Not.Null, "Barrel tile needs an atmosphere so fermentation can start");
            mix!.Temperature = Atmospherics.T20C;
        });
    }

    private async Task AddReagent(ProtoId<ReagentPrototype> reagent, FixedPoint2 amount)
    {
        await Server.WaitAssertion(() =>
        {
            Assert.That(Solutions.TryGetSolution(STarget!.Value, FermentationBarrelComponent.SolutionName, out var sol, out _), Is.True);
            Assert.That(Solutions.TryAddReagent(sol!.Value, reagent, amount, out _), Is.True);
        });
    }

    private async Task StartFermentation(bool shouldSucceed = true)
    {
        await Server.WaitAssertion(() =>
        {
            var verbs = Server.System<SharedVerbSystem>();
            var start = verbs.GetLocalVerbs(STarget!.Value, SPlayer, typeof(AlternativeVerb), force: true)
                .SingleOrDefault(verb => verb.Text == Loc.GetString("pickle-barrel-verb-start"));
            if (!shouldSucceed)
            {
                if (start != null)
                    verbs.ExecuteVerb(start, SPlayer, STarget.Value, forced: true);
                Assert.That(Barrel.State, Is.EqualTo(FermentationState.Idle));
                return;
            }

            Assert.That(start, Is.Not.Null, "Start-fermentation verb missing (is pickles.enabled on and the server system loaded?)");
            verbs.ExecuteVerb(start!, SPlayer, STarget.Value, forced: true);
            Assert.That(Barrel.State, Is.EqualTo(FermentationState.Fermenting));
        });
        await RunTicks(1);
    }

    private async Task FinishFermentation()
    {
        await Server.WaitPost(() =>
        {
            Assert.That(Barrel.State, Is.EqualTo(FermentationState.Fermenting));
            Barrel.Elapsed = Barrel.TargetDuration;
        });
        await RunSeconds(1);
        Assert.That(Barrel.State, Is.EqualTo(FermentationState.Ready));
    }
}
