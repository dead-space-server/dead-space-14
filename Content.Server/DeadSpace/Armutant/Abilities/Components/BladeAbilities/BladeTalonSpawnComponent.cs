using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.DeadSpace.Armutant.Abilities.Components.BladeAblities;

[RegisterComponent]
public sealed partial class BladeTalonSpawnComponent : Component
{
    [DataField]
    public EntityUid? ActionTalonEntity;

    [DataField("actionToggleDash", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionToggleTalonSpawn = "ActionToggleSpawnTalon";

    [DataField]
    public string MeatSound = "/Audio/_DeadSpace/Armutant/sound_effects_meat.ogg";
}
