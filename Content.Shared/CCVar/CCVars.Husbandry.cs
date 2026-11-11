// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Whether animals breed at all.
    /// </summary>
    public static readonly CVarDef<bool> HusbandryBreedingEnabled =
        CVarDef.Create("husbandry.breeding.enabled", true, CVar.SERVERONLY);

    /// <summary>
    ///     Hard cap of animals with <c>AnimalBreeding</c> on the whole server, newborns stop once it is reached.
    /// </summary>
    public static readonly CVarDef<int> HusbandryPopulationCap =
        CVarDef.Create("husbandry.breeding.population_cap", 150, CVar.SERVERONLY);

    /// <summary>
    ///     How long a failed search for food or water is not repeated, in seconds. Keeps hungry animals with
    ///     nothing to eat from searching the whole area every plan.
    /// </summary>
    public static readonly CVarDef<float> HusbandrySearchCooldown =
        CVarDef.Create("husbandry.search_cooldown", 20f, CVar.SERVERONLY);
}
