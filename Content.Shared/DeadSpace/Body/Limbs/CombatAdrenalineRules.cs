// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Body.Limbs;

public static class CombatAdrenalineRules
{
    public const float HeavyDamage = 20f;
    public const float SurgeSeconds = 12f;

    public static bool TryStart(bool implant, float positiveDamage, bool surgeActive, bool combatMode)
    {
        return combatMode && implant && !surgeActive && positiveDamage >= HeavyDamage;
    }

    public static bool TryRefresh(bool surgeActive, float positiveDamage)
    {
        return surgeActive && positiveDamage > 0f;
    }

    public static bool CanStayUp(bool leftLegSevered, bool rightLegSevered)
    {
        return !leftLegSevered || !rightLegSevered;
    }

    public static float Positive(IEnumerable<float> deltas)
    {
        var total = 0f;
        foreach (var delta in deltas)
        {
            if (delta > 0f)
                total += delta;
        }

        return total;
    }
}
