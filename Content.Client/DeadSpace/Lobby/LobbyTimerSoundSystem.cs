using Content.Client.GameTicking.Managers;
using Content.Shared.GameTicking;
using Robust.Client.Audio;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Lobby;

/// <summary>
/// Manages the timer sound effects for the lobby, including ticking, pause/resume, and time modifications.
/// </summary>
public sealed class LobbyTimerSoundSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    private ClientGameTicker _gameTicker = default!;

    /// <summary>
    /// Centralized resource paths for timer sound effects.
    /// </summary>
    private static class TimerSounds
    {
        private static readonly ResPath BasePath = ResPath.Root / "Audio" / "_DeadSpace" / "Lobby" / "Timer";

        public static readonly ResPath Tick     = BasePath / "tick.ogg";
        public static readonly ResPath Pause    = BasePath / "pause.ogg";
        public static readonly ResPath Resume   = BasePath / "resume.ogg";
        public static readonly ResPath Modified = BasePath / "modified.ogg";
    }

    /// <summary>
    /// Keeps track of the last second value when a tick sound was played.
    /// </summary>
    private int _lastTickSecond = -1;

    /// <summary>
    /// Stores the previous target (start) time to detect changes.
    /// </summary>
    private TimeSpan _lastStartTime = TimeSpan.MinValue;

    /// <summary>
    /// Remembers if the game was previously paused.
    /// </summary>
    private bool _wasPaused;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<TickerLobbyCountdownEvent>(OnTickerLobbyCountdown);
        _gameTicker = _entityManager.System<ClientGameTicker>();
    }

    /// <summary>
    /// Called every frame to update timer sounds based on the remaining time.
    /// </summary>
    public void Update()
    {
        // Do not process sounds if the game has already started or is paused.
        if (_gameTicker.IsGameStarted || _gameTicker.Paused)
            return;

        var remaining = _gameTicker.StartTime - _gameTiming.CurTime;
        if (remaining.TotalSeconds is <= 0 or > 60)
        {
            _lastTickSecond = -1;
            return;
        }

        var tickInterval = GetTickInterval(remaining.TotalSeconds);
        var secondsLeft = (int)Math.Floor(remaining.TotalSeconds);

        if (secondsLeft == _lastTickSecond || secondsLeft % tickInterval != 0)
            return;

        PlaySound(TimerSounds.Tick);
        _lastTickSecond = secondsLeft;
    }

    /// <summary>
    /// Handles lobby countdown network events to process pause/resume and start time modification events.
    /// </summary>
    /// <param name="msg">The lobby countdown event message.</param>
    /// <param name="args">Additional event arguments.</param>
    private void OnTickerLobbyCountdown(TickerLobbyCountdownEvent msg, EntitySessionEventArgs args)
    {
        if (_gameTicker.IsGameStarted)
            return;

        // First, process pause/resume events.
        if (!ProcessPauseResume(msg.Paused))
        {
            // If there is no change in the pause state, check for modifications to the start time.
            ProcessStartTimeModification(msg.StartTime);
        }
    }

    /// <summary>
    /// Processes changes to the start time and plays a sound if the time was modified.
    /// </summary>
    /// <param name="startTime">The new start time.</param>
    private void ProcessStartTimeModification(TimeSpan startTime)
    {
        if (startTime != _lastStartTime)
            PlaySound(TimerSounds.Modified);

        _lastStartTime = startTime;
    }

    /// <summary>
    /// Processes pause/resume events by comparing the current state to the previous state.
    /// </summary>
    /// <param name="paused">The current pause state.</param>
    /// <returns>True if a pause/resume sound was played; otherwise, false.</returns>
    private bool ProcessPauseResume(bool paused)
    {
        if (paused == _wasPaused)
            return false;

        PlaySound(paused ? TimerSounds.Pause : TimerSounds.Resume);
        _wasPaused = paused;

        return true;
    }

    /// <summary>
    /// Determines the tick interval based on the remaining time.
    /// Rules:
    /// - 5 seconds or less: tick every 1 second.
    /// - 14 seconds or less: tick every 2 seconds.
    /// - 30 seconds or less: tick every 3 seconds.
    /// - 60 seconds or less: tick every 5 seconds.
    /// </summary>
    /// <param name="remainingSeconds">The remaining time in seconds.</param>
    /// <returns>The tick interval in seconds.</returns>
    private static int GetTickInterval(double remainingSeconds)
    {
        return remainingSeconds switch
        {
            <= 5 => 1,
            <= 14 => 2,
            <= 30 => 3,
            _ => 5,
        };
    }

    /// <summary>
    /// Plays a sound using the global audio system.
    /// </summary>
    /// <param name="soundPath">The resource path to the sound file.</param>
    private void PlaySound(ResPath soundPath)
    {
        _audio.PlayGlobal(soundPath.ToString(), Filter.Broadcast(), recordReplay: false);
    }
}
