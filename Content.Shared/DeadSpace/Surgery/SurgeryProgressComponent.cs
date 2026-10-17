using Content.Shared.DeadSpace.Surgery.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Surgery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgeryProgressComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<SurgeryPrototype>? Active;

    [DataField, AutoNetworkedField]
    public int StepIndex;

    [DataField, AutoNetworkedField]
    public List<ProtoId<SurgeryPrototype>> Completed = [];

    public bool IsCompleted(ProtoId<SurgeryPrototype> surgery)
    {
        return Completed.Contains(surgery);
    }
}
