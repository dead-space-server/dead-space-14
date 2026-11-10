// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.NPC;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.DeadSpace.Husbandry.Feeding;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server.DeadSpace.Husbandry.Npc;

/// <summary>
/// Starts eating the target. Sets how long the animal should wait afterwards, like <c>AltInteractOperator</c>.
/// </summary>
public sealed partial class AnimalEatOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField]
    public string Key = "Target";

    /// <summary>
    /// Where the time to wait for the animal to finish is stored.
    /// </summary>
    [DataField]
    public string IdleKey = "IdleTime";

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        return (true, new Dictionary<string, object> { { IdleKey, 1f } });
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var target = blackboard.GetValue<EntityUid>(Key);

        if (!_entManager.System<AnimalFeedingSystem>().TryEat(owner, target, out var delay))
            return HTNOperatorStatus.Failed;

        blackboard.SetValue(IdleKey, (float) delay.TotalSeconds + 0.5f);
        return HTNOperatorStatus.Finished;
    }
}
