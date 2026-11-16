// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Anomaly.Components;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.DeadSpace.Anomaly;

[TestFixture]
public sealed class AnomalyParticleSweepTest
{
    [Test]
    public async Task OmegaParticleRaisesSeverityAtOneSubstep()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var entMan = server.EntMan;

        EntityUid anomaly = default;
        var before = 0f;

        await server.WaitPost(() =>
        {
            cfg.SetCVar(CVars.NetTickrate, 20);
            cfg.SetCVar(CVars.TargetMinimumTickrate, 20);

            var maps = server.System<SharedMapSystem>();
            var plating = server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId;
            for (var x = 0; x <= 12; x++)
                maps.SetTile(map.Grid, new Vector2i(x, 0), new Tile(plating));

            anomaly = entMan.SpawnEntity("AnomalyPyroclastic", new EntityCoordinates(map.Grid, 8.5f, 0.5f));
            before = entMan.GetComponent<AnomalyComponent>(anomaly).Severity;

            var particle = entMan.SpawnEntity("AnomalousParticleOmegaStrong", new EntityCoordinates(map.Grid, 2.5f, 0.5f));
            server.System<SharedPhysicsSystem>().SetLinearVelocity(particle, new Vector2(10f, 0f));
        });

        await server.WaitRunTicks(40);

        await server.WaitPost(() =>
        {
            var severity = entMan.GetComponent<AnomalyComponent>(anomaly).Severity;
            Assert.That(severity, Is.GreaterThan(before));
        });

        await pair.CleanReturnAsync();
    }
}
