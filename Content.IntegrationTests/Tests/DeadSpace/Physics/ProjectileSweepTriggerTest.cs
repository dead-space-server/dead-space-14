// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14/master/LICENSE.TXT

using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Damage.Components;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.DeadSpace.Physics;

[TestFixture]
public sealed class ProjectileSweepTriggerTest
{
    private static readonly float[] RocketSpeeds = { 25f, 45f, 80f };

    [Test]
    public async Task RocketExplodesOnEveryWall()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var entMan = server.EntMan;

        var walls = new List<EntityUid>();

        await server.WaitPost(() =>
        {
            cfg.SetCVar(CVars.NetTickrate, 20);
            cfg.SetCVar(CVars.TargetMinimumTickrate, 20);

            var maps = server.System<SharedMapSystem>();
            var physics = server.System<SharedPhysicsSystem>();
            var plating = server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId;

            for (var row = 0; row < RocketSpeeds.Length * 6; row++)
            {
                var y = row * 20;
                var speed = RocketSpeeds[row / 6];
                for (var x = 0; x <= 14; x++)
                    maps.SetTile(map.Grid, new Vector2i(x, y), new Tile(plating));

                walls.Add(entMan.SpawnEntity("WallSolid", new EntityCoordinates(map.Grid, 12.5f, y + 0.5f)));

                var rocket = entMan.SpawnEntity("BulletRocket", new EntityCoordinates(map.Grid, 1.5f + row % 6 * 0.35f, y + 0.5f));
                physics.SetLinearVelocity(rocket, new Vector2(speed, 0f));
            }
        });

        await server.WaitRunTicks(40);

        await server.WaitPost(() =>
        {
            foreach (var wall in walls)
            {
                if (!entMan.EntityExists(wall))
                    continue;

                var damage = entMan.GetComponent<DamageableComponent>(wall).TotalDamage;
                Assert.That(damage.Float(), Is.GreaterThan(7f), $"{entMan.ToPrettyString(wall)} took only the impact");
            }
        });

        await pair.CleanReturnAsync();
    }
}
