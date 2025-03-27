using Content.Shared.Damage;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Audio;
using Content.Shared.Dataset;

namespace Content.Server.DeadSpace.SmokingCarp.Abilities.Components;

[RegisterComponent]
public sealed partial class PowerPunchComponent : Component
{
    [DataField]
    public EntityUid? ActionPowerPunchCarpAttackEntity;

    [DataField("actionPowerPunchCarp", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionPowerPunchCarpAttack = "ActionPowerPunchCarp";

    [DataField]
    public bool IsDamageAttack = false;

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> PackMessage = "CarpMessageHit";

    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict =
        {
            ["Slash"] = 35
        }
    };

    [DataField]
    public float PushStrength = 300f;

    [DataField]
    public float MaxPushDistance = 5f;

    [DataField]
    public bool UseDistanceScaling = true;

    [DataField]
    public EntProtoId? EffectPunch = "EffectPowerPunchCarp";

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public SoundSpecifier HitSound = new SoundPathSpecifier("/Audio/Weapons/genhit1.ogg");
}
