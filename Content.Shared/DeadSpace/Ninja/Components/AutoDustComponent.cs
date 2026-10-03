using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AutoDustComponent : Component
{
    [DataField, AutoNetworkedField]
    public DustMode AutoDustMode = DustMode.Off;

    [DataField]
    public EntProtoId Action = "ToggleAutoDustModeAction";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField, AutoNetworkedField]
    public bool DeleteItems = true;

    [DataField, AutoNetworkedField]
    public EntProtoId SpawnOnDustProto = "Acidifier";
}

public enum DustMode : byte
{
    Off,
    Crit,
    Dead
}

public sealed partial class ToggleAutoDustModeActionEvent : InstantActionEvent;