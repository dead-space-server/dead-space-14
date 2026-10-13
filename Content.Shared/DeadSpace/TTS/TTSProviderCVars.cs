// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Robust.Shared.Configuration;

namespace Content.Shared.DeadSpace.TTS;

[CVarDefs]
public sealed class TTSProviderCVars
{
    public static readonly CVarDef<string> Provider =
        CVarDef.Create("tts.api_provider", TTSVoices.Silero, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<string> NttsApiUrl =
        CVarDef.Create("tts.ntts_api_url", "https://ntts.fdev.team/api/v1/tts", CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> NttsApiToken =
        CVarDef.Create("tts.ntts_api_token", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<bool> NttsPauses =
        CVarDef.Create("tts.ntts_pauses", true, CVar.SERVERONLY | CVar.ARCHIVE);
}
