using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.DeadSpace.Armutant.Abilities.Components.BladeAblities;

[RegisterComponent]
public sealed partial class SporeBladeComponent : Component
{
    [DataField]
    public EntityUid? ActionSporeBladeEntity;

    [DataField("actionToggleSporeBlade", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionToggleSporeBlade = "ActionToggleSporeBlade";
}
