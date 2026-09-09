using System.Linq;
using Content.Client.DeadSpace.AshWalkers;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Ghost;
using Content.Shared.Actions;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.AshWalkers;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using RitualSystem = Content.Server.DeadSpace.AshWalkers.AshWalkerRitualSystem;

namespace Content.IntegrationTests.Tests.DeadSpace.Lavaland;

[TestFixture]
public sealed class AshWalkerRitualTest : InteractionTest
{
    protected override string PlayerPrototype => "MobAshWalker";

    private async Task Prepare()
    {
        await AddGravity();
        await Server.WaitPost(() =>
        {
            for (var x = -3; x <= 5; x++)
            for (var y = -3; y <= 3; y++)
                MapSystem.SetTile(MapData.Grid, MapData.Grid.Comp, new Vector2i(x, y), new Tile(TileMan["Plating"].TileId));
            var tribe = SEntMan.GetComponent<AshWalkerTribeMemberComponent>(SPlayer);
            tribe.HomeMap = MapData.MapUid;
            SEntMan.Dirty(SPlayer, tribe);
        });
        await AddAtmosphere();
        await RunTicks(10);
    }

    private EntityUid SpawnCarcass(string prototype, float x)
    {
        var corpse = SEntMan.SpawnEntity(prototype, new EntityCoordinates(MapData.Grid, x, 0.5f));
        var lethalDamage = Server.System<MobThresholdSystem>().GetThresholdForState(corpse, MobState.Dead) + 10;
        Server.System<DamageableSystem>().SetDamage(corpse, new DamageSpecifier { DamageDict = { ["Slash"] = lethalDamage } });
        Server.System<MobStateSystem>().ChangeMobState(corpse, MobState.Dead);
        return corpse;
    }

    [Test]
    public async Task DrawingAndOfferingsGrantHuntOnceAndMendingHasALimit()
    {
        await Prepare();
        await Client.WaitPost(() => Client.ResolveDependency<IConfigurationManager>().SetCVar(CVars.NetPredictLagBias, 0.2f));
        var dagger = await PlaceInHands("DaggerBone");
        await RunTicks(10);
        await Server.WaitAssertion(() => Assert.That(Server.System<RitualSystem>().CanDraw(ToServer(dagger), SPlayer,
            SEntMan.GetCoordinates(TargetCoords), out _), Is.True));
        await Client.WaitPost(() =>
        {
            var item = CEntMan.GetComponent<AshWalkerRitualDaggerComponent>(ToClient(dagger));
            Assert.That(item.HuntAction, Is.Not.Null);
            CEntMan.RaisePredictiveEvent(new RequestPerformActionEvent(CEntMan.GetNetEntity(item.HuntAction!.Value), TargetCoords));
        });
        await RunSeconds(5);
        EntityUid rune = default;
        EntityUid offering = default;
        await Server.WaitAssertion(() =>
        {
            var runes = SEntMan.AllEntityQueryEnumerator<AshWalkerRuneComponent>();
            Assert.That(runes.MoveNext(out rune, out _), Is.True);
            Assert.That(runes.MoveNext(out _, out _), Is.False);
            Assert.That(SEntMan.GetComponent<AshWalkerRuneComponent>(rune).HomeMap, Is.EqualTo(MapData.MapUid));
            Assert.That(Server.System<RitualSystem>().CanDraw(ToServer(dagger), SPlayer, Transform.GetMoverCoordinates(rune), out _), Is.False);
            offering = SpawnCarcass("MobLavalandGoliath", 1.8f);
        });
        await Client.WaitAssertion(() => Assert.That(CEntMan.EntityQuery<AshWalkerRuneComponent>().Count(), Is.EqualTo(1)));
        await RunTicks(5);
        await Server.WaitAssertion(() =>
        {
            var rituals = Server.System<RitualSystem>();
            var interrupted = SEntMan.SpawnEntity("MobAshWalker", Transform.GetMoverCoordinates(SPlayer));
            SEntMan.GetComponent<AshWalkerTribeMemberComponent>(interrupted).HomeMap = MapData.MapUid;
            Assert.That(rituals.TryBeginRitual((rune, SEntMan.GetComponent<AshWalkerRuneComponent>(rune)), interrupted, offering), Is.True);
            SEntMan.DeleteEntity(interrupted);
            Assert.That(SEntMan.IsQueuedForDeletion(offering), Is.False);
            Assert.That(rituals.TryBeginRitual((rune, SEntMan.GetComponent<AshWalkerRuneComponent>(rune)), SPlayer, offering), Is.True);
            Assert.That(rituals.TryBeginRitual((rune, SEntMan.GetComponent<AshWalkerRuneComponent>(rune)), SPlayer, offering), Is.False);
        });
        await RunSeconds(9);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(offering), Is.True);
            Assert.That(Server.System<StatusEffectsSystem>().HasStatusEffect(SPlayer, "AshWalkerHuntEffect"), Is.True);
            var watcher = SEntMan.SpawnEntity("MobWatcherLavaland", new EntityCoordinates(MapData.Grid, 4.5f, 0.5f));
            var hit = new DamageSpecifier { DamageDict = { ["Blunt"] = 10 } };
            Server.System<DamageableSystem>().SetDamageModifierSetId(watcher, null);
            Server.System<DamageableSystem>().TryChangeDamage(watcher, hit, out var dealt, origin: SPlayer, ignoreGlobalModifiers: true);
            Assert.That((float) dealt.GetTotal(), Is.EqualTo(28.75f).Within(0.02f));
            SEntMan.DeleteEntity(watcher);
            SEntMan.DeleteEntity(rune);
            rune = SEntMan.SpawnEntity("AshWalkerMendingRune", new EntityCoordinates(MapData.Grid, 1.5f, 0.5f));
            var comp = SEntMan.GetComponent<AshWalkerRuneComponent>(rune);
            comp.HomeMap = MapData.MapUid;
            SEntMan.Dirty(rune, comp);
            offering = SpawnCarcass("MobWatcherLavaland", 1.8f);
            Server.System<DamageableSystem>().SetDamage(SPlayer, new DamageSpecifier { DamageDict = { ["Blunt"] = 120 } });
        });
        await RunTicks(5);
        await Server.WaitAssertion(() => Assert.That(Server.System<RitualSystem>().TryBeginRitual(
            (rune, SEntMan.GetComponent<AshWalkerRuneComponent>(rune)), SPlayer, offering), Is.True));
        await RunSeconds(9);
        await Server.WaitAssertion(() => Assert.That(SEntMan.Deleted(offering), Is.True));
        await RunSeconds(181);
        await Server.WaitAssertion(() =>
        {
            Assert.That((float) SEntMan.GetComponent<DamageableComponent>(SPlayer).Damage.DamageDict["Blunt"], Is.EqualTo(40).Within(0.05f));
            Assert.That(Server.System<StatusEffectsSystem>().HasStatusEffect(SPlayer, "AshWalkerMendingEffect"), Is.False);
        });
    }

    [Test]
    public async Task ReturnRequiresConsentAndConsumesOfferingsOnlyOnce()
    {
        await Prepare();
        EntityUid rune = default;
        EntityUid invoker = default;
        EntityUid mindUid = default;
        EntityUid first = default;
        EntityUid second = default;
        EntityUid flask = default;
        await Server.WaitPost(() =>
        {
            var minds = Server.System<SharedMindSystem>();
            var mind = minds.CreateMind(ServerSession.UserId, "Fallen hunter");
            mindUid = mind.Owner;
            minds.TransferTo(mindUid, SPlayer);
            invoker = SEntMan.SpawnEntity("MobAshWalker", new EntityCoordinates(MapData.Grid, 0.5f, 1.5f));
            SEntMan.GetComponent<AshWalkerTribeMemberComponent>(invoker).HomeMap = MapData.MapUid;
            rune = SEntMan.SpawnEntity("AshWalkerReturnRune", new EntityCoordinates(MapData.Grid, 1.5f, 0.5f));
            var comp = SEntMan.GetComponent<AshWalkerRuneComponent>(rune);
            comp.HomeMap = MapData.MapUid;
            SEntMan.Dirty(rune, comp);
            first = SpawnCarcass("MobLavalandGoliath", 2.1f);
            second = SEntMan.SpawnEntity("AshWalkerSoulFish", new EntityCoordinates(MapData.Grid, 1.5f, -0.2f));
            flask = SEntMan.SpawnEntity("Beaker", new EntityCoordinates(MapData.Grid, 1.4f, 0.8f));
            var solutions = Server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(flask, "beaker", out var solution), Is.True);
            solutions.TryAddReagent(solution.Value, "AshWalkerBloodTonic", 10);
            Server.System<DamageableSystem>().SetDamage(SPlayer, new DamageSpecifier { DamageDict = { ["Blunt"] = 300 } });
            Server.System<MobStateSystem>().ChangeMobState(SPlayer, MobState.Dead);
            Assert.That(mind.Comp.OwnedEntity, Is.EqualTo(SPlayer), "Original body before ghosting");
            Assert.That(minds.IsCharacterDeadPhysically(mind.Comp), Is.True, "Corpse permits returnable ghosting");
            Assert.That(Server.System<GhostSystem>().OnGhostAttempt(mindUid, true, forced: true), Is.True);
            Assert.That(mind.Comp.OwnedEntity, Is.EqualTo(SPlayer), "Ghost must retain original body");
        });
        await RunTicks(10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<TransformComponent>(rune).Anchored, Is.True, "Rune must be anchored");
            Assert.That(Server.System<MobStateSystem>().IsDead(first), Is.True, "Offering must remain dead");
            Assert.That(Server.System<SharedMindSystem>().TryGetMind(SPlayer, out var originalMind, out _), Is.True);
            Assert.That(originalMind, Is.EqualTo(mindUid));
            Assert.That(InteractSys.InRangeUnobstructed(invoker, rune), Is.True, "Invoker must reach rune");
            var solutions = Server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(second, "food", out var fishFood), Is.True);
            var portion = solutions.SplitSolution(fishFood.Value, 5);
            Assert.That(Server.System<RitualSystem>().TryBeginRitual(
                (rune, SEntMan.GetComponent<AshWalkerRuneComponent>(rune)), invoker, SPlayer), Is.False);
            Assert.That(solutions.TryAddSolution(fishFood.Value, portion), Is.True);
            Assert.That(Server.System<RitualSystem>().TryBeginRitual(
                (rune, SEntMan.GetComponent<AshWalkerRuneComponent>(rune)), invoker, SPlayer), Is.True);
        });
        await RunSeconds(21);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<MobStateSystem>().IsDead(SPlayer), Is.True);
            Assert.That(SEntMan.Deleted(first) || SEntMan.Deleted(second), Is.False);
        });
        await ClickControl<AshWalkerReturnWindow>(nameof(AshWalkerReturnWindow.NoButton));
        await RunTicks(10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(first) || SEntMan.Deleted(second), Is.False);
            Assert.That(Server.System<RitualSystem>().TryBeginRitual(
                (rune, SEntMan.GetComponent<AshWalkerRuneComponent>(rune)), invoker, SPlayer), Is.True);
        });
        await RunSeconds(21);
        await ClickControl<AshWalkerReturnWindow>(nameof(AshWalkerReturnWindow.YesButton));
        await RunTicks(10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(ServerSession.AttachedEntity, Is.EqualTo(SPlayer));
            Assert.That(Server.System<MobStateSystem>().IsAlive(SPlayer), Is.True);
            Assert.That(SEntMan.HasComponent<Content.Server.DeadSpace.AshWalkers.AshWalkerReturnedComponent>(mindUid), Is.True);
            Assert.That(SEntMan.Deleted(first) && SEntMan.Deleted(second), Is.True);
            Assert.That(Server.System<StatusEffectsSystem>().HasStatusEffect(SPlayer, "AshWalkerReturnWeaknessEffect"), Is.True);
            var solutions = Server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(flask, "beaker", out var solution), Is.True);
            Assert.That(solution.Value.Comp.Solution.GetTotalPrototypeQuantity("AshWalkerBloodTonic"), Is.EqualTo(Content.Shared.FixedPoint.FixedPoint2.Zero));
            Server.System<DamageableSystem>().SetDamage(SPlayer, new DamageSpecifier { DamageDict = { ["Blunt"] = 300 } });
            Server.System<MobStateSystem>().ChangeMobState(SPlayer, MobState.Dead);
            SpawnCarcass("MobLavalandGoliath", 2.1f);
            SpawnCarcass("MobWatcherLavaland", 1.8f);
            solutions.TryAddReagent(solution.Value, "AshWalkerBloodTonic", 10);
        });
        await RunTicks(5);
        await Server.WaitAssertion(() => Assert.That(Server.System<RitualSystem>().TryBeginRitual(
            (rune, SEntMan.GetComponent<AshWalkerRuneComponent>(rune)), invoker, SPlayer), Is.False));
    }
}
