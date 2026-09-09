using Content.Server.Fluids.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Coordinates;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Fluids
{
    [TestFixture]
    [TestOf(typeof(PuddleComponent))]
    public sealed class PuddleTest
    {
        [TestCase(0.01)]
        [TestCase(1.0)]
        public async Task MixedPuddleEvaporationStopsWhenOnlyBloodRemains(double waterAmount)
        {
            await using var pair = await PoolManager.GetServerClient();
            var server = pair.Server;
            var map = await pair.CreateTestMap();
            var puddles = server.System<PuddleSystem>();
            var solutions = server.System<SharedSolutionContainerSystem>();
            EntityUid uid = default;
            Entity<SolutionComponent> solutionEntity = default;

            await server.WaitAssertion(() =>
            {
                var solution = new Solution("Blood", FixedPoint2.New(49));
                solution.AddReagent(new ReagentId("Water", null), FixedPoint2.New(waterAmount));
                Assert.That(puddles.TrySpillAt(map.Tile, solution, out uid, sound: false), Is.True);
                var puddle = server.EntMan.GetComponent<PuddleComponent>(uid);
                solutionEntity = puddle.Solution!.Value;
                Assert.That(server.EntMan.HasComponent<EvaporationComponent>(uid), Is.True);
            });

            await pair.RunSeconds(2);

            await server.WaitAssertion(() =>
            {
                var solution = solutionEntity.Comp.Solution;
                Assert.That(solution.GetTotalPrototypeQuantity("Water"), Is.LessThan(FixedPoint2.New(waterAmount)));
                Assert.That(solution.GetTotalPrototypeQuantity("Blood"), Is.EqualTo(FixedPoint2.New(49)));

                if (waterAmount == 0.01)
                    Assert.That(server.EntMan.HasComponent<EvaporationComponent>(uid), Is.False);

                solution.RemoveReagent("Water", solution.GetTotalPrototypeQuantity("Water"));
                solutions.UpdateChemicals(solutionEntity);
                Assert.That(server.EntMan.HasComponent<EvaporationComponent>(uid), Is.False);
            });

            var lastChange = solutionEntity.Comp.LastModifiedTick;
            await pair.RunSeconds(2);
            await server.WaitAssertion(() => Assert.That(solutionEntity.Comp.LastModifiedTick, Is.EqualTo(lastChange)));
            await pair.CleanReturnAsync();
        }

        [Test]
        public async Task TilePuddleTest()
        {
            await using var pair = await PoolManager.GetServerClient();
            var server = pair.Server;

            var testMap = await pair.CreateTestMap();

            var spillSystem = server.System<PuddleSystem>();

            await server.WaitAssertion(() =>
            {
                var solution = new Solution("Water", FixedPoint2.New(20));
                var tile = testMap.Tile;
                var gridUid = tile.GridUid;
                var (x, y) = tile.GridIndices;
                var coordinates = new EntityCoordinates(gridUid, x, y);

                Assert.That(spillSystem.TrySpillAt(coordinates, solution, out _), Is.True);
            });
            await pair.RunTicksSync(5);

            await pair.CleanReturnAsync();
        }

        [Test]
        public async Task SpaceNoPuddleTest()
        {
            await using var pair = await PoolManager.GetServerClient();
            var server = pair.Server;

            var testMap = await pair.CreateTestMap();
            var grid = testMap.Grid;

            var entitySystemManager = server.ResolveDependency<IEntitySystemManager>();
            var spillSystem = server.System<PuddleSystem>();
            var mapSystem = server.System<SharedMapSystem>();

            // Remove all tiles
            await server.WaitPost(() =>
            {
                var tiles = mapSystem.GetAllTiles(grid.Owner, grid.Comp);
                foreach (var tile in tiles)
                {
                    mapSystem.SetTile(grid, tile.GridIndices, Tile.Empty);
                }
            });

            await pair.RunTicksSync(5);

            await server.WaitAssertion(() =>
            {
                var coordinates = grid.Owner.ToCoordinates();
                var solution = new Solution("Water", FixedPoint2.New(20));

                Assert.That(spillSystem.TrySpillAt(coordinates, solution, out _), Is.False);
            });

            await pair.CleanReturnAsync();
        }
    }
}
