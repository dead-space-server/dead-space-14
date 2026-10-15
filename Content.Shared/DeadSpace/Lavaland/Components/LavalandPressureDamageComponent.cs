// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Atmos;

namespace Content.Shared.DeadSpace.Lavaland.Components;

/// <summary>
/// Kinetic weapons are made for the thin lavaland air (Goob-Station PressureDamageChange):
/// outside of the pressure range their melee hits and projectiles are weaker.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandPressureDamageComponent : Component
{
    [DataField]
    public float LowerBound = Atmospherics.OneAtmosphere * 0.2f;

    [DataField]
    public float UpperBound = Atmospherics.OneAtmosphere * 0.5f;

    /// <summary>Damage multiplier outside of the range (on the station, in space...).</summary>
    [DataField]
    public float OutOfRangeModifier = 0.5f;

    [DataField]
    public bool ApplyToMelee = true;

    [DataField]
    public bool ApplyToProjectiles = true;
}
