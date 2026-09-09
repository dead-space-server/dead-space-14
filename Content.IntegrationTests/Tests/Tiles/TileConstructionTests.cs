using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.Tiles;

public sealed class TileConstructionTests : InteractionTest
{
    [TestCase("FloorBasalt")]
    [TestCase("FloorCave")]
    public async Task StoneFloorOnOverlappingGridsRestoresNaturalGround(string ground)
    {
        await Server.WaitPost(() =>
        {
            var physics = Server.System<SharedPhysicsSystem>();
            physics.SetBodyType(MapData.Grid, BodyType.Static);
            var terrain = MapMan.CreateGridEntity(MapId);
            physics.SetBodyType(terrain, BodyType.Static);
            MapSystem.SetTile(terrain, terrain.Comp, new Vector2i(1, 0), MapData.Tile.Tile);
            TargetCoords = SEntMan.GetNetCoordinates(new EntityCoordinates(MapData.Grid, 1.5f, 0.5f));
        });
        await Server.WaitPost(() => MapSystem.SetTile(MapData.Grid, MapData.Grid.Comp,
            new Vector2i(1, 0), new Tile(TileMan[ground].TileId)));
        await Pair.RunUntilSynced();
        await InteractUsing("FloorTileAshWalkerStone4", 4);
        await AssertFloor("FloorStone");
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<StackComponent>(HandSys.GetActiveItem(SPlayer)!.Value).Count, Is.EqualTo(3)));
        await Interact();
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<StackComponent>(HandSys.GetActiveItem(SPlayer)!.Value).Count, Is.EqualTo(3)));
        await InteractUsing(Pry);
        await AssertFloor(ground);
        await AssertEntityLookup(("FloorTileItemStone", 1));

        await Server.WaitPost(() => MapSystem.SetTile(MapData.Grid, MapData.Grid.Comp, new Vector2i(1, 0), Tile.Empty));
        await InteractUsing(Rod);
        await AssertFloor("Space");
        await Server.WaitAssertion(() => Assert.That(HandSys.GetActiveItem(SPlayer), Is.Not.Null));

        async Task AssertFloor(string expected)
        {
            await Server.WaitAssertion(() =>
                Assert.That(MapSystem.GetTileRef(MapData.Grid, MapData.Grid.Comp,
                    SEntMan.GetCoordinates(TargetCoords)).Tile.TypeId, Is.EqualTo(TileMan[expected].TileId)));
        }
    }

    /// <summary>
    /// Test placing and cutting a single lattice.
    /// </summary>
    [Test]
    public async Task PlaceThenCutLattice()
    {
        await AssertTile(Plating);
        await AssertTile(Plating, PlayerCoords);
        AssertGridCount(1);
        await SetTile(null);
        await InteractUsing(Rod);
        await AssertTile(Lattice);
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Null);
        await InteractUsing(Cut);
        await AssertTile(null);
        await AssertEntityLookup((Rod, 1));
        AssertGridCount(1);
    }

    /// <summary>
    /// Test placing and cutting a single lattice in space (not adjacent to any existing grid.
    /// </summary>
    [Test]
    public async Task CutThenPlaceLatticeNewGrid()
    {
        await AssertTile(Plating);
        await AssertTile(Plating, PlayerCoords);
        AssertGridCount(1);

        // Remove grid
        await SetTile(null);
        await SetTile(null, PlayerCoords);
        Assert.That(MapData.Grid.Comp.Deleted);
        AssertGridCount(0);

        // Place Lattice
        var oldPos = TargetCoords;
        TargetCoords = SEntMan.GetNetCoordinates(new EntityCoordinates(MapData.MapUid, 1, 0));
        await InteractUsing(Rod);
        TargetCoords = oldPos;
        await AssertTile(Lattice);
        AssertGridCount(1);

        // Cut lattice
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Null);
        await InteractUsing(Cut);
        await AssertTile(null);
        AssertGridCount(0);

        await AssertEntityLookup((Rod, 1));
    }

    /// <summary>
    /// Test space -> floor -> plating
    /// </summary>
    [Test]
    public async Task FloorConstructDeconstruct()
    {
        await AssertTile(Plating);
        await AssertTile(Plating, PlayerCoords);
        AssertGridCount(1);

        // Remove grid
        await SetTile(null);
        await SetTile(null, PlayerCoords);
        Assert.That(MapData.Grid.Comp.Deleted);
        AssertGridCount(0);

        // Space -> Lattice
        var oldPos = TargetCoords;
        TargetCoords = SEntMan.GetNetCoordinates(new EntityCoordinates(MapData.MapUid, 1, 0));
        await InteractUsing(Rod);
        TargetCoords = oldPos;
        await AssertTile(Lattice);
        AssertGridCount(1);

        // Lattice -> Plating
        await InteractUsing(FloorItem);
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Null);
        await AssertTile(Plating);
        AssertGridCount(1);

        // Plating -> Tile
        await InteractUsing(FloorItem);
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Null);
        await AssertTile(Floor);
        AssertGridCount(1);

        // Tile -> Plating
        await InteractUsing(Pry);
        await AssertTile(Plating);
        AssertGridCount(1);

        await AssertEntityLookup((FloorItem, 1));
    }

    /// <summary>
    /// Test brassPlating -> floor -> brassPlating using tilestacking
    /// </summary>
    [Test]
    public async Task BrassPlatingPlace()
    {
        await SetTile(PlatingBrass);

        // Brass Plating -> Tile
        await InteractUsing(FloorItem);
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Null);
        await AssertTile(Floor);
        AssertGridCount(1);

        // Tile -> Brass Plating
        await InteractUsing(Pry);
        await AssertTile(PlatingBrass);
        AssertGridCount(1);
        await AssertEntityLookup((FloorItem, 1));
    }
}
