// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Master switch of the unattended farm (<c>AnimalFarmSystem</c>): sleeping animals are fed and watered
    ///     from their pen without walking. Turn it off if the server is struggling.
    /// </summary>
    public static readonly CVarDef<bool> HusbandryFarmEnabled =
        CVarDef.Create("husbandry.farm.enabled", true, CVar.SERVERONLY);

    /// <summary>
    ///     How often the farm looks at sleeping animals, in seconds.
    /// </summary>
    public static readonly CVarDef<float> HusbandryFarmInterval =
        CVarDef.Create("husbandry.farm.interval", 15f, CVar.SERVERONLY);

    /// <summary>
    ///     At most this many animals are simulated by the farm per cycle, the rest wait for the next one
    ///     (round robin), so the cost of a cycle never grows with the number of animals.
    /// </summary>
    public static readonly CVarDef<int> HusbandryFarmBudget =
        CVarDef.Create("husbandry.farm.budget", 16, CVar.SERVERONLY);

    /// <summary>
    ///     Whether an animal of an open pen can wander off the map and be deleted. Off by default: losing
    ///     player property to a dice roll is a surprise nobody asked for.
    /// </summary>
    public static readonly CVarDef<bool> HusbandryFarmEscape =
        CVarDef.Create("husbandry.farm.escape", false, CVar.SERVERONLY);

    /// <summary>
    ///     Whether animals breed while no player is around (the farm does not make them breed in any case, this
    ///     only lets sleeping animals take part in breeding attempts).
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
