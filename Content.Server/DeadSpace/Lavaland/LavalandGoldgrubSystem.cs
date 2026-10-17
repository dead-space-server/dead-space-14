using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server.Stack;
using Content.Shared.Chasm;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Content.Shared.Damage.Systems;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandGoldgrubSystem : EntitySystem
{
    private static readonly Vector2i[] CardinalOffsets =
    [
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
    ];

    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;

    private readonly List<(MapCoordinates Coordinates, EntityUid Victim, EntProtoId Prototype, TimeSpan At)> _pendingAmbushes = new();
    private readonly Dictionary<EntityUid, TimeSpan> _ambushCooldown = new();

    private EntityQuery<GhostComponent> _ghostQuery;

    public override void Initialize()
    {
        base.Initialize();

        _ghostQuery = GetEntityQuery<GhostComponent>();

        SubscribeLocalEvent<LavalandGoldgrubComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<LavalandGoldgrubComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<LavalandGoldgrubComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<LavalandGoldgrubComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<LavalandGoldgrubAmbushTestComponent, MapInitEvent>(OnAmbushTestMapInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        ProcessPendingAmbushes(curTime);
        ProcessAmbushes(curTime);

        var query = EntityQueryEnumerator<LavalandGoldgrubComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var goldgrub, out var xform))
        {
            if (goldgrub.NextUpdate > curTime)
                continue;

            // Jumping at its victim: the ambush drives it, not the usual brain.
            if (goldgrub.AmbushVictim != null && !goldgrub.AmbushDone)
                continue;

            goldgrub.NextUpdate = curTime + goldgrub.UpdateInterval;

            if (IsDead(uid))
            {
                _steering.Unregister(uid);
                RemoveActiveNpc(uid);
                continue;
            }

            if (goldgrub.BurrowAt == null && !HasNearbyPlayer(xform, goldgrub.ActivationRange))
            {
                _steering.Unregister(uid);
                RemoveActiveNpc(uid);
                goldgrub.FleeStartedAt = null;
                goldgrub.NextUpdate = curTime + goldgrub.InactiveUpdateInterval;
                continue;
            }

            EnsureComp<ActiveNPCComponent>(uid);

            if (goldgrub.BurrowAt is { } burrowAt && burrowAt <= curTime)
            {
                BurrowAway(uid, goldgrub);
                continue;
            }

            // After the robbery it just runs from the miner until it burrows.
            if (goldgrub.AmbushDone && goldgrub.AmbushVictim is { } robbed && !TerminatingOrDeleted(robbed))
            {
                MoveAwayFrom(uid, xform, robbed, goldgrub);
                continue;
            }

            var hasThreat = TryFindNearestThreat(uid, xform, goldgrub, out var threat, out var threatDistance);
            var canEatOre = GetStoredOreCount(goldgrub) < goldgrub.MaxStoredOre;
            Entity<StackComponent> ore = default;
            var oreDistance = 0f;
            var hasOre = canEatOre && TryFindNearestOre(uid, xform, goldgrub, out ore, out oreDistance);

            if (hasThreat && threatDistance <= goldgrub.FleeRange)
            {
                if (hasOre &&
                    oreDistance <= goldgrub.GreedyOreRange &&
                    threatDistance > goldgrub.BurrowStartRange &&
                    goldgrub.BurrowAt == null)
                {
                    TryEatOrMoveToOre(uid, goldgrub, ore, oreDistance);
                    continue;
                }

                goldgrub.FleeStartedAt ??= curTime;

                if (ShouldStartBurrowing(goldgrub, threatDistance, curTime))
                    StartBurrow(uid, goldgrub, curTime);

                MoveAwayFrom(uid, xform, threat, goldgrub);
                continue;
            }

            goldgrub.FleeStartedAt = null;

            if (!canEatOre)
            {
                Wander(uid, xform, goldgrub, curTime);
                continue;
            }

            if (!hasOre)
            {
                Wander(uid, xform, goldgrub, curTime);
                continue;
            }

            TryEatOrMoveToOre(uid, goldgrub, ore, oreDistance);
        }
    }

    #region Ambush

    /// <summary>
    /// A miner broke a rock: maybe a goldgrub digs out of it a moment later and jumps at them.
    /// Called by <see cref="LavalandMiningToolSystem"/>.
    /// </summary>
    public void TryAmbush(EntityUid rock, EntityUid miner)
    {
        if (!HasComp<ActorComponent>(miner) ||
            Transform(miner).MapUid is not { } map ||
            !TryComp<LavalandMapComponent>(map, out var lavaland) ||
            lavaland.Planet.Id == null ||
            !_prototype.TryIndex(lavaland.Planet, out var planet))
        {
            return;
        }

        var config = planet.GoldgrubAmbush;
        var now = _timing.CurTime;
        if (!config.Enabled ||
            _ambushCooldown.TryGetValue(miner, out var until) && now < until ||
            !_random.Prob(config.Chance))
        {
            return;
        }

        if (config.RequireOre &&
            _prototype.TryIndex(config.Prototype, out var grubPrototype) &&
            grubPrototype.TryGetComponent<LavalandGoldgrubComponent>(out var grubComp, _factory) &&
            FindAmbushOre(miner, grubComp.AmbushOre).Count == 0)
        {
            return;
        }

        _ambushCooldown[miner] = now + config.PlayerCooldown;
        // The rock is gone a moment later: dig out where it stood.
        _pendingAmbushes.Add((_transform.GetMapCoordinates(rock), miner, config.Prototype, now + TimeSpan.FromSeconds(0.15)));
    }

    private void OnAmbushTestMapInit(Entity<LavalandGoldgrubAmbushTestComponent> ent, ref MapInitEvent args)
    {
        var xform = Transform(ent);
        var position = _transform.GetWorldPosition(xform);
        EntityUid? victim = null;
        var bestDistance = ent.Comp.Range;
        var players = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        while (players.MoveNext(out var player, out _, out var mobState, out var playerXform))
        {
            if (playerXform.MapID != xform.MapID || _ghostQuery.HasComp(player) || _mobState.IsDead(player, mobState))
                continue;

            var distance = (_transform.GetWorldPosition(playerXform) - position).Length();
            if (distance > bestDistance)
                continue;

            bestDistance = distance;
            victim = player;
        }

        if (victim != null)
            _pendingAmbushes.Add((_transform.GetMapCoordinates(ent, xform), victim.Value, ent.Comp.Prototype, _timing.CurTime));

        QueueDel(ent);
    }

    private void ProcessPendingAmbushes(TimeSpan now)
    {
        for (var i = _pendingAmbushes.Count - 1; i >= 0; i--)
        {
            var (coordinates, victim, prototype, at) = _pendingAmbushes[i];
            if (at > now)
                continue;

            _pendingAmbushes.RemoveAt(i);
            if (TerminatingOrDeleted(victim))
                continue;

            var grub = Spawn(prototype, coordinates);
            var comp = EnsureComp<LavalandGoldgrubComponent>(grub);
            comp.AmbushVictim = victim;
            comp.AmbushLeapAt = now + comp.AmbushLeapDelay;
            _steering.Unregister(grub);
            _popup.PopupEntity(Loc.GetString("lavaland-goldgrub-ambush-emerge"), grub, PopupType.LargeCaution);
        }
    }

    private void ProcessAmbushes(TimeSpan now)
    {
        var query = EntityQueryEnumerator<LavalandGoldgrubComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var grub, out var xform))
        {
            if (grub.AmbushDone || grub.AmbushVictim is not { } victim)
                continue;

            if (TerminatingOrDeleted(victim) || IsDead(uid))
            {
                grub.AmbushDone = true;
                continue;
            }

            if (grub.AmbushLeapAt is { } leapAt && now >= leapAt)
            {
                grub.AmbushLeapAt = null;
                grub.AmbushHitAt = now + grub.AmbushHitDelay;
                var toVictim = _transform.GetWorldPosition(victim) - _transform.GetWorldPosition(xform);
                _throwing.TryThrow(uid, toVictim, grub.AmbushLeapSpeed, playSound: false, doSpin: false);
                continue;
            }

            if (grub.AmbushHitAt is not { } hitAt || now < hitAt)
                continue;

            grub.AmbushHitAt = null;
            grub.AmbushDone = true;
            var distance = (_transform.GetWorldPosition(victim) - _transform.GetWorldPosition(xform)).Length();
            if (distance <= grub.AmbushHitRange)
                Rob(grub, victim);
            else
                _popup.PopupEntity(Loc.GetString("lavaland-goldgrub-ambush-miss"), uid, PopupType.Medium);

            StartBurrow(uid, grub, now);
            EnsureComp<ActiveNPCComponent>(uid);
        }
    }

    /// <summary>Knocks the miner down and eats a few whole stacks of their ore.</summary>
    private void Rob(LavalandGoldgrubComponent component, EntityUid victim)
    {
        _stun.TryUpdateParalyzeDuration(victim, component.AmbushStun);

        var stacks = FindAmbushOre(victim, component.AmbushOre);
        _random.Shuffle(stacks);
        var count = Math.Min(stacks.Count,
            _random.Next(Math.Max(0, component.AmbushStacksMin), Math.Max(component.AmbushStacksMin, component.AmbushStacksMax) + 1));

        for (var i = 0; i < count; i++)
        {
            var stack = stacks[i];
            // Stolen ore goes into its gut past the usual limit: kill it to get it back.
            component.StoredOre[stack.Comp.StackTypeId] =
                component.StoredOre.GetValueOrDefault(stack.Comp.StackTypeId) + stack.Comp.Count;
            QueueDel(stack);
        }

        _popup.PopupEntity(Loc.GetString(count > 0 ? "lavaland-goldgrub-ambush-steal" : "lavaland-goldgrub-ambush-hit"),
            victim, victim, PopupType.LargeCaution);
    }

    /// <summary>Stacks of the given ore anywhere on the miner: hands, pockets, bags, the ore bag.</summary>
    private List<Entity<StackComponent>> FindAmbushOre(EntityUid owner, HashSet<ProtoId<StackPrototype>> ore)
    {
        var result = new List<Entity<StackComponent>>();
        CollectOre(owner, ore, result, 4);
        return result;
    }

    private void CollectOre(EntityUid uid, HashSet<ProtoId<StackPrototype>> ore, List<Entity<StackComponent>> result, int depth)
    {
        if (depth <= 0 || !TryComp<ContainerManagerComponent>(uid, out var manager))
            return;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (TryComp<StackComponent>(contained, out var stack) && ore.Contains(stack.StackTypeId) && stack.Count > 0)
                    result.Add((contained, stack));
                else
                    CollectOre(contained, ore, result, depth - 1);
            }
        }
    }

    #endregion

    private void OnMapInit(EntityUid uid, LavalandGoldgrubComponent component, MapInitEvent args)
    {
        if (component.InitialOre.Count == 0)
            return;

        var initialOre = _random.Next(1, 4);
        for (var i = 0; i < initialOre; i++)
        {
            AddStoredOre(component, _random.Pick(component.InitialOre), 1);
        }
    }

    private void OnDamageChanged(EntityUid uid, LavalandGoldgrubComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || IsDead(uid))
            return;

        StartBurrow(uid, component, _timing.CurTime);
    }

    private void OnMobStateChanged(EntityUid uid, LavalandGoldgrubComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        DropStoredOre(uid, component);
        _steering.Unregister(uid);
        RemoveActiveNpc(uid);
    }

    private void OnShutdown(EntityUid uid, LavalandGoldgrubComponent component, ComponentShutdown args)
    {
        _steering.Unregister(uid);
        RemoveActiveNpc(uid);
    }

    private bool TryFindNearestThreat(
        EntityUid uid,
        TransformComponent xform,
        LavalandGoldgrubComponent component,
        out EntityUid threat,
        out float nearestDistance)
    {
        threat = default;
        var found = false;
        nearestDistance = float.MaxValue;
        var coordinates = _transform.GetMapCoordinates(uid, xform);

        foreach (var (candidate, _) in _lookup.GetEntitiesInRange<HumanoidAppearanceComponent>(coordinates, component.ThreatRange))
        {
            if (candidate == uid ||
                TerminatingOrDeleted(candidate) ||
                !TryComp<MobStateComponent>(candidate, out var mobState) ||
                _mobState.IsDead(candidate, mobState) ||
                !xform.Coordinates.TryDistance(EntityManager, Transform(candidate).Coordinates, out var distance) ||
                distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = distance;
            threat = candidate;
            found = true;
        }

        return found;
    }

    private bool TryFindNearestOre(
        EntityUid uid,
        TransformComponent xform,
        LavalandGoldgrubComponent component,
        out Entity<StackComponent> ore,
        out float distance)
    {
        ore = default;
        var found = false;
        distance = float.MaxValue;
        var coordinates = _transform.GetMapCoordinates(uid, xform);

        foreach (var candidate in _lookup.GetEntitiesInRange<StackComponent>(coordinates, component.OreSearchRange))
        {
            if (candidate.Owner == uid ||
                TerminatingOrDeleted(candidate.Owner) ||
                candidate.Comp.Unlimited ||
                !component.EdibleOre.Contains(candidate.Comp.StackTypeId) ||
                candidate.Comp.Count <= 0 ||
                !xform.Coordinates.TryDistance(EntityManager, Transform(candidate.Owner).Coordinates, out var candidateDistance) ||
                candidateDistance >= distance)
            {
                continue;
            }

            ore = candidate;
            distance = candidateDistance;
            found = true;
        }

        return found;
    }

    private void TryEatOrMoveToOre(
        EntityUid uid,
        LavalandGoldgrubComponent component,
        Entity<StackComponent> ore,
        float distance)
    {
        if (distance <= component.EatRange)
        {
            EatOre(uid, component, ore);
            return;
        }

        MoveTowards(uid, ore.Owner, component.EatRange);
    }

    private void EatOre(EntityUid uid, LavalandGoldgrubComponent component, Entity<StackComponent> ore)
    {
        var availableSpace = component.MaxStoredOre - GetStoredOreCount(component);
        if (availableSpace <= 0)
            return;

        var amount = Math.Min(ore.Comp.Count, availableSpace);
        if (amount <= 0)
            return;

        AddStoredOre(component, ore.Comp.StackTypeId, amount);
        _stack.ReduceCount(ore.AsNullable(), amount);

        if (GetStoredOreCount(component) >= component.MaxStoredOre)
            _steering.Unregister(uid);
    }

    private void MoveTowards(EntityUid uid, EntityUid target, float range)
    {
        if (TerminatingOrDeleted(target))
            return;

        StartSteering(uid, new EntityCoordinates(target, Vector2.Zero), range);
    }

    private void MoveAwayFrom(
        EntityUid uid,
        TransformComponent xform,
        EntityUid threat,
        LavalandGoldgrubComponent component)
    {
        var ourCoordinates = _transform.GetMapCoordinates(uid, xform);
        var threatCoordinates = _transform.GetMapCoordinates(threat);
        var direction = ourCoordinates.Position - threatCoordinates.Position;

        if (direction.LengthSquared() < 0.01f)
            direction = _random.NextAngle().ToVec();
        else
            direction = Vector2.Normalize(direction);

        StartSteering(uid, xform.Coordinates.Offset(direction * component.FleeTargetDistance), 0.75f);
    }

    private void Wander(EntityUid uid, TransformComponent xform, LavalandGoldgrubComponent component, TimeSpan curTime)
    {
        if (component.NextWanderAt > curTime &&
            TryComp<NPCSteeringComponent>(uid, out var currentSteering) &&
            currentSteering.Status == SteeringStatus.Moving)
        {
            return;
        }

        if (!TryPickWanderTarget(xform, component, out var target))
        {
            component.NextWanderAt = curTime + component.WanderRetryInterval;
            _steering.Unregister(uid);
            return;
        }

        component.NextWanderAt = curTime + component.WanderInterval;
        StartSteering(uid, target, 0.75f);
    }

    private bool TryPickWanderTarget(
        TransformComponent xform,
        LavalandGoldgrubComponent component,
        out EntityCoordinates target)
    {
        target = default;

        if (xform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return false;
        }

        var distanceMin = MathF.Max(0.5f, MathF.Min(component.WanderDistanceMin, component.WanderDistanceMax));
        var distanceMax = MathF.Max(distanceMin, component.WanderDistanceMax);
        var origin = _map.LocalToTile(gridUid, grid, xform.Coordinates);
        var attempts = Math.Max(1, component.WanderTargetAttempts);

        for (var i = 0; i < attempts; i++)
        {
            var offset = _random.NextAngle().ToVec() * _random.NextFloat(distanceMin, distanceMax);
            var tile = _map.LocalToTile(gridUid, grid, xform.Coordinates.Offset(offset));
            if (tile == origin || !CanWanderTo(gridUid, grid, tile))
                continue;

            target = _map.GridTileToLocal(gridUid, grid, tile);
            return true;
        }

        var offsetStart = _random.Next(CardinalOffsets.Length);
        for (var i = 0; i < CardinalOffsets.Length; i++)
        {
            var tile = origin + CardinalOffsets[(offsetStart + i) % CardinalOffsets.Length];
            if (!CanWanderTo(gridUid, grid, tile))
                continue;

            target = _map.GridTileToLocal(gridUid, grid, tile);
            return true;
        }

        return false;
    }

    private bool CanWanderTo(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) ||
            tileRef.Tile.IsEmpty ||
            _turf.IsSpace(tileRef) ||
            _turf.IsTileBlocked(tileRef, CollisionGroup.MobMask))
        {
            return false;
        }

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (uid != null && !TerminatingOrDeleted(uid.Value) && HasComp<ChasmComponent>(uid.Value))
                return false;
        }

        return true;
    }

    private void StartSteering(EntityUid uid, EntityCoordinates target, float range)
    {
        if (TryComp<NPCSteeringComponent>(uid, out var existing) &&
            existing.Status == SteeringStatus.NoPath)
        {
            _steering.Unregister(uid, existing);
        }

        var steering = _steering.Register(uid, target);
        steering.Range = range;
        steering.Status = SteeringStatus.Moving;
        steering.FailedPathCount = 0;
    }

    private bool ShouldStartBurrowing(LavalandGoldgrubComponent component, float threatDistance, TimeSpan curTime)
    {
        if (component.BurrowAt != null || threatDistance > component.BurrowStartRange)
            return false;

        return component.FleeStartedAt is { } fleeStartedAt &&
               curTime - fleeStartedAt >= component.MinFleeTimeBeforeBurrow;
    }

    private void StartBurrow(EntityUid uid, LavalandGoldgrubComponent component, TimeSpan curTime)
    {
        if (component.BurrowAt != null)
            return;

        component.BurrowAt = curTime + component.BurrowDelay;
        _popup.PopupEntity(Loc.GetString("lavaland-goldgrub-burrow-start"), uid, PopupType.MediumCaution);
    }

    private void BurrowAway(EntityUid uid, LavalandGoldgrubComponent component)
    {
        component.StoredOre.Clear();
        component.DroppedOre = true;
        _popup.PopupEntity(Loc.GetString("lavaland-goldgrub-burrow-away"), uid, PopupType.Medium);
        QueueDel(uid);
    }

    private void DropStoredOre(EntityUid uid, LavalandGoldgrubComponent component)
    {
        if (component.DroppedOre)
            return;

        component.DroppedOre = true;

        var coordinates = Transform(uid).Coordinates;
        foreach (var (stackId, amount) in component.StoredOre)
        {
            if (amount <= 0 || !_prototype.HasIndex<StackPrototype>(stackId))
                continue;

            _stack.SpawnMultipleAtPosition(stackId, amount, coordinates);
        }

        component.StoredOre.Clear();
    }

    private bool AddStoredOre(LavalandGoldgrubComponent component, ProtoId<StackPrototype> stackId, int amount)
    {
        var availableSpace = component.MaxStoredOre - GetStoredOreCount(component);
        if (amount <= 0 || availableSpace <= 0)
            return false;

        var added = Math.Min(amount, availableSpace);
        component.StoredOre[stackId] = component.StoredOre.GetValueOrDefault(stackId) + added;
        return true;
    }

    private int GetStoredOreCount(LavalandGoldgrubComponent component)
    {
        var count = 0;
        foreach (var amount in component.StoredOre.Values)
        {
            count += amount;
        }

        return count;
    }

    private bool IsDead(EntityUid uid)
    {
        return TryComp<MobStateComponent>(uid, out var mobState) && _mobState.IsDead(uid, mobState);
    }

    private bool HasNearbyPlayer(TransformComponent xform, float range)
    {
        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var playerXform))
        {
            if (_ghostQuery.HasComp(uid))
                continue;

            if (xform.Coordinates.TryDistance(EntityManager, playerXform.Coordinates, out var distance) &&
                distance <= range)
            {
                return true;
            }
        }

        return false;
    }

    private void RemoveActiveNpc(EntityUid uid)
    {
        if (HasComp<ActiveNPCComponent>(uid))
            RemComp<ActiveNPCComponent>(uid);
    }
}
