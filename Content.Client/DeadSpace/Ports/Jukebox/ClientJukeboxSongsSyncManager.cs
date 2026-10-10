using System.Threading.Tasks;
using System.Linq;
using Content.Shared.DeadSpace.Ports.Jukebox;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Network.Transfer;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Ports.Jukebox;

public sealed class ClientJukeboxSongsSyncManager : JukeboxSongsSyncManager
{
    [Dependency] private readonly ILogManager _log = default!;
    private readonly Dictionary<ResPath, int> _fileRounds = new();
    private int _clearedRound = -1;

    public override void Initialize()
    {
        base.Initialize();
        TransferManager.RegisterTransferMessage(UploadKey);
        TransferManager.RegisterTransferMessage(DownloadKey, transfer => _ = ReceiveSong(transfer));
        NetManager.Disconnect += OnDisconnected;
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
            _log.GetSawmill("jukebox").Warning($"Could not upload song: {e}");
            return false;
        }
    }

    internal async Task ReceiveSong(TransferReceivedEvent transfer)
    {
        try
        {
            await using var stream = transfer.DataStream;
            var song = await ReadSong(stream, int.MaxValue);
            if (song == null || Disposed || !transfer.Channel.IsConnected)
                return;
            if (((IClientNetManager) NetManager).ServerChannel != transfer.Channel || song.TapeCreatorUid.Id <= _clearedRound)
                return;

            var path = new ResPath(song.SongName);
            ContentRoot.AddOrUpdateFile(path, song.SongBytes);
            _fileRounds[path] = song.TapeCreatorUid.Id;
        }
        catch (Exception e)
        {
            _log.GetSawmill("jukebox").Warning($"Could not download song: {e}");
        }
    }

    public void ClearThroughRound(int round)
    {
        if (Disposed || round <= _clearedRound) return;
        _clearedRound = round;
        foreach (var (path, generation) in _fileRounds.ToArray())
        {
            if (generation > round) continue;
            ContentRoot.RemoveFile(path);
            _fileRounds.Remove(path);
        }
    }

    private void OnDisconnected(object? sender, NetDisconnectedArgs args)
    {
        if (Disposed) return;
        ContentRoot.Clear();
        _fileRounds.Clear();
        _clearedRound = -1;
    }

    public override void Dispose()
    {
        if (Disposed) return;
        NetManager.Disconnect -= OnDisconnected;
        base.Dispose();
    }
}
