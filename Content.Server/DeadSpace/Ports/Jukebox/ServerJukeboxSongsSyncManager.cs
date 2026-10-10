using Content.Shared.DeadSpace.Ports.Jukebox;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Network.Transfer;
using Robust.Shared.Utility;
using System.Threading;
using System.Globalization;
using System.IO;
using NVorbis;
using System.Threading.Tasks;

namespace Content.Server.DeadSpace.Ports.Jukebox;

public sealed class ServerJukeboxSongsSyncManager : JukeboxSongsSyncManager
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ILogManager _log = default!;
    private int _round;
    private CancellationTokenSource _roundCancel = new();
    private readonly HashSet<INetChannel> _receivingUploads = new();

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
        var relative = new ResPath($"{_round}-{Guid.NewGuid():N}.ogg");
        ContentRoot.AddOrUpdateFile(relative, bytes);
        var path = Prefix / relative;
        try
        {
            using var file = new MemoryStream(bytes, writable: false);
            using var reader = new VorbisReader(file, false);
            reader.Initialize();
            var maxDecodedBytes = _cfg.GetCVar(JKCVars.MaxDecodedJukeboxSongSizeInMB) * 1_000_000d;
            if (!IsAudioSupported(reader.TotalSamples, reader.Channels, maxDecodedBytes))
                throw new InvalidDataException("Unsupported channels or excessive decoded song size.");
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
        var receiving = false;
        try
        {
            await using var stream = transfer.DataStream;
            if (Disposed || !transfer.Channel.IsConnected || !_receivingUploads.Add(transfer.Channel))
                return;
            receiving = true;
            var maxBytes = _cfg.GetCVar(JKCVars.MaxJukeboxSongSizeInMB) * 1_000_000d;
            var song = await ReadSong(stream, maxBytes,
                creator => _entities.System<TapeCreatorSystem>().CanUploadSong(creator, transfer.Channel), _roundCancel.Token);
            if (song == null || Disposed || round != _round || !transfer.Channel.IsConnected)
                return;

            _entities.System<TapeCreatorSystem>().OnSongUploaded(song, transfer.Channel);
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            _log.GetSawmill("jukebox").Warning($"Could not receive song from {transfer.Channel}: {e}");
        }
        finally
        {
            if (receiving) _receivingUploads.Remove(transfer.Channel);
        }
    }

    private async void SendSong(INetChannel channel, ResPath path, byte[] data)
    {
        try
        {
            await using var stream = TransferManager.StartTransfer(channel, DownloadKey);
            var round = int.Parse(path.CanonPath.AsSpan(0, path.CanonPath.IndexOf('-')), CultureInfo.InvariantCulture);
            await Task.Run(() => WriteSong(stream, path.ToString(), data, new NetEntity(round)));
        }
        catch (Exception e)
        {
            _log.GetSawmill("jukebox").Warning($"Could not send song {path} to {channel}: {e}");
        }
    }

    public int CleanUp()
    {
        var oldRound = _round++;
        _roundCancel.Cancel();
        _roundCancel.Dispose();
        _roundCancel = new CancellationTokenSource();
        ContentRoot.Clear();
        return oldRound;
    }

    public override void Dispose()
    {
        if (Disposed) return;
        NetManager.Connected -= OnClientConnected;
        _roundCancel.Cancel();
        _roundCancel.Dispose();
        base.Dispose();
    }

    internal static bool IsAudioSupported(long samples, int channels, double maxBytes)
        => channels is 1 or 2 && samples > 0 && double.IsFinite(maxBytes) && maxBytes > 0 &&
           samples <= maxBytes / (channels * sizeof(short));
}
