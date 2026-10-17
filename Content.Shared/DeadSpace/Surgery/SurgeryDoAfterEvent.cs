using Content.Shared.DoAfter;
using Content.Shared.DeadSpace.Surgery.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Surgery;

[Serializable, NetSerializable]
public sealed partial class SurgeryDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public ProtoId<SurgeryPrototype> Surgery;

    [DataField]
    public int StepIndex;

    [DataField]
    public NetEntity? Tool;

    public override DoAfterEvent Clone()
    {
        return new SurgeryDoAfterEvent
        {
            Surgery = Surgery,
            StepIndex = StepIndex,
            Tool = Tool,
        };
    }

    public override bool IsDuplicate(DoAfterEvent other)
    {
        return other is SurgeryDoAfterEvent surgery
               && surgery.Surgery == Surgery
               && surgery.StepIndex == StepIndex;
    }
}
