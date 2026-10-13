using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Actions;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NinjaCaltropComponent : Component
{
    [DataField]
    public EntProtoId CaltropProto = "NinjaCaltrop";

    [DataField]
    public float Charge = 100f;

    [DataField]
    public EntProtoId Action = "NinjaCaltropAction";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;
}

public sealed partial class NinjaCaltropAbilityActionEvent : InstantActionEvent;