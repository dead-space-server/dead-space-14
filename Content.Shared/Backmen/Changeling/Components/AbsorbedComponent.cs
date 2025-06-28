// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
// Official port from the BACKMEN project. Make sure to review the original repository to avoid license violations.


using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Changeling.Components;


/// <summary>
///     Component that indicates that a person's DNA has been absorbed by a changeling.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(AbsorbedSystem))]
public sealed partial class AbsorbedComponent : Component
{

}
