using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.GameStates;
using Content.Shared.DeadSpace.GhostRoleNotify.Prototypes;
namespace Content.Shared.DeadSpace.GhostRoleNotify.Components;

[RegisterComponent, NetworkedComponent]

public sealed partial class GhostRoleNotifysComponent : Component
{
    public GhostRoleNotifysComponent()
    { }

    [DataField("GroupPrototype", required: true)]
    public string GroupPrototype = string.Empty;
}