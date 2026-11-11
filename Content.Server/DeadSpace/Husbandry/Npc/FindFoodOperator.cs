// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.NPC;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.DeadSpace.Husbandry.Feeding;
using Content.Server.DeadSpace.Husbandry.Walking;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Content.Shared.DeadSpace.Husbandry.Feeding;
using Content.Shared.Nutrition.Components;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.Husbandry.Needs;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Husbandry.Npc;

/// <summary>
/// Looks around for the closest food the animal is willing to eat and can walk to.
/// Sets the target and its coordinates like <c>UtilityOperator</c> does, so the usual move operator follows.
/// </summary>
public sealed partial class FindFoodOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private const LookupFlags Floor = LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries;
    private AnimalFeedingSystem _feeding = default!;
    private SharedAnimalNeedsSystem _needs = default!;
    private EntityLookupSystem _lookup = default!;
    private PathfindingSystem _pathfinding = default!;
    private SharedTransformSystem _transform = default!;
    private AnimalWalkerSystem _walker = default!;

    [DataField]
    public string Key = "Target";

    /// <summary>
    /// The EntityCoordinates of the target.
    /// </summary>
    [DataField]
    public string KeyCoordinates = "TargetCoordinates";

    /// <summary>
    /// The way to the target for <c>AnimalGoToOperator</c>: the tiles of the path and then the target itself.
    /// </summary>
    [DataField]
    public string KeyRoute = "TargetRoute";

    /// <summary>
    /// How far the animal looks for food, in tiles.
    /// </summary>
    [DataField]
    public float Range = 25f;

    /// <summary>
    /// How many of the closest foods are checked for a path before giving up.
    /// </summary>
    [DataField]
    public int MaxCandidates = 5;

    /// <summary>
    /// How far from the food the animal stops, the arrival distance of the move operator.
    /// </summary>
    [DataField]
    public float Reach = 1.5f;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _feeding = sysManager.GetEntitySystem<AnimalFeedingSystem>();
        _needs = sysManager.GetEntitySystem<SharedAnimalNeedsSystem>();
        _lookup = sysManager.GetEntitySystem<EntityLookupSystem>();
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
        _transform = sysManager.GetEntitySystem<SharedTransformSystem>();
        _walker = sysManager.GetEntitySystem<AnimalWalkerSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        // A search that found nothing is not repeated at once: the same lookup of the whole area every plan is the
        // cost of a hungry animal with nothing to eat.
        if (_entManager.TryGetComponent<AnimalNeedsComponent>(owner, out var needs) && !_needs.CanSearch((owner, needs), food: true))
            return (false, null);

        if (!_entManager.TryGetComponent<DietComponent>(owner, out var diet))
            return (false, null);

        var ownerCoordinates = _entManager.GetComponent<TransformComponent>(owner).Coordinates;
        var foods = new HashSet<Entity<EdibleComponent>>();
        // Only what lies about: nothing in bags, pockets or boxes is looked at, that is most of the entities of an area.
        _lookup.GetEntitiesInRange(ownerCoordinates, Range, foods, Floor);
        var feeders = new HashSet<Entity<AnimalFeederComponent>>();
        _lookup.GetEntitiesInRange(ownerCoordinates, Range, feeders, Floor);

        // A feeder comes before food on the floor, then the closest first.
        var candidates = new List<(EntityUid Food, int Priority, float Distance)>();
        foreach (var feeder in feeders)
        {
            if (!_feeding.TryFindFood((owner, diet), feeder, out _) ||
                !_walker.IsTargetAllowed(owner, _transform.GetMapCoordinates(feeder), Reach))
                continue;

            var distance = (_transform.GetWorldPosition(feeder) - _transform.GetWorldPosition(owner)).LengthSquared();
            candidates.Add((feeder, 0, distance));
        }

        foreach (var food in foods)
        {
            if (!_feeding.CanEat((owner, diet), food) ||
                !_walker.IsTargetAllowed(owner, _transform.GetMapCoordinates(food), Reach))
                continue;

            var distance = (_transform.GetWorldPosition(food) - _transform.GetWorldPosition(owner)).LengthSquared();
            candidates.Add((food, 1, distance));
        }

        candidates.Sort((a, b) => a.Priority != b.Priority
            ? a.Priority.CompareTo(b.Priority)
            : a.Distance.CompareTo(b.Distance));

        for (var i = 0; i < candidates.Count && i < MaxCandidates; i++)
        {
            var food = candidates[i].Food;
            var path = await _pathfinding.GetPath(owner, food, 1f, cancelToken);
            if (path.Result == PathResult.NoPath)
                continue;

            var target = new EntityCoordinates(food, Vector2.Zero);
            var route = new List<EntityCoordinates>(path.Path.Count + 1);
            foreach (var poly in path.Path)
            {
                route.Add(new EntityCoordinates(poly.GraphUid, poly.Box.Center));
            }

            route.Add(target);

            return (true, new Dictionary<string, object>
            {
                { Key, food },
                { KeyCoordinates, target },
                { KeyRoute, route },
            });
        }

        _feeding.DelaySearch(owner, food: true);

        return (false, null);
    }
}
