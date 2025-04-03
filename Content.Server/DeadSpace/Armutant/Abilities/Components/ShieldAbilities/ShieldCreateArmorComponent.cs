using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.DeadSpace.Armutant.Abilities.Components.ShieldAbilities;

[RegisterComponent]
public sealed partial class ShieldCreateArmorComponent : Component
{
    [DataField]
    public EntityUid? ActionShieldArmorEntity;

    [DataField("actionToggleShieldArmor", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionToggleShieldArmorSpawn = "ActionToggleShieldArmor";

    [DataField]
    public string MeatSound = "/Audio/_DeadSpace/Armutant/sound_effects_meat.ogg";

    [DataField]
    public Dictionary<string, EntityUid> Equipment = new();
}
