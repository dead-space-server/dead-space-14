using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.HandPull;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(HandPullSystem))]
public sealed partial class HandPullerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Target;
}
