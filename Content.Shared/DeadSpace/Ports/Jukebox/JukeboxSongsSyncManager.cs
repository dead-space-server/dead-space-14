using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Robust.Shared.ContentPack;
using Robust.Shared.Network;
using Robust.Shared.Network.Transfer;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Ports.Jukebox;

public abstract class JukeboxSongsSyncManager : IDisposable
{
    [Dependency] protected readonly INetManager NetManager = default!;
    [Dependency] protected readonly ITransferManager TransferManager = default!;
    protected const string UploadKey = "DS14.Jukebox.Upload";
    protected const string DownloadKey = "DS14.Jukebox.Download";
    protected bool Disposed;
    [Dependency] protected readonly IResourceManager ResourceManager = default!;

    public static readonly ResPath Prefix = ResPath.Root / "Jukebox";

    protected readonly MemoryContentRoot ContentRoot = new();

    public virtual void Initialize()
    {
        ResourceManager.AddRoot(Prefix, ContentRoot);
    }

    protected static async Task WriteSong(Stream stream, string name, byte[] data, NetEntity tapeCreator = default)
    {
        var nameBytes = Encoding.UTF8.GetBytes(name);
        if (nameBytes.Length is <= 0 or > 512)
            throw new InvalidDataException("Invalid jukebox song name.");
        var header = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0, 4), tapeCreator.Id);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4, 4), nameBytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8, 4), data.Length);
        await stream.WriteAsync(header);
        await stream.WriteAsync(nameBytes);
        await stream.WriteAsync(data);
    }

    protected static async Task<JukeboxSongUploadRequest> ReadSong(Stream stream, double maxBytes)
    {
        var header = new byte[12];
        await stream.ReadExactlyAsync(header);
        var creator = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0, 4));
        var nameLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));
        var dataLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(8, 4));
        if (nameLength is <= 0 or > 512 || dataLength < 12 || dataLength > maxBytes)
            throw new InvalidDataException("Invalid jukebox song size.");

        var name = new byte[nameLength];
        var data = new byte[dataLength];
        await stream.ReadExactlyAsync(name);
        await stream.ReadExactlyAsync(data);
        if (data[0] != 'O' || data[1] != 'g' || data[2] != 'g' || data[3] != 'S')
            throw new InvalidDataException("Jukebox songs must be Ogg files.");

        return new JukeboxSongUploadRequest
        {
            TapeCreatorUid = new NetEntity(creator),
            SongName = Encoding.UTF8.GetString(name),
            SongBytes = data,
        };
    }

    public void Dispose()
    {
        Disposed = true;
        ContentRoot.Dispose();
    }
}
