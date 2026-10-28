// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Weapons.Ranged.Upgrades;

/// <summary>
/// An upgrade that overheats the gun it sits in: <see cref="Shots"/> shots in a row, each within
/// <see cref="ResetDelay"/> of the last, and the gun will not fire for <see cref="Lockout"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(GunUpgradeOverheatSystem))]
public sealed partial class GunUpgradeOverheatComponent : Component
{
    [DataField]
    public int Shots = 4;

    /// <summary>A longer pause between shots and the count starts over.</summary>
    [DataField]
    public TimeSpan ResetDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan Lockout = TimeSpan.FromSeconds(4);

    /// <summary>Shown to the shooter as the gun overheats.</summary>
    [DataField]
    public LocId? OverheatPopup = "gun-upgrade-overheat-popup";

    /// <summary>Shown on trying to fire it while it cools down.</summary>
    [DataField]
    public LocId? LockedPopup = "gun-upgrade-overheat-locked";

    [DataField, AutoNetworkedField]
    public int Count;

    [DataField, AutoNetworkedField]
    public TimeSpan LastShot;

    [DataField, AutoNetworkedField]
    public TimeSpan LockedUntil;
}
