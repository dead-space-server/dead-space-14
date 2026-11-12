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

    [DataField]
    public float MoleUsage = 0.012f;

    [DataField]
    public EntProtoId ToggleAction = "ActionToggleJetpack";

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleActionEntity;

<<<<<<< ours
    [ViewVariables(VVAccess.ReadWrite), DataField("acceleration")]
    public float Acceleration = 1.65f; //DS14
||||||| base
    [ViewVariables(VVAccess.ReadWrite), DataField("acceleration")]
    public float Acceleration = 1f;
=======
    [DataField]
    public float Acceleration = 1f;
>>>>>>> theirs

<<<<<<< ours
    [ViewVariables(VVAccess.ReadWrite), DataField("friction")]
    public float Friction = 0.5f; // same as off-grid friction //DS14
||||||| base
    [ViewVariables(VVAccess.ReadWrite), DataField("friction")]
    public float Friction = 0.25f; // same as off-grid friction
=======
    [DataField]
    public float Friction = 0.25f; // same as off-grid friction
>>>>>>> theirs

<<<<<<< ours
    [ViewVariables(VVAccess.ReadWrite), DataField("weightlessModifier")]
    public float WeightlessModifier = 1.65f; //DS14
||||||| base
    [ViewVariables(VVAccess.ReadWrite), DataField("weightlessModifier")]
    public float WeightlessModifier = 1.2f;
=======
    [DataField]
    public float WeightlessModifier = 1.2f;

    /// <summary>
    /// Interval in seconds between gas consumption while the jetpack is active.
    /// </summary>
    [DataField]
    public float GasUsageInterval = 0.3f;
>>>>>>> theirs
}
