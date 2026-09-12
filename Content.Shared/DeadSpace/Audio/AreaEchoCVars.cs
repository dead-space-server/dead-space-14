// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Configuration;

namespace Content.Shared.DeadSpace.Audio;

[CVarDefs]
public sealed class AreaEchoCVars
{
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("deadspace.audio.area_echo", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> HighQuality =
        CVarDef.Create("deadspace.audio.area_echo_high_quality", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> SpaceMuffling =
        CVarDef.Create("deadspace.audio.space_muffling", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
