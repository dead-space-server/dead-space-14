using System.Numerics;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.Hands;

[TestFixture]
public sealed class HandTests : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestPickUpThenDropInContainerTestBox
  name: box
  components:
  - type: EntityStorage
  - type: ContainerContainer
    containers:
      entity_storage: !type:Container

- type: entity
  id: TestHandsDummy
  name: hands dummy
  components:
  - type: Hands
    hands:
      hand_right:
        location: Right
      hand_left:
        location: Left
    sortedHands:
    - hand_right
    - hand_left
  - type: MobState
";


    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        DummyTicker = false
    };

    [Test]
    public async Task TestPickupDrop()
    {
        var pair = Pair;
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = server.System<SharedMapSystem>();
        var sys = entMan.System<SharedHandsSystem>();

        var data = await pair.CreateTestMap();
        await pair.RunTicksSync(5);

        EntityUid item = default;
        EntityUid player = default;
        HandsComponent hands = default!;
        await server.WaitPost(() =>
        {
            player = entMan.SpawnEntity("TestHandsDummy", data.GridCoords);
            item = entMan.SpawnEntity("Crowbar", data.GridCoords);
            hands = entMan.GetComponent<HandsComponent>(player);
            Assert.That(sys.TryPickup(player, item, hands.ActiveHandId!), Is.True);
        });

        // run ticks here is important, as errors may happen within the container system's frame update methods.
        await pair.RunTicksSync(5);
        Assert.That(sys.GetActiveItem((player, hands)), Is.EqualTo(item));

        await server.WaitPost(() =>
        {
            sys.TryDrop(player, item);
        });

        await pair.RunTicksSync(5);
        Assert.That(sys.GetActiveItem((player, hands)), Is.Null);

        await server.WaitPost(() => mapSystem.DeleteMap(data.MapId));
    }

    [Test]
    public async Task TestPickUpThenDropInContainer()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await pair.RunTicksSync(5);

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = server.System<SharedMapSystem>();
        var sys = entMan.System<SharedHandsSystem>();
        var containerSystem = server.System<SharedContainerSystem>();

        EntityUid item = default;
        EntityUid box = default;
        EntityUid player = default;
        HandsComponent hands = default!;

        // spawn the elusive box and crowbar at the coordinates
        await server.WaitPost(() => box = server.EntMan.SpawnEntity("TestPickUpThenDropInContainerTestBox", map.GridCoords));
        await server.WaitPost(() => item = server.EntMan.SpawnEntity("Crowbar", map.GridCoords));
        // place the player at the exact same coordinates and have them grab the crowbar
        await server.WaitPost(() =>
        {
            player = entMan.SpawnEntity("TestHandsDummy", map.GridCoords);
            hands = entMan.GetComponent<HandsComponent>(player);
            Assert.That(sys.TryPickup(player, item, hands.ActiveHandId!), Is.True);
        });
        await pair.RunTicksSync(5);
        Assert.That(sys.GetActiveItem((player, hands)), Is.EqualTo(item));

        // Open then close the box to place the player, who is holding the crowbar, inside of it
        var storage = server.System<EntityStorageSystem>();
        await server.WaitPost(() =>
        {
            storage.OpenStorage(box);
            storage.CloseStorage(box);
        });
        await pair.RunTicksSync(5);
        Assert.That(containerSystem.IsEntityInContainer(player), Is.True);

        // Dropping the item while the player is inside the box should cause the item
        // to also be inside the same container the player is in now,
        // with the item not being in the player's hands
        await server.WaitPost(() =>
        {
            sys.TryDrop(player, item);
        });
        await pair.RunTicksSync(5);
        var xform = entMan.GetComponent<TransformComponent>(player);
        var itemXform = entMan.GetComponent<TransformComponent>(item);
        Assert.That(sys.GetActiveItem((player, hands)), Is.Not.EqualTo(item));
        Assert.That(containerSystem.IsInSameOrNoContainer((player, xform), (item, itemXform)));

        await server.WaitPost(() => mapSystem.DeleteMap(map.MapId));
    }

    [Test]
    public async Task TestDropTowardsWallKeepsItemOutsideWall()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false
        });
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = server.System<SharedMapSystem>();
        var handsSystem = entMan.System<SharedHandsSystem>();
        var transformSystem = entMan.System<TransformSystem>();
        var physicsSystem = entMan.System<SharedPhysicsSystem>();

        var map = await pair.CreateTestMap();
        await pair.RunTicksSync(5);

        EntityUid item = default;
        EntityUid wall = default;
        EntityUid player = default;
        HandsComponent hands = default!;

        await server.WaitPost(() =>
        {
            player = entMan.SpawnEntity("TestHandsDummy", map.GridCoords);

            var wallCoords = map.GridCoords.Offset(new Vector2(1f, 0f));
            wall = entMan.SpawnEntity("WallSolid", wallCoords);

            item = entMan.SpawnEntity("Crowbar", map.GridCoords);
            hands = entMan.GetComponent<HandsComponent>(player);
            Assert.That(handsSystem.TryPickup(player, item, hands.ActiveHandId!), Is.True);
        });

        await pair.RunTicksSync(5);
        Assert.That(handsSystem.GetActiveItem((player, hands)), Is.EqualTo(item));

        await server.WaitPost(() =>
        {
            var wallCoords = map.GridCoords.Offset(new Vector2(1f, 0f));
            Assert.That(handsSystem.TryDrop(player, item, wallCoords), Is.True);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var itemBounds = physicsSystem.GetWorldAABB(item);
            var wallBounds = physicsSystem.GetWorldAABB(wall);

            Assert.That(itemBounds.Intersects(wallBounds), Is.False);
        });

        await server.WaitPost(() => mapSystem.DeleteMap(map.MapId));
        await pair.CleanReturnAsync();
    }
}
