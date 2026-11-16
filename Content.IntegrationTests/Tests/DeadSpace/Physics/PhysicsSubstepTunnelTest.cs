// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14/master/LICENSE.TXT

using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Throwing;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Systems;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.DeadSpace.Physics;

[TestFixture]
public sealed class PhysicsSubstepTunnelTest
{
    private static readonly float[] BulletSpeeds = { 25f, 35f, 40f, 60f, 80f, 100f };
    private static readonly float[] ThrowSpeeds = { 10f, 15f, 20f, 25f, 30f };
    private static readonly float[] MobSpeeds = { 6f, 10f, 15f, 20f, 30f };
    private static readonly float[] PressSpeeds = { 8f, 10f, 12f, 15f, 20f };
    private static readonly float[] StartOffsets = { 2.5f, 2.8f, 3.1f, 3.4f, 3.7f };
    private const float WallX = 8f;

    [Test]
    public async Task BulletsAndThrowsStopAtOneTileWall()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var map = await pair.CreateTestMap();
        var passes = new List<string>();
        var runs = 0;

        await server.WaitPost(() =>
        {
            var maps = server.System<SharedMapSystem>();
            var plating = server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId;
            for (var x = -2; x <= 20; x++)
            {
                for (var y = -4; y <= 4; y++)
                {
                    maps.SetTile(map.Grid, new Vector2i(x, y), new Tile(plating));
                }
            }

            for (var y = -4; y <= 4; y++)
            {
                entMan.SpawnEntity("WallSolidIndestructible", new EntityCoordinates(map.Grid, WallX + 0.5f, y + 0.5f));
            }

            cfg.SetCVar(CVars.NetTickrate, 20);
        });

        foreach (var minTickrate in new[] { 20, 25 })
        {
            await server.WaitPost(() => cfg.SetCVar(CVars.TargetMinimumTickrate, minTickrate));
            var substeps = (int) Math.Ceiling(minTickrate / 20f);

            foreach (var start in StartOffsets)
            {
                foreach (var speed in BulletSpeeds)
                {
                    runs++;
                    if (await Fire(server, map.Grid, "BulletPistol", speed, start, false))
                        passes.Add($"substeps {substeps}, start {start}, bullet {speed}");
                }

                foreach (var speed in ThrowSpeeds)
                {
                    runs++;
                    if (await Fire(server, map.Grid, "Crowbar", speed, start, true))
                        passes.Add($"substeps {substeps}, start {start}, thrown crowbar {speed}");
                }
            }
        }

        foreach (var minTickrate in new[] { 20, 25 })
        {
            await server.WaitPost(() => cfg.SetCVar(CVars.TargetMinimumTickrate, minTickrate));
            var substeps = (int) Math.Ceiling(minTickrate / 20f);

            foreach (var start in StartOffsets)
            {
                foreach (var speed in MobSpeeds)
                {
                    runs++;
                    if (await Run(server, map.Grid, speed, start, 0))
                        passes.Add($"substeps {substeps}, start {start}, mob {speed}");
                }
            }

            foreach (var speed in PressSpeeds)
            {
                runs++;
                if (await Run(server, map.Grid, speed, WallX - 0.6f, 200))
                    passes.Add($"substeps {substeps}, mob pressing the wall at {speed}");
            }
        }

        Console.WriteLine($"{runs} shots, {passes.Count} passed the wall");
        foreach (var line in passes)
            Console.WriteLine(line);

        Assert.That(passes, Is.Empty, string.Join("; ", passes));
        await pair.CleanReturnAsync();
    }

    private static async Task<bool> Run(
        RobustIntegrationTest.ServerIntegrationInstance server,
        EntityUid grid,
        float speed,
        float start,
        int pressTicks)
    {
        var entMan = server.EntMan;
        EntityUid uid = default;
        var direction = new Vector2(1f, 0f);

        await server.WaitPost(() =>
        {
            uid = entMan.SpawnEntity("MobHuman", new EntityCoordinates(grid, start, 0.5f));
        });

        var ticks = pressTicks > 0 ? pressTicks : (int) Math.Ceiling(20f * 14f / speed) + 10;
        for (var i = 0; i < ticks; i++)
        {
            await server.WaitPost(() => server.System<SharedPhysicsSystem>().SetLinearVelocity(uid, direction * speed));
            await server.WaitRunTicks(1);
        }

        var passed = false;
        await server.WaitPost(() =>
        {
            var x = server.System<SharedTransformSystem>().GetWorldPosition(uid).X;
            passed = x > WallX + 1.2f;
            entMan.DeleteEntity(uid);
        });

        return passed;
    }

    private static async Task<bool> Fire(
        RobustIntegrationTest.ServerIntegrationInstance server,
        EntityUid grid,
        string prototype,
        float speed,
        float start,
        bool thrown)
    {
        var entMan = server.EntMan;
        EntityUid uid = default;
        var direction = new Vector2(1f, 0f);

        await server.WaitPost(() =>
        {
            uid = entMan.SpawnEntity(prototype, new EntityCoordinates(grid, start, 0.5f));
            if (thrown)
            {
                server.System<ThrowingSystem>().TryThrow(uid, direction * 14f, baseThrowSpeed: speed, doSpin: false);
            }
            else
            {
                server.System<SharedTransformSystem>().SetWorldRotation(uid, direction.ToWorldAngle());
                server.System<SharedPhysicsSystem>().SetLinearVelocity(uid, direction * speed);
            }
        });

        var ticks = (int) Math.Ceiling(20f * 14f / speed) + 10;
        await server.WaitRunTicks(ticks);

        var passed = false;
        await server.WaitPost(() =>
        {
            if (!entMan.Deleted(uid))
            {
                var x = server.System<SharedTransformSystem>().GetWorldPosition(uid).X;
                passed = x > WallX + 1.2f;
                entMan.DeleteEntity(uid);
            }
        });

        return passed;
    }
}
