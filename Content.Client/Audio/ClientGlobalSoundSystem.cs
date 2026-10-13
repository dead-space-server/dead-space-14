using Content.Shared.Audio;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.Administration.Events;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.GameTicking;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Client.Audio;

public sealed class ClientGlobalSoundSystem : SharedGlobalSoundSystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ContentAudioSystem _contentAudio = default!; // DS14

    // Admin music
    private bool _adminAudioEnabled = true;
    private readonly Dictionary<int, (EntityUid Stream, float BaseVolume)> _adminAudio = new(1); // DS14
    private int _nextUntrackedAdminAudioId; // DS14

    // Event sounds (e.g. nuke timer)
    private bool _eventAudioEnabled = true;
    private Dictionary<StationEventMusicType, EntityUid?> _eventAudio = new(1);

    // Alert level sounds
    private float _alertLevelVolume = 1f; // DS14
    private float _annocmentVolume = 1f; // DS14
    private float _adminVolume = 1f; //DS14

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeNetworkEvent<AdminSoundEvent>(PlayAdminSound);
        SubscribeNetworkEvent<AdminSoundPlaybackControlEvent>(ControlAdminSound); // DS14
        Subs.CVar(_cfg, CCVars.AdminSoundsEnabled, ToggleAdminSound, true);

        SubscribeNetworkEvent<StationEventMusicEvent>(PlayStationEventMusic);
        SubscribeNetworkEvent<StopStationEventMusic>(StopStationEventMusic);
        Subs.CVar(_cfg, CCVars.EventMusicEnabled, ToggleStationEventMusic, true);

        SubscribeNetworkEvent<GameGlobalSoundEvent>(PlayGameSound);
        SubscribeNetworkEvent<AlertLevelSoundEvent>(PlayAlertLevelSound); // DS14
        SubscribeNetworkEvent<AdminAnnouncmentSoundEvent>(PlayAnnocmentSound); // DS14
        Subs.CVar(_cfg, CCCCVars.AlertLevelVolume, SetAlertLevelVolume, true); // DS-14
        Subs.CVar(_cfg, CCCCVars.AnnonceVolume, SetAnnocmentVolume, true); // DS-14
        Subs.CVar(_cfg, CCCCVars.AdminVolume, SetAdminVolume, true); // DS-14
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        ClearAudio();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        ClearAudio();
    }

    private void ClearAudio()
    {
        foreach (var (stream, _) in _adminAudio.Values) // DS14
        {
            _audio.Stop(stream);
        }
        _adminAudio.Clear();

        foreach (var stream in _eventAudio.Values)
        {
            _audio.Stop(stream);
        }

        _eventAudio.Clear();
    }

    private void PlayAdminSound(AdminSoundEvent soundEvent)
    {
        if(!_adminAudioEnabled) return;

        // DS14-start
        var streamId = soundEvent.StreamId;
        if (streamId == 0)
            streamId = --_nextUntrackedAdminAudioId;

        if (_adminAudio.Remove(streamId, out var existing))
            _audio.Stop(existing.Stream);

        var baseAudioParams = soundEvent.AudioParams ?? AudioParams.Default;
        var audioParams = baseAudioParams.AddVolume(SharedAudioSystem.GainToVolume(_adminVolume));
        var stream = _audio.PlayGlobal(soundEvent.Specifier, Filter.Local(), false, audioParams);
        if (stream != null)
        {
            _adminAudio[streamId] = (stream.Value.Entity, baseAudioParams.Volume);
            if (soundEvent.Paused)
                _audio.SetState(stream.Value.Entity, AudioState.Paused, component: stream.Value.Component);
        }
        // DS14-end
    }

    // DS14-start
    private void ControlAdminSound(AdminSoundPlaybackControlEvent soundEvent)
    {
        if (!_adminAudio.TryGetValue(soundEvent.StreamId, out var audio))
            return;

        switch (soundEvent.Action)
        {
            case AdminGlobalSoundControl.Pause:
                _audio.SetState(audio.Stream, AudioState.Paused);
                break;
            case AdminGlobalSoundControl.Resume:
                _audio.SetState(audio.Stream, AudioState.Playing);
                break;
            case AdminGlobalSoundControl.Stop:
                _audio.Stop(audio.Stream);
                _adminAudio.Remove(soundEvent.StreamId);
                break;
            case AdminGlobalSoundControl.FadeOut:
                _contentAudio.FadeOut(audio.Stream, duration: soundEvent.FadeDuration);
                _adminAudio.Remove(soundEvent.StreamId);
                break;
        }
    }
    // DS14-end

    private void PlayStationEventMusic(StationEventMusicEvent soundEvent)
    {
        // Either the cvar is disabled or it's already playing
        if(!_eventAudioEnabled || _eventAudio.ContainsKey(soundEvent.Type)) return;

        var stream = _audio.PlayGlobal(soundEvent.Specifier, Filter.Local(), false, soundEvent.AudioParams);
        _eventAudio.Add(soundEvent.Type, stream?.Entity);
    }

    private void PlayGameSound(GameGlobalSoundEvent soundEvent)
    {
        _audio.PlayGlobal(soundEvent.Specifier, Filter.Local(), false, soundEvent.AudioParams);
    }

    // DS14-start
    private void PlayAlertLevelSound(AlertLevelSoundEvent soundEvent)
    {
        var audioParams = soundEvent.AudioParams ?? AudioParams.Default;
        audioParams = audioParams.AddVolume(SharedAudioSystem.GainToVolume(_alertLevelVolume));
        _audio.PlayGlobal(soundEvent.Specifier, Filter.Local(), false, audioParams);
    }
    private void PlayAnnocmentSound(AdminAnnouncmentSoundEvent soundEvent)
    {
        var audioParams = soundEvent.AudioParams ?? AudioParams.Default;
        audioParams = audioParams.AddVolume(SharedAudioSystem.GainToVolume(_annocmentVolume));
        _audio.PlayGlobal(soundEvent.Specifier, Filter.Local(), false, audioParams);
    }
    // DS14-end

    private void StopStationEventMusic(StopStationEventMusic soundEvent)
    {
        if (!_eventAudio.TryGetValue(soundEvent.Type, out var stream))
            return;

        _audio.Stop(stream);
        _eventAudio.Remove(soundEvent.Type);
    }

    private void ToggleAdminSound(bool enabled)
    {
        _adminAudioEnabled = enabled;
        if (_adminAudioEnabled) return;
        foreach (var (stream, _) in _adminAudio.Values) // DS14
        {
            _audio.Stop(stream);
        }
        _adminAudio.Clear();
    }

    private void ToggleStationEventMusic(bool enabled)
    {
        _eventAudioEnabled = enabled;
        if (_eventAudioEnabled) return;
        foreach (var stream in _eventAudio)
        {
            _audio.Stop(stream.Value);
        }
        _eventAudio.Clear();
    }

    // DS14-start
    private void SetAlertLevelVolume(float volume)
    {
        _alertLevelVolume = volume;
    }
    private void SetAnnocmentVolume(float volume)
    {
        _annocmentVolume = volume;
    }
    private void SetAdminVolume(float volume)
    {
        _adminVolume = volume;
        var volumeOffset = SharedAudioSystem.GainToVolume(volume);
        foreach (var (stream, baseVolume) in _adminAudio.Values)
        {
            _audio.SetVolume(stream, baseVolume + volumeOffset);
        }
    }
    // DS14-end
}
