using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.AshWalkers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AshWalkerTribeMemberComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? HomeMap;
}
