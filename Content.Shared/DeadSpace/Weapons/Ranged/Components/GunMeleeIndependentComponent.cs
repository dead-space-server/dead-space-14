// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Weapons.Ranged.Components;

/// <summary>
/// A melee weapon that also shoots (a kinetic crusher) whose shot and swing do not wait for each other: the shot
/// does not put off the next swing, a swing on cooldown does not hold back the shot. As the kinetic machete fires
/// its mark and hits at once.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GunMeleeIndependentComponent : Component;
