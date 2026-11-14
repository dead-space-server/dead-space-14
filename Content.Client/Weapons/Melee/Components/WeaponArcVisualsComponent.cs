namespace Content.Client.Weapons.Melee.Components;

/// <summary>
/// Used for melee attack animations. Typically just has a fadeout.
/// </summary>
[RegisterComponent]
public sealed partial class WeaponArcVisualsComponent : Component
{
    public EntityUid? User;

    [DataField("animation")]
    public WeaponArcAnimation Animation = WeaponArcAnimation.None;

    [ViewVariables(VVAccess.ReadWrite), DataField("fadeOut")]
    public bool Fadeout = true;

    // DS14-start
    /// <summary>The arc shows its own sprite, not a copy of the weapon's (Cain and Abel swing one blade at a time).</summary>
    [DataField]
    public bool KeepOwnSprite;
    // DS14-end
}

public enum WeaponArcAnimation : byte
{
    None,
    Thrust,
    Slash,
}
