// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System.Linq;
using Content.Shared.Corvax.TTS;
using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.TTS;

public static class TTSVoices
{
    public const string Silero = "silero";
    public const string Ntts = "ntts";

    public static readonly string[] Providers = [Silero, Ntts];

    public static string DefaultVoice(string provider, Sex sex) => provider == Ntts
        ? sex == Sex.Female ? "NTTSCirilla" : "NTTSBarni"
        : SharedHumanoidAppearanceSystem.DefaultSexVoice[sex];

    /// <summary>
    /// Resolve legacy IDs without changing the saved selection for either provider.
    /// Exact IDs/API speakers take priority over the curated replacements in voice prototypes.
    /// </summary>
    public static TTSVoicePrototype? Resolve(IPrototypeManager prototypes, string provider, string? voiceId, Sex? sex = null)
    {
        if (string.IsNullOrEmpty(voiceId) || !prototypes.TryIndex<TTSVoicePrototype>(voiceId, out var voice))
            return null;

        if (voice.Provider == provider)
            return voice;

        var match = prototypes.EnumeratePrototypes<TTSVoicePrototype>().FirstOrDefault(candidate =>
            candidate.Provider == provider &&
            (candidate.Speaker == voice.Speaker || candidate.Speaker.Equals(voice.ID, StringComparison.OrdinalIgnoreCase) ||
             candidate.ID.Equals("NTTS" + voice.ID, StringComparison.OrdinalIgnoreCase)));
        if (match != null)
            return match;

        if (voice.Fallbacks.TryGetValue(provider, out var fallbackId) &&
            prototypes.TryIndex<TTSVoicePrototype>(fallbackId, out var pairedVoice) && pairedVoice.Provider == provider)
            return pairedVoice;

        var fallback = DefaultVoice(provider, sex ?? voice.Sex);
        return prototypes.Index<TTSVoicePrototype>(fallback);
    }

    public static string? GetVoice(TTSComponent component, string provider) =>
        component.Voices.GetValueOrDefault(provider) ?? component.VoicePrototypeId;
}
