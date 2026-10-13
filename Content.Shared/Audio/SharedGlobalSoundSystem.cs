using Content.Shared.CCVar;
using Robust.Shared.Audio;
using Robust.Shared.Serialization;
namespace Content.Shared.Audio;

/// <summary>
/// Handles playing audio to all players globally unless disabled by cvar. Some events are grid-specific.
/// </summary>
public abstract class SharedGlobalSoundSystem : EntitySystem
{
}

[Virtual]
[Serializable, NetSerializable]
public class GlobalSoundEvent : EntityEventArgs
{
    public ResolvedSoundSpecifier Specifier;
    public AudioParams? AudioParams;
    public GlobalSoundEvent(ResolvedSoundSpecifier specifier, AudioParams? audioParams = null)
    {
        Specifier = specifier;
        AudioParams = audioParams;
    }
}

/// <summary>
/// Intended for admin music. Can be disabled by the <seealso cref="CCVars.AdminSoundsEnabled"/> cvar.
/// </summary>
[Serializable, NetSerializable]
public sealed class AdminSoundEvent : GlobalSoundEvent
{
    // DS14-start
    public int StreamId;
    public bool Paused;

    public AdminSoundEvent(
        ResolvedSoundSpecifier specifier,
        AudioParams? audioParams = null,
        int streamId = 0,
        bool paused = false) : base(specifier, audioParams)
    {
        StreamId = streamId;
        Paused = paused;
    }
    // DS14-end
}

/// <summary>
/// Intended for misc sound effects. Can't be disabled by cvar.
/// </summary>
[Serializable, NetSerializable]
public sealed class GameGlobalSoundEvent : GlobalSoundEvent
{
    public GameGlobalSoundEvent(ResolvedSoundSpecifier specifier, AudioParams? audioParams = null) : base(specifier, audioParams){}
}

// DS14-start
/// <summary>
/// Intended for alert level sounds. Volume is adjusted client-side by a dedicated cvar.
/// </summary>
[Serializable, NetSerializable]
public sealed class AlertLevelSoundEvent : GlobalSoundEvent
{
    public AlertLevelSoundEvent(ResolvedSoundSpecifier specifier, AudioParams? audioParams = null) : base(specifier, audioParams){}
}
/// <summary>
/// Нагло скопирован с верхнего примера, спектрум обожаю, но пожалуйста не играй на яо.
/// </summary>
[Serializable, NetSerializable]
public sealed class AdminAnnouncmentSoundEvent : GlobalSoundEvent
{
    public AdminAnnouncmentSoundEvent(ResolvedSoundSpecifier specifier, AudioParams? audioParams = null) : base(specifier, audioParams){}
}
// DS14-end

public enum StationEventMusicType : byte
{
    Nuke,
    Convergence // DS14
}

/// <summary>
/// Intended for music triggered by events on a specific station. Can be disabled by the <seealso cref="CCVars.EventMusicEnabled"/> cvar.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationEventMusicEvent : GlobalSoundEvent
{
    public StationEventMusicType Type;

    public StationEventMusicEvent(ResolvedSoundSpecifier specifier, StationEventMusicType type, AudioParams? audioParams = null) : base(
        specifier, audioParams)
    {
        Type = type;
    }
}

/// <summary>
/// Attempts to stop a playing <seealso cref="StationEventMusicEvent"/> stream.
/// </summary>
[Serializable, NetSerializable]
public sealed class StopStationEventMusic : EntityEventArgs
{
    public StationEventMusicType Type;

    public StopStationEventMusic(StationEventMusicType type)
    {
        Type = type;
    }
}
