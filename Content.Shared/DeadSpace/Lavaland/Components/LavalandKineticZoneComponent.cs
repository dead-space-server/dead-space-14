// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Lavaland.Components;

/// <summary>
/// Map component: kinetic weapons (<see cref="LavalandPressureDamageComponent"/>) hit at full strength here whatever
/// the pressure. The Celestial zone keeps a breathable station atmosphere, yet it is fought with lavaland gear.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandKineticZoneComponent : Component;
