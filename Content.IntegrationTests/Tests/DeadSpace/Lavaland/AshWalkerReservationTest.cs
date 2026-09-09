using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.DeadSpace.AshWalkers;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Station.Events;
using Content.Shared.DeadSpace.AshWalkers;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Station.Components;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.DeadSpace.Lavaland;

public sealed class AshWalkerReservationTest : InteractionTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: lavalandPlanet
          id: TestAshWalkerReservation
          biome: LavalandSurface
          mapHalfSize: 100
          boundaryEnabled: false
          landingPadRadius: 2
          terminalReservationEnabled: false
          minStructureDistance: 60
          maxStructureDistance: 60
          minStructureSeparation: 1
          tendrilsEnabled: false
          faunaEnabled: false
          ftlEnabled: false
          markerLayers: []
          customGrids:
          - path: /Maps/Lavaland/ds_ashwalkers_base.yml
            footprintRadius: 1

        - type: entity
          id: TestAshWalkerReservationStation
          components:
          - type: StationData
          - type: StationLavaland
            planets: [TestAshWalkerReservation]
        """;

    [Test]
    public async Task ActualGridFootprintRemainsReservedAfterBiomeReload()
    {
        EntityUid station = default;
        StationLavalandComponent lavaland = default!;
        await Server.WaitPost(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(CCCCVars.LavalandAutoGenerate, true);
            station = SEntMan.SpawnEntity("TestAshWalkerReservationStation", MapCoordinates.Nullspace);
            lavaland = SEntMan.GetComponent<StationLavalandComponent>(station);
            var ev = new StationPostInitEvent((station, SEntMan.GetComponent<StationDataComponent>(station)));
            SEntMan.EventBus.RaiseLocalEvent(station, ref ev);
        });

        try
        {
            await PoolManager.WaitUntil(Server, () => lavaland.GeneratedMap != null && lavaland.GenerationCancel == null);
            var map = lavaland.GeneratedMap!.Value;
            EntityUid grid = default;
            Box2 bounds = default;
            EntityUid[] mappedLava = [];

            void CheckReservation()
            {
                var biome = SEntMan.GetComponent<BiomeComponent>(map);
                var reserved = biome.ModifiedTiles.Values.SelectMany(tiles => tiles).ToHashSet();
                for (var x = (int) MathF.Floor(bounds.Left); x < MathF.Ceiling(bounds.Right); x++)
                {
                    for (var y = (int) MathF.Floor(bounds.Bottom); y < MathF.Ceiling(bounds.Top); y++)
                        Assert.That(reserved, Does.Contain(new Vector2i(x, y)), $"Unreserved terrain under the settlement: {x}, {y}");
                }

                var query = SEntMan.EntityQueryEnumerator<TransformComponent, MetaDataComponent>();
                while (query.MoveNext(out _, out var xform, out var meta))
                {
                    if (xform.GridUid == map && meta.EntityPrototype?.ID == "FloorLavaEntity")
                        Assert.That(bounds.Contains(Transform.GetWorldPosition(xform)), Is.False);
                }

                foreach (var lava in mappedLava)
                    Assert.That(SEntMan.GetComponent<TransformComponent>(lava).GridUid, Is.EqualTo(grid));
            }

            await Server.WaitAssertion(() =>
            {
                var grids = new List<EntityUid>();
                var gridQuery = SEntMan.EntityQueryEnumerator<LavalandFtlExclusionComponent, MapGridComponent, TransformComponent>();
                while (gridQuery.MoveNext(out var uid, out _, out _, out var xform))
                {
                    if (xform.MapUid == map)
                        grids.Add(uid);
                }
                grid = grids.Single();
                var gridComp = SEntMan.GetComponent<MapGridComponent>(grid);
                var position = Transform.GetWorldPosition(grid);
                bounds = new Box2Rotated(gridComp.LocalAABB, Transform.GetWorldRotation(grid), Vector2.Zero)
                    .CalcBoundingBox().Translated(position);
                var lava = new List<EntityUid>();
                var lavaQuery = SEntMan.EntityQueryEnumerator<TransformComponent, MetaDataComponent>();
                while (lavaQuery.MoveNext(out var uid, out var xform, out var meta))
                {
                    if (xform.GridUid == grid && meta.EntityPrototype?.ID == "FloorLavaEntity")
                        lava.Add(uid);
                }
                mappedLava = lava.ToArray();
                var eggs = SEntMan.EntityQueryEnumerator<AshWalkerEggComponent, TransformComponent>();
                while (eggs.MoveNext(out _, out var egg, out var xform))
                {
                    if (xform.GridUid == grid)
                        Assert.That(egg.HomeMap, Is.EqualTo(map));
                }
                var gutlunchSexes = new List<bool>();
                var gutlunches = SEntMan.EntityQueryEnumerator<GutlunchComponent, TransformComponent>();
                while (gutlunches.MoveNext(out _, out var gutlunch, out var xform))
                {
                    if (xform.GridUid == grid)
                        gutlunchSexes.Add(gutlunch.IsMale);
                }
                Assert.That(gutlunchSexes, Is.EquivalentTo(new[] { true, false }));
                CheckReservation();
                Transform.SetCoordinates(SPlayer, new EntityCoordinates(grid, Vector2.Zero));
            });
            await RunTicks(20);
            await Server.WaitAssertion(CheckReservation);
            await Server.WaitPost(() => Transform.SetCoordinates(SPlayer, ToServer(PlayerCoords)));
            await RunTicks(20);
            await Server.WaitPost(() => Transform.SetCoordinates(SPlayer, new EntityCoordinates(grid, Vector2.Zero)));
            await RunTicks(20);
            await Server.WaitAssertion(CheckReservation);
        }
        finally
        {
            await Server.WaitPost(() => SEntMan.DeleteEntity(station));
        }
    }
}
