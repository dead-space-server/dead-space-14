using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandHierophantSystem : EntitySystem
{
    private static readonly Vector2i[] Cardinals =
    {
        new(0, 1),
        new(1, 0),
        new(0, -1),
        new(-1, 0),
    };

    private static readonly Vector2i[] Diagonals =
    {
        new(1, 1),
        new(1, -1),
        new(-1, -1),
        new(-1, 1),
    };

    private static readonly Vector2i[] AllDirections =
    {
        new(0, 1),
        new(1, 1),
        new(1, 0),
        new(1, -1),
        new(0, -1),
        new(-1, -1),
        new(-1, 0),
        new(-1, 1),
    };

    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly List<EntityUid> _participants = new();
    private readonly HashSet<Vector2i> _detonatingTiles = new();
    private readonly Dictionary<Vector2i, DamageSpecifier> _detonatingDamage = new();
    private readonly HashSet<EntityUid> _tileEntities = new();
    private readonly HashSet<EntityUid> _damagedEntities = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandHierophantComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<LavalandHierophantComponent, LavalandBossFightStartedEvent>(OnBossFightStarted);
        SubscribeLocalEvent<LavalandHierophantComponent, LavalandBossResetEvent>(OnBossReset);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandHierophantComponent, LavalandBossComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var hierophant, out var boss, out var xform))
        {
            if (boss.Arena is not { Valid: true } arenaUid ||
                !TryComp<LavalandBossArenaComponent>(arenaUid, out var arena) ||
                arena.Ended ||
                !arena.FightStarted ||
                xform.GridUid != arena.Grid ||
                !TryComp<MapGridComponent>(arena.Grid, out var grid) ||
                IsDead(uid))
            {
                ClearRuntimeState(hierophant);
                continue;
            }

            var participantCount = CollectParticipants(arena);
            if (participantCount == 0)
            {
                ClearRuntimeState(hierophant);
                hierophant.BusyUntil = TimeSpan.Zero;
                continue;
            }

            if (hierophant.BusyUntil > now ||
                hierophant.NextAttack > now)
            {
                ProcessTeleports(hierophant, arena, arena.Grid, grid, now);
                ProcessPendingBlasts(uid, hierophant, arena.Grid, grid, now);
                ProcessChasers(uid, hierophant, arena, arena.Grid, grid, now);
                continue;
            }

            ProcessTeleports(hierophant, arena, arena.Grid, grid, now);
            ProcessPendingBlasts(uid, hierophant, arena.Grid, grid, now);
            ProcessChasers(uid, hierophant, arena, arena.Grid, grid, now);

            var target = PickTarget(hierophant, uid, arena.Grid, grid, now);
            if (target == null)
                continue;

            RunAttack(uid, hierophant, arena, arena.Grid, grid, target.Value, now);
            if (IsBelowCriticalHealth(uid))
            {
                var critCap = now + TimeSpan.FromSeconds(hierophant.CriticalPhaseBusyCapSeconds);
                if (hierophant.BusyUntil > critCap)
                    hierophant.BusyUntil = critCap;
            }
        }
    }

    private void OnMove(Entity<LavalandHierophantComponent> ent, ref MoveEvent args)
    {
        var now = _timing.CurTime;
        if (ent.Comp.NextMovementTrail > now ||
            args.ParentChanged ||
            (args.NewPosition.Position - args.OldPosition.Position).LengthSquared() < 0.01f ||
            IsDead(ent.Owner) ||
            !TryComp<MapGridComponent>(args.OldPosition.EntityId, out var grid))
        {
            return;
        }

        var tile = _map.LocalToTile(args.OldPosition.EntityId, grid, args.OldPosition);
        SpawnAnchored(ent.Comp.SquaresPrototype, args.OldPosition.EntityId, grid, tile);
        PlayTile(ent.Comp.MovementTrailSound, args.OldPosition.EntityId, grid, tile, -5f);
        ent.Comp.NextMovementTrail = now + ent.Comp.MovementTrailInterval;
    }

    private void OnBossReset(EntityUid uid, LavalandHierophantComponent component, LavalandBossResetEvent args)
    {
        PrepareFight(component);
    }

    private void OnBossFightStarted(EntityUid uid, LavalandHierophantComponent component, LavalandBossFightStartedEvent args)
    {
        PrepareFight(component);
    }

    private void PrepareFight(LavalandHierophantComponent component)
    {
        ClearRuntimeState(component);
        component.BusyUntil = TimeSpan.Zero;
        var now = _timing.CurTime;
        component.NextAttack = now + TimeSpan.FromSeconds(1);
        component.NextChaser = now;
        component.NextBlink = now + TimeSpan.FromSeconds(2);
        component.NextMovementTrail = now + component.MovementTrailInterval;
        component.LastPressureAt = now;
        component.BasicAttacksSinceMajor = 0;
        component.LastAttackKind = string.Empty;
        component.NextCriticalBurst = now + TimeSpan.FromSeconds(5);
    }

    private void RunAttack(
        EntityUid boss,
        LavalandHierophantComponent hierophant,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        EntityUid target,
        TimeSpan now)
    {
        var rage = CalculateRage(boss);
        var beamRange = GetBeamRange(hierophant, rage);
        var burstRange = GetBurstRange(hierophant, rage);
        var targetTile = GetEntityTile(target, gridUid, grid);
        var bossTile = GetEntityTile(boss, gridUid, grid);

        if (targetTile == null || bossTile == null)
            return;

        if (ChebyshevDistance(targetTile.Value, bossTile.Value) <= 2)
        {
            QueueBurst(hierophant, arena, gridUid, grid, bossTile.Value, burstRange, now, 0.25f);
            MarkPressure(hierophant, now, "close-burst", false, target);
            hierophant.NextAttack = now + GetFinalAttackCooldown(boss, hierophant, hierophant.RangedCooldown, rage);
            return;
        }

        var distance = ChebyshevDistance(targetTile.Value, bossTile.Value);
        var forceMajor = NeedsPressure(hierophant, now) ||
                         hierophant.BasicAttacksSinceMajor >= Math.Max(1, hierophant.ForceMajorAfterBasicAttacks);
        var majorChance = IsBelowCriticalHealth(boss) ? 0.95f : Math.Clamp(hierophant.BaseMajorAttackChance + rage * hierophant.MajorAttackChancePerRage, 0f, 0.85f);

        if ((forceMajor || _random.Prob(majorChance)) &&
            TryRunMajorAttack(boss, hierophant, arena, gridUid, grid, target, rage, beamRange, now))
        {
            MarkPressure(hierophant, now, forceMajor ? "forced-major" : "major", true, target);
            hierophant.NextAttack = now + GetFinalAttackCooldown(boss, hierophant, hierophant.MajorAttackCooldown, rage);
            return;
        }

        if (distance > 2 &&
            hierophant.NextBlink <= now &&
            _random.Prob((60f + rage * 0.2f) / 100f))
        {
            QueueBlink(boss, hierophant, arena, gridUid, grid, bossTile.Value, targetTile.Value, now);
           hierophant.NextBlink = now + GetFinalAttackCooldown(boss, hierophant, hierophant.BlinkCooldown, rage);
            MarkPressure(hierophant, now, "blink", false, target);
            hierophant.NextAttack = now + GetFinalAttackCooldown(boss, hierophant, hierophant.RangedCooldown, rage);
            return;
        }

        if (hierophant.NextChaser <= now && hierophant.Chasers.Count < hierophant.MaxActiveChasers)
        {
            SpawnChaser(hierophant, arena, gridUid, grid, bossTile.Value, target, rage, now, null);
            hierophant.NextChaser = now + hierophant.ChaserCooldown;

            if (hierophant.Chasers.Count < hierophant.MaxActiveChasers &&
                (_random.Prob(rage * 0.006f) || distance <= 2) &&
                _participants.Count > 1)
            {
                var secondTarget = PickSecondaryTarget(hierophant, target, gridUid, grid, now) ?? _random.Pick(_participants);
                SpawnChaser(hierophant, arena, gridUid, grid, bossTile.Value, secondTarget, rage, now, _random.Pick(Cardinals));
                MarkTargetPressure(hierophant, secondTarget, now);
            }

            MarkPressure(hierophant, now, "chaser", false, target);
            hierophant.NextAttack = now + GetFinalAttackCooldown(boss, hierophant, hierophant.RangedCooldown, rage);
            return;
        }

        if (distance > 2 &&
            hierophant.NextBlink <= now &&
            _random.Prob((25f + rage * 0.2f) / 100f))
        {
            QueueBlink(boss, hierophant, arena, gridUid, grid, bossTile.Value, targetTile.Value, now);
            hierophant.NextBlink = now + GetScaledCooldown(hierophant.BlinkCooldown, rage);
            MarkPressure(hierophant, now, "fallback-blink", false, target);
            hierophant.NextAttack = now + GetFinalAttackCooldown(boss, hierophant, hierophant.RangedCooldown, rage);
            return;
        }

        if (_random.Prob(Math.Max(5f, 70f - rage) / 100f))
        {
            if (_random.Prob(rage * 0.012f) && IsBelowHalfHealth(boss))
                QueueCross(hierophant, arena, gridUid, grid, targetTile.Value, AllDirections, hierophant.TelegraphPrototype, beamRange, now);
            else if (_random.Prob(0.6f))
                QueueCross(hierophant, arena, gridUid, grid, targetTile.Value, Cardinals, hierophant.TelegraphCardinalPrototype, beamRange, now);
            else
                QueueCross(hierophant, arena, gridUid, grid, targetTile.Value, Diagonals, hierophant.TelegraphDiagonalPrototype, beamRange, now);
        }
        else
        {
            QueueBurst(hierophant, arena, gridUid, grid, bossTile.Value, burstRange, now, 0.5f);
        }

        MarkPressure(hierophant, now, "pattern", false, target);
        hierophant.NextAttack = now + GetFinalAttackCooldown(boss, hierophant, hierophant.RangedCooldown, rage);
    }

    private bool TryRunMajorAttack(
        EntityUid boss,
        LavalandHierophantComponent hierophant,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        EntityUid target,
        float rage,
        int beamRange,
        TimeSpan now)
    {
        var choices = new List<string>();
        var crossCounter = Math.Min(hierophant.MaxCrossSpamCount, 1 + (int) MathF.Round(rage * 0.08f));
        var blinkCounter = Math.Min(hierophant.MaxBlinkSpamCount, 1 + (int) MathF.Round(rage * 0.06f));
        var targetTile = GetEntityTile(target, gridUid, grid);
        var bossTile = GetEntityTile(boss, gridUid, grid);

        if (targetTile == null || bossTile == null)
            return false;

        if (crossCounter > 1)
            choices.Add("cross_blast_spam");

        if (ChebyshevDistance(targetTile.Value, bossTile.Value) > 2)
            choices.Add("blink_spam");

        if (hierophant.NextChaser <= now && hierophant.Chasers.Count < hierophant.MaxActiveChasers)
            choices.Add("chaser_swarm");

        if (choices.Count == 0)
            return false;

        if (IsBelowCriticalHealth(boss) && choices.Contains("cross_blast_spam"))
        {
            QueueCrossSpam(hierophant, arena, gridUid, grid, target, Math.Max(2, crossCounter), beamRange, now);
            return true;
        }

        switch (_random.Pick(choices))
        {
            case "blink_spam":
                QueueBlinkSpam(boss, hierophant, arena, gridUid, grid, target, Math.Max(1, blinkCounter), now);
                hierophant.NextBlink = now + GetScaledCooldown(hierophant.BlinkCooldown, rage);
                return true;
            case "cross_blast_spam":
                QueueCrossSpam(hierophant, arena, gridUid, grid, target, Math.Max(2, crossCounter), beamRange, now);
                return true;
            case "chaser_swarm":
                QueueChaserSwarm(hierophant, arena, gridUid, grid, bossTile.Value, target, rage, now);
                hierophant.NextChaser = now + hierophant.ChaserCooldown;
                return true;
        }

        return false;
    }

    private void QueueCrossSpam(
        LavalandHierophantComponent hierophant,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        EntityUid target,
        int count,
        int beamRange,
        TimeSpan now)
    {
        hierophant.BusyUntil = now + TimeSpan.FromSeconds(0.6 + count * 0.6 + 0.8);

        for (var i = 0; i < count; i++)
        {
            var start = now + TimeSpan.FromSeconds(0.6 + i * 0.6);
            var attackTarget = i > 0
                ? PickSecondaryTarget(hierophant, target, gridUid, grid, start) ?? target
                : target;
            var tile = GetEntityTile(attackTarget, gridUid, grid);
            if (tile == null)
                return;

            var origin = tile.Value;
            if (i > 0 && _random.Prob(0.65f))
                origin = ClampToInnerArena(arena, origin + _random.Pick(Cardinals));

            if (_random.Prob(0.6f))
                QueueCross(hierophant, arena, gridUid, grid, origin, Cardinals, hierophant.TelegraphCardinalPrototype, beamRange, start);
            else
                QueueCross(hierophant, arena, gridUid, grid, origin, Diagonals, hierophant.TelegraphDiagonalPrototype, beamRange, start);

            MarkTargetPressure(hierophant, attackTarget, start);
        }
    }

    private void QueueBlinkSpam(
        EntityUid boss,
        LavalandHierophantComponent hierophant,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        EntityUid target,
        int count,
        TimeSpan now)
    {
        hierophant.BusyUntil = now + TimeSpan.FromSeconds(0.6 + count * 0.8 + 0.8);
        var source = GetEntityTile(boss, gridUid, grid);
        if (source == null)
            return;

        for (var i = 0; i < count; i++)
        {
            var blinkTarget = i > 0
                ? PickSecondaryTarget(hierophant, target, gridUid, grid, now + TimeSpan.FromSeconds(0.6 + i * 0.8)) ?? target
                : target;
            var targetTile = GetEntityTile(blinkTarget, gridUid, grid);
            if (targetTile == null)
                return;

            var destination = targetTile.Value + (i == 0 ? Vector2i.Zero : _random.Pick(AllDirections));
            destination = ClampToInnerArena(arena, destination);
            QueueBlink(boss, hierophant, arena, gridUid, grid, source.Value, destination, now + TimeSpan.FromSeconds(0.6 + i * 0.8));
            MarkTargetPressure(hierophant, blinkTarget, now + TimeSpan.FromSeconds(0.6 + i * 0.8));
            source = destination;
        }
    }

    private void QueueChaserSwarm(
        LavalandHierophantComponent hierophant,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i start,
        EntityUid primaryTarget,
        float rage,
        TimeSpan now)
    {
        var availableChasers = Math.Max(0, hierophant.MaxActiveChasers - hierophant.Chasers.Count);
        var chaserCount = Math.Min(hierophant.ChaserSwarmCount, availableChasers);
        if (chaserCount <= 0)
            return;

        hierophant.BusyUntil = now + TimeSpan.FromSeconds(0.6 + chaserCount * 0.8 + 0.8);
        var directions = new List<Vector2i>(Cardinals);

        for (var i = 0; i < chaserCount && directions.Count > 0; i++)
        {
            var target = i == 0
                ? primaryTarget
                : PickSecondaryTarget(hierophant, primaryTarget, gridUid, grid, now + TimeSpan.FromSeconds(0.6 + i * 0.8)) ?? _random.Pick(_participants);
            if (!target.Valid)
                return;

            var dir = _random.PickAndTake(directions);
            SpawnChaser(hierophant, arena, gridUid, grid, start, target, rage, now + TimeSpan.FromSeconds(0.6 + i * 0.8), dir);
            MarkTargetPressure(hierophant, target, now + TimeSpan.FromSeconds(0.6 + i * 0.8));
        }
    }

    private void QueueCross(
        LavalandHierophantComponent hierophant,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i origin,
        IReadOnlyList<Vector2i> directions,
        string telegraphPrototype,
        int beamRange,
        TimeSpan now)
    {
        var wallTelegraphAt = now + hierophant.CrossTelegraphDelay;
        var detonateAt = wallTelegraphAt + hierophant.BlastDamageDelay;
        PlayTile(hierophant.CrossTelegraphSound, gridUid, grid, origin, -2f);
        QueueBlast(hierophant, arena, gridUid, grid, origin, telegraphPrototype, now, detonateAt, hierophant.BlastDamage);

        foreach (var direction in directions)
        {
            for (var i = 1; i <= beamRange; i++)
            {
                QueueBlast(hierophant, arena, gridUid, grid, origin + direction * i, hierophant.SquaresPrototype, wallTelegraphAt, detonateAt, hierophant.BlastDamage);
            }
        }
    }

    private void QueueBlink(
        EntityUid boss,
        LavalandHierophantComponent hierophant,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i source,
        Vector2i destination,
        TimeSpan now)
    {
        if (!IsInsideInnerArena(arena, destination))
            destination = ClampToInnerArena(arena, destination);

        SpawnAnchored(hierophant.TelegraphTeleportPrototype, gridUid, grid, source);
        SpawnAnchored(hierophant.TelegraphTeleportPrototype, gridUid, grid, destination);
        PlayTile(hierophant.BlinkSourceSound, gridUid, grid, source, -1f);
        PlayTile(hierophant.BlinkDestinationSound, gridUid, grid, destination, -1f);

        var executeAt = now + hierophant.BlinkDelay;
        var detonateAt = executeAt + hierophant.BlastDamageDelay;
        QueueRadiusBlasts(hierophant, arena, gridUid, grid, source, hierophant.BlinkBlastRadius, executeAt, detonateAt, hierophant.SquaresPrototype, hierophant.BlinkDamage);
        QueueRadiusBlasts(hierophant, arena, gridUid, grid, destination, hierophant.BlinkBlastRadius, executeAt, detonateAt, hierophant.SquaresPrototype, hierophant.BlinkDamage);

        hierophant.PendingTeleports.Add(new LavalandHierophantPendingTeleport
        {
            Boss = boss,
            Grid = gridUid,
            Destination = destination,
            ExecuteAt = executeAt,
        });

        hierophant.BusyUntil = TimeSpan.FromTicks(Math.Max(hierophant.BusyUntil.Ticks, (executeAt + TimeSpan.FromSeconds(0.4)).Ticks));
    }

    private void QueueBurst(
        LavalandHierophantComponent hierophant,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i origin,
        int range,
        TimeSpan now,
        float spreadMultiplier)
    {
        var stepDelay = TimeSpan.FromSeconds(Math.Max(0.02, hierophant.BurstStepDelay.TotalSeconds * spreadMultiplier));
        PlayTile(hierophant.BurstSound, gridUid, grid, origin, -2f);

        for (var distance = 0; distance <= range; distance++)
        {
            var detonateAt = now + TimeSpan.FromTicks(stepDelay.Ticks * distance);
            for (var x = -distance; x <= distance; x++)
            {
                for (var y = -distance; y <= distance; y++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) != distance)
                        continue;

                    QueueBlast(hierophant, arena, gridUid, grid, origin + new Vector2i(x, y), hierophant.SquaresPrototype, detonateAt, detonateAt + hierophant.BlastDamageDelay, hierophant.BlastDamage);
                }
            }
        }
    }

    private void QueueRadiusBlasts(
        LavalandHierophantComponent hierophant,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i origin,
        int radius,
        TimeSpan telegraphAt,
        TimeSpan detonateAt,
        string telegraphPrototype,
        DamageSpecifier damage)
    {
        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                QueueBlast(hierophant, arena, gridUid, grid, origin + new Vector2i(x, y), telegraphPrototype, telegraphAt, detonateAt, damage);
            }
        }
    }

    private void QueueBlast(
        LavalandHierophantComponent hierophant,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        string? telegraphPrototype,
        TimeSpan? telegraphAt,
        TimeSpan detonateAt,
        DamageSpecifier damage)
    {
        if (!IsInsideInnerArena(arena, tile) ||
            hierophant.PendingBlasts.Count >= Math.Max(0, hierophant.MaxPendingBlasts) ||
            HasSimilarPendingBlast(hierophant, gridUid, tile, detonateAt) ||
            !_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) ||
            tileRef.Tile.IsEmpty)
        {
            return;
        }

        hierophant.PendingBlasts.Add(new LavalandHierophantPendingBlast
        {
            Grid = gridUid,
            Tile = tile,
            DetonateAt = detonateAt,
            TelegraphAt = telegraphAt ?? TimeSpan.Zero,
            TelegraphPrototype = telegraphPrototype,
            Telegraphed = telegraphPrototype == null,
            BlastPrototype = hierophant.BlastPrototype,
            Damage = new(damage),
        });
    }

    private static bool HasSimilarPendingBlast(
        LavalandHierophantComponent hierophant,
        EntityUid gridUid,
        Vector2i tile,
        TimeSpan detonateAt)
    {
        var duplicateWindow = Math.Max(0, hierophant.PendingBlastDuplicateWindow.TotalSeconds);
        foreach (var pending in hierophant.PendingBlasts)
        {
            if (pending.Grid != gridUid ||
                pending.Tile != tile)
            {
                continue;
            }

            if (Math.Abs((pending.DetonateAt - detonateAt).TotalSeconds) <= duplicateWindow)
                return true;
        }

        return false;
    }

    private void SpawnChaser(
        LavalandHierophantComponent hierophant,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i start,
        EntityUid target,
        float rage,
        TimeSpan now,
        Vector2i? initialDirection)
    {
        if (!IsInsideInnerArena(arena, start) ||
            IsDead(target) ||
            hierophant.Chasers.Count >= hierophant.MaxActiveChasers)
        {
            return;
        }

        var stepDelay = TimeSpan.FromSeconds(Math.Max(0.1, hierophant.ChaserStepDelay.TotalSeconds - rage * 0.004));
        hierophant.Chasers.Add(new LavalandHierophantChaser
        {
            Grid = gridUid,
            Target = target,
            Tile = start,
            MovingDirection = initialDirection ?? Vector2i.Zero,
            StepsBeforeRepath = initialDirection == null ? 0 : 3,
            NextStep = now,
            ExpiresAt = now + hierophant.ChaserLifetime,
            StepDelay = stepDelay,
        });
    }

    private void ProcessChasers(
        EntityUid boss,
        LavalandHierophantComponent hierophant,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        for (var i = hierophant.Chasers.Count - 1; i >= 0; i--)
        {
            var chaser = hierophant.Chasers[i];
            if (chaser.ExpiresAt <= now ||
                chaser.Grid != gridUid ||
                IsDead(chaser.Target) ||
                GetEntityTile(chaser.Target, gridUid, grid) == null)
            {
                hierophant.Chasers.RemoveAt(i);
                continue;
            }

            if (chaser.NextStep > now)
                continue;

            if (chaser.StepsBeforeRepath <= 0 || chaser.MovingDirection == Vector2i.Zero)
            {
                chaser.OlderDirection = chaser.PreviousDirection;
                chaser.PreviousDirection = chaser.MovingDirection;
                chaser.MovingDirection = GetChaserDirection(chaser, gridUid, grid);
                chaser.StepsBeforeRepath = hierophant.ChaserRepathSteps;
            }

            var nextTile = chaser.Tile + chaser.MovingDirection;
            if (!IsInsideInnerArena(arena, nextTile))
            {
                chaser.StepsBeforeRepath = 0;
                chaser.MovingDirection = Vector2i.Zero;
                chaser.NextStep = now + chaser.StepDelay;
                continue;
            }

            chaser.Tile = nextTile;
            chaser.StepsBeforeRepath--;
            chaser.NextStep = now + chaser.StepDelay;

            QueueBlast(hierophant, arena, gridUid, grid, chaser.Tile, hierophant.SquaresPrototype, now, now + hierophant.BlastDamageDelay, hierophant.BlastDamage);
        }
    }

    private Vector2i GetChaserDirection(LavalandHierophantChaser chaser, EntityUid gridUid, MapGridComponent grid)
    {
        var targetTile = GetEntityTile(chaser.Target, gridUid, grid);
        if (targetTile == null)
            return _random.Pick(Cardinals);

        var delta = targetTile.Value - chaser.Tile;
        var direction = Math.Abs(delta.X) > Math.Abs(delta.Y)
            ? new Vector2i(Math.Sign(delta.X), 0)
            : new Vector2i(0, Math.Sign(delta.Y));

        if (direction == Vector2i.Zero ||
            (direction != chaser.PreviousDirection && direction == chaser.OlderDirection))
        {
            var choices = new List<Vector2i>(Cardinals);
            choices.Remove(chaser.OlderDirection);
            direction = _random.Pick(choices);
        }

        return direction;
    }

    private void ProcessTeleports(
        LavalandHierophantComponent hierophant,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        for (var i = hierophant.PendingTeleports.Count - 1; i >= 0; i--)
        {
            var pending = hierophant.PendingTeleports[i];
            if (pending.ExecuteAt > now)
                continue;

            if (pending.Grid == gridUid &&
                Exists(pending.Boss) &&
                TryComp(pending.Boss, out TransformComponent? xform))
            {
                var destination = ClampToInnerArena(arena, pending.Destination);
                _transform.SetCoordinates(pending.Boss, _map.GridTileToLocal(gridUid, grid, destination));
                var destTile = _map.LocalToTile(gridUid, grid, _map.GridTileToLocal(gridUid, grid, pending.Destination));
                SpawnAnchored(hierophant.TelegraphTeleportPrototype, gridUid, grid, destTile);
                _transform.SetLocalRotation(pending.Boss, xform.LocalRotation);
            }

            hierophant.PendingTeleports.RemoveAt(i);
        }
    }

    private void ProcessPendingBlasts(
        EntityUid boss,
        LavalandHierophantComponent hierophant,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        if (hierophant.PendingBlasts.Count == 0)
            return;

        _detonatingTiles.Clear();
        _detonatingDamage.Clear();
        Vector2i? soundTile = null;

        for (var i = hierophant.PendingBlasts.Count - 1; i >= 0; i--)
        {
            var pending = hierophant.PendingBlasts[i];
            if (pending.Grid == gridUid &&
                !pending.Telegraphed &&
                pending.TelegraphPrototype != null &&
                pending.TelegraphAt <= now)
            {
                SpawnAnchored(pending.TelegraphPrototype, gridUid, grid, pending.Tile);
                pending.Telegraphed = true;
            }

            if (pending.DetonateAt > now)
                continue;

            if (pending.Grid == gridUid)
            {
                SpawnAnchored(pending.BlastPrototype, gridUid, grid, pending.Tile);
                soundTile ??= pending.Tile;
                _detonatingTiles.Add(pending.Tile);
                _detonatingDamage[pending.Tile] = pending.Damage;
            }

            hierophant.PendingBlasts.RemoveAt(i);
        }

        if (_detonatingTiles.Count == 0)
            return;

        if (soundTile != null)
            PlayTile(hierophant.BlastSound, gridUid, grid, soundTile.Value, -3f);

        DamageDetonatingTiles(boss, hierophant, gridUid, grid);
        _detonatingTiles.Clear();
        _detonatingDamage.Clear();
    }

    private void DamageDetonatingTiles(
        EntityUid boss,
        LavalandHierophantComponent hierophant,
        EntityUid gridUid,
        MapGridComponent grid)
    {
        _damagedEntities.Clear();

        foreach (var tile in _detonatingTiles)
        {
            if (!_detonatingDamage.TryGetValue(tile, out var damage))
                continue;

            _tileEntities.Clear();
            _lookup.GetLocalEntitiesIntersecting(
                gridUid,
                tile,
                _tileEntities,
                flags: LookupFlags.Dynamic | LookupFlags.Sundries,
                gridComp: grid);

            foreach (var uid in _tileEntities)
            {
                if (uid == boss ||
                    !_damagedEntities.Add(uid) ||
                    !TryComp(uid, out DamageableComponent? damageable) ||
                    !TryComp(uid, out MobStateComponent? mobState) ||
                    mobState.CurrentState == MobState.Dead ||
                    !TryComp(uid, out TransformComponent? xform) ||
                    xform.GridUid != gridUid ||
                    _map.LocalToTile(gridUid, grid, xform.Coordinates) != tile)
                {
                    continue;
                }

                _damageable.TryChangeDamage((uid, damageable), damage, origin: boss);
                _audio.PlayPvs(hierophant.HitSound, uid, AudioParams.Default.WithVolume(-4f));
            }
        }

        _tileEntities.Clear();
        _damagedEntities.Clear();
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
        LavalandHierophantComponent hierophant,
        EntityUid boss,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        if (_participants.Count == 0)
            return null;

        PruneTargetMemory(hierophant);

        if (_participants.Count == 1)
        {
            SetPrimaryTarget(hierophant, _participants[0], now);
            return _participants[0];
        }

        if (hierophant.CurrentPrimaryTarget is { Valid: true } current &&
            _participants.Contains(current) &&
            now - hierophant.LastTargetSwitchAt < hierophant.TargetSwitchCooldown)
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

            var score = ScoreTarget(hierophant, participant, bossTile, tile.Value, now, true);
            if (score <= bestScore)
                continue;

            best = participant;
            bestScore = score;
        }

        if (best == null)
            best = _random.Pick(_participants);

        SetPrimaryTarget(hierophant, best.Value, now);
        return best;
    }

    private EntityUid? PickSecondaryTarget(
        LavalandHierophantComponent hierophant,
        EntityUid excluded,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        if (_participants.Count <= 1)
            return null;

        PruneTargetMemory(hierophant);

        EntityUid? best = null;
        var bestScore = float.MinValue;
        foreach (var participant in _participants)
        {
            if (participant == excluded)
                continue;

            var tile = GetEntityTile(participant, gridUid, grid);
            if (tile == null)
                continue;

            var score = ScoreTarget(hierophant, participant, null, tile.Value, now, false);
            if (score <= bestScore)
                continue;

            best = participant;
            bestScore = score;
        }

        return best;
    }

    private float ScoreTarget(
        LavalandHierophantComponent hierophant,
        EntityUid target,
        Vector2i? bossTile,
        Vector2i targetTile,
        TimeSpan now,
        bool applyCurrentPenalty)
    {
        var safeSeconds = hierophant.TargetPressureMemory.TotalSeconds;
        if (hierophant.LastPressureByTarget.TryGetValue(target, out var lastPressure))
            safeSeconds = Math.Clamp((now - lastPressure).TotalSeconds, 0, hierophant.TargetPressureMemory.TotalSeconds);

        var distancePenalty = bossTile == null
            ? 0f
            : ChebyshevDistance(bossTile.Value, targetTile) * 0.2f;
        var score = (float) safeSeconds - distancePenalty + _random.NextFloat(0f, 1.5f);

        if (applyCurrentPenalty &&
            hierophant.CurrentPrimaryTarget == target &&
            now - hierophant.LastTargetSwitchAt >= hierophant.TargetSwitchCooldown)
        {
            score -= 8f;
        }

        return score;
    }

    private void PruneTargetMemory(LavalandHierophantComponent hierophant)
    {
        if (hierophant.CurrentPrimaryTarget is { Valid: true } current &&
            !_participants.Contains(current))
        {
            hierophant.CurrentPrimaryTarget = null;
        }

        if (hierophant.LastPressureByTarget.Count == 0)
            return;

        foreach (var target in new List<EntityUid>(hierophant.LastPressureByTarget.Keys))
        {
            if (!_participants.Contains(target))
                hierophant.LastPressureByTarget.Remove(target);
        }
    }

    private static void SetPrimaryTarget(LavalandHierophantComponent hierophant, EntityUid target, TimeSpan now)
    {
        if (hierophant.CurrentPrimaryTarget == target)
            return;

        hierophant.CurrentPrimaryTarget = target;
        hierophant.LastTargetSwitchAt = now;
    }

    private float CalculateRage(EntityUid boss)
    {
        if (!TryComp<LavalandBossComponent>(boss, out var bossComp) ||
            !TryComp<DamageableComponent>(boss, out var damageable))
        {
            return 0f;
        }

        var missing = Math.Max(0f, damageable.TotalDamage.Float());
        return Math.Clamp(missing / 42f, 0f, 50f);
    }

    private int GetBurstRange(LavalandHierophantComponent hierophant, float rage)
    {
        return Math.Clamp(hierophant.BaseBurstRange + (int) MathF.Round(rage * 0.08f), 1, Math.Max(1, hierophant.MaxBurstRange));
    }

    private int GetBeamRange(LavalandHierophantComponent hierophant, float rage)
    {
        return Math.Clamp(hierophant.BaseBeamRange + (int) MathF.Round(rage * 0.12f), 1, Math.Max(1, hierophant.MaxBeamRange));
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
        return TimeSpan.FromSeconds(Math.Max(1.45, baseCooldown.TotalSeconds - rage * 0.045));
    }

    private Vector2i? GetEntityTile(EntityUid uid, EntityUid gridUid, MapGridComponent grid)
    {
        if (!TryComp(uid, out TransformComponent? xform) ||
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

    private void SpawnAnchored(string prototype, EntityUid gridUid, MapGridComponent grid, Vector2i index)
    {
        var uid = Spawn(prototype, _map.GridTileToLocal(gridUid, grid, index));
        if (!TryComp(uid, out TransformComponent? xform) || xform.Anchored)
            return;

        _transform.AnchorEntity((uid, xform), (gridUid, grid), index);
    }

    private void PlayTile(SoundSpecifier sound, EntityUid gridUid, MapGridComponent grid, Vector2i index, float volume = 0f)
    {
        _audio.PlayPvs(sound, _map.GridTileToLocal(gridUid, grid, index), AudioParams.Default.WithVolume(volume));
    }

    private void ClearRuntimeState(LavalandHierophantComponent hierophant)
    {
        hierophant.PendingBlasts.Clear();
        hierophant.PendingTeleports.Clear();
        hierophant.Chasers.Clear();
        hierophant.CurrentPrimaryTarget = null;
        hierophant.LastTargetSwitchAt = TimeSpan.Zero;
        hierophant.LastPressureByTarget.Clear();
    }

    private static bool NeedsPressure(LavalandHierophantComponent hierophant, TimeSpan now)
    {
        return hierophant.LastPressureAt == TimeSpan.Zero ||
               now - hierophant.LastPressureAt >= hierophant.ForcePressureAfter;
    }

    private static void MarkPressure(LavalandHierophantComponent hierophant, TimeSpan now, string attackKind, bool major, EntityUid target)
    {
        hierophant.LastPressureAt = now;
        hierophant.LastAttackKind = attackKind;
        MarkTargetPressure(hierophant, target, now);
        hierophant.BasicAttacksSinceMajor = major
            ? 0
            : hierophant.BasicAttacksSinceMajor + 1;
    }

    private static void MarkTargetPressure(LavalandHierophantComponent hierophant, EntityUid target, TimeSpan now)
    {
        if (!target.Valid)
            return;

        hierophant.LastPressureByTarget[target] = now;
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
    private bool IsBelowCriticalHealth(EntityUid boss)
    {
        if (!TryComp<LavalandBossComponent>(boss, out var bossComp) ||
            !TryComp<DamageableComponent>(boss, out var damageable))
            return false;

        return damageable.TotalDamage.Float() >= bossComp.MaxHealth * 0.75f;
    }

    private void TryFireCriticalBurst(
        EntityUid boss,
        LavalandHierophantComponent hierophant,
        LavalandBossArenaComponent arena,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        if (!IsBelowCriticalHealth(boss) || now < hierophant.NextCriticalBurst)
            return;

        hierophant.NextCriticalBurst = now + TimeSpan.FromSeconds(1.2);

        var bossTile = GetEntityTile(boss, gridUid, grid);
        if (bossTile == null)
            return;

        var rage = CalculateRage(boss);
        QueueBurst(hierophant, arena, gridUid, grid, bossTile.Value, GetBurstRange(hierophant, rage), now, 0.35f);

        var beamRange = GetBeamRange(hierophant, rage);
        foreach (var participant in _participants)
        {
            var targetTile = GetEntityTile(participant, gridUid, grid);
            if (targetTile == null)
                continue;

            QueueCross(hierophant, arena, gridUid, grid, targetTile.Value, AllDirections, hierophant.TelegraphPrototype, beamRange, now + TimeSpan.FromSeconds(0.5));
        }
    }

    private TimeSpan GetFinalAttackCooldown(
        EntityUid boss,
        LavalandHierophantComponent hierophant,
        TimeSpan baseCooldown,
        float rage)
    {
        if (IsBelowCriticalHealth(boss))
            return hierophant.CriticalPhaseCooldown;
    
        return GetScaledCooldown(baseCooldown, rage);
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
        var origin = arena.Origin;
        return (origin.X - halfWidth + 1, origin.X + halfWidth - 1, origin.Y - halfHeight + 1, origin.Y + halfHeight - 1);
    }
}
