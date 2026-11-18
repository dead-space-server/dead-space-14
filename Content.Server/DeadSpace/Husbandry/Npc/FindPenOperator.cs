// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Threading;
using System.Threading.Tasks;
using Content.Server.DeadSpace.Husbandry.Pen;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Robust.Shared.Map;

namespace Content.Server.DeadSpace.Husbandry.Npc;

/// <summary>
/// For an animal that is not in a pen (it has none, or it was taken out of it): puts a straight way to the middle of its
/// own pen, or of the nearest one, on the blackboard for <c>AnimalGoToOperator</c> (see
/// <see cref="AnimalPenSystem.TryGetRouteToPen"/>). Does nothing (the plan goes on to the food, the water, the stroll) for
/// an animal that is in its pen, and an animal is not sent again for a while, so this costs a lookup by tile.
/// </summary>
public sealed partial class FindPenOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private AnimalPenSystem _pens = default!;

    /// <summary>
    /// The way to the pen for <c>AnimalGoToOperator</c>.
    /// </summary>
    [DataField]
    public string KeyRoute = "TargetRoute";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pens = sysManager.GetEntitySystem<AnimalPenSystem>();
    }

    public override Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<AnimalPenComponent>(owner, out var penComp) ||
            _pens.GetPen((owner, penComp)) is { } pen && _pens.IsInPen((owner, penComp), pen) ||
            !_pens.TryGetRouteToPen((owner, penComp), out var route))
        {
            return Task.FromResult<(bool, Dictionary<string, object>?)>((false, null));
        }

        return Task.FromResult<(bool, Dictionary<string, object>?)>((true, new Dictionary<string, object>
        {
            { KeyRoute, route },
        }));
    }
}
