using Content.Shared.DeadSpace.TTS;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Corvax.TTS;

/// <summary>
/// Apply TTS for entity chat say messages
/// </summary>
[RegisterComponent, NetworkedComponent]
// ReSharper disable once InconsistentNaming
public sealed partial class TTSComponent : Component
{
    /// <summary>
    /// Prototype of used voice for TTS.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("voice", customTypeSerializer: typeof(PrototypeIdSerializer<TTSVoicePrototype>))]
    // DS14-start
    public string? VoicePrototypeId
    {
        get => Voices.GetValueOrDefault(TTSVoices.Silero);
        set
        {
            if (value == null)
                Voices.Remove(TTSVoices.Silero);
            else
                Voices[TTSVoices.Silero] = value;
        }
    }

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("voices", customTypeSerializer: typeof(PrototypeIdValueDictionarySerializer<string, TTSVoicePrototype>))]
    public Dictionary<string, string> Voices { get; set; } = new();
    // DS14-end
}
