// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.Body.Part;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Surgery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BodyIncisionComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<BodyPartType> IncisedParts = new();
}
