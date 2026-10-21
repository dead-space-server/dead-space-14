// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Weapons.Ranged.Upgrades;

/// <summary>
/// On a gun that stores its charges (the kinetic repeater): the fire rate upgrades only quicken the recharge,
/// not the delay between the stored shots.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FireRateUpgradeRechargeOnlyComponent : Component;
