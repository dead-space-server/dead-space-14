// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System.IO;
using System.Collections;
using System.Reflection;
using System.Linq;
using Robust.Client.ResourceManagement;
using ClientJukeboxSystem = Content.Client.DeadSpace.Ports.Jukebox.JukeboxSystem;
using ServerJukeboxSystem = Content.Server.DeadSpace.Ports.Jukebox.JukeboxSystem;
using Content.Client.DeadSpace.Ports.Jukebox;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.DeadSpace.Ports.Jukebox;
using Content.Shared.DeadSpace.Ports.Jukebox;
using Robust.Shared.ContentPack;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Network.Transfer;
using Robust.Shared.Prototypes;
using Robust.Shared.Upload;
using Robust.Shared.Utility;
using TapeCreatorSystem = Content.Server.DeadSpace.Ports.Jukebox.TapeCreatorSystem;

namespace Content.IntegrationTests.Tests.DeadSpace.Jukebox;

[TestFixture]
public sealed class JukeboxUploadTest : InteractionTest
{
    [Test]
    public async Task RecordingRequiresOpenUiAndDownloadsTheSong()
    {
        var recorder = await SpawnTarget("TapeRecorderr");
        var tape = await Spawn("TapeEmpty");
        JukeboxSongUploadRequest request = null;
        await Server.WaitAssertion(() =>
        {
            var creator = SEntMan.GetComponent<TapeCreatorComponent>(ToServer(recorder));
            Assert.That(Server.System<SharedContainerSystem>().Insert(ToServer(tape), creator.TapeContainer), Is.True);
            creator.InsertedTape = tape;
            creator.CoinBalance = 1;
            using var file = Server.ResolveDependency<IResourceManager>().ContentFileRead(new ResPath("/Audio/Effects/beep1.ogg"));
            using var data = new MemoryStream();
            file.CopyTo(data);
            request = new JukeboxSongUploadRequest
            {
                TapeCreatorUid = recorder,
                SongName = "Transfer integration song",
                SongBytes = data.ToArray(),
            };
            Server.System<TapeCreatorSystem>().OnSongUploaded(request, ServerSession.Channel);
            Assert.That(SEntMan.GetComponent<TapeComponent>(ToServer(tape)).Songs, Is.Empty);
            Assert.That(creator.CoinBalance, Is.EqualTo(1));
            Assert.That(SUiSys.TryOpenUi(ToServer(recorder), TapeCreatorUIKey.Key, SPlayer), Is.True);
        });

        await RunTicks(5);
        // IntegrationNetManager skips the production handshake, so initialize the real transfer managers explicitly.
        Task handshake = null;
        await Server.WaitPost(() =>
        {
            var transfers = Server.ResolveDependency<ITransferManager>();
            handshake = (Task) transfers.GetType().GetMethod("ServerHandshake")!.Invoke(transfers, new object[] { ServerSession.Channel });
        });
        for (var i = 0; i < 100 && !handshake.IsCompleted; i++) await RunTicks(2);
        Assert.That(handshake.IsCompletedSuccessfully, Is.True);
        Task<bool> upload = null;
        await Client.WaitPost(() => upload = Client.ResolveDependency<ClientJukeboxSongsSyncManager>().UploadSong(request));
        ResPath? downloaded = null;
        for (var i = 0; i < 100 && downloaded == null; i++)
        {
            await RunTicks(2);
            await Server.WaitPost(() =>
            {
                var songs = SEntMan.GetComponent<TapeComponent>(ToServer(tape)).Songs;
                if (songs.Count == 1) downloaded = songs[0].SongPath;
            });
        }
        Assert.That(downloaded, Is.Not.Null, "The server must finish recording the uploaded song.");
        Assert.That(await upload, Is.True);

        var exists = false;
        for (var i = 0; i < 100 && !exists; i++)
        {
            await RunTicks(2);
            await Client.WaitPost(() => exists = Client.ResolveDependency<IResourceManager>().ContentFileExists(downloaded.Value));
        }
        await Client.WaitAssertion(() =>
        {
            Assert.That(exists, Is.True, "The recorded song must be distributed back to the client.");
            using var received = Client.ResolveDependency<IResourceManager>().ContentFileRead(downloaded.Value);
            using var data = new MemoryStream();
            received.CopyTo(data);
            Assert.That(data.ToArray().AsSpan().SequenceEqual(request.SongBytes), Is.True);
        });
        await Server.WaitAssertion(() =>
        {
            var creator = SEntMan.GetComponent<TapeCreatorComponent>(ToServer(recorder));
            Assert.That(creator.CoinBalance, Is.Zero);
            Assert.That(creator.Recording, Is.False);
            Assert.That(creator.InsertedTape, Is.Null);
            Server.System<TapeCreatorSystem>().OnSongUploaded(request, ServerSession.Channel);
            Assert.That(SEntMan.GetComponent<TapeComponent>(ToServer(tape)).Songs.Count, Is.EqualTo(1));
            Assert.That(creator.CoinBalance, Is.Zero);
        });

        EntProtoId prototypeId = "JukeboxUploadedPrototypeFixture";
        var prototypeData = $"""
            - type: entity
              id: {prototypeId}
              parent: TapeEmpty
              components:
              - type: Tape
                songs:
                - songName: Transfer integration song
                  path: {downloaded.Value}
            """;
        await Client.WaitPost(() => Client.ResolveDependency<IGamePrototypeLoadManager>().SendGamePrototype(prototypeData));
        var loaded = false;
        for (var i = 0; i < 100 && !loaded; i++)
        {
            await RunTicks(2);
            var serverLoaded = false;
            var clientLoaded = false;
            await Server.WaitPost(() => serverLoaded = ProtoMan.HasIndex(prototypeId));
            await Client.WaitPost(() => clientLoaded = Client.ProtoMan.HasIndex(prototypeId));
            loaded = serverLoaded && clientLoaded;
        }
        Assert.That(loaded, Is.True, "A runtime prototype using the uploaded song must reach both sides.");
        await Server.WaitAssertion(() =>
        {
            Assert.That(ProtoMan.Index(prototypeId).TryGetComponent<TapeComponent>(out var component, Factory), Is.True);
            Assert.That(component.Songs[0].SongPath, Is.EqualTo(downloaded));
        });
        await Client.WaitAssertion(() =>
        {
            Assert.That(Client.ProtoMan.Index(prototypeId).TryGetComponent<TapeComponent>(out var component,
                Client.ResolveDependency<IComponentFactory>()), Is.True);
            Assert.That(component.Songs[0].SongPath, Is.EqualTo(downloaded));
        });

        var jukebox = await Spawn("JukeboxMK");
        await Server.WaitAssertion(() =>
        {
            var box = SEntMan.GetComponent<WhiteJukeboxComponent>(ToServer(jukebox));
            Assert.That(Server.System<SharedContainerSystem>().Insert(ToServer(tape), box.TapeContainer), Is.True);
            Assert.That(SUiSys.TryOpenUi(ToServer(jukebox), JukeboxUIKey.Key, SPlayer), Is.True);
            Server.System<ServerJukeboxSystem>().OnSongRequestPlay(new JukeboxRequestSongPlay
            {
                Jukebox = jukebox,
                SongPath = downloaded,
            }, new EntitySessionEventArgs(ServerSession));
            Assert.That(box.PlayingSongData, Is.Not.Null);
        });
        await RunTicks(10);
        await Client.WaitAssertion(() =>
        {
            var playback = Client.System<ClientJukeboxSystem>();
            playback.FrameUpdate(0.016f);
            var decoded = (IDictionary) typeof(ClientJukeboxSystem).GetField("_songStreams", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(playback);
            Assert.That(decoded.Contains(downloaded.Value), Is.True);
            Assert.That(Client.ResolveDependency<IResourceCache>().GetAllResources<AudioResource>()
                .Any(resource => resource.Key == downloaded), Is.False,
                "Dynamic songs must not leak into the permanent engine resource cache.");
        });
        await Server.WaitPost(() => Server.System<ServerJukeboxSongsSyncSystem>().ClearSongs());
        await RunTicks(5);
        await Client.WaitAssertion(() =>
        {
            Assert.That(Client.ResolveDependency<IResourceManager>().ContentFileExists(downloaded.Value), Is.False);
            var decoded = (IDictionary) typeof(ClientJukeboxSystem).GetField("_songStreams", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(Client.System<ClientJukeboxSystem>());
            Assert.That(decoded.Count, Is.Zero);
        });
        await Server.WaitAssertion(() =>
        {
            var box = SEntMan.GetComponent<WhiteJukeboxComponent>(ToServer(jukebox));
            box.PlayingSongData = null;
            Server.System<ServerJukeboxSystem>().OnSongRequestPlay(new JukeboxRequestSongPlay
            {
                Jukebox = jukebox,
                SongPath = downloaded,
            }, new EntitySessionEventArgs(ServerSession));
            Assert.That(box.PlayingSongData, Is.Null, "Cached duration must not resurrect a deleted song.");
        });
    }

    [Test]
    public async Task ReplacingAndRemovingTapeKeepsRecorderStateConsistent()
    {
        var recorder = await SpawnTarget("TapeRecorderr");
        await InteractUsing("TapeEmpty");
        await InteractUsing("TapeEmpty");
        await Server.WaitAssertion(() =>
        {
            var creator = SEntMan.GetComponent<TapeCreatorComponent>(ToServer(recorder));
            Assert.That(creator.TapeContainer.ContainedEntities.Count, Is.EqualTo(1));
            Assert.That(creator.InsertedTape, Is.Not.Null);
            Server.System<SharedContainerSystem>().EmptyContainer(creator.TapeContainer, force: true);
            Assert.That(creator.InsertedTape, Is.Null);
        });
    }
}
