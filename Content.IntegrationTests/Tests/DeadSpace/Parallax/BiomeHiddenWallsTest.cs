// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14/master/LICENSE.TXT

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Server.Atmos.Components;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Parallax;
using Content.Shared.Parallax.Biomes;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.DeadSpace.Parallax;

[TestFixture]
public sealed class BiomeHiddenWallsTest
{
    [Test]
    public async Task HiddenRockStaysClosed()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var session = await server.AddDummySession();
        string report = null;

        await server.WaitAssertion(() =>
        {
            server.ResolveDependency<IConfigurationManager>().SetCVar(CVars.NetMaxUpdateRange, 24f);
            var entMan = server.EntMan;
            var maps = server.System<SharedMapSystem>();
            var xforms = server.System<SharedTransformSystem>();
            var biomeSys = server.System<BiomeSystem>();
            ProtoId<BiomeTemplatePrototype> templateId = "LavalandSurface";
            var template = server.ProtoMan.Index(templateId);

            var plain = LoadMap(server, maps, biomeSys, template, false, session, out var plainMapId);
            var plainWalls = CountWalls(entMan, plain);
            maps.DeleteMap(plainMapId);

            var lazy = LoadMap(server, maps, biomeSys, template, true, session, out var lazyMapId);
            var lazyWalls = CountWalls(entMan, lazy);
            var hidden = Hidden(biomeSys, lazy);
            var grid = entMan.GetComponent<MapGridComponent>(lazy);
            var biome = entMan.GetComponent<BiomeComponent>(lazy);

            Assert.That(hidden.Count, Is.GreaterThan(0), "nothing was hidden");
            Assert.That(lazyWalls, Is.LessThan(plainWalls));
            var leaks = Leaks(entMan, maps, biome, grid, lazy, hidden);
            Assert.That(leaks, Is.EqualTo(0), "a hidden tile touches open floor after load");

            var wall = FindExposedWall(entMan, maps, grid, lazy, hidden);
            Assert.That(wall, Is.Not.Null, "no wall next to hidden rock");
            var wallTile = maps.LocalToTile(lazy, grid, entMan.GetComponent<TransformComponent>(wall.Value).Coordinates);
            entMan.DeleteEntity(wall.Value);
            biomeSys.Update(0.05f);
            Assert.That(Leaks(entMan, maps, biome, grid, lazy, hidden), Is.EqualTo(0), "a hidden tile touches the broken wall");
            Assert.That(HiddenAround(hidden, wallTile), Is.False);

            var mob = session.AttachedEntity!.Value;
            var target = hidden.Keys.First();
            xforms.SetCoordinates(mob, new EntityCoordinates(lazy, target.X + 0.5f, target.Y + 0.5f));
            for (var i = 0; i < 20; i++)
                biomeSys.Update(0.05f);

            Assert.That(HiddenAround(hidden, target), Is.False, "rock around a teleported mob is still hidden");

            report = $"plain walls {plainWalls}, lazy walls {lazyWalls}, hidden {hidden.Count}";
            Console.WriteLine(report);
            maps.DeleteMap(lazyMapId);
        });

        Assert.That(report, Is.Not.Null);
        await pair.CleanReturnAsync();
    }

    private static EntityUid LoadMap(
        RobustIntegrationTest.ServerIntegrationInstance server,
        SharedMapSystem maps,
        BiomeSystem biomeSys,
        BiomeTemplatePrototype template,
        bool lazy,
        Robust.Shared.Player.ICommonSession session,
        out MapId mapId)
    {
        var entMan = server.EntMan;
        var map = maps.CreateMap(out mapId);
        if (lazy)
            entMan.AddComponent<LavalandMapComponent>(map);

        biomeSys.EnsurePlanet(map, template, 1);
        var mob = entMan.SpawnEntity("MobHuman", new EntityCoordinates(map, 0.5f, 0.5f));
        server.PlayerMan.SetAttachedEntity(session, mob);
        var biome = entMan.GetComponent<BiomeComponent>(map);
        for (var i = 0; i < 400 && biome.LoadedChunks.Count < 30; i++)
            biomeSys.Update(0.05f);

        return map;
    }

    private static int CountWalls(IEntityManager entMan, EntityUid map)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<AirtightComponent, TransformComponent>();
        while (query.MoveNext(out _, out var xform))
        {
            if (xform.GridUid == map)
                count++;
        }

        return count;
    }

    private static Dictionary<Vector2i, string> Hidden(BiomeSystem biomeSys, EntityUid map)
    {
        var field = typeof(BiomeSystem).GetField("_hiddenWalls", BindingFlags.Instance | BindingFlags.NonPublic);
        var all = (Dictionary<EntityUid, Dictionary<Vector2i, string>>) field.GetValue(biomeSys);
        return all.TryGetValue(map, out var hidden) ? hidden : new Dictionary<Vector2i, string>();
    }

    private static bool IsWall(IEntityManager entMan, SharedMapSystem maps, MapGridComponent grid, EntityUid map, Vector2i tile)
    {
        var anchored = maps.GetAnchoredEntitiesEnumerator(map, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (entMan.HasComponent<AirtightComponent>(uid.Value))
                return true;
        }

        return false;
    }

    private static int Leaks(
        IEntityManager entMan,
        SharedMapSystem maps,
        BiomeComponent biome,
        MapGridComponent grid,
        EntityUid map,
        Dictionary<Vector2i, string> hidden)
    {
        var loaded = Enumerable.ToHashSet(biome.LoadedChunks);
        var leaks = 0;
        foreach (var tile in hidden.Keys)
        {
            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    var near = new Vector2i(tile.X + x, tile.Y + y);
                    var origin = new Vector2i((int) MathF.Floor(near.X / 8f) * 8, (int) MathF.Floor(near.Y / 8f) * 8);
                    if (!loaded.Contains(origin))
                        continue;

                    if (!hidden.ContainsKey(near) && !IsWall(entMan, maps, grid, map, near))
                        leaks++;
                }
            }
        }

        return leaks;
    }

    private static EntityUid? FindExposedWall(
        IEntityManager entMan,
        SharedMapSystem maps,
        MapGridComponent grid,
        EntityUid map,
        Dictionary<Vector2i, string> hidden)
    {
        foreach (var tile in hidden.Keys)
        {
            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    var near = new Vector2i(tile.X + x, tile.Y + y);
                    var anchored = maps.GetAnchoredEntitiesEnumerator(map, grid, near);
                    while (anchored.MoveNext(out var uid))
                    {
                        if (entMan.HasComponent<AirtightComponent>(uid.Value))
                            return uid.Value;
                    }
                }
            }
        }

        return null;
    }

    private static bool HiddenAround(Dictionary<Vector2i, string> hidden, Vector2i tile)
    {
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (hidden.ContainsKey(new Vector2i(tile.X + x, tile.Y + y)))
                    return true;
            }
        }

        return false;
    }
}
