using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class NinjaCloneVisualComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Source = EntityUid.Invalid;
}