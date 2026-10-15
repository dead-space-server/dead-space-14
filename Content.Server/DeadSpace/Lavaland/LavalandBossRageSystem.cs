// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Construction;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.NPC.HTN;
using Content.Shared.Throwing;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.Ghost;
using Content.Shared.Item;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Sends lavaland bosses into absolute rage when players build structures (railings, crates, barricades...)
/// in their arena or next to a mini-boss. Enraged bosses attack much more often, move faster and hit harder,
/// but keep their telegraphs, so careful dodging still wins.
/// </summary>
public sealed class LavalandBossRageSystem : EntitySystem
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan VictimLifetime = TimeSpan.FromSeconds(2.5);
    private static readonly Color RageColor = Color.FromHex("#ff1e1e");

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly LavalandBossArenaSystem _arena = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    private TimeSpan _nextScan;
    private readonly HashSet<EntityUid> _violating = new();
    private readonly HashSet<EntityUid> _violatedArenas = new();
    private readonly List<(EntityUid Uid, LavalandBossRageComponent Rage, TransformComponent Xform)> _miniBosses = new();
    private readonly List<EntityUid> _expiredVictims = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandBossRageComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<LavalandBossRageComponent, ComponentShutdown>(OnRageShutdown);
        SubscribeLocalEvent<LavalandBossRageVictimComponent, DamageModifyEvent>(OnVictimDamageModify);
    }

    public bool IsEnraged(EntityUid uid)
    {
        return TryComp<LavalandBossRageComponent>(uid, out var rage) && rage.Enraged;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var rages = EntityQueryEnumerator<LavalandBossRageComponent, TransformComponent>();
        while (rages.MoveNext(out var uid, out var rage, out var xform))
        {
            if (!rage.Enraged)
                continue;
            CompressCooldowns(uid, rage, now);
            Charge(uid, rage, xform, frameTime, now);
        }

        if (now < _nextScan)
            return;
        _nextScan = now + ScanInterval;

        EnsureRageComponents();
        CollectViolations();
        foreach (var arena in _violatedArenas)
        {
            _arena.HandleConstructionViolation(arena);
        }
        UpdateRageStates(now);
        ExpireVictims(now);
    }

    private void EnsureRageComponents()
    {
        var bosses = EntityQueryEnumerator<LavalandBossComponent>();
        while (bosses.MoveNext(out var uid, out _))
            EnsureComp<LavalandBossRageComponent>(uid);

        var miners = EntityQueryEnumerator<LavalandMadMinerComponent>();
        while (miners.MoveNext(out var uid, out _))
            EnsureComp<LavalandBossRageComponent>(uid);

        var legions = EntityQueryEnumerator<LavalandLegionMegafaunaComponent>();
        while (legions.MoveNext(out var uid, out _))
            EnsureComp<LavalandBossRageComponent>(uid);

        var elites = EntityQueryEnumerator<LavalandEliteComponent>();
        while (elites.MoveNext(out var uid, out _))
            EnsureComp<LavalandBossRageComponent>(uid);
    }

    private void CollectViolations()
    {
        _violating.Clear();
        _violatedArenas.Clear();
        _miniBosses.Clear();

        var miners = EntityQueryEnumerator<LavalandMadMinerComponent, LavalandBossRageComponent, TransformComponent>();
        while (miners.MoveNext(out var uid, out _, out var rage, out var xform))
        {
            if (!_mobState.IsDead(uid))
                _miniBosses.Add((uid, rage, xform));
        }

        var eliteQuery = EntityQueryEnumerator<LavalandEliteComponent, LavalandBossRageComponent, TransformComponent>();
        while (eliteQuery.MoveNext(out var uid, out _, out var rage, out var xform))
        {
            if (!_mobState.IsDead(uid))
                _miniBosses.Add((uid, rage, xform));
        }

        var built = EntityQueryEnumerator<PlayerConstructedComponent, TransformComponent>();
        while (built.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid is not { } map || !HasComp<LavalandMapComponent>(map) ||
                HasComp<ItemComponent>(uid) || TerminatingOrDeleted(uid))
            {
                continue;
            }

            if (xform.GridUid is { } grid &&
                TryComp<LavalandBossArenaComponent>(grid, out var arena) &&
                !arena.Ended && Exists(arena.Boss))
            {
                _violating.Add(arena.Boss);
                _violatedArenas.Add(grid);
            }

            if (_miniBosses.Count == 0)
                continue;

            var position = _transform.GetWorldPosition(xform);
            foreach (var (miner, rage, minerXform) in _miniBosses)
            {
                if (minerXform.MapUid != map ||
                    Vector2.DistanceSquared(position, _transform.GetWorldPosition(minerXform)) >
                    rage.StructureRadius * rage.StructureRadius)
                {
                    continue;
                }

                _violating.Add(miner);
            }
        }

        // Every Legion body in a violated arena rages, not only the one holding the boss role.
        if (_violatedArenas.Count == 0)
            return;
        var legions = EntityQueryEnumerator<LavalandLegionMegafaunaComponent>();
        while (legions.MoveNext(out var uid, out var legion))
        {
            if (legion.Arena is { } legionArena && _violatedArenas.Contains(legionArena))
                _violating.Add(uid);
        }
    }

    private void UpdateRageStates(TimeSpan now)
    {
        var query = EntityQueryEnumerator<LavalandBossRageComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var rage, out var xform))
        {
            if (_mobState.IsDead(uid))
            {
                if (rage.Enraged)
                    SetEnraged((uid, rage), false);
                continue;
            }

            if (_violating.Contains(uid))
            {
                rage.LastViolation = now;
                if (!rage.Enraged)
                    SetEnraged((uid, rage), true);
            }
            else if (rage.Enraged && now - rage.LastViolation >= rage.CalmDelay)
            {
                SetEnraged((uid, rage), false);
            }

            if (rage.Enraged)
                RefreshVictims(uid, rage, xform, now);
        }
    }

    private void SetEnraged(Entity<LavalandBossRageComponent> ent, bool enraged)
    {
        ent.Comp.Enraged = enraged;
        ent.Comp.CompressedTimers.Clear();
        _movement.RefreshMovementSpeedModifiers(ent);

        // The charge drives the body itself; the AI would only fight it for movement.
        if (TryComp<HTNComponent>(ent, out var htn))
        {
            if (enraged && htn.Enabled)
            {
                _htn.SetHTNEnabled((ent, htn), false);
                ent.Comp.DisabledAi = true;
            }
            else if (!enraged && ent.Comp.DisabledAi)
            {
                _htn.SetHTNEnabled((ent, htn), true, 0.75f);
                ent.Comp.DisabledAi = false;
            }
        }

        if (enraged)
        {
            if (!_light.TryGetLight(ent, out _))
            {
                ent.Comp.AddedLight = true;
                var light = _light.EnsureLight(ent);
                _light.SetColor(ent, RageColor, light);
                _light.SetRadius(ent, 4f, light);
                _light.SetEnergy(ent, 3f, light);
            }

            // Legion has many bodies; only the one holding the boss role announces the rage.
            if (!ShouldAnnounce(ent))
                return;

            var popup = Loc.GetString("lavaland-boss-rage-start", ("boss", Name(ent)));
            var subtitle = Loc.GetString("lavaland-boss-rage-subtitle");
            foreach (var session in GetAffectedSessions(ent, Transform(ent)))
            {
                RaiseNetworkEvent(new LavalandBossRageAnnouncementEvent(subtitle, 3.5f), session.Channel);
                if (session.AttachedEntity is { } player)
                    _popup.PopupEntity(popup, player, player, PopupType.LargeCaution);
            }
            return;
        }

        if (ent.Comp.AddedLight)
        {
            ent.Comp.AddedLight = false;
            _light.RemoveLightDeferred(ent);
        }

        if (!ShouldAnnounce(ent))
            return;

        var calm = Loc.GetString("lavaland-boss-rage-end", ("boss", Name(ent)));
        foreach (var session in GetAffectedSessions(ent, Transform(ent)))
        {
            if (session.AttachedEntity is { } player)
                _popup.PopupEntity(calm, player, player, PopupType.Medium);
        }
    }

    /// <summary>Rush at the nearest player at full speed, through walls and barricades, and hit on contact.</summary>
    private void Charge(EntityUid uid, LavalandBossRageComponent rage, TransformComponent xform, float frameTime, TimeSpan now)
    {
        if (_mobState.IsDead(uid) || xform.MapUid == null)
            return;

        var position = _transform.GetWorldPosition(xform);
        EntityUid? target = null;
        var best = float.MaxValue;
        foreach (var session in GetAffectedSessions(uid, xform))
        {
            if (session.AttachedEntity is not { } player)
                continue;
            var distance = Vector2.DistanceSquared(position, _transform.GetWorldPosition(player));
            if (distance >= best)
                continue;
            best = distance;
            target = player;
        }

        if (target is not { } victim)
            return;

        var victimPosition = _transform.GetWorldPosition(victim);
        var offset = victimPosition - position;
        var length = offset.Length();
        if (length > rage.ChargeReach)
        {
            var step = MathF.Min(rage.ChargeSpeed * frameTime, length - rage.ChargeReach);
            _transform.SetWorldPosition(uid, position + offset / length * step);
            return;
        }

        if (now < rage.NextChargeHit)
            return;

        rage.NextChargeHit = now + rage.ChargeHitCooldown;
        _damageable.TryChangeDamage(victim, rage.ChargeDamage, origin: uid);
        if (rage.ChargeKnockback > 0f && length > 0.01f)
            _throwing.TryThrow(victim, offset / length * rage.ChargeKnockback, 8f, uid, recoil: false);
    }

    private bool ShouldAnnounce(EntityUid uid)
    {
        return !HasComp<LavalandLegionMegafaunaComponent>(uid) || HasComp<LavalandBossComponent>(uid);
    }

    private void RefreshVictims(EntityUid boss, LavalandBossRageComponent rage, TransformComponent xform, TimeSpan now)
    {
        foreach (var session in GetAffectedSessions(boss, xform))
        {
            if (session.AttachedEntity is not { } player)
                continue;
            var victim = EnsureComp<LavalandBossRageVictimComponent>(player);
            victim.Boss = boss;
            victim.Multiplier = rage.DamageMultiplier;
            victim.ExpiresAt = now + VictimLifetime;
        }
    }

    private IEnumerable<ICommonSession> GetAffectedSessions(EntityUid boss, TransformComponent bossXform)
    {
        var arenaGrid = TryComp<LavalandBossComponent>(boss, out var bossComp)
            ? bossComp.Arena
            : CompOrNull<LavalandLegionMegafaunaComponent>(boss)?.Arena;
        var radius = CompOrNull<LavalandBossRageComponent>(boss)?.VictimRadius ?? 30f;
        var bossPosition = _transform.GetWorldPosition(bossXform);

        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } player ||
                TerminatingOrDeleted(player) ||
                HasComp<GhostComponent>(player) ||
                _mobState.IsDead(player))
            {
                continue;
            }

            var playerXform = Transform(player);
            if (playerXform.MapID != bossXform.MapID)
                continue;

            if (arenaGrid != null)
            {
                if (playerXform.GridUid == arenaGrid)
                    yield return session;
                continue;
            }

            if (Vector2.DistanceSquared(bossPosition, _transform.GetWorldPosition(playerXform)) <= radius * radius)
                yield return session;
        }
    }

    private void ExpireVictims(TimeSpan now)
    {
        _expiredVictims.Clear();
        var victims = EntityQueryEnumerator<LavalandBossRageVictimComponent>();
        while (victims.MoveNext(out var uid, out var victim))
        {
            if (now >= victim.ExpiresAt || !IsEnraged(victim.Boss))
                _expiredVictims.Add(uid);
        }

        foreach (var uid in _expiredVictims)
            RemComp<LavalandBossRageVictimComponent>(uid);
    }

    /// <summary>
    /// Every boss schedules its next attack through a "next attack" timestamp. While enraged,
    /// each newly scheduled delay is shortened once, keeping the boss's own attack logic intact.
    /// </summary>
    private void CompressCooldowns(EntityUid uid, LavalandBossRageComponent rage, TimeSpan now)
    {
        if (TryComp<LavalandAshDrakeComponent>(uid, out var drake))
            drake.NextAttack = Compress(rage, nameof(LavalandAshDrakeComponent), drake.NextAttack, now);
        if (TryComp<LavalandBubblegumComponent>(uid, out var bubblegum))
            bubblegum.NextAttack = Compress(rage, nameof(LavalandBubblegumComponent), bubblegum.NextAttack, now);
        if (TryComp<LavalandColossusComponent>(uid, out var colossus))
            colossus.NextAttack = Compress(rage, nameof(LavalandColossusComponent), colossus.NextAttack, now);
        if (TryComp<LavalandEliteComponent>(uid, out var elite))
            elite.NextAttack = Compress(rage, nameof(LavalandEliteComponent), elite.NextAttack, now);
        if (TryComp<LavalandLegionMegafaunaComponent>(uid, out var legion))
            legion.NextAttack = Compress(rage, nameof(LavalandLegionMegafaunaComponent), legion.NextAttack, now);
        if (TryComp<LavalandHierophantComponent>(uid, out var hierophant))
            hierophant.NextAttack = Compress(rage, nameof(LavalandHierophantComponent), hierophant.NextAttack, now);
        if (TryComp<LavalandMadMinerComponent>(uid, out var miner))
        {
            miner.NextShot = Compress(rage, nameof(miner.NextShot), miner.NextShot, now);
            miner.NextHook = Compress(rage, nameof(miner.NextHook), miner.NextHook, now);
            miner.NextBlink = Compress(rage, nameof(miner.NextBlink), miner.NextBlink, now);
            miner.NextFeint = Compress(rage, nameof(miner.NextFeint), miner.NextFeint, now);
        }
    }

    private static TimeSpan Compress(LavalandBossRageComponent rage, string key, TimeSpan value, TimeSpan now)
    {
        if (value <= now || rage.CompressedTimers.TryGetValue(key, out var seen) && seen == value)
            return value;

        var compressed = now + (value - now) * Math.Clamp(rage.CooldownMultiplier, 0.05f, 1f);
        rage.CompressedTimers[key] = compressed;
        return compressed;
    }

    private void OnRefreshSpeed(Entity<LavalandBossRageComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Enraged)
            args.ModifySpeed(ent.Comp.SpeedMultiplier, ent.Comp.SpeedMultiplier);
    }

    private void OnRageShutdown(Entity<LavalandBossRageComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.AddedLight && !TerminatingOrDeleted(ent))
            _light.RemoveLightDeferred(ent);
    }

    private void OnVictimDamageModify(Entity<LavalandBossRageVictimComponent> ent, ref DamageModifyEvent args)
    {
        // Only hostile damage from the arena is amplified: player-caused and environmental (no origin) damage is untouched.
        if (args.Origin is not { } origin ||
            origin == ent.Owner ||
            HasComp<ActorComponent>(origin) ||
            !args.Damage.AnyPositive() ||
            !IsEnraged(ent.Comp.Boss))
        {
            return;
        }

        args.Damage *= ent.Comp.Multiplier;
    }
}
