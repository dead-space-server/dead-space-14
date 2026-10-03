using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent]
public sealed partial class NinjaInfoConditionComponent : Component
{
    [DataField] public List<ProtoId<JobPrototype>> TargetJobs = new();

    [DataField] public EntityUid? PriorityMind;

    [DataField] public int TargetCount = 3;
    [DataField] public int JobCount = 3;
    [DataField] public int CorrectScans;
    [DataField] public HashSet<EntityUid> ScannedEntities = new();
}
