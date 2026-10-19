using System.Linq;
using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Random.Helpers;
using Content.Shared.Throwing;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandBubblegumSystem : EntitySystem
{
    private static readonly Vector2i[] Cardinals =
    {
        new(0, 1),
        new(1, 0),
        new(0, -1),
        new(-1, 0),
    };

    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    private readonly List<Vector2i> _deadlyHandTiles = new();
    private readonly HashSet<Entity<MobStateComponent>> _deadlyHandVictims = new();

    private readonly List<EntityUid> _participants = new();
    private readonly List<EntityUid> _bloodTargets = new();
    private readonly List<Vector2i> _poolTiles = new();
    private readonly List<Vector2i> _cloneTiles = new();
    private readonly HashSet<EntityUid> _tileEntities = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandBubblegumComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<LavalandBubblegumComponent, LavalandBossFightStartedEvent>(OnBossFightStarted);
        SubscribeLocalEvent<LavalandBubblegumComponent, LavalandBossResetEvent>(OnBossReset);
        SubscribeLocalEvent<LavalandBubblegumComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<LavalandBubblegumComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        ProcessDeadlyHands(frameTime);
        var query = EntityQueryEnumerator<LavalandBubblegumComponent, LavalandBossComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var bubblegum, out var boss, out var xform))
        {
            if (boss.Arena is not { Valid: true } arenaUid ||
                !TryComp<LavalandBossArenaComponent>(arenaUid, out var arena) ||
                arena.Ended ||
                !arena.FightStarted ||
                xform.GridUid != arena.Grid ||
                !TryComp<MapGridComponent>(arena.Grid, out var grid) ||
                IsDead(uid))
            {
                ClearRuntimeState(uid, bubblegum, false);
                continue;
            }

            PruneTracked(bubblegum);
            ProcessActiveClones(bubblegum, now);
            ProcessCloneCharges(uid, bubblegum, arena, arena.Grid, grid, now);
            ProcessHallucinationWaves(uid, bubblegum, arena, arena.Grid, grid, now);
            ProcessPendingBloodTiles(bubblegum, arena, arena.Grid, grid, now);
            ProcessPendingHandAttacks(uid, bubblegum, arena, arena.Grid, grid, now);

            var participantCount = CollectParticipants(arena);
            if (participantCount == 0)
            {
                ClearRuntimeState(uid, bubblegum, true);
                bubblegum.BusyUntil = TimeSpan.Zero;
                continue;
            }

            if (GetHealthFraction(uid) <= Math.Clamp(bubblegum.CloneCriticalHealthThreshold, 0f, 1f))
                TrySpawnCriticalClones(uid, bubblegum, arena, arena.Grid, grid, now);

            ProcessKiting(uid, bubblegum, arena, arena.Grid, grid, frameTime);

            if (ProcessCharge(uid, bubblegum, arena, arena.Grid, grid, now) ||
                ProcessQueuedCharge(uid, bubblegum, arena, arena.Grid, grid, now))
            {
                continue;
            }

            var bloodReactionWindow = GetBloodReactionWindow(bubblegum);
            if (bubblegum.BusyUntil <= now &&
                bubblegum.NextAttack - now > bloodReactionWindow &&
                bubblegum.NextBloodReaction <= now &&
                TryQueueBloodAttack(uid, bubblegum, arena, arena.Grid, grid, now))
            {
                bubblegum.LastPressureAt = now;
                bubblegum.LastAttackKind = "blood-reaction";
                bubblegum.NextBloodReaction = now + bubblegum.BloodReactionCooldown;
                continue;
            }

            if (bubblegum.BusyUntil > now ||
                bubblegum.NextAttack > now)
            {
                continue;
            }

            var target = PickTarget(bubblegum, uid, arena.Grid, grid, now);
            if (target == null)
                continue;

            RunAttack(uid, bubblegum, arena, arena.Grid, grid, target.Value, now);
        }
    }

    private void OnDamageChanged(Entity<LavalandBubblegumComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased ||
            args.DamageDelta == null ||
            args.DamageDelta.GetTotal() <= 0 ||
            !_random.Prob(0.25f) ||
            !TryComp<LavalandBossComponent>(ent.Owner, out var boss) ||
            boss.Arena is not { Valid: true } arenaUid ||
            !TryComp<LavalandBossArenaComponent>(arenaUid, out var arena) ||
            !TryComp<MapGridComponent>(arena.Grid, out var grid))
        {
            return;
        }

        var tile = GetEntityTile(ent.Owner, arena.Grid, grid);
        if (tile == null)
            return;

        if (_random.Prob(0.4f))
            tile += _random.Pick(Cardinals);

        if (IsInsideInnerArena(arena, tile.Value))
        {
            TrySpawnBloodPool(ent.Comp, arena, arena.Grid, grid, tile.Value);
            SpawnAnchored(ent.Comp.BloodGibsPrototype, arena.Grid, grid, tile.Value);
        }
    }

    private void OnBossReset(EntityUid uid, LavalandBubblegumComponent component, LavalandBossResetEvent args)
    {
        PrepareFight(uid, component);
    }

    private void OnBossFightStarted(EntityUid uid, LavalandBubblegumComponent component, LavalandBossFightStartedEvent args)
    {
        PrepareFight(uid, component);
    }

    private void PrepareFight(EntityUid uid, LavalandBubblegumComponent component)
    {
        ClearRuntimeState(uid, component, true);
        var now = _timing.CurTime;
        component.NextAttack = now + TimeSpan.FromSeconds(1);
        component.NextSummon = now + TimeSpan.FromSeconds(3);
        component.NextBloodReaction = now + TimeSpan.FromSeconds(1.5);
        component.LastPressureAt = now;
        component.LastAttackKind = string.Empty;
        component.NextCriticalCloneSpawn = now + TimeSpan.FromSeconds(3);
    }

    private void OnMobStateChanged(Entity<LavalandBubblegumComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            ClearRuntimeState(ent.Owner, ent.Comp, false);
    }

    private void OnRefreshMovementSpeed(EntityUid uid, LavalandBubblegumComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (component.Charging)
        {
            args.ModifySpeed(0f, 0f);
            return;
        }

        var rage = CalculateRage(uid);
        var modifier = Math.Clamp(1f + rage * 0.025f, 1f, 1.5f);
        args.ModifySpeed(modifier, modifier);
    }

    private void RunAttack(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        EntityUid target,
        TimeSpan now)
    {
        var rage = CalculateRage(boss);
        var healthFraction = GetHealthFraction(boss);
        var belowHalf = healthFraction <= 0.5f;
        var targetTile = GetEntityTile(target, gridUid, grid);
        var bossTile = GetEntityTile(boss, gridUid, grid);
        if (targetTile == null || bossTile == null)
        {
            bubblegum.NextAttack = now + TimeSpan.FromSeconds(0.5);
            return;
        }

        var targetDistance = ChebyshevDistance(bossTile.Value, targetTile.Value);
        if (ShouldPrioritizeMovement(bubblegum, boss, targetDistance, now))
        {
            RunMovementCombo(boss, bubblegum, arena, gridUid, grid, target, rage, healthFraction, now);
            return;
        }

        var forcePressure = NeedsPressure(bubblegum, now);
        var pressureTarget = PickPressureTarget(bubblegum, target, gridUid, grid, now) ?? target;
        var pressureTargetTile = GetEntityTile(pressureTarget, gridUid, grid) ?? targetTile.Value;
        var pressureDistance = ChebyshevDistance(bossTile.Value, pressureTargetTile);
        var pressureTargetHasBlood = HasBloodPoolWithin(bubblegum, gridUid, pressureTargetTile, 1);
        var pressureStale = IsPressureStale(bubblegum, pressureTarget, now, TimeSpan.FromSeconds(bubblegum.TargetPressureMemory.TotalSeconds * 0.45));
        var forcedAmbush = false;

        if ((forcePressure || !IsRecentPressureAttack(bubblegum)) &&
            (forcePressure || !pressureTargetHasBlood && (pressureDistance > 3 || belowHalf || pressureStale)))
        {
            forcedAmbush = QueueBloodPressureAtTarget(boss, bubblegum, arena, gridUid, grid, pressureTargetTile, now, forcePressure);
            if (forcedAmbush)
            {
                MarkPressure(bubblegum, now, forcePressure ? "forced-blood-pressure" : "blood-pressure", pressureTarget);
                bubblegum.BusyUntil = now + bubblegum.BloodSmackDelay + bubblegum.BloodHandRecover;
                bubblegum.NextAttack = now + GetScaledCooldown(bubblegum.RangedCooldown, rage);
                bubblegum.NextBloodReaction = now + bubblegum.BloodReactionCooldown;
                return;
            }
        }

        var canUseBloodHand = !IsRecentBloodHandAttack(bubblegum) || _random.Prob(belowHalf ? 0.45f : 0.25f);
        var didBloodAttack = !forcedAmbush &&
                             canUseBloodHand &&
                             TryQueueBloodAttack(boss, bubblegum, arena, gridUid, grid, now);
        if (didBloodAttack)
        {
            MarkPressure(bubblegum, now, "blood-hand", target);
            bubblegum.NextAttack = now + GetScaledCooldown(bubblegum.RangedCooldown, rage);
            bubblegum.NextBloodReaction = now + bubblegum.BloodReactionCooldown;
            return;
        }

        var warped = false;
        if (!didBloodAttack)
        {
            var sprayTarget = belowHalf && _participants.Count > 1 && _random.Prob(0.35f)
                ? PickSecondaryTarget(bubblegum, target, gridUid, grid, now) ?? target
                : target;
            QueueBloodSpray(boss, bubblegum, arena, gridUid, grid, sprayTarget, rage, now);
            warped = TryBloodWarp(boss, bubblegum, arena, gridUid, grid, target);
            if (warped)
                MarkPressure(bubblegum, now, "blood-warp", target);
        }

        var shouldSummon = forcePressure || !_random.Prob(Math.Clamp((88f - rage) / 100f, 0f, 1f));
        if (shouldSummon &&
            TrySummonSlaughterlings(boss, bubblegum, arena, gridUid, grid, now, out var summonedFullWave) &&
            summonedFullWave)
        {
            MarkPressure(bubblegum, now, "summon", target);
            bubblegum.BusyUntil = now + TimeSpan.FromSeconds(0.6);
            bubblegum.NextAttack = now + GetScaledCooldown(bubblegum.RangedCooldown, rage);
            return;
        }

        if (TryHallucinationAttack(boss, bubblegum, arena, gridUid, grid, target, belowHalf, now))
        {
            MarkPressure(bubblegum, now, "hallucination", target);
            bubblegum.NextAttack = now + GetScaledCooldown(bubblegum.RangedCooldown, rage);
            return;
        }

        if (belowHalf)
        {
            var tripleChargeChance = healthFraction <= 0.25f ? 0.9f : 0.78f;
            if (_random.Prob(tripleChargeChance) || warped)
                StartCharge(boss, bubblegum, arena, gridUid, grid, target, bubblegum.TripleChargeSteps, 2, now);
            else
            {
                TryBloodWarp(boss, bubblegum, arena, gridUid, grid, target);
                StartCharge(boss, bubblegum, arena, gridUid, grid, target, bubblegum.ChargeMaxSteps, 0, now);
            }
        }
        else
        {
            StartCharge(boss, bubblegum, arena, gridUid, grid, target, bubblegum.ChargeMaxSteps, 0, now);
        }

        MarkPressure(bubblegum, now, belowHalf ? "triple-charge" : "charge", target);
        bubblegum.NextAttack = now + GetScaledCooldown(bubblegum.RangedCooldown, rage);
    }

    private void RunMovementCombo(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        EntityUid target,
        float rage,
        float healthFraction,
        TimeSpan now)
    {
        QueueBloodSpray(boss, bubblegum, arena, gridUid, grid, target, rage, now);

        var warped = false;
        if (healthFraction <= 0.5f || _random.Prob(0.35f))
            warped = TryBloodWarp(boss, bubblegum, arena, gridUid, grid, target);

        var belowHalf = healthFraction <= 0.5f;
        if (belowHalf)
        {
            var extraCharges = healthFraction <= 0.25f ? 2 : 1;
            StartCharge(boss, bubblegum, arena, gridUid, grid, target, bubblegum.TripleChargeSteps, extraCharges, now);
        }
        else
        {
            StartCharge(boss, bubblegum, arena, gridUid, grid, target, bubblegum.ChargeMaxSteps, 0, now);
        }

        MarkPressure(bubblegum, now, warped ? "movement-warp-charge" : "movement-charge", target);
        bubblegum.NextAttack = now + GetScaledCooldown(bubblegum.RangedCooldown, rage);
    }

    private bool TryQueueBloodAttack(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        _bloodTargets.Clear();
        foreach (var participant in _participants)
        {
            var tile = GetEntityTile(participant, gridUid, grid);
            if (tile != null && HasBloodPoolWithin(bubblegum, gridUid, tile.Value, 1))
                _bloodTargets.Add(participant);
        }

        if (_bloodTargets.Count == 0)
            return false;

        var attacks = Math.Min(GetBloodHandAttackLimit(boss), _bloodTargets.Count);
        var rightHand = _random.Prob(0.5f);
        var latestAttack = now;
        for (var i = 0; i < attacks; i++)
        {
            var target = _random.PickAndTake(_bloodTargets);
            var tile = GetEntityTile(target, gridUid, grid);
            if (tile == null)
                continue;

            var grabChance = IsBelowHalfHealth(boss)
                ? bubblegum.BloodGrabChanceBelowHalf
                : bubblegum.BloodGrabChance;
            var grab = (TryComp<MobStateComponent>(target, out var targetMobState) &&
                targetMobState.CurrentState != MobState.Alive) || _random.Prob(Math.Clamp(grabChance, 0f, 1f));
            QueueHandAttack(bubblegum, gridUid, grid, tile.Value, now, grab, rightHand);
            QueueCloneHandAttacks(boss, bubblegum, arena, gridUid, grid, tile.Value, now, grab);
            MarkTargetPressure(bubblegum, target, now);
            latestAttack = now + (grab ? bubblegum.BloodGrabDelay : bubblegum.BloodSmackDelay);
            rightHand = !rightHand;
        }

        bubblegum.BusyUntil = latestAttack + TimeSpan.FromSeconds(0.25);
        TrySpawnDeadlyHand(bubblegum, gridUid, grid);
        return true;
    }

    /// <summary>
    /// One of the hands is redder than the rest: it lingers on a blood pool next to a player,
    /// and whoever stands on it for a moment dies.
    /// </summary>
    private void TrySpawnDeadlyHand(LavalandBubblegumComponent bubblegum, EntityUid gridUid, MapGridComponent grid)
    {
        if (_participants.Count == 0 || !_random.Prob(bubblegum.DeadlyHandChance))
            return;

        var victim = _random.Pick(_participants);
        if (GetEntityTile(victim, gridUid, grid) is not { } center)
            return;

        _deadlyHandTiles.Clear();
        GetPoolsAround(bubblegum, gridUid, center, 2, _deadlyHandTiles);
        _deadlyHandTiles.Remove(center);
        if (_deadlyHandTiles.Count == 0)
            return;

        SpawnAnchored(bubblegum.DeadlyHandPrototype, gridUid, grid, _random.Pick(_deadlyHandTiles));
    }

    private void ProcessDeadlyHands(float frameTime)
    {
        var hands = EntityQueryEnumerator<LavalandBubblegumDeadlyHandComponent, TransformComponent>();
        while (hands.MoveNext(out var hand, out var deadly, out var xform))
        {
            var position = _transform.GetWorldPosition(xform);
            _deadlyHandVictims.Clear();
            _lookup.GetEntitiesInRange(xform.Coordinates, 0.45f, _deadlyHandVictims, LookupFlags.Dynamic);

            foreach (var standing in deadly.StandTime.Keys.ToList())
            {
                if (!_deadlyHandVictims.Any(v => v.Owner == standing))
                    deadly.StandTime.Remove(standing);
            }

            foreach (var victim in _deadlyHandVictims)
            {
                if (victim.Comp.CurrentState != MobState.Alive || HasComp<LavalandBubblegumComponent>(victim) ||
                    !_players.TryGetSessionByEntity(victim, out _))
                {
                    continue;
                }

                var stood = deadly.StandTime.GetValueOrDefault(victim) + frameTime;
                deadly.StandTime[victim] = stood;
                if (stood < deadly.KillTime)
                    continue;

                _damageable.TryChangeDamage(victim.Owner, deadly.Damage, true, origin: hand);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/gib1.ogg"), victim);
                Spawn("LavalandBubblegumBloodGibs", new Robust.Shared.Map.MapCoordinates(position, xform.MapID));
                QueueDel(hand);
                break;
            }
        }
    }

    private void QueueHandAttack(
        LavalandBubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        TimeSpan now,
        bool grab,
        bool rightHand)
    {
        if (grab)
        {
            SpawnAnchored(rightHand ? bubblegum.RightPawPrototype : bubblegum.LeftPawPrototype, gridUid, grid, tile);
            SpawnAnchored(rightHand ? bubblegum.RightThumbPrototype : bubblegum.LeftThumbPrototype, gridUid, grid, tile);
        }
        else
        {
            SpawnAnchored(rightHand ? bubblegum.RightSmackPrototype : bubblegum.LeftSmackPrototype, gridUid, grid, tile);
        }

        bubblegum.PendingHandAttacks.Add(new LavalandBubblegumPendingHandAttack
        {
            Grid = gridUid,
            Tile = tile,
            AttackAt = now + (grab ? bubblegum.BloodGrabDelay : bubblegum.BloodSmackDelay),
            Grab = grab,
            RightHand = rightHand,
        });
    }

    private void ProcessPendingHandAttacks(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        for (var i = bubblegum.PendingHandAttacks.Count - 1; i >= 0; i--)
        {
            var pending = bubblegum.PendingHandAttacks[i];
            if (pending.AttackAt > now)
                continue;

            if (pending.Grid == gridUid)
                DamageHandTile(boss, bubblegum, arena, gridUid, grid, pending.Tile, pending.Grab);

            bubblegum.PendingHandAttacks.RemoveAt(i);
        }
    }

    private void DamageHandTile(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        bool grab)
    {
        var bossTile = GetEntityTile(boss, gridUid, grid);
        var hit = false;

        _tileEntities.Clear();
        _lookup.GetLocalEntitiesIntersecting(gridUid, tile, _tileEntities, flags: LookupFlags.Dynamic | LookupFlags.Sundries, gridComp: grid);

        foreach (var uid in _tileEntities)
        {
            if (uid == boss ||
                bubblegum.Slaughterlings.Contains(uid) ||
                !TryComp(uid, out DamageableComponent? damageable) ||
                !TryComp(uid, out MobStateComponent? mobState) ||
                !TryComp(uid, out TransformComponent? xform) ||
                mobState.CurrentState == MobState.Dead ||
                xform.GridUid != gridUid ||
                _map.LocalToTile(gridUid, grid, xform.Coordinates) != tile)
            {
                continue;
            }

            _damageable.TryChangeDamage((uid, damageable), grab ? bubblegum.GrabDamage : bubblegum.SmackDamage, origin: boss);
            hit = true;

            if (!grab || bossTile == null)
                continue;

            var direction = StepTowards(bossTile.Value, tile) - bossTile.Value;
            if (direction == Vector2i.Zero)
                direction = _random.Pick(Cardinals);

            var destination = ClampToInnerArena(arena, bossTile.Value + direction);
            _audio.PlayPvs(bubblegum.EnterBloodSound, uid, AudioParams.Default.WithVolume(-3f));
            _transform.SetCoordinates(uid, _map.GridTileToLocal(gridUid, grid, destination));
            _audio.PlayPvs(bubblegum.ExitBloodSound, uid, AudioParams.Default.WithVolume(-3f));
        }

        _tileEntities.Clear();

        if (hit)
            _audio.PlayPvs(bubblegum.AttackSound, _map.GridTileToLocal(gridUid, grid, tile), AudioParams.Default.WithVolume(-2f));
    }

    private void QueueBloodSpray(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        EntityUid target,
        float rage,
        TimeSpan now)
    {
        var bossTile = GetEntityTile(boss, gridUid, grid);
        var targetTile = GetEntityTile(target, gridUid, grid);
        if (bossTile == null || targetTile == null)
            return;

        var direction = StepTowards(bossTile.Value, targetTile.Value) - bossTile.Value;
        if (direction == Vector2i.Zero)
            direction = _random.Pick(Cardinals);

        TrySpawnBloodPool(bubblegum, arena, gridUid, grid, bossTile.Value);
        var range = Math.Max(1, bubblegum.BloodSprayBaseRange + (int) MathF.Round(rage * bubblegum.BloodSprayRageRangeMultiplier));
        for (var step = 1; step <= range; step++)
        {
            if (bubblegum.PendingBloodTiles.Count >= Math.Max(0, bubblegum.MaxPendingBloodTiles))
                break;

            var tile = bossTile.Value + direction * step;
            if (!IsInsideInnerArena(arena, tile))
                break;

            if (HasBloodPoolAt(bubblegum, gridUid, tile) || HasPendingBloodTile(bubblegum, gridUid, tile))
                continue;

            bubblegum.PendingBloodTiles.Add(new LavalandBubblegumPendingBloodTile
            {
                Grid = gridUid,
                Tile = tile,
                SpawnAt = now + TimeSpan.FromSeconds(bubblegum.BloodSprayStepDelay.TotalSeconds * step),
            });
        }

        QueueCloneBloodSprays(boss, bubblegum, arena, gridUid, grid, bossTile.Value, targetTile.Value, rage, now);
        _audio.PlayPvs(bubblegum.SplatSound, boss, AudioParams.Default.WithVolume(-2f));
    }

    private void ProcessPendingBloodTiles(
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        var playedSound = false;
        for (var i = bubblegum.PendingBloodTiles.Count - 1; i >= 0; i--)
        {
            var pending = bubblegum.PendingBloodTiles[i];
            if (pending.SpawnAt > now)
                continue;

            if (pending.Grid == gridUid && IsInsideInnerArena(arena, pending.Tile))
            {
                if (!pending.Fake)
                    TrySpawnBloodPool(bubblegum, arena, gridUid, grid, pending.Tile);

                if (pending.Fake || _random.Prob(0.65f))
                    SpawnAnchored(bubblegum.BloodSplatterPrototype, gridUid, grid, pending.Tile);

                if (!playedSound)
                {
                    _audio.PlayPvs(bubblegum.SplatSound, _map.GridTileToLocal(gridUid, grid, pending.Tile), AudioParams.Default.WithVolume(-5f));
                    playedSound = true;
                }
            }

            bubblegum.PendingBloodTiles.RemoveAt(i);
        }
    }

    private bool QueueBloodPressureAtTarget(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i targetTile,
        TimeSpan now,
        bool urgent)
    {
        if (!IsInsideInnerArena(arena, targetTile))
            return false;

        var queued = TrySpawnBloodPool(bubblegum, arena, gridUid, grid, targetTile);
        var maxAdjacent = urgent ? 4 : 2;
        var addedAdjacent = 0;

        foreach (var direction in Cardinals)
        {
            if (addedAdjacent >= maxAdjacent ||
                bubblegum.PendingBloodTiles.Count >= Math.Max(0, bubblegum.MaxPendingBloodTiles))
            {
                break;
            }

            var tile = targetTile + direction;
            if (!IsInsideInnerArena(arena, tile) ||
                HasBloodPoolAt(bubblegum, gridUid, tile) ||
                HasPendingBloodTile(bubblegum, gridUid, tile))
            {
                continue;
            }

            bubblegum.PendingBloodTiles.Add(new LavalandBubblegumPendingBloodTile
            {
                Grid = gridUid,
                Tile = tile,
                SpawnAt = now + TimeSpan.FromSeconds(0.06 * (addedAdjacent + 1)),
            });
            addedAdjacent++;
            queued = true;
        }

        if (bubblegum.PendingHandAttacks.Count < 12)
        {
            var grabChance = urgent ? bubblegum.BloodGrabChanceBelowHalf : bubblegum.BloodGrabChance;
            var grab = _random.Prob(Math.Clamp(grabChance, 0f, 1f));
            QueueHandAttack(
                bubblegum,
                gridUid,
                grid,
                targetTile,
                now + (urgent ? TimeSpan.Zero : TimeSpan.FromSeconds(0.12)),
                grab,
                _random.Prob(0.5f));
            QueueCloneHandAttacks(boss, bubblegum, arena, gridUid, grid, targetTile, now, grab);
            queued = true;
        }

        if (queued)
            _audio.PlayPvs(bubblegum.SplatSound, _map.GridTileToLocal(gridUid, grid, targetTile), AudioParams.Default.WithVolume(-3f));

        return queued;
    }

    private bool TryBloodWarp(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        EntityUid target)
    {
        var bossTile = GetEntityTile(boss, gridUid, grid);
        var targetTile = GetEntityTile(target, gridUid, grid);
        if (bossTile == null ||
            targetTile == null ||
            ChebyshevDistance(bossTile.Value, targetTile.Value) <= 1)
        {
            return false;
        }

        GetPoolsAround(bubblegum, gridUid, bossTile.Value, 1, _poolTiles);
        if (_poolTiles.Count == 0)
            return false;

        GetPoolsAround(bubblegum, gridUid, targetTile.Value, 2, _poolTiles);
        for (var i = _poolTiles.Count - 1; i >= 0; i--)
        {
            if (ChebyshevDistance(_poolTiles[i], targetTile.Value) <= 1)
                _poolTiles.RemoveAt(i);
        }

        if (_poolTiles.Count == 0)
            return false;

        var destination = _random.Pick(_poolTiles);
        destination = ClampToInnerArena(arena, destination);

        _audio.PlayPvs(bubblegum.EnterBloodSound, boss, AudioParams.Default.WithVolume(-2f));
        SpawnAnchored(bubblegum.BloodSplatterPrototype, gridUid, grid, bossTile.Value);
        _transform.SetCoordinates(boss, _map.GridTileToLocal(gridUid, grid, destination));
        SpawnAnchored(bubblegum.BloodSplatterPrototype, gridUid, grid, destination);
        _audio.PlayPvs(bubblegum.ExitBloodSound, boss, AudioParams.Default.WithVolume(-2f));
        return true;
    }

    private bool TrySummonSlaughterlings(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now,
        out bool summonedFullWave)
    {
        summonedFullWave = false;
        if (now < bubblegum.NextSummon)
            return false;

        PruneTracked(bubblegum);
        var maxActive = Math.Max(0, bubblegum.MaxActiveSlaughterlings);
        if (maxActive == 0)
            return false;

        var active = bubblegum.Slaughterlings.Count;
        if (active >= maxActive)
            return false;

        var bossTile = GetEntityTile(boss, gridUid, grid);
        if (bossTile == null)
            return false;

        GetPoolsAround(bubblegum, gridUid, bossTile.Value, 1, _poolTiles);
        _random.Shuffle(_poolTiles);

        var limit = Math.Min(
            Math.Max(0, bubblegum.MaxSummonsPerCast),
            Math.Max(0, maxActive - active));
        if (limit <= 0)
            return false;

        var spawned = 0;
        foreach (var tile in _poolTiles)
        {
            if (spawned >= limit)
                break;

            if (!IsInsideInnerArena(arena, tile))
                continue;

            var summon = Spawn(bubblegum.SlaughterlingPrototype, _map.GridTileToLocal(gridUid, grid, tile));
            bubblegum.Slaughterlings.Add(summon);
            spawned++;
        }

        if (spawned <= 0)
            return false;

        summonedFullWave = spawned >= Math.Max(1, bubblegum.MaxSummonsPerCast);
        bubblegum.NextSummon = now + bubblegum.SummonCooldown;
        _audio.PlayPvs(bubblegum.SplatSound, _map.GridTileToLocal(gridUid, grid, bossTile.Value), AudioParams.Default.WithVolume(-1f));
        return true;
    }

    /// <summary>
    /// tgstation hallucination charges. Ring: clones around the target charge it all at once, the real
    /// Bubblegum hides among them (below half health - several waves, each closer and faster).
    /// Surround: pairs of clones rush in from close by, then the real one charges.
    /// </summary>
    private bool TryHallucinationAttack(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        EntityUid target,
        bool belowHalf,
        TimeSpan now)
    {
        if (string.IsNullOrWhiteSpace(bubblegum.ClonePrototype) || bubblegum.HallucinationWaves.Count > 0)
            return false;

        if (_random.Prob(bubblegum.HallucinationRingChance))
        {
            var waves = belowHalf ? Math.Max(1, bubblegum.HallucinationEnragedWaves) : 1;
            var radius = belowHalf ? Math.Max(2, bubblegum.HallucinationRingRadius - 2) : bubblegum.HallucinationRingRadius;
            var clones = belowHalf ? Math.Max(3, bubblegum.HallucinationRingClones - 2) : bubblegum.HallucinationRingClones;
            var at = now;
            for (var i = 0; i < waves; i++)
            {
                bubblegum.HallucinationWaves.Add(new LavalandBubblegumHallucinationWave
                {
                    Target = target,
                    At = at,
                    Clones = clones,
                    Radius = radius,
                    IncludeSelf = i == waves - 1,
                });
                // Each next wave comes sooner.
                at += TimeSpan.FromSeconds(Math.Max(0.35, bubblegum.HallucinationWaveDelay.TotalSeconds - 0.2 * i));
            }

            bubblegum.BusyUntil = at + bubblegum.ChargeWindup + TimeSpan.FromSeconds(1.5);
            return true;
        }

        if (_random.Prob(bubblegum.HallucinationSurroundChance))
        {
            var at = now;
            for (var i = 0; i < bubblegum.HallucinationSurroundRounds; i++)
            {
                bubblegum.HallucinationWaves.Add(new LavalandBubblegumHallucinationWave
                {
                    Target = target, At = at, Clones = 2, Radius = 2, IncludeSelf = false,
                });
                at += bubblegum.HallucinationSurroundDelay;
            }

            bubblegum.HallucinationWaves.Add(new LavalandBubblegumHallucinationWave
            {
                Target = target, At = at, Clones = 1, Radius = 4, IncludeSelf = true,
            });
            bubblegum.BusyUntil = at + bubblegum.ChargeWindup + TimeSpan.FromSeconds(1.5);
            return true;
        }

        return false;
    }

    private void ProcessHallucinationWaves(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        for (var i = 0; i < bubblegum.HallucinationWaves.Count; i++)
        {
            var wave = bubblegum.HallucinationWaves[i];
            if (wave.At > now)
                continue;

            bubblegum.HallucinationWaves.RemoveAt(i);
            i--;
            if (!TerminatingOrDeleted(wave.Target) && !IsDead(wave.Target))
                SpawnHallucinationRing(boss, bubblegum, arena, gridUid, grid, wave, now);
        }
    }

    private void SpawnHallucinationRing(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        LavalandBubblegumHallucinationWave wave,
        TimeSpan now)
    {
        if (GetEntityTile(wave.Target, gridUid, grid) is not { } targetTile)
            return;

        var start = _random.NextFloat(0f, MathF.Tau);
        var selfSlot = wave.IncludeSelf ? _random.Next(wave.Clones) : -1;
        var chargeDuration = bubblegum.ChargeWindup +
                             TimeSpan.FromSeconds(bubblegum.ChargeStepDelay.TotalSeconds * (wave.Radius + 3)) +
                             bubblegum.CloneLinger;

        for (var i = 0; i < wave.Clones; i++)
        {
            var angle = start + MathF.Tau * i / wave.Clones;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var tile = ClampToInnerArena(arena, targetTile + new Vector2i(
                (int) MathF.Round(direction.X * wave.Radius),
                (int) MathF.Round(direction.Y * wave.Radius)));
            // They run through the target and a little past it.
            var destination = ClampToInnerArena(arena, targetTile - new Vector2i(
                (int) MathF.Round(direction.X * 2),
                (int) MathF.Round(direction.Y * 2)));

            SpawnAnchored(bubblegum.BloodSplatterPrototype, gridUid, grid, tile);
            if (i == selfSlot)
            {
                // The real Bubblegum hides in the ring and charges with the clones.
                _transform.SetCoordinates(boss, _map.GridTileToLocal(gridUid, grid, tile));
                StartCharge(boss, bubblegum, arena, gridUid, grid, wave.Target, wave.Radius + 2, 0, now);
                continue;
            }

            var clone = SpawnClone(bubblegum, gridUid, grid, tile, now, chargeDuration);
            if (clone == null)
                continue;

            SpawnAnchored(bubblegum.LandingPrototype, gridUid, grid, destination);
            bubblegum.CloneCharges.Add(new LavalandBubblegumCloneCharge
            {
                Entity = clone.Value,
                TargetTile = destination,
                RemainingSteps = Math.Max(1, ChebyshevDistance(tile, destination)),
                NextStep = now + bubblegum.ChargeWindup,
                ChargeDamage = bubblegum.CloneChargeDamage,
            });
        }

        _audio.PlayPvs(bubblegum.EnterBloodSound, _map.GridTileToLocal(gridUid, grid, targetTile), AudioParams.Default.WithVolume(-2f));
    }

    private void StartCharge(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        EntityUid target,
        int steps,
        int extraCharges,
        TimeSpan now)
    {
        var bossTile = GetEntityTile(boss, gridUid, grid);
        var targetTile = GetEntityTile(target, gridUid, grid);
        if (bossTile == null || targetTile == null)
            return;

        var destination = ClampToInnerArena(arena, targetTile.Value);
        SpawnAnchored(bubblegum.LandingPrototype, gridUid, grid, destination);
        StartCloneCharges(boss, bubblegum, arena, gridUid, grid, bossTile.Value, destination, steps, now);
        bossTile = GetEntityTile(boss, gridUid, grid);
        if (bossTile == null)
            return;

        bubblegum.Charging = true;
        bubblegum.ChargeTargetTile = destination;
        bubblegum.ChargeRemainingSteps = Math.Max(1, Math.Min(Math.Max(1, steps), Math.Max(1, ChebyshevDistance(bossTile.Value, destination))));
        bubblegum.NextChargeStep = now + bubblegum.ChargeWindup;
        bubblegum.PendingCharges = Math.Max(0, extraCharges);
        bubblegum.PendingChargeSteps = Math.Max(1, steps);
        bubblegum.NextQueuedCharge = TimeSpan.Zero;
        bubblegum.ChargeHitEntities.Clear();
        bubblegum.LastMovementAt = now;
        bubblegum.BusyUntil = now + bubblegum.ChargeWindup + TimeSpan.FromSeconds(bubblegum.ChargeStepDelay.TotalSeconds * bubblegum.ChargeRemainingSteps) + bubblegum.ChargeRecover;

        _movement.RefreshMovementSpeedModifiers(boss);
    }

    private bool ProcessCharge(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        if (!bubblegum.Charging)
            return false;

        if (now < bubblegum.NextChargeStep)
            return true;

        var currentTile = GetEntityTile(boss, gridUid, grid);
        if (currentTile == null ||
            currentTile.Value == bubblegum.ChargeTargetTile ||
            bubblegum.ChargeRemainingSteps <= 0)
        {
            FinishCharge(boss, bubblegum, gridUid, grid, now);
            return true;
        }

        var nextTile = StepTowards(currentTile.Value, bubblegum.ChargeTargetTile);
        if (!IsInsideInnerArena(arena, nextTile))
        {
            FinishCharge(boss, bubblegum, gridUid, grid, now);
            return true;
        }

        TrySpawnBloodPool(bubblegum, arena, gridUid, grid, currentTile.Value);
        TrySpawnBloodPool(bubblegum, arena, gridUid, grid, nextTile);
        var chargeDirection = nextTile - currentTile.Value;
        _transform.SetCoordinates(boss, _map.GridTileToLocal(gridUid, grid, nextTile));

        var hit = DamageChargeTile(boss, bubblegum, gridUid, grid, nextTile, chargeDirection);
        bubblegum.ChargeRemainingSteps--;
        bubblegum.NextChargeStep = now + bubblegum.ChargeStepDelay;

        if (hit ||
            nextTile == bubblegum.ChargeTargetTile ||
            bubblegum.ChargeRemainingSteps <= 0)
        {
            FinishCharge(boss, bubblegum, gridUid, grid, bubblegum.NextChargeStep);
        }

        return true;
    }

    private bool ProcessQueuedCharge(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        if (bubblegum.PendingCharges <= 0)
            return false;

        if (now < bubblegum.NextQueuedCharge)
            return true;

        var target = PickTarget(bubblegum, boss, gridUid, grid, now);
        if (target == null)
        {
            bubblegum.PendingCharges = 0;
            return false;
        }

        var remaining = Math.Max(0, bubblegum.PendingCharges - 1);
        StartCharge(boss, bubblegum, arena, gridUid, grid, target.Value, bubblegum.PendingChargeSteps, remaining, now);
        MarkTargetPressure(bubblegum, target.Value, now);
        return true;
    }

    private bool DamageChargeTile(
        EntityUid attacker,
        LavalandBubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        Vector2i chargeDirection,
        DamageSpecifier? damageOverride = null,
        EntityUid? additionalIgnored = null,
        HashSet<EntityUid>? hitEntities = null)
    {
        var hit = false;
        hitEntities ??= bubblegum.ChargeHitEntities;

        _tileEntities.Clear();
        _lookup.GetLocalEntitiesIntersecting(gridUid, tile, _tileEntities, flags: LookupFlags.Dynamic | LookupFlags.Sundries, gridComp: grid);

        foreach (var uid in _tileEntities)
        {
            if (uid == attacker ||
                uid == additionalIgnored ||
                bubblegum.Slaughterlings.Contains(uid) ||
                hitEntities.Contains(uid) ||
                !TryComp(uid, out DamageableComponent? damageable) ||
                !TryComp(uid, out MobStateComponent? mobState) ||
                !TryComp(uid, out TransformComponent? xform) ||
                mobState.CurrentState == MobState.Dead ||
                xform.GridUid != gridUid ||
                _map.LocalToTile(gridUid, grid, xform.Coordinates) != tile)
            {
                continue;
            }

            _damageable.TryChangeDamage((uid, damageable), damageOverride ?? bubblegum.ChargeDamage, origin: attacker);
            hitEntities.Add(uid);
            hit = true;

            var direction = new Vector2(chargeDirection.X, chargeDirection.Y);
            if (direction.LengthSquared() < 0.01f)
                direction = _random.NextVector2();

            _throwing.TryThrow(uid, direction.Normalized() * 2.5f, bubblegum.ChargeThrowSpeed, attacker, playSound: false, doSpin: false);
        }

        _tileEntities.Clear();

        if (hit)
            _audio.PlayPvs(bubblegum.ImpactSound, _map.GridTileToLocal(gridUid, grid, tile), AudioParams.Default.WithVolume(0f));

        return hit;
    }

    private void FinishCharge(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        var tile = GetEntityTile(boss, gridUid, grid) ?? Vector2i.Zero;
        _audio.PlayPvs(bubblegum.ImpactSound, _map.GridTileToLocal(gridUid, grid, tile), AudioParams.Default.WithVolume(-2f));

        bubblegum.Charging = false;
        bubblegum.ChargeHitEntities.Clear();
        _movement.RefreshMovementSpeedModifiers(boss);

        if (bubblegum.PendingCharges > 0)
        {
            bubblegum.NextQueuedCharge = now + bubblegum.ChainedChargeDelay;
            bubblegum.BusyUntil = bubblegum.NextQueuedCharge + bubblegum.ChargeWindup;
        }
        else
        {
            bubblegum.NextQueuedCharge = TimeSpan.Zero;
            bubblegum.BusyUntil = now + bubblegum.ChargeRecover;
        }
    }

    private void ProcessActiveClones(LavalandBubblegumComponent bubblegum, TimeSpan now)
    {
        for (var i = bubblegum.ActiveClones.Count - 1; i >= 0; i--)
        {
            var clone = bubblegum.ActiveClones[i];
            if (clone.DespawnAt > now && Exists(clone.Entity))
                continue;

            if (Exists(clone.Entity))
                QueueDel(clone.Entity);

            bubblegum.ActiveClones.RemoveAt(i);
        }
    }

    private void ProcessCloneCharges(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        for (var i = bubblegum.CloneCharges.Count - 1; i >= 0; i--)
        {
            var charge = bubblegum.CloneCharges[i];
            if (!Exists(charge.Entity))
            {
                bubblegum.CloneCharges.RemoveAt(i);
                continue;
            }

            if (now < charge.NextStep)
                continue;

            var currentTile = GetEntityTile(charge.Entity, gridUid, grid);
            if (currentTile == null ||
                currentTile.Value == charge.TargetTile ||
                charge.RemainingSteps <= 0)
            {
                FinishCloneCharge(bubblegum, charge.Entity, gridUid, grid, currentTile ?? charge.TargetTile);
                bubblegum.CloneCharges.RemoveAt(i);
                continue;
            }

            var nextTile = StepTowards(currentTile.Value, charge.TargetTile);
            if (!IsInsideInnerArena(arena, nextTile))
            {
                FinishCloneCharge(bubblegum, charge.Entity, gridUid, grid, currentTile.Value);
                bubblegum.CloneCharges.RemoveAt(i);
                continue;
            }

            if (_random.Prob(0.65f))
                SpawnAnchored(bubblegum.BloodSplatterPrototype, gridUid, grid, currentTile.Value);

            SpawnAnchored(bubblegum.BloodSplatterPrototype, gridUid, grid, nextTile);
            _transform.SetCoordinates(charge.Entity, _map.GridTileToLocal(gridUid, grid, nextTile));
            if (charge.ChargeDamage != null)
                DamageChargeTile(charge.Entity, bubblegum, gridUid, grid, nextTile, nextTile - currentTile.Value, charge.ChargeDamage, boss, charge.HitEntities);

            charge.RemainingSteps--;
            charge.NextStep = now + bubblegum.ChargeStepDelay;
        }
    }

    private void QueueCloneHandAttacks(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i targetTile,
        TimeSpan now,
        bool grab)
    {
        if (!ShouldUseClones(boss, bubblegum))
            return;

        var count = Math.Min(2, GetCloneCount(boss, bubblegum));
        PickCloneTiles(bubblegum, arena, targetTile, count, _cloneTiles);

        var swapped = false;
        foreach (var cloneTile in _cloneTiles)
        {
            var fakeTarget = ClampToInnerArena(arena, targetTile + new Vector2i(_random.Next(-3, 4), _random.Next(-3, 4)));
            if (fakeTarget == targetTile)
                fakeTarget = ClampToInnerArena(arena, targetTile + _random.Pick(Cardinals));

            var clone = SpawnClone(bubblegum, gridUid, grid, cloneTile, now, GetBloodReactionWindow(bubblegum) + bubblegum.CloneLinger);
            if (clone == null)
                continue;

            if (!swapped)
                swapped = TrySwapWithClone(boss, bubblegum, arena, gridUid, grid, clone.Value, now);

            SpawnFakeHandVisual(bubblegum, gridUid, grid, fakeTarget, grab, _random.Prob(0.5f));
        }
    }

    private void QueueCloneBloodSprays(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i sourceTile,
        Vector2i targetTile,
        float rage,
        TimeSpan now)
    {
        if (!ShouldUseClones(boss, bubblegum))
            return;

        var range = Math.Max(1, bubblegum.BloodSprayBaseRange + (int) MathF.Round(rage * bubblegum.BloodSprayRageRangeMultiplier));
        var duration = TimeSpan.FromSeconds(bubblegum.BloodSprayStepDelay.TotalSeconds * range) + bubblegum.CloneLinger + TimeSpan.FromSeconds(0.35);
        PickCloneTiles(bubblegum, arena, sourceTile, GetCloneCount(boss, bubblegum), _cloneTiles);

        var swapped = false;
        foreach (var cloneTile in _cloneTiles)
        {
            var fakeTarget = ClampToInnerArena(arena, targetTile + new Vector2i(_random.Next(-5, 6), _random.Next(-5, 6)));
            var clone = SpawnClone(bubblegum, gridUid, grid, cloneTile, now, duration);
            if (clone == null)
                continue;

            if (!swapped)
                swapped = TrySwapWithClone(boss, bubblegum, arena, gridUid, grid, clone.Value, now);

            var currentCloneTile = GetEntityTile(clone.Value, gridUid, grid) ?? cloneTile;
            QueueFakeBloodSpray(bubblegum, arena, gridUid, grid, currentCloneTile, fakeTarget, range, now);
        }
    }

    private void StartCloneCharges(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i sourceTile,
        Vector2i targetTile,
        int steps,
        TimeSpan now)
    {
        if (!ShouldUseClones(boss, bubblegum))
            return;

        PickCloneTiles(bubblegum, arena, sourceTile, GetCloneCount(boss, bubblegum), _cloneTiles);
        var chargeDuration = bubblegum.ChargeWindup +
                             TimeSpan.FromSeconds(bubblegum.ChargeStepDelay.TotalSeconds * Math.Max(1, steps)) +
                             bubblegum.CloneLinger + TimeSpan.FromSeconds(0.5);

        var swapped = false;
        foreach (var cloneTile in _cloneTiles)
        {
            var fakeTarget = ClampToInnerArena(arena, targetTile + new Vector2i(_random.Next(-7, 8), _random.Next(-7, 8)));
            if (fakeTarget == cloneTile)
                fakeTarget = ClampToInnerArena(arena, cloneTile + _random.Pick(Cardinals) * Math.Max(1, bubblegum.CloneMinOffset));

            var clone = SpawnClone(bubblegum, gridUid, grid, cloneTile, now, chargeDuration);
            if (clone == null)
                continue;

            if (!swapped)
                swapped = TrySwapWithClone(boss, bubblegum, arena, gridUid, grid, clone.Value, now);

            var currentCloneTile = GetEntityTile(clone.Value, gridUid, grid) ?? cloneTile;
            SpawnAnchored(bubblegum.LandingPrototype, gridUid, grid, fakeTarget);

            bubblegum.CloneCharges.Add(new LavalandBubblegumCloneCharge
            {
                Entity = clone.Value,
                TargetTile = fakeTarget,
                RemainingSteps = Math.Max(1, Math.Min(Math.Max(1, steps), Math.Max(1, ChebyshevDistance(currentCloneTile, fakeTarget)))),
                NextStep = now + bubblegum.ChargeWindup,
                ChargeDamage = bubblegum.CloneChargeDamage,
            });
        }
    }

    private void QueueFakeBloodSpray(
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i sourceTile,
        Vector2i targetTile,
        int range,
        TimeSpan now)
    {
        var direction = StepTowards(sourceTile, targetTile) - sourceTile;
        if (direction == Vector2i.Zero)
            direction = _random.Pick(Cardinals);

        SpawnAnchored(bubblegum.BloodSplatterPrototype, gridUid, grid, sourceTile);

        for (var step = 1; step <= range; step++)
        {
            if (bubblegum.PendingBloodTiles.Count >= Math.Max(0, bubblegum.MaxPendingBloodTiles))
                break;

            var tile = sourceTile + direction * step;
            if (!IsInsideInnerArena(arena, tile))
                break;

            if (HasPendingBloodTile(bubblegum, gridUid, tile))
                continue;

            bubblegum.PendingBloodTiles.Add(new LavalandBubblegumPendingBloodTile
            {
                Grid = gridUid,
                Tile = tile,
                SpawnAt = now + TimeSpan.FromSeconds(bubblegum.BloodSprayStepDelay.TotalSeconds * step),
                Fake = true,
            });
        }
    }

    private void SpawnFakeHandVisual(
        LavalandBubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        bool grab,
        bool rightHand)
    {
        if (grab)
        {
            SpawnAnchored(rightHand ? bubblegum.RightPawPrototype : bubblegum.LeftPawPrototype, gridUid, grid, tile);
            SpawnAnchored(rightHand ? bubblegum.RightThumbPrototype : bubblegum.LeftThumbPrototype, gridUid, grid, tile);
            return;
        }

        SpawnAnchored(rightHand ? bubblegum.RightSmackPrototype : bubblegum.LeftSmackPrototype, gridUid, grid, tile);
    }

    private EntityUid? SpawnClone(
        LavalandBubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        TimeSpan now,
        TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(bubblegum.ClonePrototype) ||
            !_prototype.HasIndex<EntityPrototype>(bubblegum.ClonePrototype) ||
            bubblegum.ActiveClones.Count >= Math.Max(0, bubblegum.MaxActiveClones))
        {
            return null;
        }

        var clone = Spawn(bubblegum.ClonePrototype, _map.GridTileToLocal(gridUid, grid, tile));
        bubblegum.ActiveClones.Add(new LavalandBubblegumActiveClone
        {
            Entity = clone,
            DespawnAt = now + duration,
        });

        return clone;
    }

    private bool TrySwapWithClone(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        EntityUid clone,
        TimeSpan now)
    {
        if (bubblegum.Charging ||
            now < bubblegum.NextCloneSwap ||
            !ShouldUseClones(boss, bubblegum) ||
            !_random.Prob(GetCloneSwapChance(boss, bubblegum)))
        {
            return false;
        }

        var bossTile = GetEntityTile(boss, gridUid, grid);
        var cloneTile = GetEntityTile(clone, gridUid, grid);
        if (bossTile == null ||
            cloneTile == null ||
            bossTile.Value == cloneTile.Value ||
            !IsInsideInnerArena(arena, bossTile.Value) ||
            !IsInsideInnerArena(arena, cloneTile.Value))
        {
            return false;
        }

        SpawnAnchored(bubblegum.BloodSplatterPrototype, gridUid, grid, bossTile.Value);
        SpawnAnchored(bubblegum.BloodSplatterPrototype, gridUid, grid, cloneTile.Value);

        _audio.PlayPvs(bubblegum.EnterBloodSound, _map.GridTileToLocal(gridUid, grid, bossTile.Value), AudioParams.Default.WithVolume(-3f));
        _transform.SetCoordinates(boss, _map.GridTileToLocal(gridUid, grid, cloneTile.Value));
        _transform.SetCoordinates(clone, _map.GridTileToLocal(gridUid, grid, bossTile.Value));
        _audio.PlayPvs(bubblegum.ExitBloodSound, _map.GridTileToLocal(gridUid, grid, cloneTile.Value), AudioParams.Default.WithVolume(-3f));

        bubblegum.NextCloneSwap = now + bubblegum.CloneSwapCooldown;
        return true;
    }

    private void FinishCloneCharge(
        LavalandBubblegumComponent bubblegum,
        EntityUid clone,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile)
    {
        SpawnAnchored(bubblegum.BloodSplatterPrototype, gridUid, grid, tile);
        _audio.PlayPvs(bubblegum.ImpactSound, _map.GridTileToLocal(gridUid, grid, tile), AudioParams.Default.WithVolume(-5f));
        DeleteClone(bubblegum, clone);
    }

    private void DeleteClone(LavalandBubblegumComponent bubblegum, EntityUid clone)
    {
        for (var i = bubblegum.ActiveClones.Count - 1; i >= 0; i--)
        {
            if (bubblegum.ActiveClones[i].Entity == clone)
                bubblegum.ActiveClones.RemoveAt(i);
        }

        if (Exists(clone))
            QueueDel(clone);
    }

    private void PickCloneTiles(
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        Vector2i origin,
        int count,
        List<Vector2i> output)
    {
        output.Clear();
        count = Math.Max(0, count);
        if (count == 0)
            return;

        var minOffset = Math.Max(1, bubblegum.CloneMinOffset);
        var maxOffset = Math.Max(minOffset, bubblegum.CloneMaxOffset);

        for (var attempt = 0; attempt < count * 20 && output.Count < count; attempt++)
        {
            var offset = new Vector2i(_random.Next(-maxOffset, maxOffset + 1), _random.Next(-maxOffset, maxOffset + 1));
            if (offset == Vector2i.Zero ||
                Math.Max(Math.Abs(offset.X), Math.Abs(offset.Y)) < minOffset)
            {
                continue;
            }

            var tile = ClampToInnerArena(arena, origin + offset);
            if (tile == origin || output.Contains(tile))
                continue;

            output.Add(tile);
        }

        foreach (var direction in Cardinals)
        {
            if (output.Count >= count)
                break;

            var tile = ClampToInnerArena(arena, origin + direction * minOffset);
            if (tile != origin && !output.Contains(tile))
                output.Add(tile);
        }
    }

    private bool TrySpawnBloodPool(
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile)
    {
        if (!IsInsideInnerArena(arena, tile) ||
            HasBloodPoolAt(bubblegum, gridUid, tile) ||
            !_prototype.HasIndex<EntityPrototype>(bubblegum.BloodPoolPrototype))
        {
            return false;
        }

        PruneTracked(bubblegum);
        if (bubblegum.BloodPools.Count >= Math.Max(1, bubblegum.MaxBloodPools))
        {
            var oldest = bubblegum.BloodPools[0];
            bubblegum.BloodPools.RemoveAt(0);
            if (Exists(oldest))
                QueueDel(oldest);
        }

        var pool = Spawn(bubblegum.BloodPoolPrototype, _map.GridTileToLocal(gridUid, grid, tile));
        if (TryComp(pool, out TransformComponent? xform) && !xform.Anchored)
            _transform.AnchorEntity((pool, xform), (gridUid, grid), tile);

        var poolComponent = EnsureComp<LavalandBubblegumBloodPoolComponent>(pool);
        poolComponent.Grid = gridUid;
        poolComponent.Tile = tile;

        bubblegum.BloodPools.Add(pool);
        return true;
    }

    private void SpawnAnchored(string prototype, EntityUid gridUid, MapGridComponent grid, Vector2i index)
    {
        if (string.IsNullOrWhiteSpace(prototype) ||
            !_prototype.HasIndex<EntityPrototype>(prototype))
        {
            return;
        }

        var uid = Spawn(prototype, _map.GridTileToLocal(gridUid, grid, index));
        if (!TryComp(uid, out TransformComponent? xform) || xform.Anchored)
            return;

        _transform.AnchorEntity((uid, xform), (gridUid, grid), index);
    }

    private void GetPoolsAround(LavalandBubblegumComponent bubblegum, EntityUid gridUid, Vector2i center, int range, List<Vector2i> output)
    {
        output.Clear();
        foreach (var pool in bubblegum.BloodPools)
        {
            if (!TryComp<LavalandBubblegumBloodPoolComponent>(pool, out var poolComponent) ||
                poolComponent.Grid != gridUid ||
                ChebyshevDistance(poolComponent.Tile, center) > range ||
                output.Contains(poolComponent.Tile))
            {
                continue;
            }

            output.Add(poolComponent.Tile);
        }
    }

    private bool HasBloodPoolAt(LavalandBubblegumComponent bubblegum, EntityUid gridUid, Vector2i tile)
    {
        foreach (var pool in bubblegum.BloodPools)
        {
            if (TryComp<LavalandBubblegumBloodPoolComponent>(pool, out var poolComponent) &&
                poolComponent.Grid == gridUid &&
                poolComponent.Tile == tile)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasBloodPoolWithin(LavalandBubblegumComponent bubblegum, EntityUid gridUid, Vector2i tile, int range)
    {
        foreach (var pool in bubblegum.BloodPools)
        {
            if (TryComp<LavalandBubblegumBloodPoolComponent>(pool, out var poolComponent) &&
                poolComponent.Grid == gridUid &&
                ChebyshevDistance(poolComponent.Tile, tile) <= range)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasPendingBloodTile(LavalandBubblegumComponent bubblegum, EntityUid gridUid, Vector2i tile)
    {
        foreach (var pending in bubblegum.PendingBloodTiles)
        {
            if (pending.Grid == gridUid && pending.Tile == tile)
                return true;
        }

        return false;
    }

    private int CollectParticipants(LavalandBossArenaComponent arena)
    {
        _participants.Clear();

        foreach (var userId in arena.Participants)
        {
            if (!_players.TryGetSessionById(userId, out var session) ||
                session.AttachedEntity is not { Valid: true } attached ||
                !Exists(attached) ||
                IsDead(attached) ||
                !TryComp(attached, out TransformComponent? xform) ||
                xform.GridUid != arena.Grid)
            {
                continue;
            }

            _participants.Add(attached);
        }

        return _participants.Count;
    }

    private EntityUid? PickTarget(
        LavalandBubblegumComponent bubblegum,
        EntityUid boss,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        if (_participants.Count == 0)
            return null;

        PruneTargetMemory(bubblegum);

        if (_participants.Count == 1)
        {
            SetPrimaryTarget(bubblegum, _participants[0], now);
            return _participants[0];
        }

        if (bubblegum.CurrentPrimaryTarget is { Valid: true } current &&
            _participants.Contains(current) &&
            now - bubblegum.LastTargetSwitchAt < bubblegum.TargetSwitchCooldown)
        {
            return current;
        }

        var bossTile = boss.Valid ? GetEntityTile(boss, gridUid, grid) : null;
        EntityUid? best = null;
        var bestScore = float.MinValue;
        foreach (var participant in _participants)
        {
            var tile = GetEntityTile(participant, gridUid, grid);
            if (tile == null)
                continue;

            var score = ScoreTarget(bubblegum, participant, bossTile, tile.Value, now, true);
            if (score <= bestScore)
                continue;

            best = participant;
            bestScore = score;
        }

        if (best == null)
            best = _random.Pick(_participants);

        SetPrimaryTarget(bubblegum, best.Value, now);
        return best;
    }

    private EntityUid? PickSecondaryTarget(
        LavalandBubblegumComponent bubblegum,
        EntityUid excluded,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        if (_participants.Count <= 1)
            return null;

        PruneTargetMemory(bubblegum);

        EntityUid? best = null;
        var bestScore = float.MinValue;
        foreach (var participant in _participants)
        {
            if (participant == excluded)
                continue;

            var tile = GetEntityTile(participant, gridUid, grid);
            if (tile == null)
                continue;

            var score = ScoreTarget(bubblegum, participant, null, tile.Value, now, false);
            if (score <= bestScore)
                continue;

            best = participant;
            bestScore = score;
        }

        return best;
    }

    private EntityUid? PickPressureTarget(
        LavalandBubblegumComponent bubblegum,
        EntityUid preferred,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        if (_participants.Count == 0)
            return null;

        PruneTargetMemory(bubblegum);

        EntityUid? best = null;
        var bestScore = float.MinValue;
        foreach (var participant in _participants)
        {
            var tile = GetEntityTile(participant, gridUid, grid);
            if (tile == null)
                continue;

            var score = GetPressureSafeSeconds(bubblegum, participant, now) + _random.NextFloat(0f, 1.5f);
            if (!HasBloodPoolWithin(bubblegum, gridUid, tile.Value, 1))
                score += 18f;

            if (participant != preferred)
                score += 3f;

            if (score <= bestScore)
                continue;

            best = participant;
            bestScore = score;
        }

        return best;
    }

    private float ScoreTarget(
        LavalandBubblegumComponent bubblegum,
        EntityUid target,
        Vector2i? bossTile,
        Vector2i targetTile,
        TimeSpan now,
        bool applyCurrentPenalty)
    {
        var safeSeconds = bubblegum.TargetPressureMemory.TotalSeconds;
        if (bubblegum.LastPressureByTarget.TryGetValue(target, out var lastPressure))
            safeSeconds = Math.Clamp((now - lastPressure).TotalSeconds, 0, bubblegum.TargetPressureMemory.TotalSeconds);

        var distancePenalty = bossTile == null
            ? 0f
            : ChebyshevDistance(bossTile.Value, targetTile) * 0.2f;
        var score = (float) safeSeconds - distancePenalty + _random.NextFloat(0f, 1.5f);

        if (applyCurrentPenalty &&
            bubblegum.CurrentPrimaryTarget == target &&
            now - bubblegum.LastTargetSwitchAt >= bubblegum.TargetSwitchCooldown)
        {
            score -= 8f;
        }

        return score;
    }

    private static float GetPressureSafeSeconds(LavalandBubblegumComponent bubblegum, EntityUid target, TimeSpan now)
    {
        if (!bubblegum.LastPressureByTarget.TryGetValue(target, out var lastPressure))
            return (float) bubblegum.TargetPressureMemory.TotalSeconds;

        return (float) Math.Clamp(
            (now - lastPressure).TotalSeconds,
            0,
            bubblegum.TargetPressureMemory.TotalSeconds);
    }

    private void PruneTargetMemory(LavalandBubblegumComponent bubblegum)
    {
        if (bubblegum.CurrentPrimaryTarget is { Valid: true } current &&
            !_participants.Contains(current))
        {
            bubblegum.CurrentPrimaryTarget = null;
        }

        if (bubblegum.LastPressureByTarget.Count == 0)
            return;

        foreach (var target in new List<EntityUid>(bubblegum.LastPressureByTarget.Keys))
        {
            if (!_participants.Contains(target))
                bubblegum.LastPressureByTarget.Remove(target);
        }
    }

    private static void SetPrimaryTarget(LavalandBubblegumComponent bubblegum, EntityUid target, TimeSpan now)
    {
        if (bubblegum.CurrentPrimaryTarget == target)
            return;

        bubblegum.CurrentPrimaryTarget = target;
        bubblegum.LastTargetSwitchAt = now;
    }

    private void PruneTracked(LavalandBubblegumComponent bubblegum)
    {
        for (var i = bubblegum.BloodPools.Count - 1; i >= 0; i--)
        {
            if (!Exists(bubblegum.BloodPools[i]))
                bubblegum.BloodPools.RemoveAt(i);
        }

        for (var i = bubblegum.BloodWalls.Count - 1; i >= 0; i--)
        {
            if (!Exists(bubblegum.BloodWalls[i]))
                bubblegum.BloodWalls.RemoveAt(i);
        }

        for (var i = bubblegum.Slaughterlings.Count - 1; i >= 0; i--)
        {
            var summon = bubblegum.Slaughterlings[i];
            if (!Exists(summon) || IsDead(summon))
                bubblegum.Slaughterlings.RemoveAt(i);
        }
    }

    /// <summary>
    /// Whoever keeps running away from it gets a wall of blood across the way: the longer the fight goes (the less
    /// health it has), the sooner.
    /// </summary>
    private void ProcessKiting(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        float frameTime)
    {
        var bossPosition = _transform.GetWorldPosition(boss);
        var health = Math.Clamp(GetHealthFraction(boss), 0f, 1f);
        var threshold = bubblegum.KiteTimeLowHealth +
                        (bubblegum.KiteTimeFullHealth - bubblegum.KiteTimeLowHealth) * health;

        foreach (var key in bubblegum.KiteTime.Keys.ToArray())
        {
            if (!_participants.Contains(key))
                bubblegum.KiteTime.Remove(key);
        }

        foreach (var player in _participants)
        {
            var away = _transform.GetWorldPosition(player) - bossPosition;
            if (away.Length() <= bubblegum.KiteDistance)
            {
                bubblegum.KiteTime.Remove(player);
                continue;
            }

            var time = bubblegum.KiteTime.GetValueOrDefault(player) + frameTime;
            if (time < threshold)
            {
                bubblegum.KiteTime[player] = time;
                continue;
            }

            bubblegum.KiteTime.Remove(player);
            SpawnBloodWall(bubblegum, arena, gridUid, grid, player, away);
        }
    }

    /// <summary>A line of blood across the runner's way, a little ahead of them.</summary>
    private void SpawnBloodWall(
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        EntityUid player,
        Vector2 awayFromBoss)
    {
        // Where they run; standing still, away from it.
        var direction = TryComp<PhysicsComponent>(player, out var body) && body.LinearVelocity.LengthSquared() > 0.25f
            ? body.LinearVelocity
            : awayFromBoss;
        if (direction.LengthSquared() < 0.0001f)
            return;

        direction = Vector2.Normalize(direction);
        var across = new Vector2(-direction.Y, direction.X);
        var center = _transform.GetWorldPosition(player) + direction * bubblegum.BloodWallAhead;
        var gridXform = Transform(gridUid);
        var spawned = false;
        _cloneTiles.Clear();

        var half = bubblegum.BloodWallLength / 2f;
        for (var step = -half; step <= half; step += 0.5f)
        {
            var world = center + across * step;
            var local = Vector2.Transform(world, _transform.GetInvWorldMatrix(gridXform));
            var tile = _map.LocalToTile(gridUid, grid, new EntityCoordinates(gridUid, local));
            if (_cloneTiles.Contains(tile) || !IsInsideInnerArena(arena, tile) || !IsFreeForBloodWall(gridUid, grid, tile))
                continue;

            _cloneTiles.Add(tile);
            var wall = Spawn(bubblegum.BloodWallPrototype, _map.GridTileToLocal(gridUid, grid, tile));
            if (TryComp(wall, out TransformComponent? wallXform) && !wallXform.Anchored)
                _transform.AnchorEntity((wall, wallXform), (gridUid, grid), tile);
            bubblegum.BloodWalls.Add(wall);
            spawned = true;
        }

        _cloneTiles.Clear();
        if (spawned && bubblegum.BloodWallSound != null)
            _audio.PlayPvs(bubblegum.BloodWallSound, _map.GridTileToLocal(gridUid, grid,
                _map.LocalToTile(gridUid, grid, new EntityCoordinates(gridUid,
                    Vector2.Transform(center, _transform.GetInvWorldMatrix(gridXform))))));
    }

    /// <summary>No wall of blood in a wall or on top of anyone: it would shut them in.</summary>
    private bool IsFreeForBloodWall(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return false;

        _tileEntities.Clear();
        _lookup.GetLocalEntitiesIntersecting(gridUid, tile, _tileEntities,
            flags: LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries, gridComp: grid);
        foreach (var entity in _tileEntities)
        {
            if (HasComp<MobStateComponent>(entity) || HasComp<LavalandBloodWallComponent>(entity))
                return false;

            if (TryComp<PhysicsComponent>(entity, out var physics) && physics.BodyType == BodyType.Static &&
                physics.CanCollide && physics.Hard)
            {
                return false;
            }
        }

        return true;
    }

    private void ClearRuntimeState(EntityUid uid, LavalandBubblegumComponent bubblegum, bool refreshMovement)
    {
        bubblegum.KiteTime.Clear();
        foreach (var wall in bubblegum.BloodWalls)
        {
            if (Exists(wall))
                QueueDel(wall);
        }

        bubblegum.BloodWalls.Clear();
        bubblegum.PendingBloodTiles.Clear();
        bubblegum.PendingHandAttacks.Clear();
        bubblegum.Charging = false;
        bubblegum.PendingCharges = 0;
        bubblegum.NextQueuedCharge = TimeSpan.Zero;
        bubblegum.ChargeHitEntities.Clear();
        bubblegum.CurrentPrimaryTarget = null;
        bubblegum.LastTargetSwitchAt = TimeSpan.Zero;
        bubblegum.LastPressureByTarget.Clear();
        bubblegum.CloneCharges.Clear();
        bubblegum.HallucinationWaves.Clear();
        bubblegum.NextCloneSwap = TimeSpan.Zero;
        bubblegum.LastMovementAt = TimeSpan.Zero;

        foreach (var pool in bubblegum.BloodPools)
        {
            if (Exists(pool))
                QueueDel(pool);
        }

        bubblegum.BloodPools.Clear();

        foreach (var summon in bubblegum.Slaughterlings)
        {
            if (Exists(summon))
                QueueDel(summon);
        }

        bubblegum.Slaughterlings.Clear();

        foreach (var clone in bubblegum.ActiveClones)
        {
            if (Exists(clone.Entity))
                QueueDel(clone.Entity);
        }

        bubblegum.ActiveClones.Clear();

        if (refreshMovement && Exists(uid))
            _movement.RefreshMovementSpeedModifiers(uid);
    }

    private static bool NeedsPressure(LavalandBubblegumComponent bubblegum, TimeSpan now)
    {
        return bubblegum.LastPressureAt == TimeSpan.Zero ||
               now - bubblegum.LastPressureAt >= bubblegum.ForcePressureAfter;
    }

    private static bool IsPressureStale(
        LavalandBubblegumComponent bubblegum,
        EntityUid target,
        TimeSpan now,
        TimeSpan staleAfter)
    {
        return !bubblegum.LastPressureByTarget.TryGetValue(target, out var lastPressure) ||
               now - lastPressure >= staleAfter;
    }

    private bool ShouldPrioritizeMovement(
        LavalandBubblegumComponent bubblegum,
        EntityUid boss,
        int targetDistance,
        TimeSpan now)
    {
        var healthFraction = GetHealthFraction(boss);
        var distance = healthFraction <= 0.25f
            ? Math.Max(1, bubblegum.MovementCriticalDistance)
            : Math.Max(1, bubblegum.MovementDistance);
        if (targetDistance < distance)
            return false;

        var cooldown = healthFraction <= 0.25f
            ? bubblegum.MovementCriticalCooldown
            : bubblegum.MovementCooldown;
        if (bubblegum.LastMovementAt != TimeSpan.Zero &&
            now - bubblegum.LastMovementAt < cooldown)
        {
            return false;
        }

        if (targetDistance >= distance + 5)
            return true;

        var chance = healthFraction <= 0.25f
            ? 0.65f
            : healthFraction <= 0.5f
                ? 0.5f
                : 0.35f;

        return _random.Prob(chance);
    }

    private static bool IsRecentPressureAttack(LavalandBubblegumComponent bubblegum)
    {
        return bubblegum.LastAttackKind is "blood-pressure" or "forced-blood-pressure";
    }

    private static bool IsRecentBloodHandAttack(LavalandBubblegumComponent bubblegum)
    {
        return bubblegum.LastAttackKind is "blood-hand" or "blood-reaction";
    }

    private static void MarkPressure(LavalandBubblegumComponent bubblegum, TimeSpan now, string attackKind, EntityUid target)
    {
        bubblegum.LastPressureAt = now;
        bubblegum.LastAttackKind = attackKind;
        MarkTargetPressure(bubblegum, target, now);
    }

    private static void MarkTargetPressure(LavalandBubblegumComponent bubblegum, EntityUid target, TimeSpan now)
    {
        if (!target.Valid)
            return;

        bubblegum.LastPressureByTarget[target] = now;
    }

    private float CalculateRage(EntityUid boss)
    {
        if (!TryComp<DamageableComponent>(boss, out var damageable))
            return 0f;

        return Math.Clamp(Math.Max(0f, damageable.TotalDamage.Float()) / 60f, 0f, 20f);
    }

    private int GetBloodHandAttackLimit(EntityUid boss)
    {
        var healthFraction = GetHealthFraction(boss);
        if (healthFraction <= 0.25f)
            return 4;

        return healthFraction <= 0.5f ? 3 : 2;
    }

    private bool ShouldUseClones(EntityUid boss, LavalandBubblegumComponent bubblegum)
    {
        return Math.Max(0, bubblegum.CloneCount) > 0 &&
               GetHealthFraction(boss) <= Math.Clamp(bubblegum.CloneHealthThreshold, 0f, 1f);
    }

    private int GetCloneCount(EntityUid boss, LavalandBubblegumComponent bubblegum)
    {
        var cloneCount = Math.Max(0, bubblegum.CloneCount);
        if (GetHealthFraction(boss) <= Math.Clamp(bubblegum.CloneCriticalHealthThreshold, 0f, 1f))
            cloneCount = Math.Max(cloneCount, bubblegum.CloneCriticalCount);

        return cloneCount;
    }

    private float GetCloneSwapChance(EntityUid boss, LavalandBubblegumComponent bubblegum)
    {
        var healthFraction = GetHealthFraction(boss);
        var chance = healthFraction <= Math.Clamp(bubblegum.CloneCriticalHealthThreshold, 0f, 1f)
            ? bubblegum.CloneSwapCriticalChance
            : bubblegum.CloneSwapChance;

        return Math.Clamp(chance, 0f, 1f);
    }

    private float GetHealthFraction(EntityUid boss)
    {
        if (!TryComp<LavalandBossComponent>(boss, out var bossComp) ||
            bossComp.MaxHealth <= 0f ||
            !TryComp<DamageableComponent>(boss, out var damageable))
        {
            return 1f;
        }

        return Math.Clamp((bossComp.MaxHealth - damageable.TotalDamage.Float()) / bossComp.MaxHealth, 0f, 1f);
    }

    private bool IsBelowHalfHealth(EntityUid boss)
    {
        if (!TryComp<LavalandBossComponent>(boss, out var bossComp) ||
            !TryComp<DamageableComponent>(boss, out var damageable))
        {
            return false;
        }

        return damageable.TotalDamage.Float() >= bossComp.MaxHealth * 0.5f;
    }

    private static TimeSpan GetScaledCooldown(TimeSpan baseCooldown, float rage)
    {
        return TimeSpan.FromSeconds(Math.Max(2.0, baseCooldown.TotalSeconds - rage * 0.045));
    }

    private static TimeSpan GetBloodReactionWindow(LavalandBubblegumComponent bubblegum)
    {
        return TimeSpan.FromSeconds(Math.Max(bubblegum.BloodSmackDelay.TotalSeconds, bubblegum.BloodGrabDelay.TotalSeconds)) +
               bubblegum.BloodHandRecover;
    }

    private Vector2i? GetEntityTile(EntityUid uid, EntityUid gridUid, MapGridComponent grid)
    {
        if (!uid.Valid ||
            !TryComp(uid, out TransformComponent? xform) ||
            xform.GridUid != gridUid)
        {
            return null;
        }

        return _map.LocalToTile(gridUid, grid, xform.Coordinates);
    }

    private bool IsDead(EntityUid uid)
    {
        return TryComp(uid, out MobStateComponent? mobState) && mobState.CurrentState == MobState.Dead;
    }

    private static Vector2i StepTowards(Vector2i from, Vector2i to)
    {
        return new Vector2i(
            from.X + Math.Sign(to.X - from.X),
            from.Y + Math.Sign(to.Y - from.Y));
    }

    private static int ChebyshevDistance(Vector2i a, Vector2i b)
    {
        return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }

    private static bool IsInsideInnerArena(LavalandBossArenaComponent arena, Vector2i tile)
    {
        var (minX, maxX, minY, maxY) = GetInnerBounds(arena);
        return tile.X >= minX && tile.X <= maxX && tile.Y >= minY && tile.Y <= maxY;
    }

    private static Vector2i ClampToInnerArena(LavalandBossArenaComponent arena, Vector2i tile)
    {
        var (minX, maxX, minY, maxY) = GetInnerBounds(arena);
        return new Vector2i(
            Math.Clamp(tile.X, minX, maxX),
            Math.Clamp(tile.Y, minY, maxY));
    }

    private static (int MinX, int MaxX, int MinY, int MaxY) GetInnerBounds(LavalandBossArenaComponent arena)
    {
        var halfWidth = arena.Width / 2;
        var halfHeight = arena.Height / 2;
        return (-halfWidth + 1, halfWidth - 1, -halfHeight + 1, halfHeight - 1);
    }
    
    private void TrySpawnCriticalClones(
        EntityUid boss,
        LavalandBubblegumComponent bubblegum,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        if (now < bubblegum.NextCriticalCloneSpawn)
            return;

        var availableClones = Math.Max(0, bubblegum.MaxActiveClones - bubblegum.ActiveClones.Count);
        if (availableClones <= 0)
            return;

        var cloneCount = Math.Min(Math.Max(0, bubblegum.CloneCriticalCount), availableClones);
        if (cloneCount <= 0)
            return;

        bubblegum.NextCriticalCloneSpawn = now + bubblegum.CriticalCloneCooldown;
    
        var bossTile = GetEntityTile(boss, gridUid, grid);
        if (bossTile == null)
            return;
    
        PickCloneTiles(bubblegum, arena, bossTile.Value, cloneCount, _cloneTiles);
    
        var chargeDuration = bubblegum.ChargeWindup +
                             TimeSpan.FromSeconds(bubblegum.ChargeStepDelay.TotalSeconds * bubblegum.TripleChargeSteps) +
                             bubblegum.CloneLinger + TimeSpan.FromSeconds(0.5);
    
        var swapped = false;
        foreach (var cloneTile in _cloneTiles)
        {
            var clone = SpawnClone(bubblegum, gridUid, grid, cloneTile, now, chargeDuration);
            if (clone == null)
                continue;
    
            if (!swapped)
                swapped = TrySwapWithClone(boss, bubblegum, arena, gridUid, grid, clone.Value, now);
    
            var chargeTarget = _participants.Count > 0
                ? _random.Pick(_participants)
                : (EntityUid?) null;
    
            var targetTile = chargeTarget != null
                ? GetEntityTile(chargeTarget.Value, gridUid, grid)
                : null;
    
            var destination = targetTile != null
                ? ClampToInnerArena(arena, targetTile.Value)
                : ClampToInnerArena(arena, cloneTile + _random.Pick(Cardinals) * bubblegum.TripleChargeSteps);
    
            SpawnAnchored(bubblegum.LandingPrototype, gridUid, grid, destination);
    
            var currentCloneTile = GetEntityTile(clone.Value, gridUid, grid) ?? cloneTile;
            bubblegum.CloneCharges.Add(new LavalandBubblegumCloneCharge
            {
                Entity = clone.Value,
                TargetTile = destination,
                RemainingSteps = Math.Max(1, Math.Min(bubblegum.TripleChargeSteps, ChebyshevDistance(currentCloneTile, destination))),
                NextStep = now + bubblegum.ChargeWindup,
                ChargeDamage = bubblegum.CloneChargeDamage,
            });
        }
    }
}
