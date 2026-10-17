using Content.Shared.DeadSpace.Surgery.Conditions;
using Content.Shared.DeadSpace.Surgery.Effects;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Surgery.Prototypes;

[Prototype("surgery")]
public sealed partial class SurgeryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public LocId? Name;

    [DataField]
    public LocId? Description;

    [DataField(required: true)]
    public ProtoId<SurgeryStepPrototype>[] Steps = [];

    [DataField]
    public ProtoId<SurgeryPrototype>[] Requires = [];

    [DataField]
    public SurgeryCondition[] Conditions = [];

    [DataField]
    public ProtoId<SurgeryComplicationPrototype>[] Complications = [];

    [DataField]
    public SurgeryEffect[] Effects = [];

    [DataField]
    public bool Repeatable;

    [DataField]
    public int Priority;
}
