// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using Content.Server.Mind;
using Content.Server.Spawners.Components;
using Content.Shared.Examine;
using Content.Shared.Maps;
using Content.Shared.Warps;
using Robust.Shared;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.DeadSpace.Diagnostics;

[TestFixture]
[NonParallelizable]
public sealed class PvsCostBenchmarkTest
{
    private const int Players = 70;
    private const int SightPlayers = 20;
    private const int Ticks = 100;
    private const float ChunkSize = 8f;
    private const float WalkPerTick = 0.25f;

    private static readonly string[] Maps =
    [
        "Box",
        "DsTram2",
    ];

    private static readonly (float Range, float Priority, float Detail)[] Ranges =
    [
        (24f, 30f, 0f),
        (24f, 30f, 4f),
        (24f, 30f, 0f),
        (24f, 30f, 4f),
    ];

    private static readonly Vector2[] Steps =
    [
        new(1f, 0f),
        new(-1f, 0f),
        new(0f, 1f),
        new(0f, -1f),
    ];

    [Explicit]
    [Test, TestCaseSource(nameof(Maps))]
    public async Task PvsCost(string map)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var cfg = server.CfgMan;
        var xforms = server.System<SharedTransformSystem>();

        await server.WaitPost(() =>
        {
            cfg.SetCVar(CVars.NetPVS, true);
            cfg.SetCVar(CVars.NetMaxUpdateRange, 24f);
            cfg.SetCVar(CVars.NetPvsPriorityRange, 30f);
            var path = server.ProtoMan.Index<GameMapPrototype>(map).MapPath;
            var opts = DeserializationOptions.Default with { InitializeMaps = true };
            Assert.That(server.System<MapLoaderSystem>().TryLoadMap(path, out _, out _, opts), $"Map {path} did not load.");
        });

        await server.WaitRunTicks(60);

        var players = await server.AddDummySessions(Players);
        var mobs = new EntityUid[Players];
        var homes = new EntityCoordinates[Players];
        await server.WaitPost(() =>
        {
            var spots = Spots(entMan);
            Assert.That(spots, Is.Not.Empty, "No spawn or warp points on the map.");
            var mind = server.System<MindSystem>();
            for (var i = 0; i < Players; i++)
            {
                homes[i] = spots[i % spots.Count];
                mobs[i] = entMan.SpawnAttachedTo("MobHuman", homes[i]);
                mind.ControlMob(players[i].UserId, mobs[i]);
            }
        });

        await server.WaitRunTicks(5);

        var report = new StringBuilder();
        report.AppendLine($"pvs cost: map {map}, {Players} players, {Ticks} ticks per row, states are built but not sent");
        report.AppendLine("   range/priority/detail   parallel ms/tick   one thread ms/tick   walking parallel ms/tick");
        foreach (var (range, priority, detail) in Ranges)
        {
            await server.WaitPost(() =>
            {
                cfg.SetCVar(CVars.NetMaxUpdateRange, range);
                cfg.SetCVar(CVars.NetPvsPriorityRange, priority);
                cfg.SetCVar(CVars.NetPvsDetailRange, detail);
            });

            var parallel = Measure(server, players);

            await server.WaitPost(() =>
            {
                cfg.SetCVar(CVars.ThreadParallelCount, 1);
                cfg.SetCVar(CVars.NetPvsAsync, false);
            });

            var single = Measure(server, players);

            await server.WaitPost(() =>
            {
                cfg.SetCVar(CVars.ThreadParallelCount, 0);
                cfg.SetCVar(CVars.NetPvsAsync, true);
            });

            var walking = await MeasureWalking(server, players, mobs, homes, xforms);
            report.AppendLine($"   {range,5:F0}/{priority:F0}/{detail,-5:F0}     {parallel,10:F2}         {single,10:F2}             {walking,10:F2}");
        }

        await server.WaitPost(() =>
        {
            cfg.SetCVar(CVars.NetMaxUpdateRange, 24f);
            cfg.SetCVar(CVars.NetPvsPriorityRange, 30f);
            AppendSight(report, entMan, xforms, mobs);
        });

        TestContext.Out.WriteLine(report.ToString());
        await pair.CleanReturnAsync();
    }

    private static List<EntityCoordinates> Spots(IEntityManager entMan)
    {
        var spots = new List<EntityCoordinates>();
        var seen = new HashSet<Vector2i>();
        var spawns = entMan.EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (spawns.MoveNext(out _, out _, out var xform))
        {
            Add(xform);
        }

        var warps = entMan.EntityQueryEnumerator<WarpPointComponent, TransformComponent>();
        while (warps.MoveNext(out _, out _, out var xform))
        {
            Add(xform);
        }

        return spots;

        void Add(TransformComponent xform)
        {
            if (xform.GridUid == null)
                return;

            if (seen.Add(new Vector2i((int) MathF.Floor(xform.LocalPosition.X), (int) MathF.Floor(xform.LocalPosition.Y))))
                spots.Add(xform.Coordinates);
        }
    }

    private static double Measure(RobustIntegrationTest.ServerIntegrationInstance server, ICommonSession[] players)
    {
        for (var i = 0; i < 60; i++)
        {
            server.PvsTick(players);
        }

        var watch = Stopwatch.StartNew();
        for (var i = 0; i < Ticks; i++)
        {
            server.PvsTick(players);
        }

        return watch.Elapsed.TotalMilliseconds / Ticks;
    }

    private static async Task<double> MeasureWalking(
        RobustIntegrationTest.ServerIntegrationInstance server,
        ICommonSession[] players,
        EntityUid[] mobs,
        EntityCoordinates[] homes,
        SharedTransformSystem xforms)
    {
        var watch = new Stopwatch();
        for (var tick = 0; tick < Ticks; tick++)
        {
            var step = tick;
            await server.WaitPost(() =>
            {
                for (var i = 0; i < mobs.Length; i++)
                {
                    var offset = Steps[i % Steps.Length] * (WalkPerTick * (step + 1));
                    xforms.SetCoordinates(mobs[i], homes[i].Offset(offset));
                }
            });

            watch.Start();
            server.PvsTick(players);
            watch.Stop();
        }

        await server.WaitPost(() =>
        {
            for (var i = 0; i < mobs.Length; i++)
            {
                xforms.SetCoordinates(mobs[i], homes[i]);
            }
        });

        server.PvsTick(players);
        return watch.Elapsed.TotalMilliseconds / Ticks;
    }

    private static void AppendSight(StringBuilder report, IEntityManager entMan, SharedTransformSystem xforms, EntityUid[] mobs)
    {
        var lookup = entMan.System<EntityLookupSystem>();
        var examine = entMan.System<ExamineSystemShared>();
        const float view = 15f;
        var half = view + ChunkSize + 2f;
        var near = new HashSet<EntityUid>();

        long box = 0;
        long seen = 0;
        long chunkSeen = 0;
        long boxChurn = 0;
        long sightChurn = 0;
        long chunkChurn = 0;
        long stepCount = 0;
        var count = Math.Min(SightPlayers, mobs.Length);
        var timer = Stopwatch.StartNew();

        for (var p = 0; p < count; p++)
        {
            var origin = xforms.GetMapCoordinates(mobs[p]);
            near.Clear();
            lookup.GetEntitiesIntersecting(origin.MapId,
                Box2.CenteredAround(origin.Position, new Vector2(half * 2f)),
                near,
                LookupFlags.Uncontained);
            near.Remove(mobs[p]);

            var targets = near
                .Select(uid => xforms.GetMapCoordinates(uid))
                .ToArray();

            var here = Sight(origin);
            box += here.Box.Count;
            seen += here.Seen.Count;
            chunkSeen += here.ChunkSeen.Count;

            foreach (var step in Steps)
            {
                var next = new MapCoordinates(origin.Position + step, origin.MapId);
                if (!examine.InRangeUnOccluded(origin, next, 0f, null))
                    continue;

                var there = Sight(next);
                boxChurn += Churn(here.Box, there.Box);
                sightChurn += Churn(here.Seen, there.Seen);
                chunkChurn += Churn(here.ChunkSeen, there.ChunkSeen);
                stepCount++;
            }

            (HashSet<int> Box, HashSet<int> Seen, HashSet<int> ChunkSeen) Sight(MapCoordinates eye)
            {
                var inBox = new HashSet<int>();
                var inSight = new HashSet<int>();
                var chunks = new Dictionary<Vector2i, List<int>>();
                var litChunks = new HashSet<Vector2i>();
                for (var i = 0; i < targets.Length; i++)
                {
                    var pos = targets[i].Position;
                    var chunk = new Vector2i((int) MathF.Floor(pos.X / ChunkSize), (int) MathF.Floor(pos.Y / ChunkSize));
                    var min = new Vector2(chunk.X, chunk.Y) * ChunkSize;
                    var max = min + new Vector2(ChunkSize);
                    if (max.X < eye.Position.X - view || min.X > eye.Position.X + view ||
                        max.Y < eye.Position.Y - view || min.Y > eye.Position.Y + view)
                    {
                        continue;
                    }

                    inBox.Add(i);
                    if (!chunks.TryGetValue(chunk, out var list))
                        chunks[chunk] = list = new List<int>();
                    list.Add(i);

                    if (examine.InRangeUnOccluded(eye, targets[i], 0f, null))
                    {
                        inSight.Add(i);
                        litChunks.Add(chunk);
                    }
                }

                var inChunks = new HashSet<int>();
                foreach (var chunk in litChunks)
                {
                    inChunks.UnionWith(chunks[chunk]);
                }

                return (inBox, inSight, inChunks);
            }
        }

        double Per(long value, long total) => total > 0 ? (double) value / total * 100d : 0d;
        double Avg(long value, long total) => total > 0 ? (double) value / total : 0d;

        report.AppendLine($"sight: {count} players, top level entities only, box side 30 + chunk, {timer.Elapsed.TotalSeconds:F1} s");
        report.AppendLine($"   in box {Avg(box, count):F0} per player, in line of sight {Per(seen, box):F0}%, " +
                          $"in chunks with anything in sight {Per(chunkSeen, box):F0}%");
        report.AppendLine($"   change per tile step: box {Avg(boxChurn, stepCount):F1}, line of sight {Avg(sightChurn, stepCount):F1}, " +
                          $"chunks with sight {Avg(chunkChurn, stepCount):F1} entities ({stepCount} steps)");
    }

    private static int Churn(HashSet<int> from, HashSet<int> to)
    {
        var changed = 0;
        foreach (var i in from)
        {
            if (!to.Contains(i))
                changed++;
        }

        foreach (var i in to)
        {
            if (!from.Contains(i))
                changed++;
        }

        return changed;
    }
}
