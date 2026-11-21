using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Movement.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class JetpackComponent : Component
{
    // DS14-start
    [DataField, AutoNetworkedField]
    public bool RequiresGas = true;
    // DS14-end

    [DataField, AutoNetworkedField]
    public EntityUid? JetpackUser;

    [ViewVariables(VVAccess.ReadWrite), DataField("moleUsage")]
    public float MoleUsage = 0.012f;

    [DataField] public EntProtoId ToggleAction = "ActionToggleJetpack";

    [DataField, AutoNetworkedField] public EntityUid? ToggleActionEntity;

    [ViewVariables(VVAccess.ReadWrite), DataField("acceleration")]
    public float Acceleration = 1.65f; //DS14

    [ViewVariables(VVAccess.ReadWrite), DataField("friction")]
    public float Friction = 0.5f; // same as off-grid friction //DS14

    [ViewVariables(VVAccess.ReadWrite), DataField("weightlessModifier")]
    public float WeightlessModifier = 1.65f; //DS14
}
