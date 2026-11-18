// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Husbandry.Breeding;

/// <summary>
/// Raised on a mother before she gets pregnant. Cancel it to stop it.
/// </summary>
[ByRefEvent]
public record struct AnimalBreedingAttemptEvent(EntityUid Father, bool Cancelled = false);

/// <summary>
/// Raised on a mother after she got pregnant.
/// </summary>
[ByRefEvent]
public readonly record struct AnimalBreedingStartedEvent(EntityUid Father);

/// <summary>
/// Raised on a mother when her time came. Cancel it to put the birth off for a while.
/// </summary>
[ByRefEvent]
public record struct AnimalBeforeBirthEvent(bool Cancelled = false);

/// <summary>
/// Raised on a mother after the birth.
/// </summary>
[ByRefEvent]
public readonly record struct AnimalBornEvent(int Count);
