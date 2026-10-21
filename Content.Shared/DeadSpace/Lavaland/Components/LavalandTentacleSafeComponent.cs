// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Lavaland.Components;

/// <summary>
/// The tamer of a goliath that has just summoned its tentacles: they do not stun him (the goliath tentacle's
/// blacklist), until <see cref="Until"/>.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandTentacleSafeComponent : Component
{
    [ViewVariables]
    public TimeSpan Until;
}
