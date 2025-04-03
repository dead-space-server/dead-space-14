using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.DeadSpace.Armutant.Abilities.Components.ShieldAbilities;

[RegisterComponent]
public sealed partial class VoidShieldComponent : Component
{
    [DataField]
    public EntityUid? ActionVoidShieldEntity;

    [DataField("actionToggleVoidShield", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionToggleVoidShield = "ActionToggleVoidShield";

    [DataField]
    public SoundSpecifier? ReflectSound = new SoundPathSpecifier("/Audio/_DeadSpace/Armutant/sound_effetc_reflect.ogg");

    [DataField]
    public EntProtoId? SelfEffect = "VoidShieldSelfEffect";
}
