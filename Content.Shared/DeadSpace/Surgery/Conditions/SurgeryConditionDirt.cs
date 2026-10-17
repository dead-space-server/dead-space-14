namespace Content.Shared.DeadSpace.Surgery.Conditions;

public sealed partial class SurgeryConditionDirt : SurgeryConditionBase<SurgeryConditionDirt>
{
    [DataField]
    public SurgeryEntityTarget Target = SurgeryEntityTarget.Surgeon;

    [DataField]
    public float Threshold = 20f;
}

public sealed partial class SurgeryConditionDirtSystem : SurgeryConditionSystem<SurgeryConditionDirt>
{
    protected override void OnCondition(ref SurgeryConditionEvent<SurgeryConditionDirt> args)
    {
        args.Result = false;

        if (args.Context.GetEntity(args.Condition.Target) is not { } ent)
            return;

        if (!TryComp<SurgeryDirtinessComponent>(ent, out var dirt))
            return;

        args.Result = dirt.Amount >= args.Condition.Threshold;
    }
}
