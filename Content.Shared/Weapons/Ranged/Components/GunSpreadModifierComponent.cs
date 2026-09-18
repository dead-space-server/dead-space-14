using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Ranged.Components;

/// <summary>
/// This component modifies the spread of the gun it is attached to.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GunSpreadModifierComponent : Component
{
    /// <summary>
    /// A scalar value multiplied by the spread built into the ammo itself.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Spread = 1;

    // DS14-start
    /// <summary>
    /// A scalar value used instead of <see cref="Spread"/> while the weapon is wielded.
    /// If not specified, <see cref="Spread"/> is used in both wielded and unwielded states.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float? WieldedSpread;
    // DS14-end
}
