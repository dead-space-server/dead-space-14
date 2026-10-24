// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Robust.Shared.GameObjects;

namespace Content.Shared.DeadSpace.OrganDamage;

[ByRefEvent]
public readonly record struct OrganDamagedEvent(Entity<OrganDamageableComponent> Organ, EntityUid? Body, EntityUid? Origin);

[ByRefEvent]
public readonly record struct OrganDestroyedEvent(Entity<OrganDamageableComponent> Organ, EntityUid? Body);

[ByRefEvent]
public readonly record struct OrganRestoredEvent(Entity<OrganDamageableComponent> Organ, EntityUid? Body);
