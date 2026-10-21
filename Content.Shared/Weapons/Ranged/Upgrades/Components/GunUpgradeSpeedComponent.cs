using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Ranged.Upgrades.Components;

/// <summary>
/// A <see cref="GunUpgradeComponent"/> for increasing the speed of a gun's projectile.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(GunUpgradeSystem))]
public sealed partial class GunUpgradeSpeedComponent : Component
{
    /// <summary>
    /// Multiplier for the speed of a gun's projectile.
    /// </summary>
    [DataField]
    public float Coefficient = 1;

    // DS14-start
    /// <summary>
    /// Multiplier for how long the projectile flies (<c>TimedDespawn</c>): the range grows without the speed,
    /// too fast a projectile skips through walls and mobs.
    /// </summary>
    [DataField]
    public float LifetimeCoefficient = 1;
    // DS14-end
}
