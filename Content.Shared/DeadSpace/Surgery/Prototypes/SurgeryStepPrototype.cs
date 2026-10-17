using Content.Shared.DeadSpace.Surgery.Conditions;
using Content.Shared.DeadSpace.Surgery.Effects;
using Content.Shared.Tools;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Surgery.Prototypes;

[Prototype("surgeryStep")]
public sealed partial class SurgeryStepPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public SurgeryStepVariant[] Variants = [];

    [DataField]
    public ProtoId<SurgeryComplicationPrototype>[] Complications = [];
}

[DataDefinition]
public sealed partial class SurgeryStepVariant
{
    [DataField]
    public EntityWhitelist? Tool;

    [DataField]
    public HashSet<ProtoId<ToolQualityPrototype>> Qualities = [];

    [DataField]
    public SurgeryCondition[] Conditions = [];

    [DataField]
    public float Duration = 3f;

    [DataField]
    public bool BreakOnMove = true;

    [DataField]
    public bool BreakOnDamage = true;

    [DataField]
    public bool NeedHand = true;

    [DataField]
    public LocId? StartMessage;

    [DataField]
    public LocId? SuccessMessage;

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public SurgeryEffect[] Effects = [];

    [DataField]
    public SurgeryEffect[] FailEffects = [];

    [DataField]
    public ProtoId<SurgeryComplicationPrototype>[] Complications = [];
}
