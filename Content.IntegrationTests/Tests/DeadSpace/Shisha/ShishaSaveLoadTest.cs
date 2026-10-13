using Content.Server.DeadSpace.Smokables.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DeadSpace.Smokables;
using Content.Shared.FixedPoint;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.DeadSpace.Shisha;

public sealed class ShishaSaveLoadTest
{
    // An initialized map is distinct from the existing uninitialized-prototype save check.
    [TestCase("docked")]
    [TestCase("held")]
    [TestCase("destroyed")]
    [TestCase("unlit")]
    public async Task SaveLoadPreservesOwnedHoseFillerAndFuel(string state)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var em = server.EntMan;
        var mapData = await pair.CreateTestMap();
        var maps = server.System<SharedMapSystem>();
        var loader = server.System<MapLoaderSystem>();
        var shishas = server.System<ShishaSystem>();
        var solutions = server.System<SharedSolutionContainerSystem>();
        var mapId = mapData.MapId;
        var path = new ResPath($"/shisha-save-{state}.yml");

        await server.WaitAssertion(() =>
        {
            var uid = em.SpawnEntity("Shisha", mapData.GridCoords);
            var component = em.GetComponent<ShishaComponent>(uid);
            component.FuelRemaining = 123;
            component.Lit = state != "unlit";
            Assert.That(solutions.TryGetSolution(uid, "shisha", out var sol, out _), Is.True);
            Assert.That(solutions.TryAddSolution(sol!.Value, new Solution("Nicotine", 12)), Is.True);
            if (state == "held")
            {
                var holder = em.SpawnEntity("MobHuman", mapData.GridCoords);
                Assert.That(shishas.TryTakeHose((uid, component), holder), Is.True);
            }
            else if (state == "destroyed")
            {
                em.DeleteEntity(component.Hose!.Value);
            }

            for (var cycle = 0; cycle < 2; cycle++)
            {
                Assert.That(loader.TrySaveMap(mapId, path), Is.True);
                if (cycle == 0 && state != "destroyed")
                    Assert.That(shishas.IsDocked((uid, component)), Is.True);
                maps.DeleteMap(mapId);
                Assert.That(loader.TryLoadMap(path, out var map, out _), Is.True);
                mapId = map!.Value.Comp.MapId;

                var bases = em.EntityQueryEnumerator<ShishaComponent, TransformComponent>();
                var baseCount = 0;
                while (bases.MoveNext(out var loaded, out var shisha, out var xform))
                {
                    if (xform.MapID != mapId)
                        continue;
                    baseCount++;
                    Assert.That(shisha.HoseInitialized, Is.True);
                    Assert.That(shisha.FuelRemaining, Is.EqualTo(123));
                    Assert.That(shisha.Lit, Is.EqualTo(state != "unlit"));
                    Assert.That(solutions.TryGetSolution(loaded, "shisha", out _, out var reservoir), Is.True);
                    Assert.That(reservoir!.Volume, Is.EqualTo(FixedPoint2.New(12)));
                    if (state == "destroyed")
                    {
                        Assert.That(shisha.Hose, Is.Null);
                        continue;
                    }

                    Assert.That(shisha.Hose, Is.Not.Null);
                    var hose = em.GetComponent<ShishaHoseComponent>(shisha.Hose!.Value);
                    Assert.That(hose.Base, Is.EqualTo(loaded));
                    Assert.That(hose.Puff, Is.Null);
                    Assert.That(shishas.IsDocked((loaded, shisha)), Is.True);
                }
                Assert.That(baseCount, Is.EqualTo(1));

                var hoses = em.EntityQueryEnumerator<ShishaHoseComponent, TransformComponent>();
                var hoseCount = 0;
                while (hoses.MoveNext(out _, out _, out var xform))
                {
                    if (xform.MapID == mapId)
                        hoseCount++;
                }
                Assert.That(hoseCount, Is.EqualTo(state == "destroyed" ? 0 : 1));
            }
            maps.DeleteMap(mapId);
        });
        await pair.CleanReturnAsync();
    }
}
