// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Content.Client.DeadSpace.CameraArchives;
using Content.Client.Eye;
using Content.Server.DeadSpace.CameraArchives;
using Content.Server.Decals;
using Content.Shared.Atmos.Piping.Unary.Visuals;
using Content.Shared.DeadSpace.CameraArchives;
using Content.Shared.Decals;
using Content.Shared.Maps;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.DeadSpace.CameraArchives;

[TestFixture]
public sealed class CameraArchiveMatchTest
{
    [Test]
    public async Task PlaybackMatchesLivePoseAndSprites()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;

        var map = await pair.CreateTestMap();
        var grid = map.Grid;
        var gridUid = grid.Owner;

        var tiles = server.ResolveDependency<ITileDefinitionManager>();
        var plating = tiles["Plating"].TileId;
        var floor = tiles["FloorSteel"].TileId;

        EntityUid camera = default;
        await server.WaitPost(() =>
        {
            var mapSys = server.System<SharedMapSystem>();
            var xform = server.System<SharedTransformSystem>();
            var appearance = server.System<SharedAppearanceSystem>();
            var decals = server.System<DecalSystem>();

            var area = new List<(Vector2i, Tile)>();
            for (var x = 0; x <= 5; x++)
            {
                for (var y = 0; y <= 4; y++)
                    area.Add((new Vector2i(x, y), new Tile(plating)));
            }

            mapSys.SetTiles(gridUid, grid.Comp, area);

            EntityUid Spawn(string proto, float x, float y)
            {
                return server.EntMan.SpawnEntity(proto, new EntityCoordinates(gridUid, x, y));
            }

            Spawn("WallSolid", 1.5f, 1.5f);
            Spawn("WallSolid", 2.5f, 1.5f);
            Spawn("WallSolid", 1.5f, 2.5f);
            Spawn("WallSolidDiagonal", 4.5f, 2.5f);
            var scrubber = Spawn("GasVentScrubber", 3.5f, 1.5f);
            var chair = Spawn("ChairWood", 2.5f, 2.5f);
            Spawn("PoweredSmallLight", 3.5f, 3.5f);
            camera = Spawn("SurveillanceCameraSecurity", 0.5f, 1.5f);

            xform.SetLocalRotation(chair, Angle.FromDegrees(90));
            appearance.SetData(scrubber, ScrubberVisuals.State, ScrubberState.Scrub);

            for (var i = 0; i < area.Count; i++)
            {
                var index = area[i].Item1;
                var mirror = index == new Vector2i(2, 0) ? (byte) 2 : (byte) 0;
                area[i] = (index, new Tile(floor, 0, 0, mirror));
            }

            mapSys.SetTiles(gridUid, grid.Comp, area);
            Assert.That(decals.TryAddDecal("MiniTileSteelBox", new EntityCoordinates(gridUid, 2.5f, 0.5f), out _, rotation: Angle.FromDegrees(90)), Is.True);
            xform.SetWorldRotation(gridUid, Angle.FromDegrees(90));
        });

        await pair.RunTicksSync(20);

        await client.WaitPost(() => client.System<CameraArchivePlaybackSystem>().Settle());

        CameraArchive still = null;
        await server.WaitAssertion(() =>
        {
            still = server.System<CameraArchiveSystem>().Capture(camera);
            Assert.That(still, Is.Not.Null);
            Assert.That(still!.Entities, Is.Not.Empty);
            Assert.That(still.Tiles, Is.Not.Empty);
            Assert.That(still.Decals, Is.Empty);
            Assert.That(still.GridRotation.Degrees, Is.EqualTo(90).Within(0.01));
        });

        CameraArchiveView view = null;
        await client.WaitAssertion(() =>
        {
            var playback = client.System<CameraArchivePlaybackSystem>();
            view = playback.Play(still!);
            playback.Settle();

            var clientEnt = client.EntMan;
            var xformSys = client.System<SharedTransformSystem>();
            Assert.That(view.Eye.Rotation.Theta, Is.EqualTo(0).Within(0.0001));
            Assert.That(xformSys.GetWorldRotation(view.Grid).Degrees, Is.EqualTo(0).Within(0.01));

            var sourceGrid = clientEnt.GetEntity(server.EntMan.GetNetEntity(gridUid));
            var origin = CameraArchive.PlaybackOrigin(still!.CameraLocal);
            var live = DescribeGrid(clientEnt, sourceGrid, origin);
            var copy = DescribeGrid(clientEnt, view.Grid, Vector2.Zero);
            Assert.That(copy, Is.EqualTo(live), "playback sprites, pose, and rotation diverged from the live camera scene");

            Assert.That(clientEnt.TryGetComponent(view.Grid, out MapGridComponent copyGrid), Is.True);
            var mapSys = client.System<SharedMapSystem>();
            foreach (var tile in still.Tiles)
            {
                var shifted = tile.Indices - new Vector2i((int) origin.X, (int) origin.Y);
                var copyTile = mapSys.GetTileRef(view.Grid, copyGrid!, shifted).Tile;
                Assert.That(copyTile.TypeId, Is.EqualTo(tile.TypeId), $"tile {tile.Indices} type");
                Assert.That(copyTile.Variant, Is.EqualTo(tile.Variant), $"tile {tile.Indices} variant");
                Assert.That(copyTile.RotationMirroring, Is.EqualTo(tile.RotationMirroring), $"tile {tile.Indices} rotation");
            }

            playback.Clear();
        });

        await pair.CleanReturnAsync();
    }

    private static List<string> DescribeGrid(IEntityManager entities, EntityUid grid, Vector2 origin)
    {
        var list = new List<string>();
        var xform = entities.GetComponent<TransformComponent>(grid);
        var children = xform.ChildEnumerator;
        while (children.MoveNext(out var child))
            Walk(entities, grid, child, origin, list);

        list.Sort(StringComparer.Ordinal);
        return list;
    }

    private static void Walk(IEntityManager entities, EntityUid grid, EntityUid uid, Vector2 origin, List<string> list)
    {
        var xform = entities.GetComponent<TransformComponent>(uid);
        var pos = xform.ParentUid == grid ? xform.LocalPosition - origin : xform.LocalPosition;
        list.Add(Describe(entities, uid, pos));
        var children = xform.ChildEnumerator;
        while (children.MoveNext(out var child))
            Walk(entities, grid, child, origin, list);
    }

    private static string Describe(IEntityManager entities, EntityUid uid, Vector2 position)
    {
        var meta = entities.GetComponent<MetaDataComponent>(uid);
        var xform = entities.GetComponent<TransformComponent>(uid);
        var text = new StringBuilder();
        text.Append(meta.EntityPrototype?.ID ?? "?");
        text.Append('|');
        text.Append(position.X.ToString("F3", CultureInfo.InvariantCulture));
        text.Append(',');
        text.Append(position.Y.ToString("F3", CultureInfo.InvariantCulture));
        text.Append('|');
        text.Append(xform.LocalRotation.Theta.ToString("F4", CultureInfo.InvariantCulture));
        text.Append('|');
        text.Append(xform.Anchored ? '1' : '0');

        if (!entities.TryGetComponent(uid, out SpriteComponent sprite))
            return text.ToString();

        foreach (var layer in sprite.AllLayers)
        {
            text.Append('|');
            text.Append(layer.Rsi?.Path.ToString() ?? layer.ActualRsi?.Path.ToString() ?? "");
            text.Append(':');
            text.Append(layer.RsiState.Name ?? "");
            text.Append(':');
            text.Append(layer.Visible ? '1' : '0');
            text.Append(':');
            text.Append(layer.DirOffset);
            text.Append(':');
            text.Append(layer.Rotation.Theta.ToString("F3", CultureInfo.InvariantCulture));
            text.Append(':');
            text.Append(layer.Scale.X.ToString("F3", CultureInfo.InvariantCulture));
            text.Append(',');
            text.Append(layer.Scale.Y.ToString("F3", CultureInfo.InvariantCulture));
            text.Append(':');
            text.Append(layer.Color.R.ToString("F3", CultureInfo.InvariantCulture));
            text.Append(',');
            text.Append(layer.Color.G.ToString("F3", CultureInfo.InvariantCulture));
            text.Append(',');
            text.Append(layer.Color.B.ToString("F3", CultureInfo.InvariantCulture));
            text.Append(',');
            text.Append(layer.Color.A.ToString("F3", CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }
}
