// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Collections.Generic;
using System.Numerics;
using Content.Client.DeadSpace.Audio;
using Content.Client.Light.EntitySystems;
using Content.Shared.DeadSpace.Audio;
using Content.Shared.Light.Components;
using Robust.Client.Audio;
using Robust.Client.Timing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.DeadSpace.Audio;

[TestFixture]
public sealed class AreaEchoTest
{
    private const string Boundary = "AreaEchoTestBoundary";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: AreaEchoTestBoundary
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      wall:
        shape:
          !type:PhysShapeAabb
          bounds: '-0.5,-0.5,0.5,0.5'
        hard: true
        layer:
        - Impassable
";

    [TestCase(false)]
    [TestCase(true)]
    public async Task RoomSizeRoofAndGridTransform(bool highQuality)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var entMan = client.EntMan;
            var maps = client.System<SharedMapSystem>();
            var transform = client.System<SharedTransformSystem>();
            var echo = client.System<AreaEchoSystem>();
            var cfg = client.ResolveDependency<IConfigurationManager>();
            var oldQuality = cfg.GetCVar(AreaEchoCVars.HighQuality);
            cfg.SetCVar(AreaEchoCVars.HighQuality, highQuality);
            var map = maps.CreateMap(out var mapId);

            try
            {
                var grid = client.ResolveDependency<IMapManager>().CreateGridEntity(mapId);
                entMan.EnsureComponent<ImplicitRoofComponent>(grid);
                var floor = new Tile(client.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
                var tiles = new List<(Vector2i, Tile)>();
                for (var x = -16; x <= 16; x++)
                for (var y = -16; y <= 16; y++)
                    tiles.Add((new Vector2i(x, y), floor));
                maps.SetTiles(grid, grid.Comp, tiles);

                List<EntityUid> Enclose(int radius)
                {
                    var walls = new List<EntityUid>();
                    for (var x = -radius; x <= radius; x++)
                    for (var y = -radius; y <= radius; y++)
                    {
                        if (Math.Abs(x) != radius && Math.Abs(y) != radius)
                            continue;
                        walls.Add(entMan.SpawnEntity(Boundary,
                            new EntityCoordinates(grid, new Vector2(x + 0.5f, y + 0.5f))));
                    }
                    return walls;
                }

                var walls = Enclose(16);
                var largeRoom = echo.MeasureRoom(grid, Vector2i.Zero);
                Assert.That(largeRoom, Is.GreaterThanOrEqualTo(0), "An enclosed hall should reverberate.");

                var listener = new MapCoordinates(new Vector2(0.5f), mapId);
                var emitter = entMan.SpawnEntity(null, new EntityCoordinates(grid, new Vector2(0.5f)));
                var audio = client.System<AudioSystem>();
                using (var stream = client.ResolveDependency<IAudioManager>().LoadAudioRaw(new short[8000], 1, 8000))
                {
                    var sounds = new[]
                    {
                        audio.PlayStatic(stream, new EntityCoordinates(grid, new Vector2(0.5f)), null),
                        audio.PlayEntity(stream, emitter, null), // The raw streamed-audio path used by local TTS.
                        audio.PlayEntity(stream, emitter, null, AudioParams.Default.WithLoop(true)),
                        audio.PlayGlobal(stream, null),
                    };
                    try
                    {
                        for (var i = 0; i < sounds.Length; i++)
                        {
                            Assert.That(sounds[i], Is.Not.Null);
                            var (uid, sound) = sounds[i]!.Value;
                            Assert.That(echo.TryGetRoomPreset((uid, sound, entMan.GetComponent<TransformComponent>(uid)),
                                listener, out var preset), Is.True);
                            Assert.That(preset, Is.EqualTo(i == sounds.Length - 1 ? -1 : largeRoom),
                                "Positional actions, streamed TTS and machine loops must share room selection; global audio must not.");
                        }

                        var (testUid, testSound) = sounds[0]!.Value;
                        var timing = client.ResolveDependency<IClientGameTiming>();
                        var oldTick = timing.CurTick;
                        var modified = testSound.LastModifiedTick;
                        var parameters = testSound.Params;
                        var state = testSound.State;
                        var playing = testSound.Playing;
                        testSound.PlaybackPosition = 0.25f;
                        var playbackPosition = testSound.PlaybackPosition;
                        try
                        {
                            timing.CurTick = new GameTick(Math.Max(oldTick.Value, timing.LastRealTick.Value) + 10);
                            Assert.That(timing.InPrediction, Is.True);
                            for (var i = 0; i < 3; i++)
                                echo.SetAuxiliaryLocally((testUid, testSound), null);
                            Assert.Multiple(() =>
                            {
                                Assert.That(testSound.LastModifiedTick, Is.EqualTo(modified),
                                    "An acoustic effect must not cause prediction reconciliation of AudioComponent.");
                                Assert.That(testSound.NetSyncEnabled, Is.True);
                                Assert.That(testSound.Params, Is.EqualTo(parameters));
                                Assert.That(testSound.State, Is.EqualTo(state));
                                Assert.That(testSound.Playing, Is.EqualTo(playing));
                                Assert.That(testSound.PlaybackPosition, Is.EqualTo(playbackPosition));
                            });
                        }
                        finally
                        {
                            timing.CurTick = oldTick;
                        }
                    }
                    finally
                    {
                        foreach (var sound in sounds)
                        {
                            if (sound != null)
                                entMan.DeleteEntity(sound.Value.Entity);
                        }
                        entMan.DeleteEntity(emitter);
                    }
                }

                var emittingObstacle = entMan.SpawnEntity(Boundary,
                    new EntityCoordinates(grid, new Vector2(0.5f)));
                Assert.That(echo.MeasureRoom(grid, Vector2i.Zero, emittingObstacle), Is.EqualTo(largeRoom),
                    "The emitting object must not count its own collider as the boundary of a tiny room.");
                entMan.DeleteEntity(emittingObstacle);

                var partition = Enclose(2);
                Assert.That(echo.MeasureRoom(grid, Vector2i.Zero), Is.EqualTo(-1),
                    "A small room inside a hall must not inherit the hall's echo.");
                foreach (var wall in partition)
                    entMan.DeleteEntity(wall);

                transform.SetLocalRotation(grid, new Angle(0.73));
                transform.SetWorldPosition(grid, new Vector2(70f, -40f));
                Assert.That(echo.MeasureRoom(grid, Vector2i.Zero), Is.EqualTo(largeRoom),
                    "Moving and rotating the grid must not change its acoustic size.");

                entMan.RemoveComponent<ImplicitRoofComponent>(grid);
                var roof = entMan.EnsureComponent<RoofComponent>(grid);
                Assert.That(echo.MeasureRoom(grid, Vector2i.Zero), Is.EqualTo(-1),
                    "An unroofed courtyard must not reverberate.");
                var roofs = client.System<RoofSystem>();
                roofs.SetRoof((grid.Owner, grid.Comp, roof), Vector2i.Zero, true);
                Assert.That(echo.MeasureRoom(grid, Vector2i.Zero), Is.EqualTo(-1),
                    "One covered tile is insufficient when the surrounding room is unroofed.");
                foreach (var (tile, _) in tiles)
                    roofs.SetRoof((grid.Owner, grid.Comp, roof), tile, true);
                Assert.That(echo.MeasureRoom(grid, Vector2i.Zero), Is.EqualTo(largeRoom));

                maps.SetTile(grid, grid.Comp, Vector2i.Zero, Tile.Empty);
                Assert.That(echo.MeasureRoom(grid, Vector2i.Zero), Is.EqualTo(-1),
                    "A sound over a hole in the floor must not acquire room echo.");
                maps.SetTile(grid, grid.Comp, Vector2i.Zero, floor);

                foreach (var wall in walls)
                    entMan.DeleteEntity(wall);
                Assert.That(echo.MeasureRoom(grid, Vector2i.Zero), Is.EqualTo(-1),
                    "Rays reaching the edge of an open platform must escape.");
            }
            finally
            {
                entMan.DeleteEntity(map);
                cfg.SetCVar(AreaEchoCVars.HighQuality, oldQuality);
            }
        });

        await pair.CleanReturnAsync();
    }
}
