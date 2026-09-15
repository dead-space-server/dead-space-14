// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Gavel.Components;

/// <summary>
/// Marks an item as a judge's gavel hammer. Striking an entity with
/// <see cref="GavelBlockComponent"/> while holding this item plays a
/// hit sound and a strike popup, handled by <see cref="SharedGavelSystem"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GavelHammerComponent : Component
{
}
