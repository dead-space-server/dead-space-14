using Content.Shared.Dataset;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.DeadSpace.MartialArts;

[Prototype("martialArt")]
[Serializable, NetSerializable, DataDefinition]
public sealed partial class MartialArtPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField]
    public MartialArtsForms MartialArtsForm;

    [DataField("atackOne", required: true)]
    public ProtoId<CombatTechPrototype> AtackOne = default!;

    [DataField("atackTwo", required: true)]
    public ProtoId<CombatTechPrototype> AtackTwo = default!;

    [DataField("atackThree", required: true)]
    public ProtoId<CombatTechPrototype> AtackThree = default!;
}
[Prototype("combatTech")]
[Serializable, NetSerializable, DataDefinition]
public sealed partial class CombatTechPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public MartialArtsForms MartialArtsForm;

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> PackMessageOnHit;

    [DataField]
    public float PushStrength;

    [DataField]
    public int HitDamage;

    [DataField]
    public float ParalyzeTime;

    [DataField]
    public float StaminaDamage;

    [DataField]
    public float MaxPushDistance;

    [DataField]
    public string DamageType = "Slash";

    [DataField]
    public EntProtoId? EffectPunch;

    [DataField]
    public EntProtoId? SelfEffect;

    [DataField]
    public float Range;

    [DataField]
    public SoundSpecifier HitSound = default!;
}
