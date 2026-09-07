using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Chasm;
using Content.Shared.Input;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Vehicle.Components;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.DeadSpace.Lavaland;

[TestFixture]
public sealed class LavaBoatTest : InteractionTest
{
    protected override string PlayerPrototype => "MobAshWalker";

    private async Task PrepareBoat()
    {
        await AddGravity();
        await Server.WaitPost(() =>
        {
            var tile = new Tile(TileMan["Plating"].TileId);
            for (var x = -2; x <= 8; x++)
            for (var y = -2; y <= 2; y++)
            {
                MapSystem.SetTile(MapData.Grid, MapData.Grid.Comp, new Vector2i(x, y), tile);
                if (x is >= 1 and <= 6)
                    SEntMan.SpawnEntity("FloorLavaEntity", new EntityCoordinates(MapData.Grid, x + 0.5f, y + 0.5f));
            }

            var shore = new EntityCoordinates(MapData.Grid, 0.5f, 0.5f);
            Transform.SetCoordinates(SPlayer, shore);
            TargetCoords = SEntMan.GetNetCoordinates(shore);
        });
        await AddAtmosphere();
        await SpawnTarget("LavaBoat");
        await DeleteHeldEntity();
        await PressKey(EngineKeyFunctions.Use);
        await RunTicks(5);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<VehicleComponent>(STarget!.Value).Operator, Is.EqualTo(SPlayer)));
    }

    [Test]
    public async Task RowingIsPredictedRequiresHeldOarAndProtectsOnlyWhileSeated()
    {
        await Client.WaitPost(() => Client.ResolveDependency<IConfigurationManager>().SetCVar(CVars.NetPredictLagBias, 0.2f));
        await PrepareBoat();
        await PressKey(EngineKeyFunctions.MoveRight, 15);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<TransformComponent>(STarget!.Value).LocalPosition.X, Is.EqualTo(0.5f).Within(0.1f)));
        await PlaceInHands("BoneOar");
        await RunTicks(5);
        await SetKey(EngineKeyFunctions.MoveRight, BoundKeyState.Down);
        await Client.WaitRunTicks(2);
        await Client.WaitAssertion(() => Assert.That(CEntMan.GetComponent<InputMoverComponent>(CTarget!.Value).CanMove, Is.True));
        await RunTicks(25);
        await SetKey(EngineKeyFunctions.MoveRight, BoundKeyState.Up);
        await RunTicks(15);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<TransformComponent>(STarget!.Value).LocalPosition.X, Is.InRange(1.1f, 6.9f));
            Assert.That(SEntMan.GetComponent<FlammableComponent>(SPlayer).OnFire, Is.False);
            Assert.That(SEntMan.GetComponent<FlammableComponent>(SPlayer).FireStacks, Is.Zero);
            Assert.That(HandSys.TryDrop(SPlayer), Is.True);
            Assert.That(SEntMan.GetComponent<InputMoverComponent>(STarget.Value).CanMove, Is.False);
            Transform.SetCoordinates(STarget.Value, new EntityCoordinates(MapData.Grid, 3.5f, 0.5f));
            Assert.That(Server.System<SharedBuckleSystem>().TryUnbuckle(SPlayer, SPlayer), Is.False);
            Transform.SetCoordinates(STarget.Value, new EntityCoordinates(MapData.Grid, 1.1f, 0.5f));
            Assert.That(Server.System<SharedBuckleSystem>().TryUnbuckle(SPlayer, SPlayer), Is.True);
            Assert.That(SEntMan.GetComponent<TransformComponent>(SPlayer).LocalPosition.X, Is.LessThan(1));
        });
        await RunTicks(5);
        await Server.WaitAssertion(() =>
        {
            var lava = SEntMan.SpawnEntity("FloorLavaEntity", new EntityCoordinates(MapData.Grid, 7.5f, 0.5f));
            var step = new StepTriggerAttemptEvent { Source = lava, Tripper = SPlayer };
            SEntMan.EventBus.RaiseLocalEvent(lava, ref step);
            Assert.That(step.Cancelled, Is.False);
            Assert.That(step.Continue, Is.True);
        });
        await Server.WaitPost(() => Transform.SetCoordinates(STarget!.Value, new EntityCoordinates(MapData.Grid, 2.25f, 0.5f)));
        await PressKey(ContentKeyFunctions.TryPullObject);
        await RunTicks(10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<PullableComponent>(STarget!.Value).Puller, Is.EqualTo(SPlayer));
            Server.System<PullingSystem>().TryStopPull(STarget.Value, SEntMan.GetComponent<PullableComponent>(STarget.Value), SPlayer);
            Transform.SetCoordinates(STarget.Value, new EntityCoordinates(MapData.Grid, 1.1f, 0.5f));
        });
        await PlaceInHands("BoneOar");
        await RunTicks(5);
        await Server.WaitPost(() => Server.System<FlammableSystem>().SetFireStacks(SPlayer, 3, ignite: true));
        await PressKey(EngineKeyFunctions.Use);
        await RunTicks(10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<BuckleComponent>(SPlayer).BuckledTo, Is.EqualTo(STarget));
            Assert.That(SEntMan.GetComponent<FlammableComponent>(SPlayer).OnFire, Is.False);
            var chasm = new EntityCoordinates(MapData.Grid, 8.5f, 0.5f);
            SEntMan.SpawnEntity("FloorChasmEntity", chasm);
            Transform.SetCoordinates(STarget!.Value, chasm);
            Server.System<SharedPhysicsSystem>().WakeBody(STarget.Value);
        });
        await RunTicks(10);
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<ChasmFallingComponent>(STarget!.Value), Is.True));
    }

    [TestCase(3.5f, false)]
    [TestCase(1.1f, true)]
    public async Task CargoRejectsLivingPassengersAndSurvivesHullDestruction(float boatX, bool nearShore)
    {
        await PrepareBoat();
        EntityUid body = default;
        EntityUid cargo = default;
        await Server.WaitAssertion(() =>
        {
            var boat = STarget!.Value;
            var storage = Server.System<SharedEntityStorageSystem>();
            var coordinates = SEntMan.GetComponent<TransformComponent>(boat).Coordinates;
            body = SEntMan.SpawnEntity("MobReptilian", coordinates);
            cargo = SEntMan.SpawnEntity("MaterialBones1", coordinates);
            Assert.That(storage.CanInsert(body, boat), Is.False);
            Server.System<MobStateSystem>().ChangeMobState(body, MobState.Dead);
            Assert.That(storage.CanInsert(body, boat), Is.True);
            Assert.That(storage.Insert(body, boat), Is.True);
            Assert.That(storage.Insert(cargo, boat), Is.True);
            Assert.That(storage.TryOpenStorage(SPlayer, boat), Is.True);
            Assert.That(SEntMan.GetComponent<EntityStorageComponent>(boat).Contents.Count, Is.Zero);
        });
        await RunTicks(5);
        await Server.WaitAssertion(() =>
        {
            var boat = STarget!.Value;
            var storage = Server.System<SharedEntityStorageSystem>();
            Assert.That(storage.TryCloseStorage(boat, SPlayer), Is.True);
            Assert.That(SEntMan.GetComponent<EntityStorageComponent>(boat).Contents.Count, Is.EqualTo(2));
            Transform.SetCoordinates(boat, new EntityCoordinates(MapData.Grid, boatX, 0.5f));
            Assert.That(storage.TryOpenStorage(SPlayer, boat, silent: true), Is.False);
            Assert.That(SEntMan.GetComponent<EntityStorageComponent>(boat).Contents.Count, Is.EqualTo(2));
            Server.System<DamageableSystem>().TryChangeDamage(boat,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 1000 } }, true);
        });
        await RunTicks(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(STarget!.Value), Is.True);
            Assert.That(SEntMan.Deleted(body), Is.False);
            Assert.That(SEntMan.Deleted(cargo), Is.False);
            Assert.That(SEntMan.GetComponent<TransformComponent>(body).ParentUid, Is.EqualTo(MapData.Grid.Owner));
            Assert.That(SEntMan.GetComponent<TransformComponent>(cargo).ParentUid, Is.EqualTo(MapData.Grid.Owner));
            Assert.That(SEntMan.GetComponent<BuckleComponent>(SPlayer).BuckledTo, Is.Null);
            Assert.That(SEntMan.GetComponent<FlammableComponent>(SPlayer).OnFire, Is.EqualTo(!nearShore));
            if (nearShore)
            {
                Assert.That(SEntMan.GetComponent<TransformComponent>(SPlayer).LocalPosition.X, Is.LessThan(1));
                Assert.That(SEntMan.GetComponent<TransformComponent>(body).LocalPosition.X, Is.LessThan(1));
            }
        });
    }
}
