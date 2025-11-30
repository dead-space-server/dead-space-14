//Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.GameStates;
using Content.Shared.DeadSpace.GhostRoleNotify.Prototypes;
namespace Content.Shared.DeadSpace.GhostRoleNotify.Components;

[RegisterComponent, NetworkedComponent]

public sealed partial class GhostRoleNotifysComponent : Component
{
    public GhostRoleNotifysComponent()
    { }

    [DataField(required: true)]
    public string GroupPrototype = string.Empty;
}