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
/// Drinks from the target, sip after sip into the stomach (see <see cref="AnimalFeedingSystem"/>), until the stomach is
/// full, the container is empty, or the animal has had enough: its hydration is <see cref="EnoughFraction"/> of the
/// most. The hydration itself comes later, as the stomach digests. Sets how long the animal should wait afterwards.
/// </summary>
public sealed partial class AnimalDrinkOperator : HTNOperator
{
    private const string SipsKey = "AnimalDrinkSips";

    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField]
    public string Key = "Target";

    /// <summary>
    /// Where the time to wait after the drink is stored.
    /// </summary>
    [DataField]
    public string IdleKey = "IdleTime";

    [DataField]
    public float EnoughFraction = 0.9f;

    /// <summary>
    /// A drink is never longer than this many sips.
    /// </summary>
    [DataField]
    public int MaxSips = 6;

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
        var started = _entManager.System<AnimalFeedingSystem>().TryDrink(owner, target, out _);
        blackboard.SetValue(SipsKey, started ? 1 : 0);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var feeding = _entManager.System<AnimalFeedingSystem>();

        var sips = blackboard.GetValueOrDefault<int>(SipsKey, _entManager);
        if (sips <= 0)
        {
            feeding.DelaySearch(owner, food: false);
            return HTNOperatorStatus.Failed;
        }

        // A sip is going on.
        if (feeding.IsBusy(owner))
            return HTNOperatorStatus.Continuing;

        blackboard.SetValue(IdleKey, 2f);

        if (sips >= MaxSips || HasHadEnough(owner))
        {
            feeding.DelaySearch(owner, food: false);
            return HTNOperatorStatus.Finished;
        }

        if (!blackboard.TryGetValue<EntityUid>(Key, out var target, _entManager) ||
            _entManager.Deleted(target) ||
            !feeding.TryDrink(owner, target, out _))
        {
            feeding.DelaySearch(owner, food: false);
            return HTNOperatorStatus.Finished;
        }

        blackboard.SetValue(SipsKey, sips + 1);
        return HTNOperatorStatus.Continuing;
    }

    private bool HasHadEnough(EntityUid owner)
    {
        if (!_entManager.TryGetComponent<AnimalNeedsComponent>(owner, out var needs))
            return true;

        return needs.Hydration.Value >= needs.Hydration.Max * EnoughFraction;
    }
}
