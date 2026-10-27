// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Weapons.Ranged.Events;

/// <summary>
/// Raised on a gun with self-recharging ammo when the time to its next charge is worked out: the handlers may make it
/// longer or shorter (an overheated barrel, the current fire mode).
/// </summary>
[ByRefEvent]
public record struct RechargeBasicEntityAmmoCooldownEvent(float Cooldown);
