using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Surgery.Conditions;

public sealed partial class SurgeryConditionToolQuality : SurgeryConditionBase<SurgeryConditionToolQuality>
{
    [DataField]
    public SurgeryEntityTarget Target = SurgeryEntityTarget.Tool;

    [DataField]
    public HashSet<ProtoId<ToolQualityPrototype>> Qualities = [];

    [DataField]
    public bool RequireAll = true;
}

public sealed partial class SurgeryConditionToolQualitySystem : SurgeryConditionSystem<SurgeryConditionToolQuality>
{
    [Dependency] private readonly SharedToolSystem _tools = default!;

    protected override void OnCondition(ref SurgeryConditionEvent<SurgeryConditionToolQuality> args)
    {
        args.Result = false;

        if (args.Context.GetEntity(args.Condition.Target) is not { } ent)
            return;

        if (args.Condition.Qualities.Count == 0)
            return;

        foreach (var quality in args.Condition.Qualities)
        {
            var matches = _tools.HasQuality(ent, quality.Id);

            if (args.Condition.RequireAll && !matches)
                return;

            if (!args.Condition.RequireAll && matches)
            {
                args.Result = true;
                return;
            }
        }

        args.Result = args.Condition.RequireAll;
    }
}
