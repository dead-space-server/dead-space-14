// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Narcotics;

/// <summary>
/// Marker for a mob currently under the influence of a narcotics-group reagent.
/// Added and removed by the server so the client can apply the drug echo audio effect.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DrugEchoComponent : Component
{
}