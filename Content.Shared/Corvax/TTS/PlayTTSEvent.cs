using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Corvax.TTS;

[Serializable, NetSerializable]
// ReSharper disable once InconsistentNaming
public sealed class PlayTTSEvent : EntityEventArgs
{
    public byte[] Data { get; }
    public NetEntity? SourceUid { get; }
    public bool IsWhisper { get; }
    public bool IsRadio { get; }
    // DS14-start
    public bool IsSuitRadio { get; }
    public bool RadioCueOnly { get; }
    public bool SuppressEcho { get; }
    // DS14-end
    public bool IsLexiconSound { get; } // DS14-Language
    public string LanguageId { get; } // DS14-Language
    public float? DistanceOverride { get; } // DS14: remote microphones provide their own listening distance.
    public PlayTTSEvent(byte[] data, NetEntity? sourceUid = null, bool isWhisper = false, bool isRadio = false, bool isSoundLexicon = false, string languageId = "", float? distanceOverride = null, bool isSuitRadio = false, bool radioCueOnly = false, bool suppressEcho = false) // DS14: per-recipient microphone attenuation and radio speech effects.
    {
        Data = data;
        SourceUid = sourceUid;
        IsWhisper = isWhisper;
        IsRadio = isRadio;
        // DS14-start
        IsSuitRadio = isSuitRadio;
        RadioCueOnly = radioCueOnly;
        SuppressEcho = suppressEcho;
        // DS14-end
        IsLexiconSound = isSoundLexicon; // DS14-Language
        LanguageId = languageId; // DS14-Language
        DistanceOverride = distanceOverride; // DS14
    }
}
