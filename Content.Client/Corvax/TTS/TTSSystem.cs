using Content.Shared.Chat;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Corvax.TTS;
using Content.Shared.DeadSpace.CCCCVars;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Timing;
using Content.Shared.GameTicking;
using Content.Client.DeadSpace.Audio;
using Content.Shared.DeadSpace.Audio;
using Content.Shared.DeadSpace.Languages.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio.Components;

namespace Content.Client.Corvax.TTS;

/// <summary>
/// Plays TTS audio in world
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class TTSSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    // DS14-start
    [Dependency] private readonly AreaEchoSystem _echo = default!;
    [Dependency] private readonly IAudioManager _audioManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    private readonly Dictionary<EntityUid, (AudioStream Stream, bool Radio)> _streams = new();
    private readonly List<Transmission> _transmissions = new();
    [Dependency] private readonly IGameTiming _timing = default!;
    private readonly List<EntityUid> _finished = new();
    private bool _radioCues = true;
    // DS14-end

    private ISawmill _sawmill = default!;
    // DS14-start
    /// <summary>
    /// Gain multiplier for whispered TTS relative to normal local TTS.
    /// </summary>
    internal const float WhisperVolumeMultiplier = 0.25f;

    // A remote microphone supplies attenuation itself, so the listener's movable AI eye must not clip the stream.
    private const float RemoteMicrophonePlaybackRange = 64f;
    // DS14-end

    /// <summary>
    /// The volume at which the TTS sound will not be heard.
    /// </summary>
    private const float MinimalVolume = -6f;

    private float _volume = 0.0f;
    private float _volumeRadio = 0.0f;
    private bool _playRadio = true;

    public override void Initialize()
    {
        // DS14-start
        UpdatesAfter.Add(typeof(AudioSystem));
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => ClearSpeech());
        SubscribeNetworkEvent<TickerJoinLobbyEvent>(_ => ClearSpeech());
        Subs.CVar(_cfg, AreaEchoCVars.RadioCues, value => _radioCues = value, true);
        // DS14-end
        _sawmill = Logger.GetSawmill("tts");
        _cfg.OnValueChanged(CCCVars.TTSVolume, OnTtsVolumeChanged, true);
        _cfg.OnValueChanged(CCCCVars.TTSVolumeRadio, OnTtsRadioVolumeChanged, true);
        _cfg.OnValueChanged(CCCCVars.RadioTTSSoundsEnabled, OnTtsPlayRadioChanged, true);
        SubscribeNetworkEvent<PlayTTSEvent>(OnPlayTTS);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(CCCVars.TTSVolume, OnTtsVolumeChanged);
        _cfg.UnsubValueChanged(CCCCVars.TTSVolumeRadio, OnTtsRadioVolumeChanged);
        _cfg.UnsubValueChanged(CCCCVars.RadioTTSSoundsEnabled, OnTtsPlayRadioChanged);
        ClearSpeech(); // DS14
    }

    public void RequestPreviewTTS(string voiceId)
    {
        RaiseNetworkEvent(new RequestPreviewTTSEvent(voiceId));
    }

    private void OnTtsVolumeChanged(float volume)
    {
        _volume = volume;
    }

    private void OnTtsRadioVolumeChanged(float volume)
    {
        _volumeRadio = volume;
    }
    private void OnTtsPlayRadioChanged(bool radio)
    {
        _playRadio = radio;
    }

    // DS14-start
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        _finished.Clear();
        foreach (var (uid, stream) in _streams)
        {
            if (TryComp<AudioComponent>(uid, out var audio))
            {
                // Positional processing resets occlusion every frame. Apply the receiver filter afterwards.
                if (stream.Radio)
                    audio.Occlusion = RadioSpeech.FilterStrength;
                continue;
            }
            stream.Stream.Dispose();
            _finished.Add(uid);
        }
        foreach (var uid in _finished)
            _streams.Remove(uid);
        UpdateTransmissions(_timing.RealTime);
    }

    internal void UpdateTransmissions(TimeSpan now)
    {
        for (var i = _transmissions.Count - 1; i >= 0; i--)
        {
            var transmission = _transmissions[i];
            if (now < transmission.NextAt)
                continue;
            if (!transmission.Started)
            {
                transmission.Started = true;
                var duration = transmission.Stream?.Length ?? TimeSpan.Zero;
                if (transmission.Stream is { } stream)
                {
                    transmission.Stream = null;
                    if (transmission.Event.RadioCueOnly)
                        stream.Dispose();
                    else
                        PlaySpeech(stream, transmission.Source, transmission.Params, transmission.Event.IsWhisper, true);
                }
                transmission.NextAt = now + duration;
                if (duration > TimeSpan.Zero)
                    continue;
            }
            PlayCue(transmission.Source, transmission.Params, true);
            _transmissions.RemoveAt(i);
        }
    }

    private sealed class Transmission(AudioStream? stream, EntityUid? source, AudioParams parameters,
        PlayTTSEvent ev, TimeSpan nextAt)
    {
        public AudioStream? Stream = stream;
        public readonly EntityUid? Source = source;
        public readonly AudioParams Params = parameters;
        public readonly PlayTTSEvent Event = ev;
        public TimeSpan NextAt = nextAt;
        public bool Started;
    }

    internal void ClearSpeech()
    {
        foreach (var (uid, stream) in _streams)
        {
            if (!Deleted(uid))
                Del(uid);
            stream.Stream.Dispose();
        }
        _streams.Clear();
        foreach (var transmission in _transmissions)
            transmission.Stream?.Dispose();
        _transmissions.Clear();
    }

    private void PlayCue(EntityUid? source, AudioParams parameters, bool closing)
    {
        if (!_radioCues)
            return;
        var stream = _audioManager.LoadAudioRaw(RadioSpeech.CreateCue(closing), 1, RadioSpeech.SampleRate);
        PlaySpeech(stream, source, parameters, false, true, cue: true);
    }

    private void PlaySpeech(AudioStream stream, EntityUid? source, AudioParams parameters, bool whisper, bool radio,
        bool cue = false, bool suppressEcho = false)
    {
        EntityUid? soundUid = null;
        try
        {
            if (source is { } uid && TerminatingOrDeleted(uid))
            {
                stream.Dispose();
                return;
            }
            var playing = source is { } entity
                ? _audio.PlayEntity(stream, entity, null, parameters.WithVolume(float.NegativeInfinity))
                : _audio.PlayGlobal(stream, null, parameters.WithVolume(float.NegativeInfinity));
            if (playing is not { } sound)
            {
                stream.Dispose();
                return;
            }
            soundUid = sound.Entity;
            _streams.Add(sound.Entity, (stream, radio && !cue));
            // Configure the receiver before making it audible, including short opening/closing cues.
            _echo.ConfigureSpeech((sound.Entity, sound.Component), whisper, radio, suppressEcho);
            if (radio && !cue)
                sound.Component.Occlusion = RadioSpeech.FilterStrength;
            _audio.SetVolume(sound.Entity, parameters.Volume, sound.Component);
        }
        catch (Exception e)
        {
            if (soundUid is { } uid)
            {
                _streams.Remove(uid);
                if (!Deleted(uid))
                    Del(uid);
            }
            stream.Dispose();
            _sawmill.Warning($"Could not play speech audio: {e.Message}");
        }
    }

    internal void OnPlayTTS(PlayTTSEvent ev)
    {
        if (ev.IsRadio && (!_playRadio || _volumeRadio <= 0f) || !ev.IsRadio && _volume <= 0f)
            return;
        EntityUid? source = null;
        if (!ev.IsRadio && ev.SourceUid is { } netSource)
        {
            if (!TryGetEntity(netSource, out source) || source == null)
                return;
        }

        var radio = ev.IsRadio || ev.IsSuitRadio;
        var maxDistance = ev.IsWhisper ? SpatialAudio.WhisperRange : SharedChatSystem.VoiceRange;
        var audioParams = AudioParams.Default
            .WithVolume(AdjustVolume(ev.IsWhisper, ev.IsRadio))
            .WithMaxDistance(maxDistance);
        if (ev.DistanceOverride is { } distance)
        {
            var gain = CalculateDistanceGain(distance, maxDistance);
            if (gain <= 0f)
                return;
            audioParams = audioParams.AddVolume(SharedAudioSystem.GainToVolume(gain))
                .WithRolloffFactor(0f).WithMaxDistance(RemoteMicrophonePlaybackRange);
        }

        AudioStream? stream = null;
        try
        {
            var data = ev.Data;
            if (ev.IsLexiconSound && !string.IsNullOrEmpty(ev.LanguageId) && _prototypes.TryIndex<LanguagePrototype>(ev.LanguageId, out var language) &&
                language.LexiconSound is { } lexicon)
            {
                var path = _audio.GetAudioPath(_audio.ResolveSound(lexicon))!;
                using var file = _res.ContentFileRead(path);
                using var memory = new System.IO.MemoryStream();
                file.CopyTo(memory);
                data = memory.ToArray();
            }
            if (data.Length == 0 && !radio)
                return;

            if (data.Length > 0)
            {
                // Only the engine may access the Vorbis decoder from a sandboxed client assembly.
                using var input = new System.IO.MemoryStream(data, false);
                stream = _audioManager.LoadAudioOggVorbis(input);
            }
            if (radio)
            {
                PlayCue(source, audioParams, false);
                _transmissions.Add(new Transmission(stream, source, audioParams, ev,
                    _timing.RealTime + TimeSpan.FromSeconds(_radioCues ? RadioSpeech.OpeningDuration : 0f)));
            }
            else if (stream != null)
                PlaySpeech(stream, source, audioParams, ev.IsWhisper, false, suppressEcho: ev.SuppressEcho);
            stream = null;
        }
        catch (Exception e)
        {
            stream?.Dispose();
            _sawmill.Warning($"Could not play speech audio: {e.Message}");
        }
    }
    // DS14-end

    private float AdjustVolume(bool isWhisper, bool isRadio)
    {
        var volume = MinimalVolume + SharedAudioSystem.GainToVolume(_volume);

        if (isWhisper && !isRadio)
        {
            volume += SharedAudioSystem.GainToVolume(WhisperVolumeMultiplier); // DS14
        }
        else if (isRadio)
        {
            volume = MinimalVolume + SharedAudioSystem.GainToVolume(_volumeRadio);
        }

        return volume;
    }

    // DS14-start
    internal static float CalculateDistanceGain(float distance, float maxDistance)
    {
        var referenceDistance = AudioParams.Default.ReferenceDistance;
        if (maxDistance <= referenceDistance)
            return distance <= referenceDistance ? 1f : 0f;

        var clampedDistance = Math.Clamp(distance, referenceDistance, maxDistance);
        return Math.Clamp(
            1f - AudioParams.Default.RolloffFactor *
            (clampedDistance - referenceDistance) / (maxDistance - referenceDistance),
            0f,
            1f);
    }
    // DS14-end
}
