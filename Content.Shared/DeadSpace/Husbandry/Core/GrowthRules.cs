// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Husbandry.Core;

/// <summary>
/// Rules for moving through growth stages. Engine independent on purpose.
/// </summary>
public static class GrowthRules
{
    /// <summary>
    /// Whether there is a stage after <paramref name="current"/>.
    /// </summary>
    public static bool TryGetNext(int current, int stageCount, out int next)
    {
        next = current + 1;
        return next < stageCount;
    }

    /// <summary>
    /// Picks the sex for a newborn, <paramref name="roll"/> is a random number in <c>[0, 1)</c>.
    /// </summary>
    public static AnimalSexKind RollSex(double roll, float maleChance)
    {
        return roll < maleChance ? AnimalSexKind.Male : AnimalSexKind.Female;
    }
}
