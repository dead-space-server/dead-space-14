// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.NPC.HTN;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Lavaland.Bosses;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Ghost;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.MouseRotator;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
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
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly LavalandNecropolisGateSystem _gates = default!;
    [Dependency] private readonly LavalandBossArenaSystem _arenas = default!;
    [Dependency] private readonly LavalandFaunaRaidSystem _raids = default!;
    [Dependency] private readonly Content.Shared.Jittering.SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly LavalandLegionHandsSystem _hands = default!;
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
    private readonly HashSet<Entity<LavalandNecropolisGateComponent>> _nearGates = new();

    private static readonly TimeSpan FreeHudInterval = TimeSpan.FromSeconds(0.5);

    /// <summary>Players this close to any body of a free Legion get its health bar and music.</summary>
    private const float FreeHudRange = 40f;

    private int _nextHudId = 300000;

    /// <summary>Health bar and music of each free Legion, by its root (outlives the first body).</summary>
    private readonly Dictionary<EntityUid, FreeLegionHud> _freeHuds = new();
    private readonly Dictionary<EntityUid, List<EntityUid>> _bodiesByRoot = new();

    private sealed class FreeLegionHud
    {
        public int Id;
        public string Name = string.Empty;
        public float MaxHealth;
        public ResolvedSoundSpecifier? Music;
        public AudioParams MusicParams;
        public TimeSpan NextUpdate;
        public readonly HashSet<ICommonSession> Recipients = new();
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandLegionMegafaunaComponent, MapInitEvent>(OnMapInit);
        // Must run before the mob thresholds so a body splits instead of dying normally.
        SubscribeLocalEvent<LavalandLegionMegafaunaComponent, DamageChangedEvent>(OnDamageChanged,
            before: [typeof(MobThresholdSystem)]);
        SubscribeLocalEvent<LavalandLegionMegafaunaComponent, LavalandBossHudHealthEvent>(OnHudHealth);
        SubscribeLocalEvent<LavalandLegionMegafaunaComponent, LavalandBossResetEvent>(OnBossReset);
        SubscribeLocalEvent<LavalandLegionMegafaunaComponent, LavalandBossFightStartedEvent>(OnFightStarted);
    }

    private void OnMapInit(Entity<LavalandLegionMegafaunaComponent> ent, ref MapInitEvent args)
    {
        if (!ent.Comp.Root.IsValid())
            ent.Comp.Root = ent.Owner;
        ent.Comp.Home ??= _transform.GetMapCoordinates(ent.Owner);
        ent.Comp.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(1);
    }

    private void OnFightStarted(Entity<LavalandLegionMegafaunaComponent> ent, ref LavalandBossFightStartedEvent args)
    {
        // Players at the tower do not wake the Legion: it waits behind its gate until the gate opens.
        if (ent.Comp.Dormant)
            SetAi(ent, false);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        UpdateFreeHuds(now);

        var query = EntityQueryEnumerator<LavalandLegionMegafaunaComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var legion, out var xform))
        {
            if (legion.Dying || _mobState.IsDead(uid))
                continue;

            if (TryComp<LavalandBossComponent>(uid, out var boss))
                legion.Arena = boss.Arena;
            if (!legion.Free && HasComp<LavalandFreeBossComponent>(uid))
                legion.Free = true;

            // Back home from a raid: asleep until a player comes close.
            if (legion.WaitingAtHome)
            {
                WakeAtHomeIfApproached((uid, legion), xform);
                continue;
            }

            // Asleep until its gate opens; then in an arena it fights while the fight is on, outside one always.
            if (legion.Dormant)
                continue;

            // A raid on the outpost: it does not follow the players there, it flies home and waits for them.
            if (legion.Free && !TryGetArena(legion, out _) && FlyHomeFromRaid((uid, legion), xform, frameTime))
                continue;

            LavalandBossArenaComponent? arena = null;
            if (TryGetArena(legion, out var found))
            {
                if (!found.FightStarted || found.Ended)
                    continue;
                arena = found;
            }
            else if (!legion.Free)
            {
                continue;
            }

            SmashGates(xform, legion);
            UnstuckTowardsTarget(uid, legion, xform, arena, now);

            if (UpdateCharge(uid, legion, xform, arena, now))
                continue;

            // A slam or a throw of the hands is playing out: the Legion holds still.
            if (_hands.IsBusy(uid))
                continue;

            if (UpdateManeuver(uid, legion, now))
                continue;

            if (TryManeuver(uid, legion, xform, arena, now))
                continue;

            if (now < legion.NextAttack || !TryFindTarget(xform, legion, arena, out var target))
                continue;

            legion.NextAttack = now + legion.AttackCooldown;
            if (_hands.TryAttack(uid, target))
                continue;

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

    #region Raids

    /// <summary>
    /// While a raid is on the outpost, a free Legion drops the chase and flies straight home - over everything -
    /// to fall asleep there. False while there is no raid: it goes on as before.
    /// </summary>
    private bool FlyHomeFromRaid(Entity<LavalandLegionMegafaunaComponent> ent, TransformComponent xform, float frameTime)
    {
        var legion = ent.Comp;
        if (xform.MapUid is not { } map || !_raids.IsRaidRunning(map) ||
            legion.Home is not { } home || home.MapId != xform.MapID)
        {
            legion.FledRaid = false;
            return false;
        }

        var now = _timing.CurTime;
        var position = _transform.GetWorldPosition(xform);
        var offset = home.Position - position;
        var distance = offset.Length();

        if (!legion.ReturningHome)
        {
            // Fled this raid already, or close to home anyway: it fights as usual.
            if (legion.FledRaid || distance < 8f)
                return false;

            legion.ReturningHome = true;
            legion.FledRaid = true;
            legion.Pursued = null;
            legion.Lunging = false;
            legion.ChargeStartAt = null;
            legion.ChargeEndAt = null;
            legion.ChargeHit.Clear();
            SetAi(ent, false);
            SetSpinning(ent, false);

            // Frozen in fright first: shaking all over, the hands up.
            legion.RaidPanicUntil = now + legion.RaidPanicTime;
            _jitter.DoJitter(ent, legion.RaidPanicTime, true, 30f, 12f);
            _hands.RaiseHandsInFright(ent, legion.RaidPanicTime + TimeSpan.FromSeconds(1.5));
            _popup.PopupEntity(Loc.GetString("lavaland-legion-raid-panic", ("legion", ent.Owner)), ent,
                PopupType.MediumCaution);
        }

        if (TryComp<PhysicsComponent>(ent, out var stillBody))
            _physics.SetLinearVelocity(ent, Vector2.Zero, body: stillBody);

        if (legion.RaidPanicUntil is { } panicUntil)
        {
            if (now < panicUntil)
                return true;

            // And off it goes, spinning away.
            legion.RaidPanicUntil = null;
            SetSpinning(ent, true);
        }

        if (distance > 0.5f)
        {
            var step = MathF.Min(legion.HomeFlightSpeed * frameTime, distance);
            _transform.SetWorldPosition(ent, position + offset / distance * step);
            return true;
        }

        // Home: asleep again until someone comes for it.
        legion.ReturningHome = false;
        legion.WaitingAtHome = true;
        legion.Dormant = true;
        SetSpinning(ent, false);
        return true;
    }

    private void WakeAtHomeIfApproached(Entity<LavalandLegionMegafaunaComponent> ent, TransformComponent xform)
    {
        var position = _transform.GetWorldPosition(xform);
        var range = ent.Comp.AggroRange * ent.Comp.AggroRange;
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } player ||
                !IsValidTarget(player, xform.MapID) ||
                Vector2.DistanceSquared(position, _transform.GetWorldPosition(player)) > range)
            {
                continue;
            }

            ent.Comp.WaitingAtHome = false;
            Wake(ent, player);
            return;
        }
    }

    #endregion

    #region Attacks

    /// <summary>
    /// Keeps the target busy: rolls around to its back when close, leaps in when it runs off.
    /// </summary>
    private bool TryManeuver(EntityUid uid,
        LavalandLegionMegafaunaComponent legion,
        TransformComponent xform,
        LavalandBossArenaComponent? arena,
        TimeSpan now)
    {
        if (now < legion.NextManeuver || !TryFindTarget(xform, legion, arena, out var target))
            return false;

        var targetCoordinates = _transform.GetMapCoordinates(target);
        var from = _transform.GetWorldPosition(xform);
        var offset = targetCoordinates.Position - from;
        var distance = offset.Length();
        if (distance < 0.1f)
            return false;

        var chasing = distance > legion.GapCloseDistance;
        if (chasing)
        {
            legion.NextManeuver = now + legion.ChaseCooldown;
        }
        else
        {
            var min = legion.ManeuverCooldownMin.TotalSeconds;
            var max = Math.Max(min, legion.ManeuverCooldownMax.TotalSeconds);
            legion.NextManeuver = now + TimeSpan.FromSeconds(_random.NextDouble() * (max - min) + min);
        }

        MapCoordinates destination;
        if (chasing)
        {
            // Lead the runner a little: roll to where it is heading.
            var lead = TryComp<PhysicsComponent>(target, out var targetBody) ? targetBody.LinearVelocity * 0.4f : Vector2.Zero;
            destination = new MapCoordinates(targetCoordinates.Position + lead - offset / distance * legion.GapCloseLanding,
                targetCoordinates.MapId);
        }
        else if (distance <= legion.FlankRange)
        {
            var facing = TryComp<MouseRotatorComponent>(target, out var mouse) && mouse.GoalRotation is { } goal
                ? goal.ToVec()
                : _transform.GetWorldRotation(target).ToVec();
            destination = targetCoordinates.Offset(-facing * legion.FlankDistance);
        }
        else
        {
            return false;
        }

        var travel = destination.Position - from;
        var length = travel.Length();
        if (length < 0.3f)
            return false;

        // A rolling body: it spins slowly on its way, straight at its spot.
        var speed = MathF.Max(1f, chasing ? legion.ChaseSpeed : legion.ManeuverSpeed);
        var limit = chasing ? legion.ChaseMaxDuration : legion.ManeuverMaxDuration;
        var seconds = Math.Min(length / speed, (float) limit.TotalSeconds);
        legion.ManeuverVelocity = travel / length * speed;
        legion.ManeuverEndAt = now + TimeSpan.FromSeconds(seconds);
        SetAi(uid, false);
        SetSpinning(uid, true);
        if (legion.ManeuverSound != null)
            _audio.PlayPvs(legion.ManeuverSound, uid);
        return true;
    }

    /// <summary>Returns true while the body is rolling.</summary>
    private bool UpdateManeuver(EntityUid uid, LavalandLegionMegafaunaComponent legion, TimeSpan now)
    {
        if (legion.ManeuverEndAt is not { } end)
            return false;

        if (!TryComp<PhysicsComponent>(uid, out var physics) || now >= end)
        {
            legion.ManeuverEndAt = null;
            if (physics != null)
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
            SetSpinning(uid, false);
            SetAi(uid, true);
            return false;
        }

        _physics.SetLinearVelocity(uid, legion.ManeuverVelocity, body: physics);
        return true;
    }

    private void SetSpinning(EntityUid uid, bool spinning)
    {
        if (!TryComp<LavalandSpinComponent>(uid, out var spin) || spin.Spinning == spinning)
            return;

        spin.Spinning = spinning;
        Dirty(uid, spin);
    }

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
        LavalandBossArenaComponent? arena,
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
            SetSpinning(uid, true);
        }

        if (legion.ChargeEndAt is not { } end)
            return false;

        if (!TryComp<PhysicsComponent>(uid, out var physics))
            return true;

        if (now >= end)
        {
            legion.ChargeEndAt = null;
            legion.Lunging = false;
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
            SetSpinning(uid, false);
            SetAi(uid, true);
            return false;
        }

        if (legion.Lunging && legion.LungeBreaksWalls)
            BreakAhead(uid, xform, legion, legion.ChargeDirection);

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
        LavalandBossArenaComponent? arena,
        out EntityUid target)
    {
        target = default;

        // Outside an arena the Legion keeps hunting whoever woke it, however far they run.
        if (arena == null && legion.Pursued is { } pursued && IsValidTarget(pursued, xform.MapID))
        {
            target = pursued;
            return true;
        }

        var range = arena == null ? legion.PursuitRange : legion.AggroRange;
        var best = range * range;
        var position = _transform.GetWorldPosition(xform);
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } player ||
                !IsValidTarget(player, xform.MapID) ||
                arena != null && Transform(player).GridUid != arena.Grid)
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
               Transform(player).MapID == map &&
               // A body its player has left (ghosted, aghosted) is not hunted any more.
               _players.TryGetSessionByEntity(player, out _);
    }

    /// <summary>A mapped Legion wakes up and lunges at whoever woke it.</summary>
    public void Wake(Entity<LavalandLegionMegafaunaComponent> ent, EntityUid? target)
    {
        if (!ent.Comp.Dormant)
            return;

        var now = _timing.CurTime;
        ent.Comp.Dormant = false;
        ent.Comp.NextAttack = now + ent.Comp.AttackCooldown;
        SetAi(ent, true);

        // In its arena the fight begins right away, so the lunge and the attacks run.
        if (ent.Comp.Arena is { } arenaUid)
            _arenas.StartFightFor(arenaUid);

        if (target is not { } victim || !IsValidTarget(victim, Transform(ent).MapID))
            return;

        ent.Comp.Pursued = victim;

        var offset = _transform.GetWorldPosition(victim) - _transform.GetWorldPosition(ent);
        if (offset.LengthSquared() < 0.01f)
            return;

        // Long enough to reach the target, never longer than the limit.
        var seconds = Math.Clamp(offset.Length() / MathF.Max(1f, ent.Comp.ChargeSpeed),
            (float) ent.Comp.ChargeDuration.TotalSeconds,
            MathF.Max((float) ent.Comp.ChargeDuration.TotalSeconds, (float) ent.Comp.WakeLungeMax.TotalSeconds));
        ent.Comp.ChargeStartAt = null;
        ent.Comp.ChargeDirection = Vector2.Normalize(offset);
        ent.Comp.ChargeEndAt = now + TimeSpan.FromSeconds(seconds);
        ent.Comp.ChargeHit.Clear();
        ent.Comp.Lunging = true;
        SetAi(ent, false);
        SetSpinning(ent, true);
        if (ent.Comp.ChargeWindupSound != null)
            _audio.PlayPvs(ent.Comp.ChargeWindupSound, ent);
    }

    /// <summary>The waking lunge: gates burst open, walls and other solid structures in the way are torn down.</summary>
    private void BreakAhead(EntityUid uid, TransformComponent xform, LavalandLegionMegafaunaComponent legion, Vector2 direction)
    {
        var ahead = new MapCoordinates(
            _transform.GetWorldPosition(xform) + direction * legion.LungeBreakRange,
            xform.MapID);
        BreakAround(uid, legion, ahead, legion.LungeBreakRange);
    }

    /// <summary>
    /// Tears down whatever solid and breakable stands in the circle: walls, rocks, trees, crystals - anchored or not.
    /// The indestructible (lavaland's edge, godmode walls) and anything without health is left alone.
    /// </summary>
    private void BreakAround(EntityUid uid, LavalandLegionMegafaunaComponent legion, MapCoordinates center, float range)
    {
        var lair = GetLair(uid, legion);
        var broke = false;
        foreach (var hit in _lookup.GetEntitiesInRange(center, range, LookupFlags.Static | LookupFlags.Dynamic))
        {
            if (hit == uid || TerminatingOrDeleted(hit) || HasComp<MobStateComponent>(hit))
                continue;

            if (TryComp<LavalandNecropolisGateComponent>(hit, out var gate))
            {
                _gates.Smash((hit, gate));
                continue;
            }

            if (!TryComp<PhysicsComponent>(hit, out var body) || !body.Hard || !body.CanCollide ||
                body.BodyType != BodyType.Static && !Transform(hit).Anchored)
            {
                continue;
            }

            // Its own lair's walls are unbreakable for everyone else, not for the Legion.
            var inLair = lair != null && Transform(hit).GridUid == lair;
            if (!inLair && (!HasComp<DamageableComponent>(hit) || HasComp<GodmodeComponent>(hit)))
                continue;

            QueueDel(hit);
            broke = true;
        }

        if (broke && legion.LungeBreakSound != null)
            _audio.PlayPvs(legion.LungeBreakSound, uid);
    }

    /// <summary>The arena grid this Legion (or the Legion it split from) was brought out of.</summary>
    private EntityUid? GetLair(EntityUid uid, LavalandLegionMegafaunaComponent legion)
    {
        if (legion.Lair is { } known && !TerminatingOrDeleted(known))
            return known;

        var arenas = EntityQueryEnumerator<LavalandBossArenaComponent>();
        while (arenas.MoveNext(out var arenaUid, out var arena))
        {
            // Any body of the same Legion counts: the boss role passes between them as it splits.
            if (arena.Boss != uid && arena.Boss != legion.Root &&
                !(arena.Boss is { } boss && TryComp<LavalandLegionMegafaunaComponent>(boss, out var bossLegion) &&
                  bossLegion.Root == legion.Root))
            {
                continue;
            }

            legion.Lair = arenaUid;
            return arenaUid;
        }

        return null;
    }

    /// <summary>
    /// The big body jams in narrow passages: when it has not moved for a while but its target is away,
    /// it tears down the walls between them.
    /// </summary>
    private void UnstuckTowardsTarget(EntityUid uid,
        LavalandLegionMegafaunaComponent legion,
        TransformComponent xform,
        LavalandBossArenaComponent? arena,
        TimeSpan now)
    {
        if (now < legion.NextStuckCheck)
            return;
        legion.NextStuckCheck = now + legion.StuckCheckInterval;

        var position = _transform.GetWorldPosition(xform);
        var moved = Vector2.Distance(position, legion.LastStuckPosition);
        legion.LastStuckPosition = position;
        // Standing still on purpose (a hand attack) is not being stuck.
        if (moved >= legion.StuckDistance || legion.ChargeEndAt != null || _hands.IsBusy(uid) ||
            !TryFindTarget(xform, legion, arena, out var target))
        {
            legion.StuckCount = 0;
            return;
        }

        var offset = _transform.GetWorldPosition(target) - position;
        if (offset.Length() <= legion.StuckIgnoreRange)
        {
            legion.StuckCount = 0;
            return;
        }

        legion.StuckCount++;
        BreakAhead(uid, xform, legion, Vector2.Normalize(offset));

        // Still jammed: whatever hugs its big body on any side goes too (corners, side rocks, trees).
        if (legion.StuckCount >= 2)
            BreakAround(uid, legion, _transform.GetMapCoordinates(xform), legion.LungeBreakRange + 1f);
    }

    /// <summary>An awake Legion does not stop at a necropolis gate: it bursts it open.</summary>
    private void SmashGates(TransformComponent xform, LavalandLegionMegafaunaComponent legion)
    {
        _nearGates.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, legion.GateSmashRange, _nearGates);
        foreach (var gate in _nearGates)
            _gates.Smash(gate);
    }

    /// <summary>Wakes every sleeping Legion on the map within <paramref name="range"/> of the spot.</summary>
    public void WakeNear(MapCoordinates coordinates, float range, EntityUid? target)
    {
        var query = EntityQueryEnumerator<LavalandLegionMegafaunaComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var legion, out var xform))
        {
            if (!legion.Dormant || xform.MapID != coordinates.MapId ||
                Vector2.DistanceSquared(_transform.GetWorldPosition(xform), coordinates.Position) > range * range)
            {
                continue;
            }

            Wake((uid, legion), target);
        }
    }

    #endregion

    #region Splitting

    private void OnDamageChanged(Entity<LavalandLegionMegafaunaComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.Dying || !args.DamageIncreased)
            return;

        if (ent.Comp.Dormant)
            Wake(ent, args.Origin);

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
            childComp.Free = ent.Comp.Free;
            childComp.Pursued = ent.Comp.Pursued;
            childComp.Home = ent.Comp.Home;
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

        // The hands outlive the split: they have their own health and keep fighting for the next body.
        _hands.TransferHands(ent.Owner, successor.Value);

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

        // An arena whose boss roams free still follows it, so the arena ends only with the last skull.
        var arenas = EntityQueryEnumerator<LavalandBossArenaComponent>();
        while (arenas.MoveNext(out var unboundArena))
        {
            if (unboundArena.Boss == from)
                unboundArena.Boss = to;
        }

        if (HasComp<LavalandFreeBossComponent>(from))
            EnsureComp<LavalandFreeBossComponent>(to);

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
        rootComp.Dormant = false;
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

    #region Free Legion health bar and music

    /// <summary>
    /// A Legion outside an arena carries its own health bar and music, like the mad miner:
    /// players near any of its bodies get them, and they go away with its last skull.
    /// </summary>
    private void UpdateFreeHuds(TimeSpan now)
    {
        foreach (var list in _bodiesByRoot.Values)
            list.Clear();

        var query = EntityQueryEnumerator<LavalandLegionMegafaunaComponent>();
        while (query.MoveNext(out var uid, out var legion))
        {
            if (!legion.Free || legion.Dormant || legion.Dying || _mobState.IsDead(uid))
                continue;

            if (!_bodiesByRoot.TryGetValue(legion.Root, out var bodies))
                _bodiesByRoot[legion.Root] = bodies = new List<EntityUid>();
            bodies.Add(uid);
        }

        foreach (var (root, bodies) in _bodiesByRoot)
        {
            if (bodies.Count == 0)
                continue;

            if (!_freeHuds.TryGetValue(root, out var hud))
            {
                hud = CreateFreeHud(bodies);
                if (hud == null)
                    continue;
                _freeHuds[root] = hud;
            }

            if (now < hud.NextUpdate)
                continue;
            hud.NextUpdate = now + FreeHudInterval;

            var health = 0f;
            foreach (var body in bodies)
            {
                var legion = Comp<LavalandLegionMegafaunaComponent>(body);
                var damage = TryComp<DamageableComponent>(body, out var damageable) ? damageable.TotalDamage.Float() : 0f;
                health += Math.Max(0f, legion.BodyHealth - damage) + legion.SplitHealthPool;
            }
            health = Math.Clamp(health, 0f, hud.MaxHealth);

            foreach (var session in _players.Sessions)
            {
                // Only living players near it: a ghost, an aghost or a corpse is out of the hunt.
                if (session.Status == SessionStatus.Disconnected ||
                    session.AttachedEntity is not { Valid: true } player ||
                    !IsValidTarget(player, Transform(player).MapID) ||
                    !NearAnyBody(player, bodies))
                {
                    // Left the hunt: the health bar and the music go away for him.
                    if (hud.Recipients.Remove(session) && session.Status != SessionStatus.Disconnected)
                        SendHideAndStop(session, hud.Id);
                    continue;
                }

                // Players who walk into the hunt get the music too, not only those there at its start.
                if (hud.Recipients.Add(session) && hud.Music is { } music)
                    RaiseNetworkEvent(new LavalandBossMusicStartEvent(hud.Id, music, hud.MusicParams), session.Channel);
                RaiseNetworkEvent(new LavalandBossHudUpdateEvent(hud.Id, hud.Name, health, hud.MaxHealth, 1), session.Channel);
            }
        }

        // Every body of a Legion is gone: its health bar and music go too.
        foreach (var root in _freeHuds.Keys.ToList())
        {
            if (_bodiesByRoot.TryGetValue(root, out var bodies) && bodies.Count > 0)
                continue;

            var hud = _freeHuds[root];
            foreach (var session in hud.Recipients)
            {
                if (session.Status != SessionStatus.Disconnected)
                    SendHideAndStop(session, hud.Id);
            }

            _freeHuds.Remove(root);
        }
    }

    private void SendHideAndStop(ICommonSession session, int hudId)
    {
        RaiseNetworkEvent(new LavalandBossHudHideEvent(hudId), session.Channel);
        RaiseNetworkEvent(new LavalandBossMusicStopEvent(hudId), session.Channel);
    }

    private FreeLegionHud? CreateFreeHud(List<EntityUid> bodies)
    {
        LavalandBossComponent? boss = null;
        foreach (var body in bodies)
        {
            if (TryComp(body, out boss))
                break;
        }

        if (boss == null)
            return null;

        var sound = boss.SelectedMusic
                    ?? (boss.MusicPool.Count > 0 ? _random.Pick(boss.MusicPool) : boss.Music);
        boss.SelectedMusic = sound;
        return new FreeLegionHud
        {
            Id = _nextHudId++,
            Name = string.IsNullOrWhiteSpace(boss.BossName) ? Name(bodies[0]) : boss.BossName,
            MaxHealth = Math.Max(1f, boss.MaxHealth),
            Music = sound == null ? null : _audio.ResolveSound(sound),
            MusicParams = (sound?.Params ?? AudioParams.Default).WithLoop(true),
        };
    }

    private bool NearAnyBody(EntityUid player, List<EntityUid> bodies)
    {
        var playerXform = Transform(player);
        var position = _transform.GetWorldPosition(playerXform);
        foreach (var body in bodies)
        {
            var bodyXform = Transform(body);
            if (bodyXform.MapID == playerXform.MapID &&
                Vector2.DistanceSquared(position, _transform.GetWorldPosition(bodyXform)) <= FreeHudRange * FreeHudRange)
            {
                return true;
            }
        }

        return false;
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
