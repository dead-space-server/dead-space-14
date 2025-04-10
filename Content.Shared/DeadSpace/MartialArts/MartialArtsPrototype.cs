//using Content.Shared.Dataset;
//using Robust.Shared.Audio;
//using Robust.Shared.Prototypes;
//using Robust.Shared.Serialization;
//using Robust.Shared.Serialization.Manager.Attributes;

//namespace Content.Shared.DeadSpace.MartialArts;

//[Prototype("martialArt")]
//[Serializable, NetSerializable, DataDefinition]
//public sealed partial class MartialArtPrototype : IPrototype
//{
//    [IdDataField] public string ID { get; private set; } = default!;

//    [DataField]
//    public MartialArtsForms MartialArtsForm;

//    [DataField]
//    public ProtoId<ComboTechiqueListPrototype> ComboValues = "ArkalyseList";
//}
//[Prototype("combatTech")]
//[Serializable, NetSerializable, DataDefinition]
//public sealed partial class CombatTechPrototype : IPrototype
//{
//    [IdDataField] public string ID { get; private set; } = default!;

//    [DataField(required: true)]
//    public MartialArtsForms MartialArtsForm;

//    [DataField]
//    public ProtoId<LocalizedDatasetPrototype> PackMessageOnHit;

//    [DataField]
//    public float PushStrength;

//    [DataField]
//    public int HitDamage;

//    [DataField]
//    public float ParalyzeTime;

//    [DataField]
//    public float StaminaDamage;

//    [DataField]
//    public float MaxPushDistance;

//    [DataField]
//    public string DamageType = "Slash";

//    [DataField]
//    public EntProtoId? EffectPunch;

//    [DataField]
//    public EntProtoId? SelfEffect;

//    [DataField]
//    public float Range;

//    [DataField]
//    public SoundSpecifier HitSound = default!;
//}

//[Prototype("techList")]
//public sealed partial class ComboTechiqueListPrototype : IPrototype
//{
//    [IdDataField] public string ID { get; private init; } = default!;

//    [DataField(required: true)]
//    public List<ProtoId<CombatTechPrototype>> Technique = new();
//}
