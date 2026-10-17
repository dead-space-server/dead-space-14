using Content.Shared.Body.Components;
using Content.Shared.Body.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Surgery.Conditions;

public sealed partial class SurgeryConditionAnatomy : SurgeryConditionBase<SurgeryConditionAnatomy>
{
    [DataField]
    public SurgeryEntityTarget Target = SurgeryEntityTarget.Patient;

    [DataField]
    public List<ProtoId<BodyPrototype>> Bodies = [];
}

public sealed partial class SurgeryConditionAnatomySystem : SurgeryConditionSystem<SurgeryConditionAnatomy>
{
    protected override void OnCondition(ref SurgeryConditionEvent<SurgeryConditionAnatomy> args)
    {
        args.Result = false;

        if (args.Context.GetEntity(args.Condition.Target) is not { } ent)
            return;

        if (!TryComp<BodyComponent>(ent, out var body) || body.Prototype is not { } prototype)
            return;

        args.Result = args.Condition.Bodies.Contains(prototype);
    }
}
