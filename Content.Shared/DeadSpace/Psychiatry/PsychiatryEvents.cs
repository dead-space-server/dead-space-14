// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Psychiatry;

[Serializable, NetSerializable]
public sealed class PsychiatryWhisperEvent : EntityEventArgs
{
    public string SpeakerName;
    public string Message;
    public NetEntity? Source;

    public bool AsRadio;

    public string Job = "";

    public string JobColor = "#32cd32";

    public PsychiatryWhisperEvent(string speakerName, string message, NetEntity? source)
    {
        SpeakerName = speakerName;
        Message = message;
        Source = source;
    }
}

[ByRefEvent]
public record struct PsychiatryBrainActivityEvent(PsychiatryBrainRegion Region, float Strength = 1f);

public enum PsychiatryBrainRegion : byte
{
    Hearing,
    Voice,
    Vision,
    Arousal,
    Fear,
    Movement,
    Memory,
}

[Serializable, NetSerializable]
public sealed class PsychiatryTypingRequestEvent : EntityEventArgs
{
    public bool Typing;

    public PsychiatryTypingRequestEvent(bool typing)
    {
        Typing = typing;
    }
}

[Serializable, NetSerializable]
public sealed class PsychiatryUnrealSoundEvent : EntityEventArgs
{
    public float Hearing;
    public float Fear;

    public PsychiatryUnrealSoundEvent(float hearing, float fear)
    {
        Hearing = hearing;
        Fear = fear;
    }
}
