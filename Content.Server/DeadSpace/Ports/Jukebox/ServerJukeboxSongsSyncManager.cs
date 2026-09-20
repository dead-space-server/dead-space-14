using Content.Shared.DeadSpace.Ports.Jukebox;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Network.Transfer;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.Ports.Jukebox;

public sealed class ServerJukeboxSongsSyncManager : JukeboxSongsSyncManager
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ILogManager _log = default!;
    private int _round;

    public override void Initialize()
    {
        base.Initialize();
        TransferManager.RegisterTransferMessage(UploadKey, ReceiveSong);
        TransferManager.RegisterTransferMessage(DownloadKey);
        NetManager.Connected += OnClientConnected;
    }

    private void OnClientConnected(object? sender, NetChannelArgs e)
    {
        foreach (var (path, data) in ContentRoot.GetAllFiles())
        {
            SendSong(e.Channel, path, data);
        }
    }

    public (string SongName, ResPath Path)? SyncSongData(string songName, byte[] bytes)
    {
        // Immutable paths avoid stale AudioResource caches across rounds and duplicate song names.
        var relative = new ResPath($"{Guid.NewGuid():N}.ogg");
        ContentRoot.AddOrUpdateFile(relative, bytes);
        var path = Prefix / relative;
        try
        {
            if (_entities.System<SharedAudioSystem>().GetAudioLength(new ResolvedPathSpecifier(path)) <= TimeSpan.Zero)
                throw new InvalidOperationException("The song is empty.");
        }
        catch (Exception e)
        {
            ContentRoot.RemoveFile(relative);
            _log.GetSawmill("jukebox").Warning($"Invalid song: {e.Message}");
            return null;
        }
        foreach (var channel in NetManager.Channels)
            SendSong(channel, relative, bytes);
        return (songName, path);
    }

    private async void ReceiveSong(TransferReceivedEvent transfer)
    {
        var round = _round;
        try
        {
            await using var stream = transfer.DataStream;
            var maxBytes = _cfg.GetCVar(JKCVars.MaxJukeboxSongSizeInMB) * 1_000_000d;
            var song = await ReadSong(stream, maxBytes);
            if (Disposed || round != _round || !transfer.Channel.IsConnected)
                return;

            _entities.System<TapeCreatorSystem>().OnSongUploaded(song, transfer.Channel);
        }
        catch (Exception e)
        {
            _log.GetSawmill("jukebox").Warning($"Could not receive song: {e.Message}");
        }
    }

    private async void SendSong(INetChannel channel, ResPath path, byte[] data)
    {
        try
        {
            await using var stream = TransferManager.StartTransfer(channel, DownloadKey);
            await WriteSong(stream, path.ToString(), data);
        }
        catch (Exception e)
        {
            _log.GetSawmill("jukebox").Warning($"Could not send song: {e.Message}");
        }
    }

    public void CleanUp()
    {
        _round++;
        ContentRoot.Clear();
    }
}
