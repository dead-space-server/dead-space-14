// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Master switch of the unattended farm (<c>AnimalFarmSystem</c> and the hives, see <c>BeeHiveSystem</c>): what the
    ///     animals and the bees would do if somebody watched, is done for them when nobody is near, without walking and
    ///     flying. Turn it off if the server is struggling.
    /// </summary>
    public static readonly CVarDef<bool> HusbandryFarmEnabled =
        CVarDef.Create("husbandry.farm.enabled", true, CVar.SERVERONLY);

    /// <summary>
    ///     How often the farm looks at sleeping animals, in seconds.
    /// </summary>
    public static readonly CVarDef<float> HusbandryFarmInterval =
        CVarDef.Create("husbandry.farm.interval", 15f, CVar.SERVERONLY);

    /// <summary>
    ///     At most this many animals are looked at by the farm per cycle, the rest wait for the next one, so the cost
    ///     of a cycle never grows with the number of animals.
    /// </summary>
    public static readonly CVarDef<int> HusbandryFarmBudget =
        CVarDef.Create("husbandry.farm.budget", 16, CVar.SERVERONLY);

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

    /// <summary>
    ///     How long the pen of an animal is kept before it is worked out again, in seconds (walls and doors change,
    ///     but not often).
    /// </summary>
    public static readonly CVarDef<float> HusbandryPenCacheTime =
        CVarDef.Create("husbandry.pen.cache_time", 30f, CVar.SERVERONLY);

    /// <summary>
    ///     How often the positions of the players are taken for the strolls of the animals that go round a player, in
    ///     seconds.
    /// </summary>
    public static readonly CVarDef<float> HusbandryPlayerScanInterval =
        CVarDef.Create("husbandry.wander.player_scan_interval", 2f, CVar.SERVERONLY);
}
