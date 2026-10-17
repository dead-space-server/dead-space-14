using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Surgery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgeryDirtinessComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Amount;

    [DataField]
    public float Max = 100f;
}
