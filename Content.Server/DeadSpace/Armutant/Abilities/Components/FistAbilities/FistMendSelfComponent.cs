using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.DeadSpace.Armutant.Abilities.Components.FistAbilities;

[RegisterComponent]
public sealed partial class FistMendSelfComponent : Component
{
    [DataField]
    public EntityUid? ActionFistMendSelfEntity;

    [DataField("actionToggleFistStunAround", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionToggleFistMendSelf = "ActionToggleFistMendSelf";

    [DataField]
    public EntProtoId? SelfEffect = "FistSelfMendEffect";
}
