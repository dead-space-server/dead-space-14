using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.GameStates;
using Content.Shared.DeadSpace.GhostRoleNotify.Prototypes;
namespace Content.Shared.DeadSpace.GhostRoleNotify.Components;

[RegisterComponent, NetworkedComponent]

public sealed partial class GhostRoleNotifyComponent : Component
{
    public GhostRoleNotifyComponent()
    { }

    [DataField("prototype", customTypeSerializer: typeof(PrototypeIdSerializer<GhostRoleGroupNotify>))]
    public string GroupPrototype = String.Empty!;
    //ViewVariables(VVAccess.ReadOnly)]
    //public TimeSpan TimeUtilNotification = TimeSpan.Zero;
    //[DataField]
    //public float Delay = 1f;

    //[ViewVariables(VVAccess.ReadOnly)]
    //public bool IsNotificationSend = false;
}