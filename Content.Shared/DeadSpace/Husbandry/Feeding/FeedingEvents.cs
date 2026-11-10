// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Husbandry.Feeding;

/// <summary>
/// Raised on an animal when it starts eating. The food is still there, the animal gets it when it is done.
/// </summary>
[ByRefEvent]
public readonly record struct AnimalEatStartedEvent(EntityUid Food);

/// <summary>
/// Raised on an animal when it was interrupted while eating and got nothing.
/// </summary>
[ByRefEvent]
public readonly record struct AnimalEatInterruptedEvent;

/// <summary>
/// Raised on an animal whenever it ate something, after its satiety was updated.
/// </summary>
/// <param name="Offline">True if the farm fed the animal while nobody was around (see AnimalFarmSystem): nothing is
/// left lying around then, whatever it makes goes into a pit or is lost.</param>
[ByRefEvent]
public readonly record struct AnimalAteEvent(EntityUid Food, float Nutrition, bool Offline = false);

/// <summary>
/// Raised on an animal whenever it drank something, after its hydration was updated.
/// </summary>
[ByRefEvent]
public readonly record struct AnimalDrankEvent(EntityUid Source, float Amount);

[Serializable, NetSerializable]
public sealed partial class AnimalEatDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class AnimalDrinkDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// A player feeds an animal by hand. Raised on the animal, the food is the one used.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class AnimalHandFeedDoAfterEvent : SimpleDoAfterEvent;
