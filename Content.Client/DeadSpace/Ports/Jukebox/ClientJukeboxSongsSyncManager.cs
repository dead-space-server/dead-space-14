using System.Threading.Tasks;
using Content.Shared.DeadSpace.Ports.Jukebox;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Network.Transfer;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Ports.Jukebox;

public sealed class ClientJukeboxSongsSyncManager : JukeboxSongsSyncManager
{
    [Dependency] private readonly ILogManager _log = default!;

    public override void Initialize()
    {
        base.Initialize();
        TransferManager.RegisterTransferMessage(UploadKey);
        TransferManager.RegisterTransferMessage(DownloadKey, ReceiveSong);
        NetManager.Disconnect += (_, _) =>
        {
            if (!Disposed)
                ContentRoot.Clear();
        };
    }

    public async Task<bool> UploadSong(JukeboxSongUploadRequest song)
    {
        try
        {
            if (((IClientNetManager) NetManager).ServerChannel is not { } channel)
                return false;

            await using var stream = TransferManager.StartTransfer(channel, UploadKey);
            await WriteSong(stream, song.SongName, song.SongBytes, song.TapeCreatorUid);
            return true;
        }
        catch (Exception e)
        {
            _log.GetSawmill("jukebox").Warning($"Could not upload song: {e.Message}");
            return false;
        }
    }

    private async void ReceiveSong(TransferReceivedEvent transfer)
    {
        try
        {
            await using var stream = transfer.DataStream;
            var song = await ReadSong(stream, int.MaxValue);
            if (Disposed || !transfer.Channel.IsConnected)
                return;

            ContentRoot.AddOrUpdateFile(new ResPath(song.SongName), song.SongBytes);
        }
        catch (Exception e)
        {
            _log.GetSawmill("jukebox").Warning($"Could not download song: {e.Message}");
        }
    }
}
