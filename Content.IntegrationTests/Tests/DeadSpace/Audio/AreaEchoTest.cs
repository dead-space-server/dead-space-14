// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Collections.Generic;
using System.Numerics;
using Content.Client.DeadSpace.Audio;
using Content.Client.Light.EntitySystems;
using Content.Shared.DeadSpace.Audio;
using Content.Shared.Audio.Jukebox;
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
- type: entity
  id: AreaEchoTestFurniture
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      furniture:
        shape:
          !type:PhysShapeAabb
          bounds: '-0.4,-0.4,0.4,0.4'
        hard: true
        layer: [MidImpassable]
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

                // Stereo jukebox gain must preserve the engine's hard mute and taper before the boundary.
                var jukebox = entMan.EnsureComponent<JukeboxComponent>(emitter = entMan.SpawnEntity(null,
                    new EntityCoordinates(grid, new Vector2(0.5f))));
                using (var stream = client.ResolveDependency<IAudioManager>().LoadAudioRaw(new short[16000], 2, 8000))
                {
                    var (uid, sound) = audio.PlayEntity(stream, emitter, null,
                        AudioParams.Default.WithMaxDistance(10f))!.Value;
                    typeof(JukeboxComponent).GetField(nameof(JukeboxComponent.AudioStream))!.SetValue(jukebox, uid);
                    Assert.That(echo.TryGetRoomPreset((uid, sound, entMan.GetComponent<TransformComponent>(uid)),
                        listener, out var preset), Is.True);
                    Assert.That(preset, Is.EqualTo(-1), "Music must not enter the room reverb send.");
                    sound.Gain = 0f;
                    sound.Volume = float.NegativeInfinity;
                    client.System<Content.Client.Audio.Jukebox.JukeboxSystem>().FrameUpdate(0.016f);
                    Assert.That(sound.Volume, Is.EqualTo(float.NegativeInfinity), "Music volume must not resurrect an out-of-range engine stream.");
                    entMan.DeleteEntity(uid);
                }
                entMan.DeleteEntity(emitter);

                var emittingObstacle = entMan.SpawnEntity(Boundary,
                    new EntityCoordinates(grid, new Vector2(0.5f)));
                Assert.That(echo.MeasureRoom(grid, Vector2i.Zero, emittingObstacle), Is.EqualTo(largeRoom),
                    "The emitting object must not count its own collider as the boundary of a tiny room.");
                entMan.DeleteEntity(emittingObstacle);

                var hall = echo.MeasureAcoustics(grid, Vector2i.Zero);
                var hallPreset = AreaEchoSystem.CreatePreset(hall, listener.Position, Matrix3x2.Identity, Angle.Zero, 1f);
                var nearWallPosition = new Vector2(-13.5f, 0.5f);
                var nearWall = echo.MeasureAcoustics(grid, new Vector2i(-14, 0));
                var nearWallPreset = AreaEchoSystem.CreatePreset(nearWall, nearWallPosition, Matrix3x2.Identity, Angle.Zero, 1f);
                Assert.That(nearWallPreset.ReflectionsDelay, Is.LessThan(hallPreset.ReflectionsDelay),
                    "Approaching a wall must bring the first reflection closer in time.");
                Assert.That(nearWallPreset.ReflectionsPan.X, Is.LessThan(0f));
                Assert.That(nearWallPreset.LateReverbPan.X, Is.GreaterThan(0f),
                    "The nearby wall and the distant room must not share one reflection direction.");
                Assert.That(hallPreset.ReflectionsPan.Length(), Is.LessThan(0.01f),
                    "A symmetric hall must not pick an arbitrary wall as the dominant reflector.");

                var partition = Enclose(2);
                var closedRoom = echo.MeasureAcoustics(grid, Vector2i.Zero);
                Assert.That(closedRoom.Preset, Is.EqualTo(-1),
                    "A small room inside a hall must not inherit the hall's echo.");
                var doorPosition = new Vector2(-1.5f, 0.5f);
                var doorway = partition.Find(wall => entMan.GetComponent<TransformComponent>(wall).LocalPosition == doorPosition);
                partition.Remove(doorway);
                entMan.DeleteEntity(doorway);
                var openRoom = echo.MeasureAcoustics(grid, Vector2i.Zero);
                var openPreset = AreaEchoSystem.CreatePreset(openRoom, listener.Position, Matrix3x2.Identity, Angle.Zero, 1f);
                Assert.That(openRoom.Strength, Is.GreaterThan(closedRoom.Strength).And.LessThan(hall.Strength * 0.5f),
                    "A small room hears a weaker tail through its doorway, not the full surrounding hall.");
                Assert.That(openRoom.Size, Is.LessThan(hall.Size * 0.25f));
                Assert.That(openPreset.LateReverbPan.X, Is.LessThan(-0.1f),
                    "The remote tail must arrive through the open doorway on the left.");
                Assert.That(openPreset.ReflectionsDelay, Is.LessThan(hallPreset.ReflectionsDelay));
                partition.Add(entMan.SpawnEntity(Boundary, new EntityCoordinates(grid, doorPosition)));
                Assert.That(echo.MeasureAcoustics(grid, Vector2i.Zero).Strength, Is.EqualTo(closedRoom.Strength),
                    "Closing the doorway must remove the remote tail.");
                foreach (var wall in partition)
                    entMan.DeleteEntity(wall);

                transform.SetLocalRotation(grid, new Angle(0.73));
                transform.SetWorldPosition(grid, new Vector2(70f, -40f));
                Assert.That(echo.MeasureRoom(grid, Vector2i.Zero), Is.EqualTo(largeRoom),
                    "Moving and rotating the grid must not change its acoustic size.");
                var gridMatrix = transform.GetWorldMatrix(grid);
                var rotatedPreset = AreaEchoSystem.CreatePreset(echo.MeasureAcoustics(grid, new Vector2i(-14, 0)),
                    Vector2.Transform(nearWallPosition, gridMatrix), gridMatrix, new Angle(-0.73), 1f);
                Assert.That(Vector3.Distance(rotatedPreset.ReflectionsPan, nearWallPreset.ReflectionsPan), Is.LessThan(0.01f),
                    "Camera rotation must keep reflections aligned with the visible walls.");

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

                var emptyHall = echo.MeasureAcoustics(grid, Vector2i.Zero);
                var struckFurniture = entMan.SpawnEntity("AreaEchoTestFurniture",
                    new EntityCoordinates(grid, new Vector2(0.5f)));
                var impactRoom = echo.MeasureAcoustics(grid, Vector2i.Zero, grid.Owner);
                var attachedRoom = echo.MeasureAcoustics(grid, Vector2i.Zero, struckFurniture);
                Assert.That(impactRoom.Scattering, Is.EqualTo(emptyHall.Scattering),
                    "An impact inside one object must not classify every room direction as furniture.");
                var impactPreset = AreaEchoSystem.CreatePreset(impactRoom, listener.Position, gridMatrix, Angle.Zero, 1f);
                var attachedPreset = AreaEchoSystem.CreatePreset(attachedRoom, listener.Position, gridMatrix, Angle.Zero, 1f);
                Assert.That(impactPreset.DecayTime, Is.EqualTo(attachedPreset.DecayTime),
                    "A static impact/landing and an entity-attached sound at the same position must share room decay.");
                Assert.That(impactPreset.GainHF, Is.EqualTo(attachedPreset.GainHF));
                Assert.That(impactPreset.Diffusion, Is.EqualTo(attachedPreset.Diffusion));
                entMan.DeleteEntity(struckFurniture);
                var furniture = new List<EntityUid>();
                foreach (var offset in new[] { new Vector2(2f, 0f), new Vector2(-2f, 0f), new Vector2(0f, 2f), new Vector2(0f, -2f) })
                    furniture.Add(entMan.SpawnEntity("AreaEchoTestFurniture",
                        new EntityCoordinates(grid, new Vector2(0.5f) + offset)));
                var furnishedHall = echo.MeasureAcoustics(grid, Vector2i.Zero);
                Assert.That(furnishedHall.Scattering, Is.LessThan(emptyHall.Scattering),
                    "Furniture absorbs/scatters reflections without pretending to be full-height walls.");
                Assert.That(furnishedHall.Preset, Is.EqualTo(emptyHall.Preset));
                var emptyPreset = AreaEchoSystem.CreatePreset(emptyHall, listener.Position, gridMatrix, Angle.Zero, 1f);
                var furnishedPreset = AreaEchoSystem.CreatePreset(furnishedHall, listener.Position, gridMatrix, Angle.Zero, 1f);
                Assert.That(furnishedPreset.DecayTime, Is.LessThan(emptyPreset.DecayTime));
                Assert.That(furnishedPreset.EchoDepth, Is.LessThan(emptyPreset.EchoDepth),
                    "Obstacles must weaken distinct repeats instead of preserving a ringing empty hall.");
                Assert.That(furnishedPreset.Diffusion, Is.GreaterThan(emptyPreset.Diffusion),
                    "Furniture should spread reflections while shortening their decay.");
                foreach (var item in furniture)
                    entMan.DeleteEntity(item);

                maps.SetTile(grid, grid.Comp, Vector2i.Zero, Tile.Empty);
                Assert.That(echo.MeasureRoom(grid, Vector2i.Zero), Is.EqualTo(-1),
                    "A sound over a hole in the floor must not acquire room echo.");
                maps.SetTile(grid, grid.Comp, Vector2i.Zero, floor);

                foreach (var wall in walls)
                    entMan.DeleteEntity(wall);
                Assert.That(echo.MeasureRoom(grid, Vector2i.Zero), Is.EqualTo(-1),
                    "Rays reaching the edge of an open platform must escape.");

                for (var x = -16; x <= 16; x++)
                foreach (var y in new[] { -2, 2 })
                    entMan.SpawnEntity(Boundary, new EntityCoordinates(grid, new Vector2(x + 0.5f, y + 0.5f)));
                var openCorridor = echo.MeasureAcoustics(grid, Vector2i.Zero);
                Assert.That(openCorridor.Preset, Is.GreaterThanOrEqualTo(0),
                    "A long corridor still reflects sound from its side walls with both ends open.");
                Assert.That(openCorridor.Enclosure, Is.LessThan(1f));
                var doors = new List<EntityUid>();
                foreach (var x in new[] { -16, 16 })
                for (var y = -1; y <= 1; y++)
                    doors.Add(entMan.SpawnEntity(Boundary, new EntityCoordinates(grid, new Vector2(x + 0.5f, y + 0.5f))));
                var closedCorridor = echo.MeasureAcoustics(grid, Vector2i.Zero);
                Assert.That(closedCorridor.Enclosure, Is.GreaterThan(openCorridor.Enclosure));
                Assert.That(closedCorridor.ReflectionDistance, Is.GreaterThan(openCorridor.ReflectionDistance),
                    "Closing the end doors adds a distant reflection along the corridor.");
                var openCorridorPreset = AreaEchoSystem.CreatePreset(openCorridor, listener.Position, gridMatrix, Angle.Zero, 1f);
                var closedCorridorPreset = AreaEchoSystem.CreatePreset(closedCorridor, listener.Position, gridMatrix, Angle.Zero, 1f);
                Assert.That(closedCorridorPreset.EchoDepth, Is.GreaterThan(openCorridorPreset.EchoDepth),
                    "Distant end walls add distinct decaying repeats to a narrow corridor.");
                var nearerDoors = new List<EntityUid>();
                foreach (var x in new[] { -8, 8 })
                for (var y = -1; y <= 1; y++)
                    nearerDoors.Add(entMan.SpawnEntity(Boundary,
                        new EntityCoordinates(grid, new Vector2(x + 0.5f, y + 0.5f))));
                var shorterCorridor = echo.MeasureAcoustics(grid, Vector2i.Zero);
                var shorterPreset = AreaEchoSystem.CreatePreset(shorterCorridor, listener.Position, gridMatrix, Angle.Zero, 1f);
                Assert.That(shorterPreset.EchoTime, Is.LessThan(closedCorridorPreset.EchoTime),
                    "Bringing the end walls closer must shorten the interval between echoes.");
                Assert.That(shorterPreset.EchoDepth, Is.LessThan(closedCorridorPreset.EchoDepth));
                foreach (var nearerDoor in nearerDoors)
                    entMan.DeleteEntity(nearerDoor);
                foreach (var door in doors)
                    entMan.DeleteEntity(door);
                Assert.That(echo.MeasureAcoustics(grid, Vector2i.Zero).Enclosure, Is.EqualTo(openCorridor.Enclosure));

            }
            finally
            {
                entMan.DeleteEntity(map);
                cfg.SetCVar(AreaEchoCVars.HighQuality, oldQuality);
            }
        });

        await pair.CleanReturnAsync();
    }
    [TestCase(false, 1)]
    [TestCase(true, 1)]
    [TestCase(false, 3)]
    [TestCase(true, 3)]
    public async Task VestibuleRetainsConnectedCorridorEcho(bool highQuality, int halfWidth)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var maps = client.System<SharedMapSystem>();
            var echo = client.System<AreaEchoSystem>();
            var cfg = client.ResolveDependency<IConfigurationManager>();
            var oldQuality = cfg.GetCVar(AreaEchoCVars.HighQuality);
            cfg.SetCVar(AreaEchoCVars.HighQuality, highQuality);
            var map = maps.CreateMap(out var mapId);
            try
            {
                var grid = client.ResolveDependency<IMapManager>().CreateGridEntity(mapId);
                entities.EnsureComponent<ImplicitRoofComponent>(grid);
                var floor = new Tile(client.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
                var tiles = new List<(Vector2i, Tile)>();
                for (var x = -19; x <= 19; x++)
                for (var y = -7; y <= 5; y++)
                    tiles.Add((new Vector2i(x, y), floor));
                maps.SetTiles(grid, grid.Comp, tiles);

                void Wall(int x, int y) => entities.SpawnEntity(Boundary,
                    new EntityCoordinates(grid, new Vector2(x + 0.5f, y + 0.5f)));
                for (var x = -19; x <= 19; x++)
                {
                    Wall(x, 5);
                    if (Math.Abs(x) > halfWidth)
                        Wall(x, -1);
                }
                for (var y = 0; y < 5; y++)
                {
                    Wall(-19, y);
                    Wall(19, y);
                }
                for (var y = -7; y < 0; y++)
                {
                    Wall(-halfWidth - 1, y);
                    Wall(halfWidth + 1, y);
                }
                for (var x = -halfWidth; x <= halfWidth; x++)
                    Wall(x, -7);

                var previousStrength = echo.MeasureAcoustics(grid, new Vector2i(0, 1)).Strength;
                for (var y = 1; y >= -6; y--)
                {
                    var room = echo.MeasureAcoustics(grid, new Vector2i(0, y));
                    Assert.That(room.Strength, Is.GreaterThan(0.25f),
                        $"The open vestibule must remain coupled to the hall at y={y}.");
                    Assert.That(MathF.Abs(room.Strength - previousStrength), Is.LessThan(0.25f),
                        "Walking one tile in an uninterrupted passage must not abruptly switch echo on/off: " +
                        $"y={y}, strength={previousStrength:F3} -> {room.Strength:F3}.");
                    previousStrength = room.Strength;
                }

                var nearDoors = new Vector2i(0, -6);
                var open = echo.MeasureAcoustics(grid, nearDoors);
                var partition = new List<EntityUid>();
                for (var x = -halfWidth; x <= halfWidth; x++)
                    partition.Add(entities.SpawnEntity(Boundary,
                        new EntityCoordinates(grid, new Vector2(x + 0.5f, -0.5f))));
                var closed = echo.MeasureAcoustics(grid, nearDoors);
                Assert.That(closed.TailSize, Is.LessThan(open.TailSize), "A closed partition must cut the acoustic connection.");
                Assert.That(closed.Strength, Is.LessThan(open.Strength));
                foreach (var wall in partition)
                    entities.DeleteEntity(wall);
                Assert.That(echo.MeasureAcoustics(grid, nearDoors).Strength, Is.EqualTo(open.Strength).Within(0.001f));
            }
            finally
            {
                entities.DeleteEntity(map);
                cfg.SetCVar(AreaEchoCVars.HighQuality, oldQuality);
            }
        });
        await pair.CleanReturnAsync();
    }

}
