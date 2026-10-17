using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryDirtCleanerComponent : Component
{
    [DataField]
    public float Amount = 25f;
}
