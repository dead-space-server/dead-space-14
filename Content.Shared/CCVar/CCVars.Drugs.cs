// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///    настройка зависимости addication от тяжести наркотика (addication не будет превышать значение для зависимости с уровнем тяжести)
    /// </summary>
    public static readonly CVarDef<bool>
        EnableMaxAddication = CVarDef.Create("drugs.max_addication", false, CVar.SERVER);


    public static readonly CVarDef<int>
        MaxDrugStr = CVarDef.Create("drugs.max_drug_str", 4, CVar.SERVER);
}
