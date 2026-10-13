using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AutoDustMarkerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid AutoDustItem;
}