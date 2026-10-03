using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Ninja.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ninja.Components;

/// <summary>
/// Adds an action to dash, teleport to clicked position, when this item is held.
/// Cancel <see cref="CheckDashEvent"/> to prevent using it.
/// </summary>
//DS14-start
[RegisterComponent, NetworkedComponent, Access(typeof(SharedDashAbilitySystem)), AutoGenerateComponentState]
//DS14-end
public sealed partial class DashAbilityComponent : Component
{
    //DS14-start
    [DataField]
    public bool CorruptByBluespaceItems = false;

    /// <summary>
    /// If true, the entity the user is pulling is teleported along with them,
    /// preserving the distance joint instead of breaking the pull.
    /// </summary>
    [DataField]
    public bool TeleportPulledEntity = false;

    [DataField]
    public float CorruptMaxDistance = 3f;

    [DataField]
    public float CorruptMinDistance = 1f;

    [DataField]
    public string? BeamProto;
    //DS14-end

    /// <summary>
    /// The action id for dashing.
    /// </summary>
    [DataField]
    public EntProtoId<WorldTargetActionComponent> DashAction = "ActionEnergyKatanaDash";

    [DataField, AutoNetworkedField]
    public EntityUid? DashActionEntity;
}

public sealed partial class DashEvent : WorldTargetActionEvent;
