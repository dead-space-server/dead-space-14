// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Weapons.Akimbo;

[ByRefEvent]
public record struct AkimboSelectGunEvent(EntityUid User, EntityUid SelectedGun, bool Active = false);
