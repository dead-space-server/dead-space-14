using System.Linq; // DS14
using Content.Shared.Audio;
using JetBrains.Annotations;
using Content.Shared.DeadSpace.Administration.Events;
using Content.Shared.GameTicking;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems; // DS14
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Server.Player;
using Robust.Shared.Timing;

namespace Content.Server.Audio;

/// <inheritdoc/>
public sealed partial class ServerGlobalSoundSystem : GlobalSoundSystem
{
    [Dependency] private IConsoleHost _conHost = default!;
    [Dependency] private IPlayerManager _playerManager = default!; // DS14
    [Dependency] private SharedAudioSystem _audio = default!; // DS14
    [Dependency] private IGameTiming _timing = default!; // DS14
    [Dependency] private IResourceManager _resourceManager = default!; // DS14

    // DS14-start
    private int _nextAdminSoundId;
    private ActiveAdminSound? _activeAdminSound;
    private readonly List<AdminQueuedSound> _adminQueue = new();

    /// <summary>
    /// Raised whenever the active admin sound or the admin sound queue changes,
    /// so open global sound EUIs can refresh their state.
    /// </summary>
    public event Action? AdminSoundQueueChanged;

    public IReadOnlyList<AdminQueuedSound> AdminQueue => _adminQueue;
    public bool HasActiveAdminSound => _activeAdminSound != null;

    public sealed record AdminQueuedSound(string Path, int Volume, string? CKey);

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
        {
            _activeAdminSound = null;
            AdminSoundQueueChanged?.Invoke();
            PlayNextQueuedSound();
        }
    }
    // DS14-end

    /// <inheritdoc/>
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
        AdminSoundQueueChanged?.Invoke();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _activeAdminSound = null;
        ClearAdminQueue();
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
                AdminSoundQueueChanged?.Invoke();
                return true;
            case AdminGlobalSoundControl.Stop:
                SendControlEvent(sound, action);
                _activeAdminSound = null;
                AdminSoundQueueChanged?.Invoke();
                return true;
            default:
                return false;
        }

        SendControlEvent(sound, action);
        AdminSoundQueueChanged?.Invoke();
        return true;
    }

    // DS14-start
    /// <summary>
    /// Adds a track to the admin sound queue. Returns false if the path is not a loadable OGG file.
    /// </summary>
    public bool TryEnqueueAdminSound(string path, int volume, string? ckey)
    {
        if (!IsValidAdminAudioPath(path))
            return false;

        _adminQueue.Add(new AdminQueuedSound(
            path,
            Math.Clamp(volume, -24, 12),
            string.IsNullOrWhiteSpace(ckey) ? null : ckey.Trim()));
        AdminSoundQueueChanged?.Invoke();
        return true;
    }

    public bool TryRemoveAdminQueueEntry(int index)
    {
        if (index < 0 || index >= _adminQueue.Count)
            return false;

        _adminQueue.RemoveAt(index);
        AdminSoundQueueChanged?.Invoke();
        return true;
    }

    public void ClearAdminQueue()
    {
        if (_adminQueue.Count == 0)
            return;

        _adminQueue.Clear();
        AdminSoundQueueChanged?.Invoke();
    }

    /// <summary>
    /// Plays the next queued track. Entries whose target player is gone are skipped.
    /// </summary>
    public void PlayNextQueuedSound()
    {
        while (_adminQueue.Count > 0)
        {
            var entry = _adminQueue[0];
            _adminQueue.RemoveAt(0);

            if (TryPlayQueuedSound(entry))
                return;
        }

        AdminSoundQueueChanged?.Invoke();
    }

    private bool TryPlayQueuedSound(AdminQueuedSound entry)
    {
        Filter filter;
        ICommonSession[]? recipients = null;

        if (entry.CKey == null)
        {
            filter = Filter.Empty().AddAllPlayers(_playerManager);
        }
        else if (_playerManager.TryGetSessionByUsername(entry.CKey, out var target))
        {
            recipients = [target];
            filter = Filter.Empty().AddPlayer(target);
        }
        else
        {
            return false;
        }

        var resolved = _audio.ResolveSound(new SoundPathSpecifier(entry.Path));
        PlayAdminGlobal(
            filter,
            resolved,
            AudioParams.Default.WithVolume(entry.Volume - 8),
            replay: true,
            recipients,
            entry.Path);
        return true;
    }

    public bool IsValidAdminAudioPath(string path)
    {
        return path.StartsWith("/Audio/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
            && _resourceManager.ContentFileExists(path);
    }
    // DS14-end

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
}
