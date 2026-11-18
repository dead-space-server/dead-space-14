// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Husbandry.Feeding;

/// <summary>
/// An animal drinks from a container. Eating is the usual one (<c>IngestionSystem</c>, bites into the stomach), only
/// drinking from a container that is no drink (a bucket, a trough) needs its own action.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class AnimalDrinkDoAfterEvent : SimpleDoAfterEvent;
