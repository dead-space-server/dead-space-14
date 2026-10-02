#nullable enable
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Prototypes;
using Content.Shared.Damage.Components;
using Content.Shared.DeadSpace.Medical.IvDrip;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Foldable;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Standing;
using Content.Shared.VendingMachines;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using System.Linq;
using System.Numerics;

namespace Content.IntegrationTests.Tests.DeadSpace.Medical;

[TestOf(typeof(IvDripComponent))]
public sealed class IvDripTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    private static readonly EntProtoId DripFolded = "IvDripFolded";
    private static readonly EntProtoId Drip = "IvDrip";
    private static readonly EntProtoId TargetProto = "MobHuman";
    private static readonly EntProtoId Bloodpack = "Bloodpack";
    private static readonly EntProtoId SyringeProto = "Syringe";
    private static readonly ProtoId<InjectorModePrototype> SyringeInjectMode = "SyringeInjectMode";
    private static readonly ProtoId<InjectorModePrototype> SyringeDrawMode = "SyringeDrawMode";
    private static readonly ProtoId<VendingMachineInventoryPrototype> NanoMedPlus = "NanoMedPlusInventory";
    private static readonly ProtoId<VendingMachineInventoryPrototype> NanoMed = "NanoMedInventory";
    private static readonly ProtoId<Content.Shared.Chemistry.Reagent.ReagentPrototype> Bicaridine = "Bicaridine";
    private static readonly ProtoId<Content.Shared.Chemistry.Reagent.ReagentPrototype> Blood = "Blood";

    [Test]
    public async Task UnfoldsOnceAndCannotRefold()
    {
        await SpawnTarget(DripFolded);
        Assert.That(Comp<FoldableComponent>().IsFolded, Is.True);

        await Server.WaitAssertion(() =>
        {
            var foldable = SEntMan.System<FoldableSystem>();
            Assert.That(foldable.TrySetFolded(STarget!.Value, Comp<FoldableComponent>(), false), Is.True);
            Assert.That(Comp<FoldableComponent>().IsFolded, Is.False);
            Assert.That(foldable.TrySetFolded(STarget.Value, Comp<FoldableComponent>(), true), Is.False);
        });
    }

    [Test]
    public async Task UnfoldedCannotBePickedUp()
    {
        await SpawnTarget(DripFolded);

        await Server.WaitAssertion(() =>
        {
            var foldable = SEntMan.System<FoldableSystem>();
            Assert.That(foldable.TrySetFolded(STarget!.Value, Comp<FoldableComponent>(), false), Is.True);

            var ev = new GettingPickedUpAttemptEvent(SPlayer, STarget.Value, showPopup: false);
            SEntMan.EventBus.RaiseLocalEvent(STarget.Value, ev);
            Assert.That(ev.Cancelled, Is.True);
        });
    }

    [Test]
    public async Task InjectsFromTankWhenAttached()
    {
        await AddAtmosphere();

        EntityUid drip = default;
        EntityUid patient = default;
        FixedPoint2 startVol = default;

        await Server.WaitPost(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            patient = SEntMan.SpawnEntity(TargetProto, coords);
            drip = SEntMan.SpawnEntity(Drip, coords.Offset(new Vector2(1.0f, 0f)));

            var foldable = SEntMan.System<FoldableSystem>();
            foldable.SetFolded(drip, SEntMan.GetComponent<FoldableComponent>(drip), false);

            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out var soln, out _), Is.True);
            Assert.That(solutions.TryAddReagent(soln!.Value, Blood, FixedPoint2.New(50)), Is.True);
            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out _, out var tank), Is.True);
            startVol = tank!.Volume;

            var dripComp = SEntMan.GetComponent<IvDripComponent>(drip);
            dripComp.AttachedPatient = patient;
            dripComp.Speed = IvDripSpeed.Fast;
            dripComp.NextTransfer = TimeSpan.Zero;
            SEntMan.Dirty(drip, dripComp);

            var connected = SEntMan.EnsureComponent<IvDripConnectedComponent>(patient);
            connected.Drip = drip;
            SEntMan.Dirty(patient, connected);
        });

        await RunSeconds(1.2f);

        await Server.WaitAssertion(() =>
        {
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out _, out var tank), Is.True);
            Assert.That(tank!.Volume, Is.LessThan(startVol));
        });
    }

    [Test]
    public async Task SyringeInjectModeRefillsTank()
    {
        await AddAtmosphere();

        await Server.WaitAssertion(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            var drip = SEntMan.SpawnEntity(Drip, coords.Offset(new Vector2(1.0f, 0f)));
            var syringe = SEntMan.SpawnEntity(SyringeProto, coords);

            SEntMan.System<FoldableSystem>()
                .SetFolded(drip, SEntMan.GetComponent<FoldableComponent>(drip), false);

            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(syringe, "injector", out var syringeSoln, out _), Is.True);
            Assert.That(solutions.TryAddReagent(syringeSoln!.Value, Bicaridine, FixedPoint2.New(10)), Is.True);

            var injector = SEntMan.GetComponent<InjectorComponent>(syringe);
            var proto = IoCManager.Resolve<IPrototypeManager>();
            SEntMan.System<InjectorSystem>()
                .ToggleMode((syringe, injector), SPlayer, proto.Index(SyringeInjectMode));

            var ev = new InteractUsingEvent(SPlayer, syringe, drip, coords);
            SEntMan.EventBus.RaiseLocalEvent(drip, ev);
            Assert.That(ev.Handled, Is.True);

            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out _, out var tank), Is.True);
            Assert.That(tank!.Volume, Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(tank.ContainsPrototype(Bicaridine), Is.True);
            Assert.That(solutions.TryGetSolution(syringe, "injector", out _, out var left), Is.True);
            Assert.That(left!.Volume, Is.EqualTo(FixedPoint2.New(5)));
        });
    }

    [Test]
    public async Task SyringeDrawModeDoesNotRefillViaInteractUsing()
    {
        await AddAtmosphere();

        await Server.WaitAssertion(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            var drip = SEntMan.SpawnEntity(Drip, coords.Offset(new Vector2(1.0f, 0f)));
            var syringe = SEntMan.SpawnEntity(SyringeProto, coords);

            SEntMan.System<FoldableSystem>()
                .SetFolded(drip, SEntMan.GetComponent<FoldableComponent>(drip), false);

            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out var tankSoln, out _), Is.True);
            Assert.That(solutions.TryAddReagent(tankSoln!.Value, Bicaridine, FixedPoint2.New(20)), Is.True);

            var injector = SEntMan.GetComponent<InjectorComponent>(syringe);
            var proto = IoCManager.Resolve<IPrototypeManager>();
            SEntMan.System<InjectorSystem>()
                .ToggleMode((syringe, injector), SPlayer, proto.Index(SyringeDrawMode));

            var ev = new InteractUsingEvent(SPlayer, syringe, drip, coords);
            SEntMan.EventBus.RaiseLocalEvent(drip, ev);
            Assert.That(ev.Handled, Is.False);

            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out _, out var tank), Is.True);
            Assert.That(tank!.Volume, Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(solutions.TryGetSolution(syringe, "injector", out _, out var left), Is.True);
            Assert.That(left!.Volume, Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task ClearTankEmptiesContentsWithoutSpillingBack()
    {
        await AddAtmosphere();

        await Server.WaitAssertion(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            var drip = SEntMan.SpawnEntity(Drip, coords.Offset(new Vector2(1.0f, 0f)));

            SEntMan.System<FoldableSystem>()
                .SetFolded(drip, SEntMan.GetComponent<FoldableComponent>(drip), false);

            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out var soln, out _), Is.True);
            Assert.That(solutions.TryAddReagent(soln!.Value, Bicaridine, FixedPoint2.New(40)), Is.True);

            SEntMan.System<SharedIvDripSystem>()
                .ClearTank((drip, SEntMan.GetComponent<IvDripComponent>(drip)), SPlayer);

            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out _, out var tank), Is.True);
            Assert.That(tank!.Volume, Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task SoftDetachWhileFlowingSpillsTankNotBlood()
    {
        await AddAtmosphere();

        await Server.WaitAssertion(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            var patient = SEntMan.SpawnEntity(TargetProto, coords);
            var drip = SEntMan.SpawnEntity(Drip, coords.Offset(new Vector2(1.0f, 0f)));

            SEntMan.System<FoldableSystem>()
                .SetFolded(drip, SEntMan.GetComponent<FoldableComponent>(drip), false);

            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out var soln, out _), Is.True);
            Assert.That(solutions.TryAddReagent(soln!.Value, Bicaridine, FixedPoint2.New(40)), Is.True);

            var dripComp = SEntMan.GetComponent<IvDripComponent>(drip);
            dripComp.AttachedPatient = patient;
            dripComp.Speed = IvDripSpeed.Fast;
            SEntMan.Dirty(drip, dripComp);
            var connected = SEntMan.EnsureComponent<IvDripConnectedComponent>(patient);
            connected.Drip = drip;
            SEntMan.Dirty(patient, connected);

            var bloodBefore = SEntMan.System<BloodstreamSystem>().GetBloodLevel(patient);

            Assert.That(SEntMan.System<SharedIvDripSystem>().TryToggleNeedle((drip, dripComp), SPlayer), Is.True);

            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out _, out var tank), Is.True);
            Assert.That(tank!.Volume, Is.LessThan(FixedPoint2.New(40)));
            Assert.That(tank.Volume, Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(tank.ContainsPrototype(Bicaridine), Is.True);
            Assert.That(tank.ContainsPrototype(Blood), Is.False);
            Assert.That(SEntMan.System<BloodstreamSystem>().GetBloodLevel(patient), Is.EqualTo(bloodBefore));
            Assert.That(SEntMan.GetComponent<IvDripComponent>(drip).AttachedPatient, Is.Null);
        });
    }

    [Test]
    public async Task SoftDetachWhileOffDoesNotSpillTank()
    {
        await AddAtmosphere();

        await Server.WaitAssertion(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            var patient = SEntMan.SpawnEntity(TargetProto, coords);
            var drip = SEntMan.SpawnEntity(Drip, coords.Offset(new Vector2(1.0f, 0f)));

            SEntMan.System<FoldableSystem>()
                .SetFolded(drip, SEntMan.GetComponent<FoldableComponent>(drip), false);

            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out var soln, out _), Is.True);
            Assert.That(solutions.TryAddReagent(soln!.Value, Bicaridine, FixedPoint2.New(40)), Is.True);

            var dripComp = SEntMan.GetComponent<IvDripComponent>(drip);
            dripComp.AttachedPatient = patient;
            dripComp.Speed = IvDripSpeed.Off;
            SEntMan.Dirty(drip, dripComp);
            var connected = SEntMan.EnsureComponent<IvDripConnectedComponent>(patient);
            connected.Drip = drip;
            SEntMan.Dirty(patient, connected);

            Assert.That(SEntMan.System<SharedIvDripSystem>().TryToggleNeedle((drip, dripComp), SPlayer), Is.True);

            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out _, out var tank), Is.True);
            Assert.That(tank!.Volume, Is.EqualTo(FixedPoint2.New(40)));
        });
    }

    [Test]
    public async Task YankOutOfRangeSpillsTankAndPatientBlood()
    {
        await AddAtmosphere();

        EntityUid drip = default;
        EntityUid patient = default;
        FixedPoint2 tankBefore = default;
        float bloodBefore = default;

        await Server.WaitPost(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            patient = SEntMan.SpawnEntity(TargetProto, coords);
            drip = SEntMan.SpawnEntity(Drip, coords.Offset(new Vector2(1.0f, 0f)));

            SEntMan.System<FoldableSystem>()
                .SetFolded(drip, SEntMan.GetComponent<FoldableComponent>(drip), false);

            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out var soln, out _), Is.True);
            Assert.That(solutions.TryAddReagent(soln!.Value, Bicaridine, FixedPoint2.New(40)), Is.True);
            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out _, out var tank), Is.True);
            tankBefore = tank!.Volume;
            bloodBefore = SEntMan.System<BloodstreamSystem>().GetBloodLevel(patient);

            var dripComp = SEntMan.GetComponent<IvDripComponent>(drip);
            dripComp.AttachedPatient = patient;
            dripComp.Speed = IvDripSpeed.Off;
            SEntMan.Dirty(drip, dripComp);
            var connected = SEntMan.EnsureComponent<IvDripConnectedComponent>(patient);
            connected.Drip = drip;
            SEntMan.Dirty(patient, connected);

            var xform = SEntMan.System<SharedTransformSystem>();
            xform.SetCoordinates(patient, coords.Offset(new Vector2(8f, 0f)));
        });

        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var dripComp = SEntMan.GetComponent<IvDripComponent>(drip);
            Assert.That(dripComp.AttachedPatient, Is.Null);

            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out _, out var tank), Is.True);
            Assert.That(tank!.Volume, Is.LessThan(tankBefore));
            Assert.That(tank.ContainsPrototype(Bicaridine), Is.True);
            Assert.That(SEntMan.System<BloodstreamSystem>().GetBloodLevel(patient), Is.LessThan(bloodBefore));
            Assert.That(SEntMan.GetComponent<DamageableComponent>(patient).TotalDamage, Is.GreaterThan(FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task NeedleSpawnsInHandNotOnMapAndDoesNotDuplicate()
    {
        await AddAtmosphere();

        EntityUid drip = default;
        EntityUid needle = default;

        await Server.WaitPost(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            drip = SEntMan.SpawnEntity(Drip, coords.Offset(new Vector2(1.0f, 0f)));

            SEntMan.System<FoldableSystem>()
                .SetFolded(drip, SEntMan.GetComponent<FoldableComponent>(drip), false);

            var system = SEntMan.System<SharedIvDripSystem>();
            var hands = SEntMan.System<SharedHandsSystem>();
            var dripComp = SEntMan.GetComponent<IvDripComponent>(drip);

            Assert.That(system.TryGiveNeedle((drip, dripComp), SPlayer), Is.True);
            needle = dripComp.ActiveNeedle!.Value;

            Assert.That(hands.IsHolding(SPlayer, needle), Is.True);
            Assert.That(SEntMan.GetComponent<TransformComponent>(needle).ParentUid, Is.EqualTo(SPlayer));
            Assert.That(SEntMan.EntityQuery<IvDripNeedleComponent>().Count(), Is.EqualTo(1));

            Assert.That(system.TryGiveNeedle((drip, dripComp), SPlayer), Is.True);
            Assert.That(dripComp.ActiveNeedle, Is.EqualTo(needle));
            Assert.That(SEntMan.EntityQuery<IvDripNeedleComponent>().Count(), Is.EqualTo(1));

            system.ClearNeedle((drip, dripComp));
            Assert.That(dripComp.ActiveNeedle, Is.Null);
        });

        await RunTicks(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(needle) || !SEntMan.EntityExists(needle), Is.True);
            Assert.That(SEntMan.EntityQuery<IvDripNeedleComponent>().Count(), Is.EqualTo(0));

            var system = SEntMan.System<SharedIvDripSystem>();
            var hands = SEntMan.System<SharedHandsSystem>();
            var dripComp = SEntMan.GetComponent<IvDripComponent>(drip);
            Assert.That(system.TryGiveNeedle((drip, dripComp), SPlayer), Is.True);
            Assert.That(SEntMan.EntityQuery<IvDripNeedleComponent>().Count(), Is.EqualTo(1));
            Assert.That(hands.IsHolding(SPlayer, dripComp.ActiveNeedle!.Value), Is.True);
        });
    }

    [Test]
    public async Task NeedleRetractsWhenHolderLeavesRange()
    {
        await AddAtmosphere();

        EntityUid drip = default;
        EntityUid needle = default;

        await Server.WaitPost(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            drip = SEntMan.SpawnEntity(Drip, coords);

            SEntMan.System<FoldableSystem>()
                .SetFolded(drip, SEntMan.GetComponent<FoldableComponent>(drip), false);

            var system = SEntMan.System<SharedIvDripSystem>();
            var dripComp = SEntMan.GetComponent<IvDripComponent>(drip);
            Assert.That(system.TryGiveNeedle((drip, dripComp), SPlayer), Is.True);
            needle = dripComp.ActiveNeedle!.Value;

            SEntMan.System<SharedTransformSystem>()
                .SetCoordinates(drip, coords.Offset(new Vector2(10f, 0f)));
        });

        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<IvDripComponent>(drip).ActiveNeedle, Is.Null);
            Assert.That(SEntMan.Deleted(needle) || !SEntMan.EntityExists(needle), Is.True);
            Assert.That(SEntMan.EntityQuery<IvDripNeedleComponent>().Count(), Is.EqualTo(0));
        });
    }

    [Test]
    public async Task IdleDripsWhenRunningDetached()
    {
        await AddAtmosphere();

        EntityUid drip = default;
        FixedPoint2 startVol = default;

        await Server.WaitPost(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            drip = SEntMan.SpawnEntity(Drip, coords.Offset(new Vector2(1.0f, 0f)));

            SEntMan.System<FoldableSystem>()
                .SetFolded(drip, SEntMan.GetComponent<FoldableComponent>(drip), false);

            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out var soln, out _), Is.True);
            Assert.That(solutions.TryAddReagent(soln!.Value, Bicaridine, FixedPoint2.New(20)), Is.True);
            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out _, out var tank), Is.True);
            startVol = tank!.Volume;

            var dripComp = SEntMan.GetComponent<IvDripComponent>(drip);
            dripComp.Speed = IvDripSpeed.Fast;
            dripComp.NextTransfer = TimeSpan.Zero;
            SEntMan.Dirty(drip, dripComp);
        });

        await RunSeconds(1.2f);

        await Server.WaitAssertion(() =>
        {
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out _, out var tank), Is.True);
            Assert.That(tank!.Volume, Is.LessThan(startVol));
        });
    }

    [Test]
    public async Task NanoMedPlusStocksFoldedDrip()
    {
        await using var pair = await PoolManager.GetServerClient();
        var proto = pair.Server.ResolveDependency<IPrototypeManager>();
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(proto.HasIndex(Drip), Is.True);
            Assert.That(proto.HasIndex(DripFolded), Is.True);
            Assert.That(proto.HasIndex(new EntProtoId("IvDripNeedle")), Is.True);
            Assert.That(proto.HasIndex(new EntProtoId("IvBloodBag")), Is.False);
            Assert.That(proto.TryIndex(NanoMedPlus, out var plus), Is.True);
            Assert.That(plus!.StartingInventory.ContainsKey(DripFolded.Id), Is.True);
            Assert.That(proto.TryIndex(NanoMed, out var nano), Is.True);
            Assert.That(nano!.StartingInventory.ContainsKey(DripFolded.Id), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NeedleAttachesPatientNearStand()
    {
        await AddAtmosphere();

        EntityUid drip = default;
        EntityUid patient = default;
        EntityUid needle = default;

        await Server.WaitPost(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            patient = SEntMan.SpawnEntity(TargetProto, coords);
            drip = SEntMan.SpawnEntity(Drip, coords.Offset(new Vector2(1.0f, 0f)));

            SEntMan.System<FoldableSystem>()
                .SetFolded(drip, SEntMan.GetComponent<FoldableComponent>(drip), false);

            var system = SEntMan.System<SharedIvDripSystem>();
            Assert.That(system.TryGiveNeedle((drip, SEntMan.GetComponent<IvDripComponent>(drip)), SPlayer), Is.True);

            var dripComp = SEntMan.GetComponent<IvDripComponent>(drip);
            Assert.That(dripComp.ActiveNeedle, Is.Not.Null);
            needle = dripComp.ActiveNeedle!.Value;
            Assert.That(SEntMan.HasComponent<IvDripNeedleComponent>(needle), Is.True);

            dripComp.AttachedPatient = patient;
            dripComp.Speed = IvDripSpeed.Off;
            SEntMan.Dirty(drip, dripComp);
            var connected = SEntMan.EnsureComponent<IvDripConnectedComponent>(patient);
            connected.Drip = drip;
            SEntMan.Dirty(patient, connected);
            system.ClearNeedle((drip, dripComp));
        });

        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(needle) || !SEntMan.EntityExists(needle), Is.True);
            Assert.That(SEntMan.GetComponent<IvDripComponent>(drip).AttachedPatient, Is.EqualTo(patient));
            Assert.That(SEntMan.GetComponent<IvDripComponent>(drip).ActiveNeedle, Is.Null);
        });
    }

    [Test]
    public async Task FoldedCanBePickedUpAndDoesNotGiveANeedle()
    {
        await SpawnTarget(DripFolded);

        await Server.WaitAssertion(() =>
        {
            var ev = new GettingPickedUpAttemptEvent(SPlayer, STarget!.Value, showPopup: false);
            SEntMan.EventBus.RaiseLocalEvent(STarget.Value, ev);
            Assert.That(ev.Cancelled, Is.False);

            Assert.That(SEntMan.System<SharedIvDripSystem>().TryGiveNeedle((STarget.Value, Comp<IvDripComponent>()), SPlayer), Is.False);
            Assert.That(SEntMan.EntityQuery<IvDripNeedleComponent>().Count(), Is.EqualTo(0));
        });
    }

    [Test]
    public async Task PrototypeSuppliesTheRates()
    {
        await SpawnTarget(Drip);

        await Server.WaitAssertion(() =>
        {
            var comp = Comp<IvDripComponent>();
            Assert.That(comp.TankSolution, Is.EqualTo("tank"));
            Assert.That(comp.NeedlePrototype.Id, Is.EqualTo("IvDripNeedle"));
            Assert.That(comp.SlowTransfer, Is.EqualTo(0.5f));
            Assert.That(comp.MediumTransfer, Is.EqualTo(1.5f));
            Assert.That(comp.FastTransfer, Is.EqualTo(3f));
            Assert.That(comp.IdleDripTransfer, Is.EqualTo(0.5f));
            Assert.That(comp.AttachDelay, Is.EqualTo(1.5f));
            Assert.That(comp.YankBlood, Is.EqualTo(5f));
            Assert.That(comp.MinYankSpill, Is.EqualTo(1f));
            Assert.That(comp.BloodpackFillAmount, Is.EqualTo(20f));
            Assert.That(comp.BloodReagent.Id, Is.EqualTo("Blood"));
            Assert.That(comp.BloodpackTag.Id, Is.EqualTo("Bloodpack"));
            Assert.That(comp.YankDamage.DamageDict["Piercing"], Is.EqualTo(FixedPoint2.New(6)));
            Assert.That(comp.YankDamage.DamageDict["Slash"], Is.EqualTo(FixedPoint2.New(4)));
            Assert.That(comp.SliderStates[IvDripSpeed.Fast], Is.EqualTo("slider_fast"));
        });
    }

    [Test]
    public async Task OffSpeedMovesNoFluid()
    {
        await AddAtmosphere();

        EntityUid drip = default;
        FixedPoint2 startVol = default;

        await Server.WaitPost(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            var patient = SEntMan.SpawnEntity(TargetProto, coords);
            drip = SEntMan.SpawnEntity(Drip, coords.Offset(new Vector2(1.0f, 0f)));
            SEntMan.System<FoldableSystem>().SetFolded(drip, SEntMan.GetComponent<FoldableComponent>(drip), false);

            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out var soln, out _), Is.True);
            Assert.That(solutions.TryAddReagent(soln!.Value, Bicaridine, FixedPoint2.New(20)), Is.True);
            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out _, out var filled), Is.True);
            startVol = filled!.Volume;

            var dripComp = SEntMan.GetComponent<IvDripComponent>(drip);
            dripComp.Speed = IvDripSpeed.Off;
            dripComp.NextTransfer = TimeSpan.Zero;
            dripComp.AttachedPatient = patient;
            SEntMan.Dirty(drip, dripComp);
            var connected = SEntMan.EnsureComponent<IvDripConnectedComponent>(patient);
            connected.Drip = drip;
        });

        await RunSeconds(1.2f);

        await Server.WaitAssertion(() =>
        {
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out _, out var tank), Is.True);
            Assert.That(tank!.Volume, Is.EqualTo(startVol));
        });
    }

    [Test]
    public async Task BloodpackFillsTankWithBlood()
    {
        await AddAtmosphere();

        await Server.WaitAssertion(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            var drip = SEntMan.SpawnEntity(Drip, coords.Offset(new Vector2(1.0f, 0f)));
            var pack = SEntMan.SpawnEntity(Bloodpack, coords);
            SEntMan.System<FoldableSystem>().SetFolded(drip, SEntMan.GetComponent<FoldableComponent>(drip), false);

            var ev = new InteractUsingEvent(SPlayer, pack, drip, coords);
            SEntMan.EventBus.RaiseLocalEvent(drip, ev);
            Assert.That(ev.Handled, Is.True);

            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out _, out var tank), Is.True);
            Assert.That(tank!.Volume, Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(tank.ContainsPrototype(Blood), Is.True);
        });
    }

    [Test]
    public async Task NeedleClickAttachesThePatient()
    {
        await AddAtmosphere();

        EntityUid drip = default;
        EntityUid patient = default;

        await Server.WaitPost(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            patient = SEntMan.SpawnEntity(TargetProto, coords);
            drip = SEntMan.SpawnEntity(Drip, coords.Offset(new Vector2(1.0f, 0f)));
            SEntMan.System<FoldableSystem>().SetFolded(drip, SEntMan.GetComponent<FoldableComponent>(drip), false);

            var system = SEntMan.System<SharedIvDripSystem>();
            Assert.That(system.TryGiveNeedle((drip, SEntMan.GetComponent<IvDripComponent>(drip)), SPlayer), Is.True);
            var needle = SEntMan.GetComponent<IvDripComponent>(drip).ActiveNeedle!.Value;

            var ev = new AfterInteractEvent(SPlayer, needle, patient, coords, canReach: true);
            SEntMan.EventBus.RaiseLocalEvent(needle, ev);
            Assert.That(ev.Handled, Is.True);
        });

        await AwaitDoAfters();

        await Server.WaitAssertion(() =>
        {
            var dripComp = SEntMan.GetComponent<IvDripComponent>(drip);
            Assert.That(dripComp.AttachedPatient, Is.EqualTo(patient));
            Assert.That(dripComp.ActiveNeedle, Is.Null);
            Assert.That(SEntMan.GetComponent<IvDripConnectedComponent>(patient).Drip, Is.EqualTo(drip));
            Assert.That(SEntMan.EntityQuery<IvDripNeedleComponent>().Count(), Is.EqualTo(0));
        });
    }

    [Test]
    public async Task NeedleClickTooFarDoesNotAttach()
    {
        await AddAtmosphere();

        await Server.WaitAssertion(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            var patient = SEntMan.SpawnEntity(TargetProto, coords.Offset(new Vector2(5f, 0f)));
            var drip = SEntMan.SpawnEntity(Drip, coords.Offset(new Vector2(1.0f, 0f)));
            SEntMan.System<FoldableSystem>().SetFolded(drip, SEntMan.GetComponent<FoldableComponent>(drip), false);

            var system = SEntMan.System<SharedIvDripSystem>();
            Assert.That(system.TryGiveNeedle((drip, SEntMan.GetComponent<IvDripComponent>(drip)), SPlayer), Is.True);
            var needle = SEntMan.GetComponent<IvDripComponent>(drip).ActiveNeedle!.Value;

            var ev = new AfterInteractEvent(SPlayer, needle, patient, coords, canReach: true);
            SEntMan.EventBus.RaiseLocalEvent(needle, ev);

            Assert.That(SEntMan.GetComponent<IvDripComponent>(drip).AttachedPatient, Is.Null);
            Assert.That(SEntMan.HasComponent<IvDripConnectedComponent>(patient), Is.False);
            Assert.That(ActiveDoAfters.Count(), Is.EqualTo(0));
        });
    }

    [Test]
    public async Task DroppedNeedleVanishes()
    {
        await AddAtmosphere();

        EntityUid needle = default;

        await Server.WaitPost(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            var drip = SEntMan.SpawnEntity(Drip, coords.Offset(new Vector2(1.0f, 0f)));
            SEntMan.System<FoldableSystem>().SetFolded(drip, SEntMan.GetComponent<FoldableComponent>(drip), false);

            var system = SEntMan.System<SharedIvDripSystem>();
            Assert.That(system.TryGiveNeedle((drip, SEntMan.GetComponent<IvDripComponent>(drip)), SPlayer), Is.True);
            needle = SEntMan.GetComponent<IvDripComponent>(drip).ActiveNeedle!.Value;
            SEntMan.EventBus.RaiseLocalEvent(needle, new DroppedEvent(SPlayer));
        });

        await RunTicks(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(needle) || !SEntMan.EntityExists(needle), Is.True);
        });
    }

    [Test]
    public async Task StandingAfterBeingDownYanksTheLine()
    {
        await AddAtmosphere();

        EntityUid drip = default;
        EntityUid patient = default;
        float bloodBefore = default;

        await Server.WaitPost(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            patient = SEntMan.SpawnEntity(TargetProto, coords);
            drip = SEntMan.SpawnEntity(Drip, coords.Offset(new Vector2(1.0f, 0f)));
            SEntMan.System<FoldableSystem>().SetFolded(drip, SEntMan.GetComponent<FoldableComponent>(drip), false);

            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out var soln, out _), Is.True);
            Assert.That(solutions.TryAddReagent(soln!.Value, Bicaridine, FixedPoint2.New(40)), Is.True);
            bloodBefore = SEntMan.System<BloodstreamSystem>().GetBloodLevel(patient);

            var dripComp = SEntMan.GetComponent<IvDripComponent>(drip);
            dripComp.AttachedPatient = patient;
            dripComp.PatientWasDowned = true;
            dripComp.Speed = IvDripSpeed.Off;
            SEntMan.Dirty(drip, dripComp);
            var connected = SEntMan.EnsureComponent<IvDripConnectedComponent>(patient);
            connected.Drip = drip;
            SEntMan.EventBus.RaiseLocalEvent(patient, new StoodEvent());
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<IvDripComponent>(drip).AttachedPatient, Is.Null);
            Assert.That(SEntMan.HasComponent<IvDripConnectedComponent>(patient), Is.False);
            Assert.That(SEntMan.System<BloodstreamSystem>().GetBloodLevel(patient), Is.LessThan(bloodBefore));
        });
    }

    [Test]
    public async Task ClearTankDoesNotLeaveAPuddle()
    {
        await AddAtmosphere();

        await Server.WaitAssertion(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            var drip = SEntMan.SpawnEntity(Drip, coords.Offset(new Vector2(1.0f, 0f)));
            SEntMan.System<FoldableSystem>().SetFolded(drip, SEntMan.GetComponent<FoldableComponent>(drip), false);

            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(drip, SEntMan.GetComponent<IvDripComponent>(drip).TankSolution, out var soln, out _), Is.True);
            Assert.That(solutions.TryAddReagent(soln!.Value, Bicaridine, FixedPoint2.New(40)), Is.True);
            var puddles = SEntMan.EntityQuery<PuddleComponent>().Count();

            SEntMan.System<SharedIvDripSystem>().ClearTank((drip, SEntMan.GetComponent<IvDripComponent>(drip)), SPlayer);

            Assert.That(SEntMan.EntityQuery<PuddleComponent>().Count(), Is.EqualTo(puddles));
        });
    }
}
