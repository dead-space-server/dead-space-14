// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Destructible;
using Content.Shared.DeadSpace.Lavaland;
using Robust.Shared.Player;
using Content.Server.NodeContainer.Nodes;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.Player;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Robust.Shared.Map;
using System.Numerics;
using Content.Server.GameTicking.Events;
using Content.Shared.Atmos.Piping.Binary.Components;
using Content.Shared.NodeContainer;
using Content.Shared.NodeContainer.NodeGroups;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandMorkiteMissionSystem : EntitySystem
{
    private static readonly TimeSpan NetworkInterval = TimeSpan.FromSeconds(1);

    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly DestructibleSystem _destructible = default!;
    private float _objectiveRoll;
    private TimeSpan _nextNetworkUpdate;

    // Pipe network -> extractors reaching it. Rebuilt every network update.
    private readonly Dictionary<INodeGroup, HashSet<EntityUid>> _extractorsByGroup = new();
    private readonly HashSet<EntityUid> _powered = new();
    private readonly HashSet<INodeGroup> _visited = new();
    private readonly Queue<INodeGroup> _queue = new();
    private readonly HashSet<EntityUid> _wellExtractors = new();
    private readonly List<EntityUid> _unpowered = new();
    private readonly Dictionary<EntityUid, LavalandMorkiteExtractorComponent> _pipes = new();

    // Pipes bucketed by map cell, so each fauna checks only the pipes around it, not every pipe.
    private const float PipeCellSize = 16f;
    private readonly Dictionary<(MapId Map, Vector2i Cell), List<(EntityUid Pipe, Vector2 Position, LavalandMorkiteExtractorComponent Config)>> _pipeCells = new();
    private float _maxAttractRange;
    private readonly List<(MapId Map, Vector2 Position)> _playerPositions = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartingEvent>(_ => _objectiveRoll = _random.NextFloat());
        SubscribeLocalEvent<LavalandMorkiteMissionComponent, MapInitEvent>(OnMissionMapInit);
        SubscribeLocalEvent<LavalandMorkitePoweredComponent, ComponentShutdown>(OnPoweredShutdown);
        SubscribeNetworkEvent<LavalandMorkiteHudReopenRequestEvent>(OnHudReopenRequest);
    }

    /// <summary>A closed morkite panel opens again on a click on the well, an extractor or a pipe of their network.</summary>
    private void OnHudReopenRequest(LavalandMorkiteHudReopenRequestEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user ||
            GetEntity(ev.Target) is not { Valid: true } target ||
            TerminatingOrDeleted(target) ||
            !HasComp<LavalandBluespaceWellComponent>(target) &&
            !HasComp<LavalandMorkiteExtractorComponent>(target) &&
            !_pipes.ContainsKey(target))
        {
            return;
        }

        var userXform = Transform(user);
        var targetXform = Transform(target);
        if (userXform.MapID != targetXform.MapID ||
            (_transform.GetWorldPosition(userXform) - _transform.GetWorldPosition(targetXform)).Length() > 3f)
        {
            return;
        }

        RaiseNetworkEvent(new LavalandMorkiteHudReopenEvent(), args.SenderSession);
    }

    private void OnMissionMapInit(Entity<LavalandMorkiteMissionComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.IsMorkite = _objectiveRoll < ent.Comp.MorkiteChance;
        var paper = Spawn(ent.Comp.IsMorkite ? ent.Comp.MorkiteObjective : ent.Comp.OreObjective,
            Transform(ent).Coordinates);
        var container = _containers.EnsureContainer<Container>(ent, "storagebase");
        _containers.Insert(paper, container);
    }

    private void OnPoweredShutdown(Entity<LavalandMorkitePoweredComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent))
            _power.SetNeedsPower(ent, true);
    }

    public override void Update(float frameTime)
    {
        UpdateMissions();

        var now = _timing.CurTime;
        if (now < _nextNetworkUpdate)
            return;
        _nextNetworkUpdate = now + NetworkInterval;
        UpdateNetworks();
        UpdateSaboteurs(now);
        SendHud();
    }

    /// <summary>Well bar, pipeline integrity and connection for everyone on a lavaland with a morkite objective.</summary>
    private void SendHud()
    {
        var maps = EntityQueryEnumerator<LavalandMapComponent, TransformComponent>();
        while (maps.MoveNext(out var map, out _, out var mapXform))
        {
            if (!HasExtractorsOnMap(map))
                continue;

            LavalandBluespaceWellComponent? best = null;
            EntityUid bestUid = default;
            var wells = EntityQueryEnumerator<LavalandBluespaceWellComponent, TransformComponent>();
            while (wells.MoveNext(out var wellUid, out var well, out var wellXform))
            {
                if (wellXform.MapUid != map || well.ReturnAt != null || !wellXform.Anchored)
                    continue;
                if (best == null || well.Morkite > best.Morkite)
                {
                    best = well;
                    bestUid = wellUid;
                }
            }

            var link = best == null
                ? LavalandMorkiteLink.NoWell
                : GetMorkiteRate(bestUid) > 0f
                    ? LavalandMorkiteLink.Connected
                    : best.WasConnected ? LavalandMorkiteLink.Leaking : LavalandMorkiteLink.NotConnected;

            var integrity = 100f;
            foreach (var pipe in _pipes.Keys)
            {
                if (TerminatingOrDeleted(pipe) || Transform(pipe).MapUid != map ||
                    !TryComp<DamageableComponent>(pipe, out var damageable))
                {
                    continue;
                }

                var destroyedAt = _destructible.DestroyedAt(pipe).Float();
                if (destroyedAt <= 0f)
                    continue;
                integrity = MathF.Min(integrity, 100f * (1f - damageable.TotalDamage.Float() / destroyedAt));
            }

            var ev = new LavalandMorkiteHudEvent(best?.Morkite ?? 0f, Math.Max(0f, integrity), link);
            RaiseNetworkEvent(ev, Filter.BroadcastMap(mapXform.MapID));
        }
    }

    /// <summary>
    /// Fauna near an extractor's pipeline leaves to chew on it, unless a player is close enough to fight.
    /// </summary>
    private void UpdateSaboteurs(TimeSpan now)
    {
        _pipes.Clear();
        foreach (var (group, extractors) in _extractorsByGroup)
        {
            LavalandMorkiteExtractorComponent? config = null;
            foreach (var extractor in extractors)
            {
                if (TryComp(extractor, out config))
                    break;
            }

            if (config == null)
                continue;

            foreach (var node in group.Nodes)
            {
                if (node is PipeNode && HasComp<DamageableComponent>(node.Owner) && !TerminatingOrDeleted(node.Owner))
                    _pipes.TryAdd(node.Owner, config);
            }
        }

        foreach (var cell in _pipeCells.Values)
            cell.Clear();
        _maxAttractRange = 0f;
        foreach (var (pipe, config) in _pipes)
        {
            var pipeXform = Transform(pipe);
            var pipePosition = _transform.GetWorldPosition(pipeXform);
            var key = (pipeXform.MapID, PipeCell(pipePosition));
            if (!_pipeCells.TryGetValue(key, out var bucket))
                _pipeCells[key] = bucket = new();
            bucket.Add((pipe, pipePosition, config));
            _maxAttractRange = MathF.Max(_maxAttractRange, config.FaunaAttractRange);
        }

        _playerPositions.Clear();
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is { Valid: true } player && _mobState.IsAlive(player))
                _playerPositions.Add((Transform(player).MapID, _transform.GetWorldPosition(player)));
        }

        var fauna = EntityQueryEnumerator<LavalandFaunaComponent, HTNComponent, TransformComponent>();
        while (fauna.MoveNext(out var uid, out _, out var htn, out var xform))
        {
            if (_pipes.Count == 0 || !_mobState.IsAlive(uid) || IsBoss(uid))
            {
                Release(uid);
                continue;
            }

            var position = _transform.GetWorldPosition(xform);
            EntityUid? target = null;
            LavalandMorkiteExtractorComponent? targetConfig = null;
            var best = float.MaxValue;
            var cell = PipeCell(position);
            var reach = (int) MathF.Ceiling(_maxAttractRange / PipeCellSize);
            for (var dx = -reach; dx <= reach; dx++)
            {
                for (var dy = -reach; dy <= reach; dy++)
                {
                    if (!_pipeCells.TryGetValue((xform.MapID, cell + new Vector2i(dx, dy)), out var bucket))
                        continue;

                    foreach (var (pipe, pipePosition, config) in bucket)
                    {
                        var distance = Vector2.DistanceSquared(position, pipePosition);
                        if (distance > config.FaunaAttractRange * config.FaunaAttractRange || distance >= best)
                            continue;

                        best = distance;
                        target = pipe;
                        targetConfig = config;
                    }
                }
            }

            if (target is not { } pipeTarget || targetConfig == null || PlayerNear(xform.MapID, position, targetConfig.FaunaPlayerAggroRange))
            {
                Release(uid);
                continue;
            }

            var saboteur = EnsureComp<LavalandPipeSaboteurComponent>(uid);
            saboteur.Pipe = pipeTarget;
            if (htn.Enabled)
            {
                _htn.SetHTNEnabled((uid, htn), false);
                saboteur.DisabledAi = true;
            }

            _steering.TryRegister(uid, Transform(pipeTarget).Coordinates);
            if (MathF.Sqrt(best) > targetConfig.FaunaBiteRange || now < saboteur.NextBite)
                continue;

            saboteur.NextBite = now + targetConfig.FaunaBiteInterval;
            _damageable.TryChangeDamage(pipeTarget, targetConfig.FaunaBiteDamage, origin: uid);
        }
    }

    private static Vector2i PipeCell(Vector2 position)
    {
        return new Vector2i((int) MathF.Floor(position.X / PipeCellSize), (int) MathF.Floor(position.Y / PipeCellSize));
    }

    private bool PlayerNear(MapId map, Vector2 position, float range)
    {
        foreach (var (playerMap, playerPosition) in _playerPositions)
        {
            if (playerMap == map && Vector2.DistanceSquared(position, playerPosition) <= range * range)
                return true;
        }
        return false;
    }

    private bool IsBoss(EntityUid uid)
    {
        return HasComp<LavalandBossComponent>(uid) || HasComp<LavalandMadMinerComponent>(uid) ||
               HasComp<LavalandLegionMegafaunaComponent>(uid) || HasComp<LavalandEliteComponent>(uid);
    }

    private void Release(EntityUid uid)
    {
        if (!TryComp<LavalandPipeSaboteurComponent>(uid, out var saboteur))
            return;

        _steering.Unregister(uid);
        if (saboteur.DisabledAi && TryComp<HTNComponent>(uid, out var htn))
            _htn.SetHTNEnabled((uid, htn), true);
        RemComp<LavalandPipeSaboteurComponent>(uid);
    }

    /// <summary>
    /// Morkite bar fill rate (percent per second) for a well, from extractors whose pipe network
    /// reaches a pipe device anchored on the well's tile.
    /// </summary>
    public float GetMorkiteRate(EntityUid well)
    {
        var xform = Transform(well);
        if (!xform.Anchored || xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return 0f;

        _wellExtractors.Clear();
        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var device))
        {
            if (device is not { } uid || !TryComp<NodeContainerComponent>(uid, out var nodes))
                continue;

            foreach (var node in nodes.Nodes.Values)
            {
                if (node is PipeNode { NodeGroup: { } group } &&
                    _extractorsByGroup.TryGetValue(group, out var extractors))
                {
                    _wellExtractors.UnionWith(extractors);
                }
            }
        }

        var rate = 0f;
        foreach (var extractor in _wellExtractors)
        {
            if (TryComp<LavalandMorkiteExtractorComponent>(extractor, out var comp))
                rate += 100f / (float) Math.Max(1, comp.RequiredExtractionTime.TotalSeconds);
        }
        return rate;
    }

    private void UpdateNetworks()
    {
        _extractorsByGroup.Clear();
        _powered.Clear();

        var extractors = EntityQueryEnumerator<LavalandMorkiteExtractorComponent, TransformComponent>();
        while (extractors.MoveNext(out var uid, out _, out var xform))
        {
            if (!xform.Anchored || xform.MapUid is not { } map || !HasComp<LavalandMapComponent>(map))
                continue;

            // The extractor joins the pipe network through a gas port (or any pipe) anchored under it.
            if (TryComp<NodeContainerComponent>(uid, out var ownNodes))
                FloodFrom(uid, ownNodes);

            if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, _map.TileIndicesFor(gridUid, grid, xform.Coordinates));
            while (anchored.MoveNext(out var device))
            {
                if (device is { } deviceUid && deviceUid != uid && TryComp<NodeContainerComponent>(deviceUid, out var nodes))
                    FloodFrom(uid, nodes);
            }
        }

        foreach (var uid in _powered)
        {
            if (HasComp<LavalandMorkitePoweredComponent>(uid))
                continue;
            AddComp<LavalandMorkitePoweredComponent>(uid);
            _power.SetNeedsPower(uid, false);
        }

        _unpowered.Clear();
        var powered = EntityQueryEnumerator<LavalandMorkitePoweredComponent>();
        while (powered.MoveNext(out var uid, out _))
        {
            if (!_powered.Contains(uid))
                _unpowered.Add(uid);
        }
        foreach (var uid in _unpowered)
            RemComp<LavalandMorkitePoweredComponent>(uid);
    }

    /// <summary>
    /// Walks pipe networks reachable from an extractor. Devices with several pipe nodes (pumps, filters, mixers)
    /// connect their networks, closed valves do not.
    /// </summary>
    private void FloodFrom(EntityUid extractor, NodeContainerComponent nodes)
    {
        foreach (var node in nodes.Nodes.Values)
        {
            if (node is PipeNode { NodeGroup: { } group })
                Flood(extractor, group);
        }
    }

    private void Flood(EntityUid extractor, INodeGroup start)
    {
        _visited.Clear();
        _queue.Clear();
        _queue.Enqueue(start);

        while (_queue.TryDequeue(out var group))
        {
            if (!_visited.Add(group))
                continue;

            if (!_extractorsByGroup.TryGetValue(group, out var reached))
                _extractorsByGroup[group] = reached = new HashSet<EntityUid>();
            reached.Add(extractor);

            foreach (var node in group.Nodes)
            {
                var owner = node.Owner;
                if (TryComp<ApcPowerReceiverComponent>(owner, out var receiver) &&
                    (receiver.NeedsPower || HasComp<LavalandMorkitePoweredComponent>(owner)))
                {
                    _powered.Add(owner);
                }

                if (TryComp<GasValveComponent>(owner, out var valve) && !valve.Open ||
                    !TryComp<NodeContainerComponent>(owner, out var container))
                {
                    continue;
                }

                foreach (var other in container.Nodes.Values)
                {
                    if (other is PipeNode { NodeGroup: { } otherGroup } && !_visited.Contains(otherGroup))
                        _queue.Enqueue(otherGroup);
                }
            }
        }
    }

    private void UpdateMissions()
    {
        var missions = EntityQueryEnumerator<LavalandMorkiteMissionComponent>();
        while (missions.MoveNext(out _, out var mission))
        {
            if (!mission.IsMorkite || mission.Spawned || !TryFindLavalandOutpost(out var map, out var outpost))
                continue;
            mission.Spawned = true;

            // Several mission folders (or a re-issued one) must not spawn extra extractor sets.
            if (HasExtractorsOnMap(map))
                continue;

            var center = _transform.GetMapCoordinates(outpost).Position;
            var baseRadius = 0f;
            if (TryComp<MapGridComponent>(outpost, out var baseGrid))
            {
                var min = new Vector2(float.MaxValue);
                var max = new Vector2(float.MinValue);
                foreach (var tile in _map.GetAllTiles(outpost, baseGrid))
                {
                    if (tile.Tile.IsEmpty)
                        continue;
                    var position = _transform.ToMapCoordinates(_map.ToCenterCoordinates(tile)).Position;
                    min = Vector2.Min(min, position);
                    max = Vector2.Max(max, position);
                }
                if (min.X != float.MaxValue)
                {
                    center = (min + max) / 2;
                    baseRadius = Vector2.Distance(min, max) / 2 + 1f;
                }
            }
            var count = Math.Max(1, mission.ExtractorCount);
            for (var i = 0; i < count; i++)
            {
                var angle = Angle.FromDegrees(360f * i / count + _random.NextFloat(0f, 360f / count));
                var distance = baseRadius + Math.Max(40f, mission.SpawnDistance) + _random.NextFloat(0f, 20f);
                var coordinates = new EntityCoordinates(map, center + angle.ToWorldVec() * distance);
                var extractor = Spawn(mission.ExtractorPrototype, coordinates);
                var extractorTransform = Transform(extractor);
                if (!extractorTransform.Anchored)
                    _transform.AnchorEntity(extractor, extractorTransform);
            }
        }
    }

    public bool HasExtractorsOnMap(EntityUid map)
    {
        var query = EntityQueryEnumerator<LavalandMorkiteExtractorComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapUid == map)
                return true;
        }
        return false;
    }

    private bool TryFindLavalandOutpost(out EntityUid map, out EntityUid outpost)
    {
        var query = EntityQueryEnumerator<LavalandOutpostComponent>();
        while (query.MoveNext(out var uid, out var marker))
        {
            if (!Exists(marker.Map))
                continue;
            map = marker.Map;
            outpost = uid;
            return true;
        }
        map = default;
        outpost = default;
        return false;
    }
}
