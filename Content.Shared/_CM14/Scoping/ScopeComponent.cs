using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
namespace Content.Shared._CM14.Scoping;
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedScopeSystem))]
public sealed partial class ScopeComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Zoom = 1f;
    [DataField, AutoNetworkedField]
    public float Offset = 15;
    [DataField, AutoNetworkedField]
    public EntProtoId ScopingToggleAction = "CMActionToggleScope";
    [DataField, AutoNetworkedField]
    public EntityUid? RelayEntity;
    [DataField, AutoNetworkedField]
    public bool Attachment;
    [DataField, AutoNetworkedField]
    public bool RequireWielding;
    [DataField, AutoNetworkedField]
    public EntityUid? ScopingToggleActionEntity;
    [DataField, AutoNetworkedField]
    public EntityUid? User;
    [DataField, AutoNetworkedField]
    public Direction? ScopingDirection;
}