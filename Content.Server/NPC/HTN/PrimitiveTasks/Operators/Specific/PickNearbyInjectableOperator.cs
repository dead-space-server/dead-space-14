using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Pathfinding;
using Content.Shared.DeadSpace.Chemistry.Components;
using Content.Shared.Interaction;
using Content.Shared.Silicons.Bots;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators.Specific;

public sealed partial class PickNearbyInjectableOperator : HTNOperator
{
<<<<<<< ours
    [Dependency] private readonly IEntityManager _entManager = default!;
    private MedibotSystem _medibot = default!;
    private PathfindingSystem _pathfinding = default!;

    private EntityQuery<DamageableComponent> _damageQuery = default!;
    private EntityQuery<InjectableSolutionComponent> _injectQuery = default!;
    private EntityQuery<NPCRecentlyInjectedComponent> _recentlyInjected = default!;
    private EntityQuery<MobStateComponent> _mobState = default!;
    private EntityQuery<EmaggedComponent> _emaggedQuery = default!;
    private EntityQuery<InjectionBlockerComponent> _injectionBlockerQuery = default!; // DS14
||||||| base
    [Dependency] private readonly IEntityManager _entManager = default!;
    private MedibotSystem _medibot = default!;
    private PathfindingSystem _pathfinding = default!;

    private EntityQuery<DamageableComponent> _damageQuery = default!;
    private EntityQuery<InjectableSolutionComponent> _injectQuery = default!;
    private EntityQuery<NPCRecentlyInjectedComponent> _recentlyInjected = default!;
    private EntityQuery<MobStateComponent> _mobState = default!;
    private EntityQuery<EmaggedComponent> _emaggedQuery = default!;
=======
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private MedibotSystem _medibot = default!;
    [Dependency] private PathfindingSystem _pathfinding = default!;
>>>>>>> theirs

    [DataField("rangeKey")] public string RangeKey = NPCBlackboard.MedibotInjectRange;

    /// <summary>
    /// Target entity to inject
    /// </summary>
    [DataField("targetKey", required: true)]
    public string TargetKey = string.Empty;

    /// <summary>
    /// Target entitycoordinates to move to.
    /// </summary>
    [DataField("targetMoveKey", required: true)]
    public string TargetMoveKey = string.Empty;

<<<<<<< ours
    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _medibot = sysManager.GetEntitySystem<MedibotSystem>();
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();

        _damageQuery = _entManager.GetEntityQuery<DamageableComponent>();
        _injectQuery = _entManager.GetEntityQuery<InjectableSolutionComponent>();
        _recentlyInjected = _entManager.GetEntityQuery<NPCRecentlyInjectedComponent>();
        _mobState = _entManager.GetEntityQuery<MobStateComponent>();
        _emaggedQuery = _entManager.GetEntityQuery<EmaggedComponent>();
        _injectionBlockerQuery = _entManager.GetEntityQuery<InjectionBlockerComponent>(); // DS14
    }

||||||| base
    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _medibot = sysManager.GetEntitySystem<MedibotSystem>();
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();

        _damageQuery = _entManager.GetEntityQuery<DamageableComponent>();
        _injectQuery = _entManager.GetEntityQuery<InjectableSolutionComponent>();
        _recentlyInjected = _entManager.GetEntityQuery<NPCRecentlyInjectedComponent>();
        _mobState = _entManager.GetEntityQuery<MobStateComponent>();
        _emaggedQuery = _entManager.GetEntityQuery<EmaggedComponent>();
    }

=======
>>>>>>> theirs
    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<float>(RangeKey, out _, _entManager))
            return (false, null);

        if (!_entManager.TryGetComponent<MedibotComponent>(owner, out var medibot))
            return (false, null);

        if (!blackboard.TryGetValue<IEnumerable<KeyValuePair<EntityUid, float>>>("TargetList", out var patients, _entManager))
            return (false, null);

        foreach (var (entity, _) in patients)
        {
<<<<<<< ours
            // DS14-start
            if (_injectionBlockerQuery.HasComponent(entity))
                continue;
            // DS14-end

            if (_mobState.TryGetComponent(entity, out var state) &&
                _injectQuery.HasComponent(entity) &&
                _damageQuery.TryGetComponent(entity, out var damage) &&
                !_recentlyInjected.HasComponent(entity))
            {
                // no treating dead bodies
                if (!_medibot.TryGetTreatment(medibot, state.CurrentState, out var treatment))
                    continue;
||||||| base
            if (_mobState.TryGetComponent(entity, out var state) &&
                _injectQuery.HasComponent(entity) &&
                _damageQuery.TryGetComponent(entity, out var damage) &&
                !_recentlyInjected.HasComponent(entity))
            {
                // no treating dead bodies
                if (!_medibot.TryGetTreatment(medibot, state.CurrentState, out var treatment))
                    continue;
=======
            if (!_medibot.CheckInjectable((owner, medibot), entity, true))
                continue;
>>>>>>> theirs

            //Needed to make sure it doesn't sometimes stop right outside it's interaction range
            var pathRange = SharedInteractionSystem.InteractionRange - 1f;
            var path = await _pathfinding.GetPath(owner, entity, pathRange, cancelToken);

            if (path.Result == PathResult.NoPath)
                continue;

            return (true, new Dictionary<string, object>()
            {
                { TargetKey, entity },
                { TargetMoveKey, _entManager.GetComponent<TransformComponent>(entity).Coordinates },
                { NPCBlackboard.PathfindKey, path },
            });
        }

        return (false, null);
    }
}
