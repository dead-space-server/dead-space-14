// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.DoAfter;

/// <summary>
/// Whatever this entity is busy doing is not interrupted by walking or running; only a hit (or cancelling it themself)
/// stops it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SteadyDoAfterComponent : Component;
