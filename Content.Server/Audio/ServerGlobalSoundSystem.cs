using System.Linq;
using Content.Server.Station.Systems;
using Content.Shared.Audio;
using Content.Shared.DeadSpace.Administration.Events;
using Content.Shared.GameTicking;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Console;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Server.Player;
using Robust.Shared.Timing;

namespace Content.Server.Audio;

public sealed class ServerGlobalSoundSystem : SharedGlobalSoundSystem
{
    [Dependency] private readonly IConsoleHost _conHost = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!; // DS14
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!; // DS14

    // DS14-start
    private int _nextAdminSoundId;
    private ActiveAdminSound? _activeAdminSound;

    public override void Initialize()
    {
        base.Initialize();
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_activeAdminSound is { Paused: false } sound && GetPlaybackPosition(sound) >= sound.Duration)
            _activeAdminSound = null;
    }
    // DS14-end

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged; // DS14
        _conHost.UnregisterCommand("playglobalsound");
    }

    // DS14-start
    public void PlayAdminGlobal(
        Filter playerFilter,
        ResolvedSoundSpecifier specifier,
        AudioParams? audioParams = null,
        bool replay = true,
        IReadOnlyCollection<ICommonSession>? recipients = null,
        string? path = null)
    {
        var parameters = audioParams ?? AudioParams.Default;
        _nextAdminSoundId = _nextAdminSoundId == int.MaxValue ? 1 : _nextAdminSoundId + 1;
        var msg = new AdminSoundEvent(specifier, parameters, _nextAdminSoundId);
        RaiseNetworkEvent(msg, playerFilter, recordReplay: replay);
        _activeAdminSound = new ActiveAdminSound(
            _nextAdminSoundId,
            specifier,
            parameters,
            path,
            recipients?.Select(session => session.UserId).ToHashSet(),
            _timing.CurTime,
            parameters.PlayOffsetSeconds,
            (float) _audio.GetAudioLength(specifier).TotalSeconds);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _activeAdminSound = null;
    }

    public bool ControlAdminGlobalSound(AdminGlobalSoundControl action)
    {
        if (_activeAdminSound is not { } sound)
            return false;

        switch (action)
        {
            case AdminGlobalSoundControl.Pause when !sound.Paused:
                sound.Position = GetPlaybackPosition(sound);
                sound.Paused = true;
                break;
            case AdminGlobalSoundControl.Resume when sound.Paused:
                sound.StartedAt = _timing.CurTime;
                sound.Paused = false;
                break;
            case AdminGlobalSoundControl.FadeOut:
                SendControlEvent(sound, action, 1.5f);
                _activeAdminSound = null;
                return true;
            case AdminGlobalSoundControl.Stop:
                SendControlEvent(sound, action);
                _activeAdminSound = null;
                return true;
            default:
                return false;
        }

        SendControlEvent(sound, action);
        return true;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.InGame || _activeAdminSound is not { } sound)
            return;

        if (sound.Recipients != null && !sound.Recipients.Contains(args.Session.UserId))
            return;

        var position = GetPlaybackPosition(sound);
        if (position >= sound.Duration)
        {
            _activeAdminSound = null;
            return;
        }

        var parameters = sound.Parameters.WithPlayOffset(position);
        var soundEvent = new AdminSoundEvent(sound.Specifier, parameters, sound.Id, sound.Paused);
        RaiseNetworkEvent(soundEvent, args.Session.Channel);
    }

    private float GetPlaybackPosition(ActiveAdminSound sound)
    {
        if (sound.Paused)
            return sound.Position;

        return sound.Position + (float) (_timing.CurTime - sound.StartedAt).TotalSeconds;
    }

    private void SendControlEvent(ActiveAdminSound sound, AdminGlobalSoundControl action, float fadeDuration = 0f)
    {
        var message = new AdminSoundPlaybackControlEvent(sound.Id, action, fadeDuration);
        if (sound.Recipients == null)
        {
            RaiseNetworkEvent(message, Filter.Broadcast(), recordReplay: false);
            return;
        }

        var filter = Filter.Empty();
        foreach (var userId in sound.Recipients)
        {
            if (_playerManager.TryGetSessionById(userId, out var session))
                filter.AddPlayer(session);
        }

        RaiseNetworkEvent(message, filter, recordReplay: false);
    }

    public string? ActiveAdminSoundPath => _activeAdminSound?.Path;
    public bool ActiveAdminSoundPaused => _activeAdminSound?.Paused ?? false;

    private sealed class ActiveAdminSound(
        int id,
        ResolvedSoundSpecifier specifier,
        AudioParams parameters,
        string? path,
        HashSet<NetUserId>? recipients,
        TimeSpan startedAt,
        float position,
        float duration)
    {
        public int Id { get; } = id;
        public ResolvedSoundSpecifier Specifier { get; } = specifier;
        public AudioParams Parameters { get; } = parameters;
        public string? Path { get; } = path;
        public HashSet<NetUserId>? Recipients { get; } = recipients;
        public TimeSpan StartedAt { get; set; } = startedAt;
        public float Position { get; set; } = position;
        public float Duration { get; } = duration;
        public bool Paused { get; set; }
    }
    // DS14-end

    // DS14-start
    public void PlayAlertLevelGlobal(Filter playerFilter, SoundSpecifier sound, AudioParams? audioParams = null, bool replay = true)
    {
        PlayAlertLevelGlobal(playerFilter, _audio.ResolveSound(sound), audioParams ?? sound.Params, replay);
    }

    public void PlayAlertLevelGlobal(Filter playerFilter, ResolvedSoundSpecifier specifier, AudioParams? audioParams = null, bool replay = true)
    {
        var msg = new AlertLevelSoundEvent(specifier, audioParams);
        RaiseNetworkEvent(msg, playerFilter, recordReplay: replay);
    }
    public void PlayAnnonceGlobal(Filter playerFilter, SoundSpecifier sound, AudioParams? audioParams = null, bool replay = true)
    {
        PlayAnnonceGlobal(playerFilter, _audio.ResolveSound(sound), audioParams ?? sound.Params, replay);
    }

    public void PlayAnnonceGlobal(Filter playerFilter, ResolvedSoundSpecifier specifier, AudioParams? audioParams = null, bool replay = true)
    {
        var msg = new AdminAnnouncmentSoundEvent(specifier, audioParams);
        RaiseNetworkEvent(msg, playerFilter, recordReplay: replay);
    }
    // DS14-end

    private Filter GetStationAndPvs(EntityUid source)
    {
        var stationFilter = _stationSystem.GetInOwningStation(source);
        stationFilter.AddPlayersByPvs(source, entityManager: EntityManager);
        return stationFilter;
    }

    public void PlayGlobalOnStation(EntityUid source, ResolvedSoundSpecifier specifier, AudioParams? audioParams = null)
    {
        var msg = new GameGlobalSoundEvent(specifier, audioParams);
        var filter = GetStationAndPvs(source);
        RaiseNetworkEvent(msg, filter);
    }

    public void StopStationEventMusic(EntityUid source, StationEventMusicType type)
    {
        // TODO REPLAYS
        // these start & stop events are gonna be a PITA
        // theres probably some nice way of handling them. Maybe it just needs dedicated replay data (in which case these events should NOT get recorded).

        var msg = new StopStationEventMusic(type);
        var filter = GetStationAndPvs(source);
        RaiseNetworkEvent(msg, filter);
    }

    public void DispatchStationEventMusic(EntityUid source, SoundSpecifier sound, StationEventMusicType type)
    {
        DispatchStationEventMusic(source, _audio.ResolveSound(sound), type);
    }

    public void DispatchStationEventMusic(EntityUid source, ResolvedSoundSpecifier specifier, StationEventMusicType type)
    {
        var audio = AudioParams.Default.WithVolume(-8);
        var msg = new StationEventMusicEvent(specifier, type, audio);

        var filter = GetStationAndPvs(source);
        RaiseNetworkEvent(msg, filter);
    }
}
