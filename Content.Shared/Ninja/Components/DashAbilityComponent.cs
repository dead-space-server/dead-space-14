using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Alert; // DS14
using Content.Shared.Ninja.Systems;
using Robust.Shared.Audio; // DS14
using Robust.Shared.GameStates;
using Robust.Shared.Map; // DS14
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization; // DS14

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
    public bool AltClickDash = false;

    [DataField]
    public float CorruptMaxDistance = 3f;

    [DataField]
    public float CorruptMinDistance = 1f;

    [DataField]
    public string? BeamProto;

    [DataField]
    public SoundSpecifier? DashSound = new SoundPathSpecifier("/Audio/Magic/blink.ogg")
    {
        Params = AudioParams.Default.WithVolume(5f),
    };
    //DS14-end

    /// <summary>
    /// The action id for dashing.
    /// </summary>
    [DataField]
    public EntProtoId<WorldTargetActionComponent>? DashAction = "ActionEnergyKatanaDash";

    [DataField, AutoNetworkedField]
    public EntityUid? DashActionEntity;

    //DS14-start
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(1);

    [DataField]
    public ProtoId<AlertPrototype> CooldownAlert = "DashCooldown";

    [DataField, AutoNetworkedField]
    public TimeSpan NextUse;
    //DS14-end
}

public sealed partial class DashEvent : WorldTargetActionEvent;

//DS14-start
[Serializable, NetSerializable]
public sealed class DashRequestEvent(NetEntity item, NetCoordinates target) : EntityEventArgs
{
    public NetEntity Item = item;
    public NetCoordinates Target = target;
}
//DS14-end
