// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System;
using Content.Shared.Eui;
using Content.Shared.Preferences;
using Lidgren.Network;
using NUnit.Framework;
using Robust.Shared.Serialization;

namespace Content.Tests.DeadSpace.Network;

[TestFixture]
public sealed class NetMessageSizeTest
{
    private const int FakeLength = 67108864;

    [TestCase(1000000)]
    [TestCase(-1)]
    public void ConstructionFavoritesRejectFakeCount(int count)
    {
        var packet = Incoming();
        packet.WriteVariableInt32(count);
        packet.Position = 0;

        var msg = new MsgUpdateConstructionFavorites();
        Assert.Throws<InvalidOperationException>(() => msg.ReadFromBuffer(packet, null));
        Assert.That(msg.Favorites, Is.Empty);
    }

    [Test]
    public void ConstructionFavoritesReadRealList()
    {
        var sent = new MsgUpdateConstructionFavorites { Favorites = ["Girder", "Window"] };
        var packet = RoundTrip(sent.WriteToBuffer);

        var received = new MsgUpdateConstructionFavorites();
        received.ReadFromBuffer(packet, null);
        Assert.That(received.Favorites, Is.EqualTo(sent.Favorites));
    }

    [Test]
    public void UpdateCharacterDoesNotAllocateFakeLength()
    {
        var packet = Incoming();
        packet.Write(0);
        packet.WriteVariableInt32(FakeLength);
        packet.Position = 0;

        AssertRejectedWithoutAllocation(() => new MsgUpdateCharacter().ReadFromBuffer(packet, null));
    }

    [Test]
    public void EuiMessageDoesNotAllocateFakeLength()
    {
        var packet = Incoming();
        packet.Write(0u);
        packet.WriteVariableInt32(FakeLength);
        packet.Position = 0;

        AssertRejectedWithoutAllocation(() => new MsgEuiMessage().ReadFromBuffer(packet, null));
    }

    private static void AssertRejectedWithoutAllocation(TestDelegate read)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Throws<InvalidOperationException>(read);
        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.LessThan(FakeLength / 16));
    }

    private static NetIncomingMessage RoundTrip(Action<NetOutgoingMessage, IRobustSerializer> write)
    {
        var outgoing = (NetOutgoingMessage) Activator.CreateInstance(typeof(NetOutgoingMessage), true);
        write(outgoing, null);

        var packet = Incoming();
        packet.Data = outgoing.Data;
        packet.LengthBits = outgoing.LengthBits;
        return packet;
    }

    private static NetIncomingMessage Incoming()
    {
        return (NetIncomingMessage) Activator.CreateInstance(typeof(NetIncomingMessage), true);
    }
}
