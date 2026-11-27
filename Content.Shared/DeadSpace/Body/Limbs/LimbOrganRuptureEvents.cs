// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Body.Limbs;

/// <summary>
/// Raised on a body before a hit may rupture organs in a zone. Organs in <see cref="Boosted"/> rupture
/// <see cref="Multiplier"/> times as often as the rest.
/// </summary>
[ByRefEvent]
public record struct LimbOrganRuptureChanceEvent(LimbZone Zone)
{
    public LimbOrganMask Boosted;
    public float Multiplier = 1f;
}

/// <summary>
/// Raised on a body right after a hit ruptured the organs in <see cref="Ruptured"/>.
/// Organs a handler puts into <see cref="Regrow"/> are made whole again on the spot.
/// </summary>
[ByRefEvent]
public record struct LimbOrgansRupturedEvent(LimbOrganMask Ruptured)
{
    public LimbOrganMask Regrow;
}
