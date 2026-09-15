// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Weapons.Parry;

[ByRefEvent]
public record struct BeforeMeleeDamageEvent(EntityUid Attacker, EntityUid Weapon, bool Cancelled = false);
