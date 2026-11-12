using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Revolutionary.Components;

/// <summary>
/// Component used for marking a Head Rev for conversion and winning/losing.
/// </summary>
<<<<<<< ours
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedRevolutionarySystem))] // DS14: networked for replay state only.
||||||| base
[RegisterComponent, NetworkedComponent, Access(typeof(SharedRevolutionarySystem))]
=======
[RegisterComponent, NetworkedComponent, Access(typeof(RevolutionarySystem))]
>>>>>>> theirs
public sealed partial class HeadRevolutionaryComponent : Component
{
    /// <summary>
    /// The status icon corresponding to the head revolutionary.
    /// </summary>
<<<<<<< ours
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)] // DS14: configure through YAML; live roster updates are event-driven.
    public ProtoId<FactionIconPrototype> StatusIcon { get; set; } = "HeadRevolutionaryFaction";
||||||| base
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<FactionIconPrototype> StatusIcon { get; set; } = "HeadRevolutionaryFaction";
=======
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<StatusIconPrototype> StatusIcon { get; set; } = "HeadRevolutionaryFaction";
>>>>>>> theirs

    /// <summary>
    /// How long the stun will last after the user is converted.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan StunTime = TimeSpan.FromSeconds(8);

    [DataField("headRevConvertAction", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string HeadRevConvertAction = "ActionHeadRevConvert";

    [DataField("actionEntity")] public EntityUid HeadRevConvertActionEntity;

    public override bool SessionSpecific => true;
}
