using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NinjaSecondChanceComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Used = false;

    [DataField, AutoNetworkedField]
    public EntityUid? Capsule;
}