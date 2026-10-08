using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Actions;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class NinjaCloakComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = false;

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField]
    public EntProtoId Action = "NinjaActionToggleCloak";

    [DataField("drainRate")]
    public float DrainRate = 1f;
}

public sealed partial class ToggleCloakNinjaEvent : InstantActionEvent;