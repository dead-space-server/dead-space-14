using Content.Shared.Humanoid;
using Content.Shared.DeadSpace.TTS;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Shared.Corvax.TTS;

/// <summary>
/// Prototype represent available TTS voices
/// </summary>
[Prototype("ttsVoice")]
// ReSharper disable once InconsistentNaming
public sealed partial class TTSVoicePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; set; } = default!;

    // DS14-start
    [DataField]
    public string Provider { get; set; } = TTSVoices.Silero;

    [DataField(customTypeSerializer: typeof(PrototypeIdValueDictionarySerializer<string, TTSVoicePrototype>))]
    public Dictionary<string, string> Fallbacks { get; set; } = new();
    // DS14-end

    [DataField("name")]
    public string Name { get; set; } = string.Empty;

    [DataField("sex", required: true)]
    public Sex Sex { get; set; } = default!;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("speaker", required: true)]
    public string Speaker { get; set; } = string.Empty;

    /// <summary>
    /// Whether the species is available "at round start" (In the character editor)
    /// </summary>
    [DataField("roundStart")]
    public bool RoundStart { get; set; } = true;

    [DataField("sponsorOnly")]
    public bool SponsorOnly { get; set; } = false;
}
