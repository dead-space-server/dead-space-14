using System.Collections.Generic;
using Content.Server.Atmos.Components;
using Content.Server.DeadSpace.SpawnERTShuttleCommand;
using Content.Server.Disposal.Unit;
using Content.Server.GameTicking;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.Power.EntitySystems;
using Content.Shared.DeadSpace.Administration;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.AntagMaps;

public sealed partial class RoundMapPreloadSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private NodeGroupSystem _nodes = default!;
    [Dependency] private PowerNetSystem _power = default!;
    [Dependency] private DisposalUnitSystem _disposal = default!;

    private readonly Queue<(ResPath Path, bool Grid, string? ShuttleId)> _pending = new();

    private static readonly TimeSpan LoadGap = TimeSpan.FromSeconds(4);

    private static readonly HashSet<ResPath> AntagPaths = new()
    {
        new("/Maps/Nonstations/nukieplanet.yml"),
        new("/Maps/Nonstations/wizardsden.yml"),
        new("/Maps/Shuttles/mothership.yml"),
        new("/Maps/Nonstations/ds_thecircle_outpost_docked.yml"),
        new("/Maps/Nonstations/freelancers_Outpost.yml"),
        new("/Maps/Nonstations/Raider_outpost.yml"),
        new("/Maps/_DeadSpace/Ninja/ninja.yml"),
        new("/Maps/Nonstations/ds_lone_ops_outpost.yml"),
        new("/Maps/Shuttles/ds_geist_shuttle.yml"),
        new("/Maps/Shuttles/ShuttleEvent/deathsquad.yml"),
    };

    private static readonly (ResPath Path, bool Grid, int Copies)[] AntagLoads =
    {
        (new("/Maps/Nonstations/ds_thecircle_outpost_docked.yml"), false, 1),
        (new("/Maps/_DeadSpace/Ninja/ninja.yml"), false, 1),
        (new("/Maps/Nonstations/nukieplanet.yml"), false, 1),
        (new("/Maps/Nonstations/Raider_outpost.yml"), false, 1),
        (new("/Maps/Nonstations/freelancers_Outpost.yml"), false, 1),
        (new("/Maps/Nonstations/wizardsden.yml"), false, 1),
        (new("/Maps/Nonstations/ds_lone_ops_outpost.yml"), false, 1),
        (new("/Maps/Shuttles/mothership.yml"), true, 1),
        (new("/Maps/Shuttles/ds_geist_shuttle.yml"), true, 2),
        (new("/Maps/Shuttles/ShuttleEvent/deathsquad.yml"), true, 1),
    };

    private bool _enabled;
    private bool _queueReady;
    private TimeSpan _nextLoadAt;
    private string _slowest = "";
    private long _slowestMs;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(NodeGroupSystem));
        UpdatesAfter.Add(typeof(PowerNetSystem));
        UpdatesAfter.Add(typeof(DisposalUnitSystem));
        UpdatesAfter.Add(typeof(PhysicsSystem));
        SubscribeLocalEvent<PostGameMapLoad>(OnMapLoad);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        Subs.CVar(_cfg, CCCCVars.RoundMapPreloadEnabled, value => _enabled = value, true);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _queueReady = false;
        _pending.Clear();
        _nextLoadAt = TimeSpan.Zero;
        _slowest = "";
        _slowestMs = 0;
    }

    private void OnMapLoad(PostGameMapLoad ev)
    {
        if (!_enabled)
            return;

        EnsureQueue();
        if (_pending.Count == 0)
            return;

        var count = _pending.Count;
        var total = System.Diagnostics.Stopwatch.StartNew();
        while (_pending.Count > 0)
        {
            var item = _pending.Dequeue();
            Load(item.Path, item.Grid, item.ShuttleId);
        }

        total.Stop();
        Log.Info($"Round map preload drained {count} maps in {total.ElapsedMilliseconds} ms. Slowest: {_slowest} {_slowestMs} ms.");
    }

    private void EnsureQueue()
    {
        if (_queueReady)
            return;

        _queueReady = true;
        foreach (var (path, grid, copies) in AntagLoads)
        {
            for (var i = 0; i < copies; i++)
                _pending.Enqueue((path, grid, null));
        }

        foreach (var shuttle in _prototypes.EnumeratePrototypes<ERTShuttlePrototype>())
            _pending.Enqueue((shuttle.Path, true, shuttle.ID));

        Log.Info($"Round map preload queued {_pending.Count} maps.");
    }

    public bool TryClaim(ResPath path, out MapId mapId, out List<EntityUid> grids)
    {
        mapId = MapId.Nullspace;
        grids = new List<EntityUid>();
        var query = AllEntityQuery<PreparedRoundMapComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.Claimed || comp.ShuttleId != null || comp.Path != path)
                continue;

            grids = CollectGrids(uid);
            if (grids.Count == 0)
                continue;

            comp.Claimed = true;
            _map.SetPaused(uid, false);
            mapId = xform.MapID;
            return true;
        }

        return false;
    }

    public bool TryClaimShuttle(string shuttleId, out MapId mapId, out EntityUid grid)
    {
        mapId = MapId.Nullspace;
        grid = EntityUid.Invalid;
        var query = AllEntityQuery<PreparedRoundMapComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.Claimed || comp.ShuttleId != shuttleId)
                continue;

            var grids = CollectGrids(uid);
            if (grids.Count == 0)
                continue;

            comp.Claimed = true;
            _map.SetPaused(uid, false);
            mapId = xform.MapID;
            grid = grids[0];
            return true;
        }

        return false;
    }

    public void EnsureAntag(EntityUid map, ResPath path)
    {
        if (!AntagPaths.Contains(path) || HasComp<PreparedRoundMapComponent>(map))
            return;

        var comp = AddComp<PreparedRoundMapComponent>(map);
        comp.Path = path;
        comp.SleepWhenEmpty = true;
        comp.Claimed = true;
        comp.LoadedAt = _timing.CurTime;
        comp.DisposalPassNeeded = _disposal.PassEpoch + 1;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var occupied = new HashSet<MapId>();
        var actors = AllEntityQuery<ActorComponent, TransformComponent>();
        while (actors.MoveNext(out var uid, out _, out var xform))
        {
            if (HasComp<GhostComponent>(uid) || HasComp<AdminGhostVisibilityComponent>(uid))
                continue;

            occupied.Add(xform.MapID);
        }

        var maps = AllEntityQuery<PreparedRoundMapComponent, MapComponent>();
        while (maps.MoveNext(out var uid, out var prepared, out var map))
        {
            if (!prepared.SleepWhenEmpty || !map.MapInitialized || Terminating(uid))
                continue;

            if (!prepared.Warmed)
            {
                if (!IsWarmed(map.MapId, prepared))
                    continue;

                prepared.Warmed = true;
            }

            var pause = !occupied.Contains(map.MapId);
            if (map.MapPaused != pause)
                _map.SetPaused(uid, pause);
        }

        if (!_enabled || _ticker.RunLevel != GameRunLevel.PreRoundLobby)
            return;

        EnsureQueue();
        if (_pending.Count == 0 || _timing.CurTime < _nextLoadAt)
            return;

        var item = _pending.Dequeue();
        Load(item.Path, item.Grid, item.ShuttleId);
        _nextLoadAt = _timing.CurTime + LoadGap;
        if (_pending.Count == 0)
            Log.Info($"Round map preload finished in the lobby. Slowest: {_slowest} {_slowestMs} ms.");
    }

    private bool IsWarmed(MapId mapId, PreparedRoundMapComponent prepared)
    {
        if (_timing.CurTime <= prepared.LoadedAt || !_nodes.IsIdle || !_power.IsIdle)
            return false;

        if (_disposal.CompletedPassEpoch < prepared.DisposalPassNeeded)
            return false;

        var grids = AllEntityQuery<GridAtmosphereComponent, TransformComponent>();
        while (grids.MoveNext(out _, out var atmos, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            if (atmos.UpdateCounter <= 1 || atmos.InvalidatedCoordsCount > 0 || atmos.ProcessingPaused)
                return false;
        }

        return true;
    }

    private void Load(ResPath path, bool grid, string? shuttleId)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var opts = DeserializationOptions.Default with { InitializeMaps = true };
        EntityUid mapUid;
        if (!grid)
        {
            if (!_loader.TryLoadMap(path, out var map, out _, opts))
            {
                Log.Error($"Failed to preload map {path}.");
                return;
            }

            mapUid = map.Value.Owner;
        }
        else
        {
            mapUid = _map.CreateMap(out var mapId);
            if (!_loader.TryLoadGrid(mapId, path, out _, opts))
            {
                Log.Error($"Failed to preload grid {path}.");
                _map.DeleteMap(mapId);
                return;
            }
        }

        var comp = EnsureComp<PreparedRoundMapComponent>(mapUid);
        comp.Path = path;
        comp.ShuttleId = shuttleId;
        comp.SleepWhenEmpty = true;
        comp.Warmed = false;
        comp.Claimed = false;
        comp.LoadedAt = _timing.CurTime;
        comp.DisposalPassNeeded = _disposal.PassEpoch + 1;
        _map.SetPaused(mapUid, false);
        _meta.SetEntityName(mapUid, shuttleId == null ? $"antag:{path.Filename}" : $"response:{shuttleId}");

        watch.Stop();
        Log.Info($"Preloaded {(shuttleId == null ? "antag" : "response")} {path} in {watch.ElapsedMilliseconds} ms.");
        if (watch.ElapsedMilliseconds > _slowestMs)
        {
            _slowestMs = watch.ElapsedMilliseconds;
            _slowest = path.ToString();
        }
    }

    private List<EntityUid> CollectGrids(EntityUid map)
    {
        var grids = new List<EntityUid>();
        if (HasComp<MapGridComponent>(map))
            grids.Add(map);

        var children = Transform(map).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (HasComp<MapGridComponent>(child))
                grids.Add(child);
        }

        return grids;
    }
}
