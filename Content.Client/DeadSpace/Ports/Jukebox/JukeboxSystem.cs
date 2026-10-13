using Content.Client.Audio;
using Content.Shared.DeadSpace.Audio;
using Content.Shared.DeadSpace.Ports.Jukebox;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.GameTicking;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio.Sources;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.Network;
using System.Linq;

namespace Content.Client.DeadSpace.Ports.Jukebox;
public sealed class JukeboxSystem : EntitySystem
{
    [Dependency] private readonly IResourceCache _resource = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IAudioManager _audioManager = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ClientJukeboxSongsSyncManager _songs = default!;
    [Dependency] private readonly INetManager _net = default!;

    private readonly Dictionary<EntityUid, JukeboxAudio> _playing = new();
    private readonly HashSet<ResPath> _failedSongs = new();
    private readonly Dictionary<ResPath, AudioStream> _songStreams = new();
    private const int CachedSongLimit = 8;
    private static readonly string SongPrefix = JukeboxSongsSyncManager.Prefix.CanonPath + "/";
    private float _volume;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WhiteJukeboxComponent, ComponentRemove>(OnRemoved);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => CleanUp());
        SubscribeNetworkEvent<TickerJoinLobbyEvent>(_ => CleanUp());
        SubscribeNetworkEvent<JukeboxRoundClearedEvent>(ev =>
        {
            _songs.ClearThroughRound(ev.Round);
            foreach (var path in _songStreams.Keys.ToArray())
            {
                if (_resource.ContentFileExists(path)) continue;
                foreach (var (uid, audio) in _playing.ToArray())
                    if (audio.Path == path) Stop(uid);
                if (_songStreams.Remove(path, out var stream)) stream.Dispose();
            }
        });
        _net.Disconnect += OnDisconnected;
        Subs.CVar(_cfg, CCCCVars.JukeboxMusicVolume, value => _volume = value, true);
    }

    public override void Shutdown()
    {
        _net.Disconnect -= OnDisconnected;
        CleanUp();
        base.Shutdown();
    }

    private void OnRemoved(EntityUid uid, WhiteJukeboxComponent component, ComponentRemove args) => Stop(uid);

    private void Stop(EntityUid uid)
    {
        if (!_playing.Remove(uid, out var audio))
            return;
        audio.Source.StopPlaying();
        audio.Source.Dispose();
        TrimSongCache();
    }

    public void RequestSongToPlay(EntityUid jukebox, WhiteJukeboxComponent component, JukeboxSong song)
    {
        if (song.SongPath is not { } path)
            return;
        RaiseNetworkEvent(new JukeboxRequestSongPlay
        {
            Jukebox = GetNetEntity(jukebox),
            SongName = song.SongName,
            SongPath = path,
        });
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var listener = _audio.GetListenerCoordinates();
        var decodedThisFrame = false;
        var query = EntityQueryEnumerator<WhiteJukeboxComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var jukebox, out var xform))
        {
            var song = jukebox.PlayingSongData;
            if (TryComp<SpriteComponent>(uid, out var sprite) && sprite.LayerMapTryGet("bars", out var layer))
                _sprites.LayerSetVisible((uid, sprite), layer, song != null);

            var position = _transform.GetWorldPosition(xform);
            var delta = position - listener.Position;
            var distance = delta.Length();
            if (song?.SongPath is not { } path || xform.MapID != listener.MapId ||
                distance > jukebox.MaxAudioRange + 2f)
            {
                Stop(uid);
                continue;
            }

            if (_playing.TryGetValue(uid, out var current) &&
                (current.Path != path || current.StartedAt != song.StartedAt))
            {
                Stop(uid);
                current = null;
            }

            if (current == null)
            {
                // Playback state can arrive before the asynchronous file transfer has finished.
                if (_failedSongs.Contains(path) || !_resource.ContentFileExists(path))
                    continue;
                AudioStream stream;
                try
                {
                    if (path.CanonPath.StartsWith(SongPrefix, StringComparison.Ordinal))
                    {
                        if (!_songStreams.TryGetValue(path, out stream!))
                        {
                            if (decodedThisFrame) continue;
                            decodedThisFrame = true;
                            using var file = _resource.ContentFileRead(path);
                            stream = _audioManager.LoadAudioOggVorbis(file, path.CanonPath);
                            _songStreams.Add(path, stream);
                        }
                    }
                    else
                        stream = _resource.GetResource<AudioResource>(path, useFallback: false).AudioStream;
                }
                catch (Exception e)
                {
                    _failedSongs.Add(path);
                    Log.Warning($"Could not load jukebox song {path}: {e.Message}");
                    continue;
                }
                var length = (float) stream.Length.TotalSeconds;
                if (length <= 0f)
                    continue;
                var offset = MathF.Max(0f, (float) (_timing.CurTime - song.StartedAt).TotalSeconds);
                if (song.EndsAt is { } end && _timing.CurTime >= end)
                    continue;
                var source = _audioManager.CreateAudioSource(stream);
                if (source == null)
                {
                    TrimSongCache();
                    continue;
                }
                var positional = stream.ChannelCount == 1;
                source.Global = !positional;
                source.Gain = 0f;
                source.RolloffFactor = 0f;
                source.MaxDistance = jukebox.MaxAudioRange;
                source.PlaybackPosition = offset % length;
                current = new JukeboxAudio(source, path, song.StartedAt, positional);
                _playing.Add(uid, current);
                TrimSongCache();
            }

            current.Source.Looping = jukebox.Playing;
            if (current.Positional)
                current.Source.Position = position;
            current.Source.Occlusion = _audio.GetOcclusion(listener, delta, distance, uid);
            // Explicit gain also attenuates stereo tracks; OpenAL does not spatially attenuate stereo buffers.
            var gain = SpatialAudio.GetDistanceGain(distance, jukebox.MaxAudioRange);
            current.Source.Volume = jukebox.Volume +
                SharedAudioSystem.GainToVolume(_volume / ContentAudioSystem.JukeboxMusicMultiplier * gain);
            if (!current.Started)
            {
                current.Source.StartPlaying();
                current.Started = true;
            }
        }
    }

    private sealed class JukeboxAudio(IAudioSource source, ResPath path, TimeSpan startedAt, bool positional)
    {
        public readonly IAudioSource Source = source;
        public readonly ResPath Path = path;
        public readonly TimeSpan StartedAt = startedAt;
        public readonly bool Positional = positional;
        public bool Started;
    }

    private void CleanUp()
    {
        foreach (var audio in _playing.Values)
        {
            audio.Source.StopPlaying();
            audio.Source.Dispose();
        }
        _playing.Clear();
        _failedSongs.Clear();
        foreach (var stream in _songStreams.Values) stream.Dispose();
        _songStreams.Clear();
    }

    private void OnDisconnected(object? sender, NetDisconnectedArgs args) => CleanUp();

    private void TrimSongCache()
    {
        if (_songStreams.Count <= CachedSongLimit) return;
        foreach (var (path, stream) in _songStreams.ToArray())
        {
            if (_songStreams.Count <= CachedSongLimit) break;
            if (_playing.Values.Any(audio => audio.Path == path)) continue;
            _songStreams.Remove(path);
            stream.Dispose();
        }
    }
}
