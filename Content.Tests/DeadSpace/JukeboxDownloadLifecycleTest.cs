// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Content.Client.DeadSpace.Ports.Jukebox;
using Content.Shared.DeadSpace.Ports.Jukebox;
using Moq;
using NUnit.Framework;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Network.Transfer;
using Robust.Shared.Utility;

namespace Content.Tests.DeadSpace;

[TestFixture]
public sealed class JukeboxDownloadLifecycleTest
{
    [Test]
    public async Task RoundClearRejectsDelayedFilesAndKeepsNewerRound()
    {
        using var fixture = new Fixture();
        await fixture.Receive("old.ogg", 0);
        await fixture.Receive("new.ogg", 1);
        fixture.Manager.ClearThroughRound(0);
        Assert.That(fixture.Root.FileExists(new ResPath("old.ogg")), Is.False);
        Assert.That(fixture.Root.FileExists(new ResPath("new.ogg")), Is.True);
        await fixture.Receive("late-old.ogg", 0);
        Assert.That(fixture.Root.FileExists(new ResPath("late-old.ogg")), Is.False);
        fixture.Manager.ClearThroughRound(1);
        Assert.That(fixture.Root.FileExists(new ResPath("new.ogg")), Is.False);
    }

    [Test]
    public async Task ReconnectAcceptsFreshServerRoundZero()
    {
        using var fixture = new Fixture();
        fixture.Manager.ClearThroughRound(8);
        fixture.Net.Raise(n => n.Disconnect += null, new NetDisconnectedArgs(fixture.Channel, "test reconnect"));
        await fixture.Receive("fresh.ogg", 0);
        Assert.That(fixture.Root.FileExists(new ResPath("fresh.ogg")), Is.True);
    }

    private sealed class Fixture : IDisposable
    {
        public readonly ClientJukeboxSongsSyncManager Manager = new();
        public readonly Mock<IClientNetManager> Net = new();
        public readonly INetChannel Channel = Mock.Of<INetChannel>(c => c.IsConnected);
        public IContentRoot Root;

        public Fixture()
        {
            Net.SetupGet(n => n.ServerChannel).Returns(Channel);
            var resources = new Mock<IResourceManager>();
            resources.Setup(r => r.AddRoot(It.IsAny<ResPath>(), It.IsAny<IContentRoot>()))
                .Callback<ResPath, IContentRoot>((_, root) => Root = root);
            var logs = new Mock<ILogManager>();
            logs.Setup(l => l.GetSawmill(It.IsAny<string>())).Returns(Mock.Of<ISawmill>());
            Set(typeof(JukeboxSongsSyncManager), "NetManager", Net.Object);
            Set(typeof(JukeboxSongsSyncManager), "ResourceManager", resources.Object);
            Set(typeof(JukeboxSongsSyncManager), "TransferManager", Mock.Of<ITransferManager>());
            Set(typeof(ClientJukeboxSongsSyncManager), "_log", logs.Object);
            Manager.Initialize();
        }

        public async Task Receive(string path, int round)
        {
            using var stream = new MemoryStream();
            await Codec.Write(stream, path, Encoding.ASCII.GetBytes("OggS01234567"), new NetEntity(round));
            stream.Position = 0;
            var transfer = (TransferReceivedEvent)Activator.CreateInstance(typeof(TransferReceivedEvent),
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { "DS14.Jukebox.Download", Channel, stream }, null);
            await Manager.ReceiveSong(transfer);
        }

        private void Set(Type type, string field, object value)
            => type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Manager, value);
        public void Dispose() => Manager.Dispose();
    }

    private sealed class Codec : JukeboxSongsSyncManager
    {
        public static Task Write(Stream stream, string name, byte[] data, NetEntity round)
            => WriteSong(stream, name, data, round);
    }
}
