// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Husbandry.Core;

/// <summary>
/// Rules of breeding that need nothing from the engine, so they can be tested without a game.
/// </summary>
public static class BreedingRules
{
    /// <summary>
    /// Whether two animals are a pair: one male and one female.
    /// </summary>
    public static bool IsPair(AnimalSexKind a, AnimalSexKind b)
    {
        return a != b;
    }

    /// <summary>
    /// Whether the room is enough for one more: the crowd around the mother and the whole server are both under
    /// their limits. A limit of zero or less means there is no limit.
    /// </summary>
    public static bool HasRoom(int crowd, int crowdLimit, int total, int totalLimit)
    {
        if (crowdLimit > 0 && crowd >= crowdLimit)
            return false;

        return totalLimit <= 0 || total < totalLimit;
    }

    /// <summary>
    /// How many babies of a litter can be born when there is room for <paramref name="room"/> more in the world.
    /// </summary>
    public static int LitterSize(int wanted, int total, int totalLimit)
    {
        if (totalLimit <= 0)
            return wanted;

        return Math.Clamp(totalLimit - total, 0, wanted);
    }
}
