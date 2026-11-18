// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Husbandry.Core;

/// <summary>
/// Rules for a need that drains over time (satiety, hydration). Engine independent on purpose.
/// </summary>
public static class NeedRules
{
    /// <summary>
    /// Drains <paramref name="value"/> for the given time, never below zero.
    /// </summary>
    /// <returns>For how many minutes the need was empty during this step, used to scale the deprivation damage.</returns>
    public static float Tick(ref float value, float decayPerMinute, float seconds)
    {
        value = MathF.Max(0f, value - decayPerMinute * seconds / 60f);
        return value <= 0f ? seconds / 60f : 0f;
    }

    /// <summary>
    /// Adds <paramref name="amount"/> to <paramref name="value"/>, clamped to <c>[0, max]</c>.
    /// </summary>
    /// <returns>The amount that actually changed the value.</returns>
    public static float Modify(ref float value, float max, float amount)
    {
        var before = value;
        value = Math.Clamp(value + amount, 0f, max);
        return value - before;
    }

    /// <summary>
    /// Whether <paramref name="amount"/> can be taken from <paramref name="value"/> and still leave at least
    /// <paramref name="floor"/>.
    /// </summary>
    public static bool CanAfford(float value, float amount, float floor = 0f)
    {
        return value - amount >= floor;
    }

    /// <summary>
    /// The share of <paramref name="max"/> that <paramref name="value"/> is, in <c>[0, 1]</c>.
    /// </summary>
    public static float Fraction(float value, float max)
    {
        return max <= 0f ? 0f : Math.Clamp(value / max, 0f, 1f);
    }
}
