using Content.Shared.Fluids.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Surgery.Conditions;

public sealed partial class SurgeryConditionTrashNearby : SurgeryConditionBase<SurgeryConditionTrashNearby>
{
    [DataField]
    public SurgeryEntityTarget Reference = SurgeryEntityTarget.Patient;

    [DataField]
    public float Radius = 1.5f;

    [DataField]
    public int MinCount = 2;

    [DataField]
    public bool CountTrash = true;

    [DataField]
    public ProtoId<TagPrototype> TrashTag = "Trash";

    [DataField]
    public bool CountPuddles = true;

    [DataField]
    public bool CountCorpses;
}

public sealed partial class SurgeryConditionTrashNearbySystem : SurgeryConditionSystem<SurgeryConditionTrashNearby>
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private readonly HashSet<EntityUid> _found = [];

    protected override void OnCondition(ref SurgeryConditionEvent<SurgeryConditionTrashNearby> args)
    {
        args.Result = false;

        var reference = args.Context.GetEntity(args.Condition.Reference) ?? args.Context.Patient;

        _found.Clear();
        _lookup.GetEntitiesInRange(reference, args.Condition.Radius, _found);

        var count = 0;
        foreach (var ent in _found)
        {
            if (ent == reference)
                continue;

            if (args.Condition.CountPuddles && HasComp<PuddleComponent>(ent))
            {
                count++;
                continue;
            }

            if (args.Condition.CountTrash && _tag.HasTag(ent, args.Condition.TrashTag))
            {
                count++;
                continue;
            }

            if (args.Condition.CountCorpses && _mobState.IsDead(ent))
                count++;
        }

        args.Result = count >= args.Condition.MinCount;
    }
}
