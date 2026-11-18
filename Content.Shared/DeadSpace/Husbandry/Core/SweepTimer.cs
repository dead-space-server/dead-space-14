// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Husbandry.Core;

/// <summary>
/// Lets a system that walks over many entities do it every so often instead of every tick. What the entities do is
/// timed by their own timers (a need, a growth stage, a pregnancy), so a sweep that is late by a fraction of a second
/// changes nothing, and the walk over all of them is paid for a few times per second instead of thirty.
/// </summary>
public struct SweepTimer
{
    private TimeSpan _next;

    /// <summary>
    /// True once per <paramref name="interval"/>: the system should do its sweep now.
    /// </summary>
    public bool Due(TimeSpan now, TimeSpan interval)
    {
        if (now < _next)
            return false;

        _next = now + interval;
        return true;
    }
}
