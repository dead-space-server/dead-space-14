// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Map;

namespace Content.Shared.DeadSpace.Husbandry.Walking;

/// <summary>
/// Raised on an animal before it picks a place to walk to or a thing to go to. Whatever keeps the animal in a place
/// (a pen) sets <see cref="Allowed"/> to false for a target it must not go to, so that the animal
/// does not pick it, walk into the end of its rope and give up.
/// </summary>
/// <param name="Target">Where the animal wants to go.</param>
/// <param name="Reach">How far from the target the animal can be and still use it: 0 for a place to stand at, the
/// distance it stops at for food and water.</param>
[ByRefEvent]
public record struct AnimalTargetCheckEvent(MapCoordinates Target, float Reach, bool Allowed = true);
