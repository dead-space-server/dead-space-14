using System.Numerics;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.Maps;
using Content.Shared.Mind.Components;
using Content.Shared.Placeable;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Prometheus;

namespace Content.Server.DeadSpace.Physics;

/// <summary>
/// Resolves dynamic bodies that remain awake because they are stuck in hard static overlaps.
/// </summary>
public sealed partial class PhysicsSanitySystem : EntitySystem
{
    private static readonly Gauge CandidatesGauge = Metrics.CreateGauge(
        "physics_sanity_candidates",
        "Number of awake physics bodies that currently look stuck in hard static overlaps.");

    private static readonly Gauge TrackedBodiesGauge = Metrics.CreateGauge(
        "physics_sanity_tracked_bodies",
        "Number of stuck physics bodies waiting for sanity resolution.");

    private static readonly Counter ResolvedCount = Metrics.CreateCounter(
        "physics_sanity_resolved_count",
        "Amount of stuck physics bodies moved by physics sanity.");

    private static readonly Counter FailedResolveCount = Metrics.CreateCounter(
        "physics_sanity_failed_resolve_count",
        "Amount of stuck physics bodies physics sanity could not move.");

    private static readonly Counter ResolveLimitReachedCount = Metrics.CreateCounter(
        "physics_sanity_resolve_limit_reached_count",
        "Amount of physics sanity updates that reached the per-update resolve limit.");

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AnchorableSystem _anchorable = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    private EntityQuery<ActorComponent> _actorQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<MindContainerComponent> _mindQuery;
    private EntityQuery<PlaceableSurfaceComponent> _placeableSurfaceQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private readonly Dictionary<EntityUid, StuckBodyState> _stuckBodies = new();
    private readonly HashSet<EntityUid> _seen = new();
    private readonly List<EntityUid> _candidates = new();
    private readonly List<EntityUid> _toRemove = new();

    private TimeSpan _nextUpdate;
    private bool _enabled = true;

    private const float MaxLinearSpeedSquared = 0.01f;
    private const float MaxAngularSpeed = 0.05f;
    private const int SearchRadius = 4;
    private const int MaxResolvesPerUpdate = 12;
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan StuckTime = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan FailedResolveCooldown = TimeSpan.FromSeconds(10);

    public override void Initialize()
    {
        base.Initialize();

        _actorQuery = GetEntityQuery<ActorComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _mindQuery = GetEntityQuery<MindContainerComponent>();
        _placeableSurfaceQuery = GetEntityQuery<PlaceableSurfaceComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        Subs.CVar(_cfg, CCCCVars.PhysicsSanityEnabled, OnEnabledChanged, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled || _timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;
        _seen.Clear();
        _candidates.Clear();

        foreach (var body in _physics.AwakeBodies)
        {
            if (!IsStuckCandidate(body.Owner, body.Comp1, body.Comp2))
                continue;

            _seen.Add(body.Owner);
            _candidates.Add(body.Owner);
        }

        CleanupStaleBodies();

        var resolved = 0;
        foreach (var uid in _candidates)
        {
            if (!_stuckBodies.TryGetValue(uid, out var state))
            {
                _stuckBodies[uid] = new StuckBodyState(_timing.CurTime + StuckTime);
                continue;
            }

            if (_timing.CurTime < state.NextResolve)
                continue;

            if (!ResolveStuckBody(uid))
            {
                FailedResolveCount.Inc();

                _stuckBodies[uid] = state with
                {
                    NextResolve = _timing.CurTime + FailedResolveCooldown,
                };

                continue;
            }

            _stuckBodies.Remove(uid);
            resolved++;
            ResolvedCount.Inc();

            if (resolved >= MaxResolvesPerUpdate)
            {
                ResolveLimitReachedCount.Inc();
                break;
            }
        }

        CandidatesGauge.Set(_candidates.Count);
        TrackedBodiesGauge.Set(_stuckBodies.Count);
    }

    private void OnEnabledChanged(bool value)
    {
        _enabled = value;

        if (value)
            return;

        _stuckBodies.Clear();
        _seen.Clear();
        _candidates.Clear();
        _toRemove.Clear();
        CandidatesGauge.Set(0);
        TrackedBodiesGauge.Set(0);
    }

    private void CleanupStaleBodies()
    {
        _toRemove.Clear();

        foreach (var uid in _stuckBodies.Keys)
        {
            if (!_seen.Contains(uid) || Deleted(uid))
                _toRemove.Add(uid);
        }

        foreach (var uid in _toRemove)
        {
            _stuckBodies.Remove(uid);
        }
    }

    private bool IsStuckCandidate(EntityUid uid, PhysicsComponent body, TransformComponent xform)
    {
        if (Deleted(uid) ||
            !body.Awake ||
            body.BodyType != BodyType.Dynamic ||
            !body.CanCollide ||
            !body.Hard ||
            !body.SleepingAllowed ||
            body.ContactCount == 0 ||
            body.LinearVelocity.LengthSquared() > MaxLinearSpeedSquared ||
            MathF.Abs(body.AngularVelocity) > MaxAngularSpeed)
        {
            return false;
        }

        if (xform.MapID == MapId.Nullspace ||
            IsPausedMap(xform.MapID) ||
            xform.GridUid == null ||
            xform.ParentUid != xform.GridUid.Value ||
            xform.Anchored)
        {
            return false;
        }

        if (_gridQuery.HasComp(uid) ||
            _actorQuery.HasComp(uid) ||
            _mindQuery.TryComp(uid, out var mind) && mind.HasMind)
        {
            return false;
        }

        var hasBlockingStaticOverlap = false;
        var contacts = _physics.GetContacts(uid);
        while (contacts.MoveNext(out var contact))
        {
            if (!contact.Enabled || contact.Deleting || !contact.IsTouching || !contact.Hard)
                continue;

            var other = contact.OtherEnt(uid);
            if (!_physicsQuery.TryComp(other, out var otherBody) ||
                !otherBody.CanCollide ||
                !otherBody.Hard)
            {
                continue;
            }

            if (IsPlaceableSurfaceContact(other))
                continue;

            hasBlockingStaticOverlap |= otherBody.BodyType == BodyType.Static ||
                _xformQuery.TryComp(other, out var otherXform) && otherXform.Anchored;
        }

        return hasBlockingStaticOverlap;
    }

    private bool IsPlaceableSurfaceContact(EntityUid uid)
    {
        return _placeableSurfaceQuery.TryComp(uid, out var surface) && surface.IsPlaceable;
    }

    private bool IsPausedMap(MapId mapId)
    {
        return _map.TryGetMap(mapId, out var mapUid) && _map.IsPaused((mapUid.Value, null));
    }

    private bool ResolveStuckBody(EntityUid uid)
    {
        if (!_physicsQuery.TryComp(uid, out var body) ||
            !_xformQuery.TryComp(uid, out var xform) ||
            xform.GridUid is not { } gridUid ||
            !_gridQuery.TryComp(gridUid, out var grid))
        {
            return false;
        }

        var origin = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        if (!TryFindFreeTile((gridUid, grid), origin, body, out var tile))
            return false;

        var coordinates = _map.ToCenterCoordinates(gridUid, tile, grid);
        _transform.SetCoordinates(uid, xform, coordinates);
        _physics.SetLinearVelocity(uid, Vector2.Zero, wakeBody: false, body: body);
        _physics.SetAngularVelocity(uid, 0f, body: body);
        _physics.SetAwake((uid, body), false);
        return true;
    }

    private bool TryFindFreeTile(
        Entity<MapGridComponent> grid,
        Vector2i origin,
        PhysicsComponent body,
        out Vector2i freeTile)
    {
        for (var radius = 0; radius <= SearchRadius; radius++)
        {
            for (var x = -radius; x <= radius; x++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius)
                        continue;

                    var tile = origin + new Vector2i(x, y);
                    if (!IsFreeTile(grid, tile, body))
                        continue;

                    freeTile = tile;
                    return true;
                }
            }
        }

        freeTile = default;
        return false;
    }

    private bool IsFreeTile(Entity<MapGridComponent> grid, Vector2i tile, PhysicsComponent body)
    {
        if (!_map.TryGetTileRef(grid.Owner, grid.Comp, tile, out var tileRef) ||
            tileRef.Tile.IsEmpty ||
            _turf.IsSpace(tileRef))
        {
            return false;
        }

        return _anchorable.TileFree(grid, tile, body.CollisionLayer, body.CollisionMask);
    }

    private readonly record struct StuckBodyState(TimeSpan NextResolve);
}
