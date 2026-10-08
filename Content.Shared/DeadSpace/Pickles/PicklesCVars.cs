// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Configuration;

namespace Content.Shared.DeadSpace.Pickles;

[CVarDefs]
public sealed class PicklesCVars
{
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("pickles.enabled", true, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);
}
