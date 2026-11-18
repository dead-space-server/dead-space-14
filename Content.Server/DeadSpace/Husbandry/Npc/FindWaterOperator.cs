// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Threading;
using System.Threading.Tasks;
using Content.Server.DeadSpace.Husbandry.Feeding;
using Content.Server.DeadSpace.Husbandry.Pen;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Chemistry.Components;
using Content.Shared.DeadSpace.Husbandry.Needs;
using Robust.Shared.Map;

namespace Content.Server.DeadSpace.Husbandry.Npc;

/// <summary>
/// Looks for the closest container with something to drink in it <b>in the pen</b> of the animal (a drink is only what
/// the animal drinks, see <see cref="DietComponent.DrinkReagents"/>) and works out the way to it over the tiles of the
/// pen. Like <see cref="FindFoodOperator"/>: no radius, no pathfinder job.
/// </summary>
public sealed partial class FindWaterOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private AnimalFeedingSystem _feeding = default!;
    private SharedAnimalNeedsSystem _needs = default!;
    private AnimalPenSystem _pens = default!;

    [DataField]
    public string Key = "Target";

    /// <summary>
    /// The EntityCoordinates of the target.
    /// </summary>
    [DataField]
    public string KeyCoordinates = "TargetCoordinates";

    /// <summary>
    /// The way to the target for <c>AnimalGoToOperator</c>: the tiles of the pen and then the target itself.
    /// </summary>
    [DataField]
    public string KeyRoute = "TargetRoute";

    private readonly HashSet<Entity<DrainableSolutionComponent>> _sources = new();
    private readonly List<EntityUid> _targets = new();

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _feeding = sysManager.GetEntitySystem<AnimalFeedingSystem>();
        _needs = sysManager.GetEntitySystem<SharedAnimalNeedsSystem>();
        _pens = sysManager.GetEntitySystem<AnimalPenSystem>();
    }

    public override Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<AnimalNeedsComponent>(owner, out var needs) ||
            !_needs.CanSearch((owner, needs), food: false) ||
            !_entManager.TryGetComponent<DietComponent>(owner, out var diet) ||
            !_entManager.TryGetComponent<AnimalPenComponent>(owner, out var penComp) ||
            _pens.GetPen((owner, penComp)) is not { } pen)
        {
            return Task.FromResult<(bool, Dictionary<string, object>?)>((false, null));
        }

        // Any open container that lies in the pen (not held, not in a bag) with a drink in it: a trough, a bucket, a bowl.
        _pens.GetEntitiesInPen(pen, _sources, adjacent: true);
        _targets.Clear();
        foreach (var source in _sources)
        {
            if (_feeding.CanDrink((owner, diet), source))
                _targets.Add(source);
        }

        if (!_pens.TryGetRoute((owner, penComp), pen, _targets, out var chosen, out var route))
        {
            _feeding.DelaySearch(owner, food: false);
            return Task.FromResult<(bool, Dictionary<string, object>?)>((false, null));
        }

        return Task.FromResult<(bool, Dictionary<string, object>?)>((true, new Dictionary<string, object>
        {
            { Key, chosen },
            { KeyCoordinates, new EntityCoordinates(chosen, System.Numerics.Vector2.Zero) },
            { KeyRoute, route },
        }));
    }
}
