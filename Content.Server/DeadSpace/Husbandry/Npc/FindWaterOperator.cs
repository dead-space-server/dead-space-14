// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.NPC;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.DeadSpace.Husbandry.Feeding;
using Content.Server.DeadSpace.Husbandry.Walking;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Content.Shared.Chemistry.Components;
using Content.Shared.DeadSpace.Husbandry.Feeding;
using Robust.Shared.Containers;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.Husbandry.Needs;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Husbandry.Npc;

/// <summary>
/// Looks around for the closest water source with something in it that the animal can walk to.
/// Sets the target and its coordinates like <c>UtilityOperator</c> does, so the usual move operator follows.
/// </summary>
public sealed partial class FindWaterOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private AnimalFeedingSystem _feeding = default!;
    private SharedAnimalNeedsSystem _needs = default!;
    private SharedContainerSystem _container = default!;
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
    /// How far the animal looks for water, in tiles.
    /// </summary>
    [DataField]
    public float Range = 25f;

    /// <summary>
    /// How many of the closest sources are checked for a path before giving up.
    /// </summary>
    [DataField]
    public int MaxCandidates = 5;

    /// <summary>
    /// How far from the water the animal stops, the arrival distance of the move operator.
    /// </summary>
    [DataField]
    public float Reach = 1.5f;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _feeding = sysManager.GetEntitySystem<AnimalFeedingSystem>();
        _needs = sysManager.GetEntitySystem<SharedAnimalNeedsSystem>();
        _container = sysManager.GetEntitySystem<SharedContainerSystem>();
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
        if (_entManager.TryGetComponent<AnimalNeedsComponent>(owner, out var needs) && !_needs.CanSearch((owner, needs), food: false))
            return (false, null);
        var ownerCoordinates = _entManager.GetComponent<TransformComponent>(owner).Coordinates;

        // Any container with something to drink in it that lies about (not held, not in a bag): a trough, a bucket, a bowl.
        var sources = new HashSet<Entity<DrainableSolutionComponent>>();
        _lookup.GetEntitiesInRange(ownerCoordinates, Range, sources, LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries);

        var candidates = new List<(EntityUid Source, float Distance)>();
        foreach (var source in sources)
        {
            if (_container.IsEntityInContainer(source) ||
                !_feeding.CanDrink(source) ||
                !_walker.IsTargetAllowed(owner, _transform.GetMapCoordinates(source), Reach))
                continue;

            var distance = (_transform.GetWorldPosition(source) - _transform.GetWorldPosition(owner)).LengthSquared();
            candidates.Add((source, distance));
        }

        candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        for (var i = 0; i < candidates.Count && i < MaxCandidates; i++)
        {
            var source = candidates[i].Source;
            var path = await _pathfinding.GetPath(owner, source, 1f, cancelToken);
            if (path.Result == PathResult.NoPath)
                continue;

            var target = new EntityCoordinates(source, Vector2.Zero);
            var route = new List<EntityCoordinates>(path.Path.Count + 1);
            foreach (var poly in path.Path)
            {
                route.Add(new EntityCoordinates(poly.GraphUid, poly.Box.Center));
            }

            route.Add(target);

            return (true, new Dictionary<string, object>
            {
                { Key, source },
                { KeyCoordinates, target },
                { KeyRoute, route },
            });
        }

        _feeding.DelaySearch(owner, food: false);

        return (false, null);
    }
}
