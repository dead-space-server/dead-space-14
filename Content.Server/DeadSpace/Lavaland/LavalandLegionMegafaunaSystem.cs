// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.NPC.HTN;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Ghost;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Legion megafauna. Summons skulls, charges and raises sentinels; each body splits into three smaller
/// ones when it runs out of health (large -> 3 medium -> 3 small each). The arena boss role moves to a
/// surviving body, so the arena fight and its rewards end only with the last skull.
/// </summary>
public sealed class LavalandLegionMegafaunaSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private readonly List<Vector2i> _freeTiles = new();
    private readonly List<EntityUid> _bodies = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandLegionMegafaunaComponent, MapInitEvent>(OnMapInit);
        // Must run before the mob thresholds so a body splits instead of dying normally.
        SubscribeLocalEvent<LavalandLegionMegafaunaComponent, DamageChangedEvent>(OnDamageChanged,
            before: [typeof(MobThresholdSystem)]);
        SubscribeLocalEvent<LavalandLegionMegafaunaComponent, LavalandBossHudHealthEvent>(OnHudHealth);
        SubscribeLocalEvent<LavalandLegionMegafaunaComponent, LavalandBossResetEvent>(OnBossReset);
    }

    private void OnMapInit(Entity<LavalandLegionMegafaunaComponent> ent, ref MapInitEvent args)
    {
        if (!ent.Comp.Root.IsValid())
            ent.Comp.Root = ent.Owner;
        ent.Comp.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(1);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandLegionMegafaunaComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var legion, out var xform))
        {
            if (legion.Dying || _mobState.IsDead(uid))
                continue;

            if (TryComp<LavalandBossComponent>(uid, out var boss))
                legion.Arena = boss.Arena;

            if (!TryGetArena(legion, out var arena) || !arena.FightStarted || arena.Ended)
                continue;

            if (UpdateCharge(uid, legion, xform, arena, now))
                continue;

            if (now < legion.NextAttack || !TryFindTarget(xform, legion, arena, out var target))
                continue;

            legion.NextAttack = now + legion.AttackCooldown;
            switch (_random.Next(5))
            {
                case 3:
                    StartCharge(uid, legion, now);
                    break;
                case 4:
                    SpawnSentinels(uid, legion, xform);
                    break;
                default:
                    Spawn(legion.SkullPrototype, xform.Coordinates);
                    break;
            }
        }
    }

    #region Attacks

    private void StartCharge(EntityUid uid, LavalandLegionMegafaunaComponent legion, TimeSpan now)
    {
        legion.ChargeStartAt = now + legion.ChargeWindup;
        _popup.PopupEntity(Loc.GetString("lavaland-legion-charge-windup", ("legion", uid)), uid, PopupType.LargeCaution);
        if (legion.ChargeWindupSound != null)
            _audio.PlayPvs(legion.ChargeWindupSound, uid);
    }

    /// <summary>Returns true while the body is busy charging.</summary>
    private bool UpdateCharge(EntityUid uid,
        LavalandLegionMegafaunaComponent legion,
        TransformComponent xform,
        LavalandBossArenaComponent arena,
        TimeSpan now)
    {
        if (legion.ChargeStartAt is { } start)
        {
            if (now < start)
                return true;

            legion.ChargeStartAt = null;
            if (!TryFindTarget(xform, legion, arena, out var target))
                return false;

            var offset = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(xform);
            if (offset.LengthSquared() < 0.01f)
                return false;

            legion.ChargeDirection = Vector2.Normalize(offset);
            legion.ChargeEndAt = now + legion.ChargeDuration;
            legion.ChargeHit.Clear();
            SetAi(uid, false);
        }

        if (legion.ChargeEndAt is not { } end)
            return false;

        if (!TryComp<PhysicsComponent>(uid, out var physics))
            return true;

        if (now >= end)
        {
            legion.ChargeEndAt = null;
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
            SetAi(uid, true);
            return false;
        }

        _physics.SetLinearVelocity(uid, legion.ChargeDirection * legion.ChargeSpeed, body: physics);

        var position = _transform.GetWorldPosition(xform);
        var damage = legion.ChargeDamage * (legion.Size / 2f);
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } player ||
                legion.ChargeHit.Contains(player) ||
                !IsValidTarget(player, xform.MapID) ||
                Vector2.DistanceSquared(position, _transform.GetWorldPosition(player)) >
                legion.ChargeHitRange * legion.ChargeHitRange)
            {
                continue;
            }

            legion.ChargeHit.Add(player);
            _damageable.TryChangeDamage(player, damage, origin: uid);
            if (TryComp<PhysicsComponent>(player, out var victim))
                _physics.SetLinearVelocity(player, legion.ChargeDirection * legion.ChargeKnockback, body: victim);
            if (legion.ChargeImpactSound != null)
                _audio.PlayPvs(legion.ChargeImpactSound, player);
        }

        return true;
    }

    private void SpawnSentinels(EntityUid uid, LavalandLegionMegafaunaComponent legion, TransformComponent xform)
    {
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        _freeTiles.Clear();
        var center = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var radius = (int) MathF.Ceiling(legion.SentinelSpawnRadius);
        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                if (x == 0 && y == 0 || x * x + y * y > legion.SentinelSpawnRadius * legion.SentinelSpawnRadius)
                    continue;

                var tile = center + new Vector2i(x, y);
                if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty ||
                    _turf.IsTileBlocked(gridUid, tile, CollisionGroup.Impassable, grid))
                {
                    continue;
                }

                _freeTiles.Add(tile);
            }
        }

        var count = Math.Min(_random.Next(2, Math.Max(2, legion.Size * 2) + 1), _freeTiles.Count);
        for (var i = 0; i < count; i++)
        {
            var index = _random.Next(_freeTiles.Count);
            var tile = _freeTiles[index];
            _freeTiles.RemoveAt(index);
            Spawn(legion.SentinelPrototype, _map.GridTileToLocal(gridUid, grid, tile));
        }

        if (count > 0 && legion.SentinelSound != null)
            _audio.PlayPvs(legion.SentinelSound, uid);
    }

    private bool TryFindTarget(TransformComponent xform,
        LavalandLegionMegafaunaComponent legion,
        LavalandBossArenaComponent arena,
        out EntityUid target)
    {
        target = default;
        var best = legion.AggroRange * legion.AggroRange;
        var position = _transform.GetWorldPosition(xform);
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } player ||
                !IsValidTarget(player, xform.MapID) ||
                Transform(player).GridUid != arena.Grid)
            {
                continue;
            }

            var distance = Vector2.DistanceSquared(position, _transform.GetWorldPosition(player));
            if (distance > best)
                continue;

            best = distance;
            target = player;
        }

        return target.IsValid();
    }

    private bool IsValidTarget(EntityUid player, MapId map)
    {
        return !TerminatingOrDeleted(player) &&
               !HasComp<GhostComponent>(player) &&
               HasComp<MobStateComponent>(player) &&
               _mobState.IsAlive(player) &&
               Transform(player).MapID == map;
    }

    #endregion

    #region Splitting

    private void OnDamageChanged(Entity<LavalandLegionMegafaunaComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.Dying || !args.DamageIncreased)
            return;

        var scale = GetHealthScale(ent.Comp);
        if (args.Damageable.TotalDamage.Float() < ent.Comp.BodyHealth * scale)
            return;

        KillBody(ent);
    }

    private void KillBody(Entity<LavalandLegionMegafaunaComponent> ent)
    {
        ent.Comp.Dying = true;
        var coordinates = Transform(ent).Coordinates;
        var now = _timing.CurTime;
        EntityUid? firstChild = null;

        foreach (var proto in ent.Comp.SplitInto)
        {
            var offset = _random.NextVector2(0.6f);
            var child = Spawn(proto, coordinates.Offset(offset));
            var childComp = EnsureComp<LavalandLegionMegafaunaComponent>(child);
            childComp.Root = ent.Comp.Root;
            childComp.Arena = ent.Comp.Arena;
            childComp.NextAttack = now + TimeSpan.FromSeconds(_random.NextFloat(0.8f, 1.6f));
            firstChild ??= child;
        }

        var successor = firstChild ?? FindAliveBody(ent.Comp.Root, ent.Owner);
        if (successor == null)
        {
            // The last skull: let the regular boss death run (arena end, death rewards).
            ent.Comp.Dying = false;
            _mobState.ChangeMobState(ent, MobState.Dead);
            return;
        }

        if (HasComp<LavalandBossComponent>(ent))
            TransferBossRole(ent.Owner, successor.Value);

        if (firstChild == null &&
            ent.Comp.PieceReward is { } reward &&
            _random.Prob(Math.Clamp(ent.Comp.PieceRewardChance, 0f, 1f)))
        {
            Spawn(reward, coordinates);
        }

        QueueDel(ent);
    }

    private EntityUid? FindAliveBody(EntityUid root, EntityUid except)
    {
        var query = EntityQueryEnumerator<LavalandLegionMegafaunaComponent>();
        while (query.MoveNext(out var uid, out var legion))
        {
            if (uid != except && legion.Root == root && !legion.Dying && !_mobState.IsDead(uid))
                return uid;
        }
        return null;
    }

    private void TransferBossRole(EntityUid from, EntityUid to)
    {
        if (!TryComp<LavalandBossComponent>(from, out var old))
            return;

        var boss = EnsureComp<LavalandBossComponent>(to);
        boss.BossName = old.BossName;
        boss.MaxHealth = old.MaxHealth;
        boss.Music = old.Music;
        boss.MusicPool = new List<Robust.Shared.Audio.SoundSpecifier>(old.MusicPool);
        boss.SelectedMusic = old.SelectedMusic;
        boss.DeathSound = old.DeathSound;
        boss.DeathRewards = new(old.DeathRewards);
        boss.DeathRewardsSpawned = old.DeathRewardsSpawned;
        boss.Arena = old.Arena;

        if (old.Arena is { } arenaUid && TryComp<LavalandBossArenaComponent>(arenaUid, out var arena))
            arena.Boss = to;

        RemComp<LavalandBossComponent>(from);
    }

    private void OnHudHealth(Entity<LavalandLegionMegafaunaComponent> ent, ref LavalandBossHudHealthEvent args)
    {
        var total = 0f;
        var query = EntityQueryEnumerator<LavalandLegionMegafaunaComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var legion, out var damageable))
        {
            if (legion.Root != ent.Comp.Root || legion.Dying || _mobState.IsDead(uid))
                continue;

            var bodyHealth = legion.BodyHealth * args.HealthScale;
            total += Math.Max(0f, bodyHealth - damageable.TotalDamage.Float()) + legion.SplitHealthPool * args.HealthScale;
        }

        args.Current = Math.Clamp(total, 0f, args.Max);
    }

    private void OnBossReset(Entity<LavalandLegionMegafaunaComponent> ent, ref LavalandBossResetEvent args)
    {
        // Nobody is left in the arena: gather Legion back into one large body.
        _bodies.Clear();
        var query = EntityQueryEnumerator<LavalandLegionMegafaunaComponent>();
        while (query.MoveNext(out var uid, out var legion))
        {
            if (uid != ent.Owner && legion.Root == ent.Comp.Root)
                _bodies.Add(uid);
        }

        foreach (var body in _bodies)
        {
            Comp<LavalandLegionMegafaunaComponent>(body).Dying = true;
            QueueDel(body);
        }

        ent.Comp.ChargeStartAt = null;
        ent.Comp.ChargeEndAt = null;
        if (ent.Comp.Size >= 3 || ent.Comp.ReassemblePrototype is not { } prototype)
            return;

        var root = Spawn(prototype, Transform(ent).Coordinates);
        var rootComp = EnsureComp<LavalandLegionMegafaunaComponent>(root);
        rootComp.Root = root;
        rootComp.Arena = ent.Comp.Arena;
        TransferBossRole(ent.Owner, root);
        SetAi(root, false);
        ent.Comp.Dying = true;
        QueueDel(ent);
    }

    private float GetHealthScale(LavalandLegionMegafaunaComponent legion)
    {
        return TryGetArena(legion, out var arena) && arena.MaxHealth > 0f
            ? Math.Max(1f, arena.ScaledMaxHealth / arena.MaxHealth)
            : 1f;
    }

    #endregion

    private bool TryGetArena(LavalandLegionMegafaunaComponent legion, out LavalandBossArenaComponent arena)
    {
        arena = default!;
        return legion.Arena is { } arenaUid && TryComp(arenaUid, out arena!);
    }

    private void SetAi(EntityUid uid, bool enabled)
    {
        if (TryComp<HTNComponent>(uid, out var htn))
            _htn.SetHTNEnabled((uid, htn), enabled);
    }
}
