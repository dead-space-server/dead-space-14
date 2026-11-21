// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Weapons.Ranged;

/// <summary>
/// Every round leaves the barrel as several bullets flying side by side, all in the shot direction
/// (unlike a shotgun spread, which fans them out).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GunParallelShotComponent : Component
{
    /// <summary>
    /// How many bullets one round turns into.
    /// </summary>
    [DataField]
    public int Count = 2;

    /// <summary>
    /// Distance between neighbouring bullets, in tiles.
    /// </summary>
    [DataField]
    public float Spacing = 0.25f;
}
