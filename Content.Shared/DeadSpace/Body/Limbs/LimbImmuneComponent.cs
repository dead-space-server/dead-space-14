// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Body.Limbs;

/// <summary>
/// The body takes no limb damage at all (no fractures, cuts, organ ruptures), the same way ash walkers are built:
/// the limb components are stripped as soon as this one starts.
/// </summary>
[RegisterComponent]
public sealed partial class LimbImmuneComponent : Component;
