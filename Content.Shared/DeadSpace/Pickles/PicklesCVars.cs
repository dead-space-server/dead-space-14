// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Configuration;

namespace Content.Shared.DeadSpace.Pickles;

[CVarDefs]
public sealed class PicklesCVars
{
    /// <summary>
    /// Master toggle for the Pickles overlay. Construction recipes stay, fermentation logic no-ops when false.
    /// </summary>
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("pickles.enabled", true, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);
}
