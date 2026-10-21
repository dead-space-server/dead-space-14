// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Weapons.Ranged.Components;

/// <summary>
/// A projectile that flies through every creature its shooter's factions are not hostile to: an NPC's shots
/// (the minebot's) do not hit the miners and the other drones in the way. Walls and rock still stop it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ProjectileHostilesOnlyComponent : Component;
