using Content.Shared.Bed.Sleep;

namespace Content.Shared.DeadSpace.Surgery.Conditions;

public sealed partial class SurgeryConditionSleeping : SurgeryConditionBase<SurgeryConditionSleeping>
{
    [DataField]
    public SurgeryEntityTarget Target = SurgeryEntityTarget.Patient;
}

public sealed partial class SurgeryConditionSleepingSystem : SurgeryConditionSystem<SurgeryConditionSleeping>
{
    protected override void OnCondition(ref SurgeryConditionEvent<SurgeryConditionSleeping> args)
    {
        args.Result = false;

        if (args.Context.GetEntity(args.Condition.Target) is not { } ent)
            return;

        args.Result = HasComp<SleepingComponent>(ent);
    }
}
