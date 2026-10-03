using Robust.Shared.GameStates;
using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NinjaEmpAbilityComponent : Component
{
    [DataField]
    public EntProtoId EmpAction = "ActionNinjaEmp";

    [DataField, AutoNetworkedField]
    public EntityUid? EmpActionEntity;

    [DataField]
    public float Charge = 180f;

    [DataField]
    public float EmpRange = 6f;

    [DataField]
    public float EmpConsumption = 100000f;

    [DataField]
    public TimeSpan EmpDuration = TimeSpan.FromSeconds(60);
}

public sealed partial class NinjaEmpEvent : InstantActionEvent;