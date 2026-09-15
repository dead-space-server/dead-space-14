// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.PipeShuttle.Systems;
using Content.Shared.DeadSpace.PipeShuttle.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace.PipeShuttle;

[TestFixture]
public sealed class PipeShuttleTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: PipeShuttleTestDoor
  components:
  - type: Door
  - type: Airlock
  - type: DoorBolt
  - type: ApcPowerReceiver
    needsPower: false
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeAabb
            bounds: ""-0.49,-0.49,0.49,0.49""
        mask:
        - Impassable
";

    [Test]
    public async Task CallOnlyStartsBoundShuttleOnSameMap()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            Dirty = true,
        });

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        var mapManager = server.ResolveDependency<IMapManager>();
        var shuttleSystem = server.System<PipeShuttleSystem>();
        var xformSystem = server.System<SharedTransformSystem>();

        await server.WaitPost(() =>
        {
            var shuttleAUid = map.Grid;
            var shuttleBUid = mapManager.CreateGridEntity(map.MapId).Owner;
            xformSystem.SetWorldPosition(shuttleBUid, new Vector2(20f, 0f));

            var shuttleA = ConfigureShuttle(entMan, shuttleAUid, Vector2.Zero);
            var shuttleB = ConfigureShuttle(entMan, shuttleBUid, new Vector2(20f, 0f));
            SpawnDoor(entMan, shuttleAUid);
            SpawnDoor(entMan, shuttleBUid);

            var callUid = entMan.SpawnEntity(
                "PipeShuttleCallButton",
                new MapCoordinates(new Vector2(-5f, 0f), map.MapId));
            var call = entMan.GetComponent<PipeShuttleCallComponent>(callUid);
            call.Shuttle = shuttleAUid;

            Assert.That(shuttleSystem.TryCallShuttleToDest("end", (callUid, call), callUid), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(shuttleA.Travelling, Is.True);
                Assert.That(shuttleA.TargetDestId, Is.EqualTo("end"));
                Assert.That(shuttleB.Travelling, Is.False);
                Assert.That(shuttleB.TargetDestId, Is.Null);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OpenDoorBlocksMovementUntilClosedAndBoltedThenArrivalUnbolts()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            Dirty = true,
        });

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        var doorSystem = server.System<SharedDoorSystem>();
        var shuttleSystem = server.System<PipeShuttleSystem>();
        var xformSystem = server.System<SharedTransformSystem>();

        EntityUid shuttleUid = map.Grid;
        EntityUid doorUid = default;
        PipeShuttleComponent shuttle = null!;
        DoorComponent door = null!;
        DoorBoltComponent bolt = null!;
        Vector2 startPosition = default;

        await server.WaitPost(() =>
        {
            shuttle = ConfigureShuttle(entMan, shuttleUid, Vector2.Zero);
            shuttle.MoveSpeed = 5f;
            shuttle.ArrivalThreshold = 0.01f;

            doorUid = SpawnDoor(entMan, shuttleUid);
            door = entMan.GetComponent<DoorComponent>(doorUid);
            bolt = entMan.GetComponent<DoorBoltComponent>(doorUid);
            doorSystem.SetState(doorUid, DoorState.Open, door);

            var callUid = entMan.SpawnEntity(
                "PipeShuttleCallButton",
                new MapCoordinates(new Vector2(-5f, 0f), map.MapId));
            var call = entMan.GetComponent<PipeShuttleCallComponent>(callUid);
            call.Shuttle = shuttleUid;

            startPosition = xformSystem.GetWorldPosition(shuttleUid);
            Assert.That(shuttleSystem.TryCallShuttleToDest("end", (callUid, call), callUid), Is.True);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            Assert.That(xformSystem.GetWorldPosition(shuttleUid), Is.EqualTo(startPosition));
            Assert.That(bolt.BoltsDown, Is.False);
        });

        var moved = false;
        for (var i = 0; i < 120 && !moved; i++)
        {
            await server.WaitRunTicks(1);
            await server.WaitAssertion(() =>
            {
                if (xformSystem.GetWorldPosition(shuttleUid) == startPosition)
                    return;

                moved = true;
                Assert.Multiple(() =>
                {
                    Assert.That(door.State, Is.EqualTo(DoorState.Closed));
                    Assert.That(bolt.BoltsDown, Is.True);
                });
            });
        }

        Assert.That(moved, Is.True, "The shuttle never started moving after its door was secured.");

        await PoolManager.WaitUntil(server, () => shuttle.CurrentDestId == "end", maxTicks: 180);
        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(shuttle.Travelling, Is.False);
                Assert.That(bolt.BoltsDown, Is.False);
                Assert.That(door.State, Is.EqualTo(DoorState.Closed));
            });
        });

        await pair.CleanReturnAsync();
    }

    private static PipeShuttleComponent ConfigureShuttle(
        IEntityManager entMan,
        EntityUid uid,
        Vector2 startPosition)
    {
        var shuttle = entMan.AddComponent<PipeShuttleComponent>(uid);
        shuttle.CurrentDestId = "start";
        shuttle.PositionOffset = Vector2.Zero;
        shuttle.Destinations.Add(new PipeShuttleDestination
        {
            Id = "start",
            Name = "pipe-shuttle-destination-left",
            Position = startPosition,
        });
        shuttle.Destinations.Add(new PipeShuttleDestination
        {
            Id = "end",
            Name = "pipe-shuttle-destination-right",
            Position = startPosition + new Vector2(2f, 0f),
        });
        return shuttle;
    }

    private static EntityUid SpawnDoor(IEntityManager entMan, EntityUid shuttleUid)
    {
        return entMan.SpawnEntity("PipeShuttleTestDoor", new EntityCoordinates(shuttleUid, Vector2.Zero));
    }
}
