using Content.Server.NPC;
using Content.Shared.DeadSpace.Critters;
using Robust.Shared.Timing;

namespace Content.Server.NPC.HTN.Preconditions;

public sealed partial class CritterGrazePrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue<EntityUid>(NPCBlackboard.Owner, out var owner, _entManager)
            || !_entManager.TryGetComponent<CritterGrazeComponent>(owner, out var graze))
            return false;

        return _timing.CurTime >= graze.NextGraze;
    }
}
