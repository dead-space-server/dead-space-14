// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Threading;
using System.Threading.Tasks;
using Content.Server.DeadSpace.Husbandry.Feeding;
using Content.Server.DeadSpace.Husbandry.Pen;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.DeadSpace.Husbandry.Needs;
using Content.Shared.Nutrition.Components;
using Robust.Shared.Map;

namespace Content.Server.DeadSpace.Husbandry.Npc;

/// <summary>
/// Looks for the closest food the animal is willing to eat <b>in its pen</b> and works out the way to it over the
/// tiles of the pen (see <see cref="AnimalPenSystem.TryGetRoute"/>). A feeder comes before food on the floor. Sets the
/// target and its coordinates like <c>UtilityOperator</c> does, so the usual move operator follows.
///
/// There is no radius and no pathfinder job: the pen is the area to look in, it is already worked out (and kept), and
/// one breadth-first search over it finds the nearest of all the foods at once. An animal that is not in its pen
/// (it was carried away) looks for nothing and waits to be at home.
/// </summary>
public sealed partial class FindFoodOperator : HTNOperator
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

    private readonly HashSet<Entity<EdibleComponent>> _foods = new();
    private readonly HashSet<Entity<AnimalFeederComponent>> _feeders = new();
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

        // A search that found nothing is not repeated at once, whatever it costs.
        if (!_entManager.TryGetComponent<AnimalNeedsComponent>(owner, out var needs) ||
            !_needs.CanSearch((owner, needs), food: true) ||
            !_entManager.TryGetComponent<DietComponent>(owner, out var diet) ||
            !_entManager.TryGetComponent<AnimalPenComponent>(owner, out var penComp) ||
            _pens.GetPen((owner, penComp)) is not { } pen)
        {
            return Task.FromResult<(bool, Dictionary<string, object>?)>((false, null));
        }

        // A feeder with food in it comes first, then the food on the floor.
        _pens.GetEntitiesInPen(pen, _feeders, adjacent: true);
        _targets.Clear();
        foreach (var feeder in _feeders)
        {
            if (_feeding.TryFindFood((owner, diet), feeder, out _))
                _targets.Add(feeder);
        }

        if (!_pens.TryGetRoute((owner, penComp), pen, _targets, out var chosen, out var route))
        {
            _pens.GetEntitiesInPen(pen, _foods, adjacent: true);
            _targets.Clear();
            foreach (var food in _foods)
            {
                if (_feeding.CanEat((owner, diet), food))
                    _targets.Add(food);
            }

            if (!_pens.TryGetRoute((owner, penComp), pen, _targets, out chosen, out route))
            {
                _feeding.DelaySearch(owner, food: true);
                return Task.FromResult<(bool, Dictionary<string, object>?)>((false, null));
            }
        }

        return Task.FromResult<(bool, Dictionary<string, object>?)>((true, new Dictionary<string, object>
        {
            { Key, chosen },
            { KeyCoordinates, new EntityCoordinates(chosen, System.Numerics.Vector2.Zero) },
            { KeyRoute, route },
        }));
    }
}
