// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Threading;
using System.Threading.Tasks;
using Content.Server.DeadSpace.Husbandry.Wander;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.DeadSpace.Husbandry.Wander;

namespace Content.Server.DeadSpace.Husbandry.Npc;

/// <summary>
/// Idle time of an animal with an <see cref="AnimalWanderComponent"/>: it either goes for a stroll or stays where it is,
/// then the time it stands around is set for the wait task after this one. The walking itself is done by
/// AnimalWanderSystem and the walker of the animal, this only starts it and waits for it.
/// </summary>
public sealed partial class AnimalWanderOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    /// <summary>
    /// Where the time to stand around afterwards is stored.
    /// </summary>
    [DataField]
    public string IdleKey = "IdleTime";

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (!_entManager.HasComponent<AnimalWanderComponent>(owner))
            return (false, null);

        return (true, new Dictionary<string, object> { { IdleKey, 1f } });
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var wander = (owner, _entManager.GetComponent<AnimalWanderComponent>(owner));

        blackboard.SetValue(IdleKey, _entManager.System<AnimalWanderSystem>().StartOrStand(wander));
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var wander = (owner, _entManager.GetComponent<AnimalWanderComponent>(owner));

        return _entManager.System<AnimalWanderSystem>().IsWalking(wander)
            ? HTNOperatorStatus.Continuing
            : HTNOperatorStatus.Finished;
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        base.TaskShutdown(blackboard, status);

        // A better plan came (hungry, thirsty) or the brain was turned off (somebody mounted): stop where it is.
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (_entManager.TryGetComponent<AnimalWanderComponent>(owner, out var comp))
            _entManager.System<AnimalWanderSystem>().StopWalk((owner, comp));
    }
}
