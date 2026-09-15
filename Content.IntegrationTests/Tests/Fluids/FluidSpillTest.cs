#nullable enable
using System.Collections.Generic;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Spreader;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Fluids;

[TestFixture]
[TestOf(typeof(SpreaderSystem))]
public sealed class FluidSpill
{
    private static PuddleComponent? GetPuddle(IEntityManager entityManager, Entity<MapGridComponent> mapGrid, Vector2i pos)
    {
        return GetPuddleEntity(entityManager, mapGrid, pos)?.Comp;
    }

    private static Entity<PuddleComponent>? GetPuddleEntity(IEntityManager entityManager, Entity<MapGridComponent> mapGrid, Vector2i pos)
    {
        var mapSys = entityManager.System<SharedMapSystem>();
        foreach (var uid in mapSys.GetAnchoredEntities(mapGrid, mapGrid.Comp, pos))
        {
            if (entityManager.TryGetComponent(uid, out PuddleComponent? puddleComponent))
                return (uid, puddleComponent);
        }

        return null;
    }

    [Test]
    public async Task SpillCorner()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var mapManager = server.ResolveDependency<IMapManager>();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var puddleSystem = server.System<PuddleSystem>();
        var mapSystem = server.System<SharedMapSystem>();
        var gameTiming = server.ResolveDependency<IGameTiming>();
        EntityUid gridId = default;
        EntityUid wall = default;

        /*
         In this test, if o is spillage puddle and # are walls, we want to ensure all tiles are empty (`.`)
            . . .
            # . .
            o # .
        */
        await server.WaitPost(() =>
        {
            mapSystem.CreateMap(out var mapId);
            var grid = mapManager.CreateGridEntity(mapId);
            gridId = grid.Owner;

            for (var x = 0; x < 3; x++)
            {
                for (var y = 0; y < 3; y++)
                {
                    mapSystem.SetTile(grid, new Vector2i(x, y), new Tile(1));
                }
            }

            wall = entityManager.SpawnEntity("WallReinforced", mapSystem.GridTileToLocal(grid, grid.Comp, new Vector2i(0, 1)));
            entityManager.SpawnEntity("WallReinforced", mapSystem.GridTileToLocal(grid, grid.Comp, new Vector2i(1, 0)));
        });


        var puddleOrigin = new Vector2i(0, 0);
        await server.WaitAssertion(() =>
        {
            var grid = entityManager.GetComponent<MapGridComponent>(gridId);
            var solution = new Solution("Carbon", FixedPoint2.New(100));
            var tileRef = mapSystem.GetTileRef(gridId, grid, puddleOrigin);
#pragma warning disable NUnit2045 // Interdependent tests
            Assert.That(puddleSystem.TrySpillAt(tileRef, solution, out _), Is.True);
            Assert.That(GetPuddle(entityManager, (gridId, grid), puddleOrigin), Is.Not.Null);
#pragma warning restore NUnit2045
        });

        var sTimeToWait = (int) Math.Ceiling(2f * gameTiming.TickRate);
        await server.WaitRunTicks(sTimeToWait);

        await server.WaitAssertion(() =>
        {
            var grid = entityManager.GetComponent<MapGridComponent>(gridId);
            var puddle = GetPuddleEntity(entityManager, (gridId, grid), puddleOrigin);

#pragma warning disable NUnit2045 // Interdependent tests
            Assert.That(puddle, Is.Not.Null);
            Assert.That(puddleSystem.CurrentVolume(puddle!.Value.Owner, puddle), Is.EqualTo(FixedPoint2.New(100)));
            Assert.That(entityManager.HasComponent<ActiveEdgeSpreaderComponent>(puddle.Value), Is.False);
#pragma warning restore NUnit2045

            for (var x = 0; x < 3; x++)
            {
                for (var y = 0; y < 3; y++)
                {
                    if (x == 0 && y == 0 || x == 0 && y == 1 || x == 1 && y == 0)
                    {
                        continue;
                    }

                    var newPos = new Vector2i(x, y);
                    var sidePuddle = GetPuddle(entityManager, (gridId, grid), newPos);
                    Assert.That(sidePuddle, Is.Null);
                }
            }
        });

        await server.WaitPost(() => entityManager.DeleteEntity(wall));
        await server.WaitRunTicks(sTimeToWait);
        await server.WaitAssertion(() =>
        {
            var grid = entityManager.GetComponent<MapGridComponent>(gridId);
            var source = GetPuddleEntity(entityManager, (gridId, grid), puddleOrigin)!.Value;
            Assert.That(puddleSystem.CurrentVolume(source), Is.LessThan(FixedPoint2.New(100)));
            Assert.That(GetPuddle(entityManager, (gridId, grid), new Vector2i(0, 1)), Is.Not.Null);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LimitedSpreadPreservesUnspentOverflow()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var maps = server.System<SharedMapSystem>();
        var puddles = server.System<PuddleSystem>();
        var spreaders = server.System<SpreaderSystem>();

        await server.WaitAssertion(() =>
        {
            for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 1; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);

            var tile = maps.GetTileRef(map.Grid, map.Grid.Comp, Vector2i.Zero);
            Assert.That(puddles.TrySpillAt(tile, new Solution("Carbon", FixedPoint2.New(150)), out var uid, sound: false), Is.True);
            spreaders.GetNeighbors(uid, entities.GetComponent<TransformComponent>(uid), "Puddle", out var freeTiles, out var neighbors);
            var args = new SpreadNeighborsEvent { NeighborFreeTiles = freeTiles, Neighbors = neighbors, Updates = 1 };
            entities.EventBus.RaiseLocalEvent(uid, ref args);

            var total = FixedPoint2.Zero;
            for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 1; y++)
            {
                if (GetPuddleEntity(entities, map.Grid, new Vector2i(x, y)) is { } puddle)
                    total += puddles.CurrentVolume(puddle);
            }

            Assert.That(args.Updates, Is.Zero);
            Assert.That(total, Is.EqualTo(FixedPoint2.New(150)));
            Assert.That(puddles.CurrentVolume(uid), Is.GreaterThan(FixedPoint2.New(50)));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpreaderBudgetIncludesPuddlesThatCannotSpread()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var maps = server.System<SharedMapSystem>();
        var puddles = server.System<PuddleSystem>();

        await server.WaitAssertion(() =>
        {
            var active = new List<ActiveEdgeSpreaderComponent>();
            for (var x = 0; x < 40; x++)
            {
                maps.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
                var tile = maps.GetTileRef(map.Grid, map.Grid.Comp, new Vector2i(x, 0));
                Assert.That(puddles.TrySpillAt(tile, new Solution("Carbon", FixedPoint2.New(50)), out var uid, sound: false), Is.True);
                active.Add(entities.EnsureComponent<ActiveEdgeSpreaderComponent>(uid));
            }

            entities.GetComponent<SpreaderGridComponent>(map.Grid).UpdateAccumulator = 0;
            server.System<SpreaderSystem>().Update(0);

            var stopped = 0;
            foreach (var component in active)
            {
                if (component.LifeStage >= ComponentLifeStage.Stopping)
                    stopped++;
            }

            Assert.That(stopped, Is.EqualTo(16));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BalancedPuddlesWakeWhenNeighborIsDrained()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var maps = server.System<SharedMapSystem>();
        var puddles = server.System<PuddleSystem>();
        EntityUid source = default;
        EntityUid target = default;

        await server.WaitAssertion(() =>
        {
            maps.SetTile(map.Grid, new Vector2i(1, 0), map.Tile.Tile);
            var sourceTile = maps.GetTileRef(map.Grid, map.Grid.Comp, Vector2i.Zero);
            var targetTile = maps.GetTileRef(map.Grid, map.Grid.Comp, new Vector2i(1, 0));
            Assert.That(puddles.TrySpillAt(sourceTile, new Solution("Carbon", FixedPoint2.New(100)), out source, sound: false), Is.True);
            Assert.That(puddles.TrySpillAt(targetTile, new Solution("Carbon", FixedPoint2.New(100)), out target, sound: false), Is.True);
        });

        await pair.RunSeconds(3);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.HasComponent<ActiveEdgeSpreaderComponent>(source), Is.False);
            Assert.That(entities.HasComponent<ActiveEdgeSpreaderComponent>(target), Is.False);
            Assert.That(puddles.CurrentVolume(source), Is.EqualTo(FixedPoint2.New(100)));

            var solution = entities.GetComponent<PuddleComponent>(target).Solution!.Value;
            server.System<SharedSolutionContainerSystem>().SplitSolution(solution, FixedPoint2.New(50));
        });

        await pair.RunSeconds(3);
        await server.WaitAssertion(() =>
        {
            Assert.That(puddles.CurrentVolume(source), Is.EqualTo(FixedPoint2.New(75)));
            Assert.That(puddles.CurrentVolume(target), Is.EqualTo(FixedPoint2.New(75)));
            Assert.That(entities.HasComponent<ActiveEdgeSpreaderComponent>(source), Is.False);
            Assert.That(entities.HasComponent<ActiveEdgeSpreaderComponent>(target), Is.False);

            maps.SetTile(map.Grid, new Vector2i(2, 0), map.Tile.Tile);
        });

        await pair.RunSeconds(3);
        await server.WaitAssertion(() =>
        {
            var expanded = GetPuddleEntity(entities, map.Grid, new Vector2i(2, 0));
            Assert.That(expanded, Is.Not.Null);
            Assert.That(puddles.CurrentVolume(source) + puddles.CurrentVolume(target) + puddles.CurrentVolume(expanded!.Value),
                Is.EqualTo(FixedPoint2.New(150)));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ActiveSpreaderFollowsGridChangesAndRemoval()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var secondGrid = server.ResolveDependency<IMapManager>().CreateGridEntity(map.MapId);
            var uid = entities.SpawnEntity(null, map.GridCoords);
#pragma warning disable RA0002
            entities.AddComponent<EdgeSpreaderComponent>(uid).Id = "Puddle";
#pragma warning restore RA0002
            entities.AddComponent<ActiveEdgeSpreaderComponent>(uid);
            var original = entities.GetComponent<SpreaderGridComponent>(map.Grid);
            var destination = entities.GetComponent<SpreaderGridComponent>(secondGrid);
            Assert.That(original.ActiveSpreaders, Does.Contain(uid));

            server.System<SharedTransformSystem>().SetCoordinates(uid, new EntityCoordinates(secondGrid, 0.5f, 0.5f));
            Assert.That(original.ActiveSpreaders, Does.Not.Contain(uid));
            Assert.That(destination.ActiveSpreaders, Does.Contain(uid));

            entities.RemoveComponent<ActiveEdgeSpreaderComponent>(uid);
            Assert.That(destination.ActiveSpreaders, Does.Not.Contain(uid));
        });

        await pair.CleanReturnAsync();
    }
}
