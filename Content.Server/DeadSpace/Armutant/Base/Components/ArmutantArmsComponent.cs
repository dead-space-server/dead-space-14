using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.DeadSpace.Armutant.Base.Components;

[RegisterComponent]
public sealed partial class ArmutantArmsComponent : Component
{
    [DataField]
    public Dictionary<string, EntityUid?> Equipment = new();

    public bool IsInStasis = false;

    public int newDamageValue = 1200;

    [DataField]
    public EntityUid? ActionArmutantShieldEntity;

    [DataField]
    public EntityUid? ActionArmutantBladeEntity;

    [DataField]
    public EntityUid? ActionArmutantFistEntity;

    [DataField]
    public EntityUid? ActionArmutantGunEntity;

    [DataField]
    public EntityUid? ActionEnterStasisEntity;

    [DataField("actionToggleBlade", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionToggleBlade = "ActionToggleBlade";

    [DataField("actionToggleShield", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionToggleShield = "ActionToggleShield";

    [DataField("actionToggleFist", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionToggleFist = "ActionToggleFist";

    [DataField("actionToggleGun", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionToggleGun = "ActionToggleGun";

    [DataField("actionToggleEnterStasis", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionToggleEnterStasis = "ActionToggleEnterStasis";

    [DataField]
    public string MeatSound = "/Audio/_DeadSpace/Armutant/sound_effects_meat.ogg";

    [DataField]
    public EntProtoId? SelfEffect = "EnterToStasisEffect";
}
