// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.ShieldBash.Components;

/// <summary>
/// Marks an item as something you can bang against a
/// <see cref="ShieldBashComponent"/> shield to make noise with — batons,
/// truncheons, energy machetes, etc.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ShieldBashToolComponent : Component
{
}
