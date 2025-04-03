using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.DeadSpace.Armutant.Abilities.Components.FistAbilities;

[RegisterComponent]
public sealed partial class FistBuffSpeedComponent : Component
{
    [DataField]
    public EntityUid? ActionFistBuffSpeedEntity;

    [DataField("actionToggleFistStunAround", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionToggleFistBuffSpeed = "ActionToggleFistBuffSpeed";
}
