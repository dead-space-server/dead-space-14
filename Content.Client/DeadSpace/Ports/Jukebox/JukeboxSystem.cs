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

namespace Content.Client.DeadSpace.Ports.Jukebox;

// DS14-start
public sealed class JukeboxSystem : EntitySystem
{
    [Dependency] private readonly IResourceCache _resource = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IAudioManager _audioManager = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, JukeboxAudio> _playing = new();
    private float _volume;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WhiteJukeboxComponent, ComponentRemove>(OnRemoved);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => CleanUp());
        SubscribeNetworkEvent<TickerJoinLobbyEvent>(_ => CleanUp());
        Subs.CVar(_cfg, CCCCVars.JukeboxMusicVolume, value => _volume = value, true);
    }

    public override void Shutdown()
    {
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
    }

    public void RequestSongToPlay(EntityUid jukebox, WhiteJukeboxComponent component, JukeboxSong song)
    {
        if (song.SongPath is not { } path || !_resource.TryGetResource<AudioResource>(path, out var resource))
            return;
        RaiseNetworkEvent(new JukeboxRequestSongPlay
        {
            Jukebox = GetNetEntity(jukebox),
            SongName = song.SongName,
            SongPath = path,
            SongDuration = (float) resource.AudioStream.Length.TotalSeconds,
        });
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var listener = _audio.GetListenerCoordinates();
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
                if (!_resource.TryGetResource<AudioResource>(path, out var resource))
                    continue;
                var length = (float) resource.AudioStream.Length.TotalSeconds;
                if (length <= 0f)
                    continue;
                var offset = MathF.Max(0f, (float) (_timing.CurTime - song.StartedAt).TotalSeconds);
                if (song.EndsAt is { } end && _timing.CurTime >= end)
                    continue;
                var source = _audioManager.CreateAudioSource(resource.AudioStream);
                if (source == null)
                    continue;
                source.Gain = 0f;
                source.RolloffFactor = 0f;
                source.MaxDistance = jukebox.MaxAudioRange;
                source.PlaybackPosition = offset % length;
                current = new JukeboxAudio(source, path, song.StartedAt);
                _playing.Add(uid, current);
            }

            current.Source.Looping = jukebox.Playing;
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

    private sealed class JukeboxAudio(IAudioSource source, ResPath path, TimeSpan startedAt)
    {
        public readonly IAudioSource Source = source;
        public readonly ResPath Path = path;
        public readonly TimeSpan StartedAt = startedAt;
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
    }
}
// DS14-end
