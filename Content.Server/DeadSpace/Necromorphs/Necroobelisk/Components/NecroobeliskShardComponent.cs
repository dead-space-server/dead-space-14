// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Content.Shared.Mobs.Components;

namespace Content.Server.DeadSpace.Necromorphs.Necroobelisk.Components;

/// <summary>
/// An experimental obelisk shard: lying loose or hidden under a floor tile (not carried, not in a bag), it drains the
/// sanity of everyone around as the obelisk does, and the mad are infected as by the obelisk. It is not an obelisk to
/// anything else - pinpointers, the Circle's activation, the convergence do not see it.
/// </summary>
[RegisterComponent]
public sealed partial class NecroobeliskShardComponent : Component
{
    [DataField]
    public float Range = 10f;

    /// <summary>Sanity taken from each one in range every check.</summary>
    [DataField]
    public float SanityDamage = 3f;

    [DataField]
    public TimeSpan CheckInterval = TimeSpan.FromSeconds(2);

    /// <summary>Dealt to whoever goes mad, as the obelisk does.</summary>
    [DataField]
    public DamageSpecifier MadDamage = new() { DamageDict = new() { { "Cellular", 2 } } };

    [ViewVariables]
    public TimeSpan NextCheck;

    /// <summary>Those it shows its overlay to now.</summary>
    [ViewVariables]
    public HashSet<Entity<MobStateComponent>> MobsInRange = new();
}
