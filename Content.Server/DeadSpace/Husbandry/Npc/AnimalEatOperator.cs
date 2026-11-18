// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Threading;
using System.Threading.Tasks;
using Content.Server.DeadSpace.Husbandry.Feeding;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.DeadSpace.Husbandry.Needs;

namespace Content.Server.DeadSpace.Husbandry.Npc;

/// <summary>
/// Eats the target the way anybody does, bite after bite into the stomach (see <see cref="AnimalFeedingSystem"/>), and
/// goes on until the stomach is full, the food is gone, or the animal has had enough: its satiety is
/// <see cref="EnoughFraction"/> of the most. The satiety itself comes later, as the stomach digests. Sets how long the
/// animal should wait afterwards, like <c>AltInteractOperator</c>.
/// </summary>
public sealed partial class AnimalEatOperator : HTNOperator
{
    private const string BitesKey = "AnimalEatBites";

    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField]
    public string Key = "Target";

    /// <summary>
    /// Where the time to wait after the meal is stored.
    /// </summary>
    [DataField]
    public string IdleKey = "IdleTime";

    /// <summary>
    /// The animal stops when its satiety is this share of the most it can have.
    /// </summary>
    [DataField]
    public float EnoughFraction = 0.9f;

    /// <summary>
    /// A meal is never longer than this many bites.
    /// </summary>
    [DataField]
    public int MaxBites = 12;

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        return (true, new Dictionary<string, object> { { IdleKey, 1f } });
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var target = blackboard.GetValue<EntityUid>(Key);
        var started = _entManager.System<AnimalFeedingSystem>().TryEat(owner, target, out _);
        blackboard.SetValue(BitesKey, started ? 1 : 0);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var feeding = _entManager.System<AnimalFeedingSystem>();

        var bites = blackboard.GetValueOrDefault<int>(BitesKey, _entManager);
        if (bites <= 0)
        {
            feeding.DelaySearch(owner, food: true);
            feeding.StopEating(owner);
            return HTNOperatorStatus.Failed;
        }

        // A bite is going on.
        if (feeding.IsBusy(owner))
            return HTNOperatorStatus.Continuing;

        blackboard.SetValue(IdleKey, 2f);

        if (bites >= MaxBites || HasHadEnough(owner))
        {
            feeding.DelaySearch(owner, food: true);
            feeding.StopEating(owner);
            return HTNOperatorStatus.Finished;
        }

        // The next bite, from the same thing or, if it is gone, from what is left of it (a feeder).
        if (!blackboard.TryGetValue<EntityUid>(Key, out var target, _entManager) ||
            _entManager.Deleted(target) ||
            !feeding.TryEat(owner, target, out _))
        {
            // The stomach is full or the food is gone: wait for it to be digested.
            feeding.DelaySearch(owner, food: true);
            feeding.StopEating(owner);
            return HTNOperatorStatus.Finished;
        }

        blackboard.SetValue(BitesKey, bites + 1);
        return HTNOperatorStatus.Continuing;
    }

    private bool HasHadEnough(EntityUid owner)
    {
        if (!_entManager.TryGetComponent<AnimalNeedsComponent>(owner, out var needs))
            return true;

        return needs.Satiety.Value >= needs.Satiety.Max * EnoughFraction;
    }
}
