using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;

namespace Content.Shared.DeadSpace.Surgery.Conditions;

public sealed partial class SurgeryConditionMobState : SurgeryConditionBase<SurgeryConditionMobState>
{
    [DataField]
    public SurgeryEntityTarget Target = SurgeryEntityTarget.Patient;

    [DataField]
    public HashSet<MobState> States = [MobState.Alive];
}

public sealed partial class SurgeryConditionMobStateSystem : SurgeryConditionSystem<SurgeryConditionMobState>
{
    [Dependency] private readonly MobStateSystem _mobState = default!;

    protected override void OnCondition(ref SurgeryConditionEvent<SurgeryConditionMobState> args)
    {
        args.Result = false;

        if (args.Context.GetEntity(args.Condition.Target) is not { } ent)
            return;

        foreach (var state in args.Condition.States)
        {
            var matches = state switch
            {
                MobState.Alive => _mobState.IsAlive(ent),
                MobState.Critical => _mobState.IsCritical(ent),
                MobState.Dead => _mobState.IsDead(ent),
                MobState.PreCritical => _mobState.IsPreCritical(ent),
                _ => false,
            };

            if (!matches)
                continue;

            args.Result = true;
            return;
        }
    }
}
