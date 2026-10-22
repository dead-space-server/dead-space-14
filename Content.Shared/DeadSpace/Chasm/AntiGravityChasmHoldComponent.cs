// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Chasm;

/// <summary>
/// Anti-gravity clothing that keeps its wearer over a chasm even lying down (a bear trap, a stun, crit) while alive.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AntiGravityChasmHoldComponent : Component
{
    /// <summary>
    /// False: no timer, the wearer stays up over the chasm as long as they live.
    /// True: lying over a chasm <see cref="Grace"/> (<see cref="DeepNightGrace"/> in the deep night) runs, then they fall.
    /// </summary>
    [DataField]
    public bool Timer;

    [DataField]
    public TimeSpan Grace = TimeSpan.FromSeconds(15);

    /// <summary>The timer in the lavaland's deep night or the chromite caves' deep dark.</summary>
    [DataField]
    public TimeSpan DeepNightGrace = TimeSpan.FromSeconds(10);
}
