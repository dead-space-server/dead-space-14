// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Threading;
using System.Threading.Tasks;
using Content.Server.DeadSpace.Husbandry.Walking;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.DeadSpace.Husbandry.Walking;
using Robust.Shared.Map;

namespace Content.Server.DeadSpace.Husbandry.Npc;

/// <summary>
/// Walks the animal along the route that <c>FindFoodOperator</c> or <c>FindWaterOperator</c> put on the blackboard, with
/// the <see cref="AnimalWalkerComponent"/> of the animal, instead of the usual steering of the AI. Finishes when it
/// gets there, fails if it cannot.
/// </summary>
public sealed partial class AnimalGoToOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    /// <summary>
    /// Where the route is stored, a list of <see cref="EntityCoordinates"/>, the last one is the target.
    /// </summary>
    [DataField]
    public string RouteKey = "TargetRoute";

    /// <summary>
    /// How close to the target counts as being there. Eating and drinking work from a couple of tiles.
    /// </summary>
    [DataField]
    public float ArriveDistance = 1.5f;

    /// <summary>
    /// Run instead of walk.
    /// </summary>
    [DataField]
    public bool Run;

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var valid = _entManager.HasComponent<AnimalWalkerComponent>(owner) &&
                    blackboard.TryGetValue<List<EntityCoordinates>>(RouteKey, out var route, _entManager) &&
                    route.Count > 0;

        return (valid, null);
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (!_entManager.TryGetComponent<AnimalWalkerComponent>(owner, out var walker) ||
            !blackboard.TryGetValue<List<EntityCoordinates>>(RouteKey, out var route, _entManager))
        {
            return;
        }

        // The route is the tiles of a path, it is cut short wherever a straight strip is free.
        var system = _entManager.System<AnimalWalkerSystem>();
        if (!system.StartRoute((owner, walker), route, ArriveDistance, Run, smooth: true))
            system.Stop((owner, walker));
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (!_entManager.TryGetComponent<AnimalWalkerComponent>(owner, out var walker))
            return HTNOperatorStatus.Failed;

        // Not walking and not arrived: it never started.
        return walker.Status switch
        {
            WalkStatus.Walking => HTNOperatorStatus.Continuing,
            WalkStatus.Arrived => HTNOperatorStatus.Finished,
            _ => HTNOperatorStatus.Failed,
        };
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        base.TaskShutdown(blackboard, status);

        // Whatever the reason, it stops where it is: there, or a better plan came, or the brain was turned off.
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (_entManager.TryGetComponent<AnimalWalkerComponent>(owner, out var walker))
            _entManager.System<AnimalWalkerSystem>().Stop((owner, walker));
    }
}
