// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using System.Linq;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.DeadSpace.Blink;
using Content.Server.Weapons.Ranged.Systems;
using Content.Server.NPC.HTN;
using Content.Server.Tiles;
using Content.Shared.Chasm;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Lavaland.Celestial;
using Content.Shared.DeadSpace.Lavaland.Bosses;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.DeadSpace.Necromorphs.InfectionDead.Components;
using Content.Shared.DeadSpace.Necromorphs.PlasmaCutter;
using Content.Shared.MouseRotator;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee;
using Content.Shared.Physics;
using Content.Shared.Verbs;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandMadMinerSystem : EntitySystem
{
    private static readonly float[] ShotgunSpread = [-18f, -9f, 0f, 9f, 18f];

    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly BlinkSystem _blink = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly Content.Shared.Administration.Systems.RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly Content.Shared.Mobs.Systems.MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly Content.Shared.Movement.Systems.MovementSpeedModifierSystem _speedModifier = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly Content.Server.NPC.Systems.NPCSystem _npc = default!;
    [Dependency] private readonly Content.Shared.Popups.SharedPopupSystem _popup = default!;
    [Dependency] private readonly Content.Shared.Jittering.SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly LavalandMadMinerHookSystem _hook = default!;

    private int _nextHudId = 100000;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandMadMinerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<LavalandMadMinerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<LavalandMadMinerComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<LavalandMadMinerComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<LavalandMadMinerComponent, NecromorphPlasmaCutterHitAttemptEvent>(OnPlasmaCutterHit);
        SubscribeLocalEvent<LavalandMadMinerHookedComponent, GetVerbsEvent<AlternativeVerb>>(OnHookedGetVerbs);
        SubscribeLocalEvent<LavalandMadMinerHookedComponent, LavalandRemoveMadMinerHookDoAfterEvent>(OnHookRemoved);
        SubscribeLocalEvent<LavalandMadMinerHookVisualComponent, InteractHandEvent>(OnHookVisualInteract);
    }

    private void OnMapInit(Entity<LavalandMadMinerComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.HudId = _nextHudId++;
        ent.Comp.SpawnCoordinates = _transform.GetMapCoordinates(ent.Owner);
        ent.Comp.SelectedMusic = ent.Comp.Music.Count == 0 ? null : _random.Pick(ent.Comp.Music);
        BuildPlatform(ent.Owner);
    }

    private void OnShutdown(Entity<LavalandMadMinerComponent> ent, ref ComponentShutdown args)
    {
        // Deleting the miner mid-fight must not leave HUD, music or hooks on players.
        EndFight(ent.Owner, ent.Comp);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        UpdateRising(now);

        var query = EntityQueryEnumerator<LavalandMadMinerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var miner, out var xform))
        {
            if (miner.NextThink > now || IsDead(uid))
                continue;
            miner.NextThink = now + TimeSpan.FromSeconds(0.1);

            // Risen as a necromorph - the second stage: no shots or hooks any more, its own AI claws at whoever is
            // near, but it still blinks after a player who runs, and the fight still has its health bar.
            if (miner.Risen)
            {
                ThinkRisen(uid, miner, xform, now);
                continue;
            }

            TrySpeak(uid, miner, now);

            if (!TryPickTarget(uid, miner, xform, out var target))
            {
                if (miner.Active)
                    EndFight(uid, miner);
                continue;
            }

            miner.Target = target;
            if (!miner.Active)
                StartFight(uid, miner, now);

            TryBeginSplitPursuit(uid, miner, now);
            ProcessHook(uid, miner, target, now);
            if (miner.HookTargets.Count > 0)
            {
                SendHud(uid, miner);
                continue;
            }
            var from = _transform.GetWorldPosition(xform);
            var to = _transform.GetWorldPosition(target);
            var distance = Vector2.Distance(from, to);

            if (distance > miner.CatchUpDistance)
            {
                if (now >= miner.NextCatchUp)
                    CatchUp(uid, miner, target, now);
                SendHud(uid, miner);
                continue;
            }

            if (distance > miner.BlinkDistance && now >= miner.NextBlink)
                BlinkBehind(uid, miner, target, now);
            else if (distance <= 9f && now >= miner.NextFeint)
                Feint(uid, miner, target, now);

            if (now >= miner.NextHook && distance <= 14f)
            {
                miner.NextHook = now + miner.HookCooldown;
                if (_random.Prob(miner.HookAttackChance))
                    BeginHook(uid, miner, target, now);
            }
            else if (now >= miner.NextShot && distance <= 16f)
                FirePattern(uid, miner, target, now);

            SendHud(uid, miner);
        }
    }

    private bool TryPickTarget(EntityUid uid, LavalandMadMinerComponent miner, TransformComponent xform, out EntityUid target)
    {
        target = default;
        var best = float.MaxValue;
        var origin = _transform.GetWorldPosition(xform);
        var range = miner.Active ? miner.ChaseRange : miner.NoticeRange;
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } player || IsDead(player) ||
                HasComp<GhostComponent>(player) ||
                Transform(player).MapID != xform.MapID)
                continue;
            var distance = Vector2.DistanceSquared(origin, _transform.GetWorldPosition(player));
            if (distance > range * range || distance >= best)
                continue;
            best = distance;
            target = player;
        }
        return target.IsValid();
    }

    private void TryBeginSplitPursuit(EntityUid uid, LavalandMadMinerComponent miner, TimeSpan now)
    {
        if (now < miner.NextSplitPursuit || now < miner.SplitDragUntil)
            return;

        var origin = _transform.GetWorldPosition(uid);
        var map = Transform(uid).MapID;
        var nearby = new List<EntityUid>();
        EntityUid farthest = default;
        var farDistance = 0f;
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } player || IsDead(player) ||
                HasComp<GhostComponent>(player) || !HasComp<MobStateComponent>(player) ||
                Transform(player).MapID != map)
                continue;
            var distance = Vector2.Distance(origin, _transform.GetWorldPosition(player));
            if (distance > miner.ChaseRange)
                continue;
            if (distance <= miner.SplitPursuitNearRadius)
                nearby.Add(player);
            if (distance <= farDistance)
                continue;
            farDistance = distance;
            farthest = player;
        }

        if (!farthest.IsValid() || nearby.Count == 0 || farDistance < miner.SplitPursuitFarDistance)
            return;
        var distantDirection = _transform.GetWorldPosition(farthest) - origin;
        var separated = farDistance >= miner.SplitPursuitVeryFarDistance;
        foreach (var player in nearby)
        {
            var closeDirection = _transform.GetWorldPosition(player) - origin;
            if (farDistance - closeDirection.Length() >= miner.SplitPursuitDistanceGap &&
                Vector2.Dot(closeDirection, distantDirection) <= 0f)
                separated = true;
        }
        if (!separated)
            return;

        ClearHooks(uid, miner);
        if (TryComp<HTNComponent>(uid, out var htn))
            _htn.SetHTNEnabled((uid, htn), false);
        miner.Target = farthest;
        miner.NextSplitPursuit = now + miner.SplitPursuitCooldown;
        miner.SplitDragUntil = now + miner.BlinkDuration;
        miner.HookPullStarts = miner.SplitDragUntil;
        miner.HookEnds = miner.SplitDragUntil + miner.HookDuration;
        miner.NextHook = miner.HookEnds + miner.HookCooldown;
        foreach (var player in nearby)
        {
            AddHookTarget(uid, miner, player);
            miner.SplitDragOffsets[player] = _transform.GetWorldPosition(player) - origin;
        }
        if (miner.HookSound != null)
            _audio.PlayPvs(miner.HookSound, uid);
        BlinkBehind(uid, miner, farthest, now);
        if (TryComp<PhysicsComponent>(uid, out var body))
        {
            foreach (var player in nearby)
            {
                if (TryComp<PhysicsComponent>(player, out var victimBody))
                    _physics.SetLinearVelocity(player, body.LinearVelocity, body: victimBody);
            }
        }
    }

    private void StartFight(EntityUid uid, LavalandMadMinerComponent miner, TimeSpan now)
    {
        miner.Active = true;
        miner.NextBlink = now + TimeSpan.FromSeconds(0.5);
        miner.NextShot = now + TimeSpan.FromSeconds(1);
        miner.NextHook = now + TimeSpan.FromSeconds(2);
        miner.NextHud = TimeSpan.Zero;
        SendHud(uid, miner);
    }

    private void EndFight(EntityUid uid, LavalandMadMinerComponent miner)
    {
        miner.Active = false;
        miner.Target = null;
        ClearHooks(uid, miner);
        foreach (var session in miner.HudRecipients)
        {
            if (session.Status == SessionStatus.Disconnected)
                continue;
            RaiseNetworkEvent(new LavalandBossHudHideEvent(miner.HudId), session.Channel);
            RaiseNetworkEvent(new LavalandBossMusicStopEvent(miner.HudId), session.Channel);
        }
        miner.HudRecipients.Clear();
    }

    private void TrySpeak(EntityUid uid, LavalandMadMinerComponent miner, TimeSpan now)
    {
        // Quiet once the fight is on: the whispers are only for those who haven't been noticed yet.
        if (miner.Active || now < miner.NextSpeech || miner.SpeechLines.Count == 0)
            return;

        var line = Loc.GetString(_random.Pick(miner.SpeechLines));
        var spoke = false;
        foreach (var session in GetNearbySessions(uid, miner.SpeechRadius))
        {
            if (session.AttachedEntity is not { } player || IsDead(player) || HasComp<GhostComponent>(player))
                continue;
            RaiseNetworkEvent(new CelestialSpeakEvent(line, miner.SpeechDuration, miner.SpeechColor), session.Channel);
            spoke = true;
        }

        if (!spoke)
            return;
        var min = miner.SpeechCooldownMin.TotalSeconds;
        var max = Math.Max(min, miner.SpeechCooldownMax.TotalSeconds);
        miner.NextSpeech = now + TimeSpan.FromSeconds(_random.NextDouble(min, max));
    }

    private void BlinkBehind(EntityUid uid, LavalandMadMinerComponent miner, EntityUid target, TimeSpan now)
    {
        var facing = TryComp<MouseRotatorComponent>(target, out var mouse) && mouse.GoalRotation is { } goal
            ? goal.ToVec()
            : _transform.GetWorldRotation(target).ToVec();
        var destination = _transform.GetMapCoordinates(target).Offset(-facing * 1.3f);
        var distance = Vector2.Distance(_transform.GetMapCoordinates(uid).Position, destination.Position);
        var speed = Math.Max(28f, distance / Math.Max(0.05f, (float) miner.BlinkDuration.TotalSeconds));
        if (_blink.TryStartDash(uid, destination, speed, miner.BlinkDuration, miner.BlinkStallTimeout, miner.BlinkSound))
            miner.NextBlink = now + miner.BlinkCooldown;
    }

    /// <summary>A far target: jump onto open ground a few tiles from it instead of pathing across the map.</summary>
    private void CatchUp(EntityUid uid, LavalandMadMinerComponent miner, EntityUid target, TimeSpan now)
    {
        miner.NextCatchUp = now + miner.CatchUpCooldown;
        var targetCoordinates = _transform.GetMapCoordinates(target);
        var away = _transform.GetWorldPosition(uid) - targetCoordinates.Position;
        if (away.LengthSquared() < 0.01f)
            return;
        away = Vector2.Normalize(away);

        for (var landing = miner.CatchUpLanding; landing >= 2f; landing -= 1f)
        {
            var destination = targetCoordinates.Offset(away * landing);
            if (!IsOpenGround(destination))
                continue;

            if (miner.BlinkSound != null)
                _audio.PlayPvs(miner.BlinkSound, uid);
            _transform.SetMapCoordinates(uid, destination);
            if (miner.BlinkSound != null)
                _audio.PlayPvs(miner.BlinkSound, uid);
            miner.NextBlink = now + miner.BlinkCooldown;
            return;
        }
    }

    private bool IsOpenGround(MapCoordinates coordinates)
    {
        if (!_mapManager.TryFindGridAt(coordinates, out var gridUid, out var grid))
            return false;

        var tile = _map.TileIndicesFor(gridUid, grid, coordinates);
        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return false;

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var entity))
        {
            if (entity is not { } blocker)
                continue;
            if (HasComp<ChasmComponent>(blocker) || HasComp<TileEntityEffectComponent>(blocker) ||
                TryComp<PhysicsComponent>(blocker, out var body) && body.CanCollide && body.Hard)
            {
                return false;
            }
        }

        return true;
    }

    private void Feint(EntityUid uid, LavalandMadMinerComponent miner, EntityUid target, TimeSpan now)
    {
        var angle = _random.NextAngle();
        var destination = _transform.GetMapCoordinates(target).Offset(angle.ToVec() * _random.NextFloat(1.4f, 3.2f));
        var distance = Vector2.Distance(_transform.GetMapCoordinates(uid).Position, destination.Position);
        var speed = Math.Max(28f, distance / Math.Max(0.05f, (float) miner.BlinkDuration.TotalSeconds));
        if (_blink.TryStartDash(uid, destination, speed, miner.BlinkDuration, miner.BlinkStallTimeout, miner.BlinkSound))
            miner.NextFeint = now + miner.FeintCooldown;
    }

    private void FirePattern(EntityUid uid, LavalandMadMinerComponent miner, EntityUid target, TimeSpan now)
    {
        var direction = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(uid);
        if (TryComp<PhysicsComponent>(target, out var targetPhysics))
        {
            var travelTime = direction.Length() / Math.Max(1f, miner.ProjectileSpeed);
            direction += targetPhysics.LinearVelocity * travelTime;
        }
        if (direction.LengthSquared() < 0.01f)
            return;
        var baseAngle = direction.ToWorldAngle();
        _transform.SetWorldRotation(uid, baseAngle);
        switch (miner.AttackPattern++ % 3)
        {
            case 0:
                Fire(uid, miner, baseAngle);
                break;
            case 1:
                foreach (var offset in ShotgunSpread)
                    Fire(uid, miner, baseAngle + Angle.FromDegrees(offset));
                break;
            default:
                Fire(uid, miner, baseAngle - Angle.FromDegrees(5));
                Fire(uid, miner, baseAngle);
                Fire(uid, miner, baseAngle + Angle.FromDegrees(5));
                break;
        }
        miner.NextShot = now + miner.ShootCooldown;
    }

    private void Fire(EntityUid uid, LavalandMadMinerComponent miner, Angle angle)
    {
        var projectile = Spawn(miner.Projectile, Transform(uid).Coordinates);
        _gun.ShootProjectile(projectile, angle.ToWorldVec(), Vector2.Zero, uid, uid, miner.ProjectileSpeed);
    }

    private void BeginHook(EntityUid uid, LavalandMadMinerComponent miner, EntityUid target, TimeSpan now)
    {
        ClearHooks(uid, miner);
        if (TryComp<HTNComponent>(uid, out var htn))
            _htn.SetHTNEnabled((uid, htn), false);
        miner.HookEnds = now + miner.HookDuration;
        miner.HookPullStarts = now + miner.BlinkDuration;
        miner.NextHook = now + miner.HookCooldown;
        if (miner.HookSound != null)
            _audio.PlayPvs(miner.HookSound, uid);

        var minerPosition = _transform.GetMapCoordinates(uid);
        var targetPosition = _transform.GetMapCoordinates(target);
        var retreat = minerPosition.Position - targetPosition.Position;
        if (retreat.LengthSquared() < 0.01f)
            retreat = _random.NextAngle().ToVec();
        var destination = new MapCoordinates(
            targetPosition.Position + Vector2.Normalize(retreat) * miner.HookRetreatDistance,
            targetPosition.MapId);
        var distance = Vector2.Distance(minerPosition.Position, destination.Position);
        var speed = Math.Max(28f, distance / Math.Max(0.05f, (float) miner.BlinkDuration.TotalSeconds));
        _blink.TryStartDash(uid, destination, speed, miner.BlinkDuration, miner.BlinkStallTimeout, miner.BlinkSound);

        var impactCenter = _transform.GetWorldPosition(target);
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } victim ||
                IsDead(victim) || HasComp<GhostComponent>(victim) ||
                Transform(victim).MapID != Transform(uid).MapID ||
                Vector2.DistanceSquared(impactCenter, _transform.GetWorldPosition(victim)) > miner.HookSplashRadius * miner.HookSplashRadius)
                continue;
            AddHookTarget(uid, miner, victim);
        }
    }

    private void ProcessHook(EntityUid uid, LavalandMadMinerComponent miner, EntityUid fallbackTarget, TimeSpan now)
    {
        if (now >= miner.HookEnds || miner.HookTargets.Count == 0)
        {
            ClearHooks(uid, miner);
            return;
        }

        if (now < miner.SplitDragUntil)
        {
            var origin = _transform.GetWorldPosition(uid);
            var velocity = TryComp<PhysicsComponent>(uid, out var dragBody)
                ? dragBody.LinearVelocity : Vector2.Zero;
            foreach (var (target, visual) in miner.HookTargets.ToArray())
            {
                if (!Exists(target) || IsDead(target) || Transform(target).MapID != Transform(uid).MapID)
                {
                    RemoveHookTarget(miner, target);
                    continue;
                }
                if (Exists(visual))
                    _transform.SetMapCoordinates(visual, _transform.GetMapCoordinates(target));
                ConstrainHookDistance(uid, target, miner.HookMaxLength);
                if (miner.SplitDragOffsets.TryGetValue(target, out var offset) &&
                    TryComp<PhysicsComponent>(target, out var body))
                {
                    var correction = origin + offset - _transform.GetWorldPosition(target);
                    _physics.SetLinearVelocity(target, velocity + correction * 10f, body: body);
                }
            }
            return;
        }

        if (TryComp<PhysicsComponent>(uid, out var minerPhysics))
        {
            var velocity = Vector2.Zero;
            if (miner.HookAttacker is { Valid: true } attacker && Exists(attacker) &&
                Transform(attacker).MapID == Transform(uid).MapID)
            {
                var pursuit = _transform.GetWorldPosition(attacker) - _transform.GetWorldPosition(uid);
                if (pursuit.LengthSquared() > 0.04f)
                    velocity = Vector2.Normalize(pursuit) * miner.HookRetaliationSpeed;
            }
            _physics.SetLinearVelocity(uid, velocity, body: minerPhysics);
        }

        foreach (var (target, visual) in miner.HookTargets.ToArray())
        {
            if (!Exists(target) || IsDead(target))
            {
                RemoveHookTarget(miner, target);
                continue;
            }
            if (Exists(visual))
                _transform.SetMapCoordinates(visual, _transform.GetMapCoordinates(target));
            if (now < miner.HookPullStarts)
                continue;

            ConstrainHookDistance(uid, target, miner.HookMaxLength);
            var direction = _transform.GetWorldPosition(uid) - _transform.GetWorldPosition(target);
            var distance = direction.Length();
            if (distance <= miner.HookImpactRange)
            {
                var damage = TryComp<LavalandBossRageComponent>(uid, out var rage) && rage.Enraged
                    ? miner.HookImpactDamage * miner.RageHookDamageMultiplier
                    : miner.HookImpactDamage;
                _damage.TryChangeDamage(target, damage, ignoreResistances: true, origin: uid);
                if (TryComp<PhysicsComponent>(target, out var body) && distance > 0.01f)
                    _physics.SetLinearVelocity(target, -Vector2.Normalize(direction) * miner.HookKnockback, body: body);
                RemoveHookTarget(miner, target);
                continue;
            }
            if (TryComp<PhysicsComponent>(target, out var physics) && distance > 0.01f)
            {
                var stretch = Math.Max(0f, distance - miner.HookImpactRange);
                var pullSpeed = miner.HookForce + stretch * miner.HookStretchForce;
                _physics.SetLinearVelocity(target, Vector2.Normalize(direction) * pullSpeed, body: physics);
            }
        }
    }

    private void ConstrainHookDistance(EntityUid miner, EntityUid target, float maxLength)
    {
        if (maxLength <= 0f)
            return;

        var minerCoordinates = _transform.GetMapCoordinates(miner);
        var targetCoordinates = _transform.GetMapCoordinates(target);
        if (minerCoordinates.MapId != targetCoordinates.MapId)
            return;

        var offset = targetCoordinates.Position - minerCoordinates.Position;
        if (offset.LengthSquared() <= maxLength * maxLength || offset.LengthSquared() < 0.001f)
            return;

        var constrained = new MapCoordinates(
            minerCoordinates.Position + Vector2.Normalize(offset) * maxLength,
            minerCoordinates.MapId);
        _transform.SetMapCoordinates(target, constrained);
    }

    private void AddHookTarget(EntityUid uid, LavalandMadMinerComponent miner, EntityUid target)
    {
        if (miner.HookTargets.ContainsKey(target))
            return;
        var visual = Spawn(miner.HookProjectile, Transform(target).Coordinates);
        var rope = EnsureComp<JointVisualsComponent>(visual);
        rope.Sprite = new SpriteSpecifier.Rsi(
            new ResPath("_DeadSpace/Necromorfs/TheCircle/Weapon/melee/hand-hook.rsi"), "rope");
        rope.Target = uid;
        Dirty(visual, rope);
        EnsureComp<LavalandMadMinerHookVisualComponent>(visual).Target = target;
        miner.HookTargets[target] = visual;
        var hooked = EnsureComp<LavalandMadMinerHookedComponent>(target);
        _hook.SetMiner((target, hooked), uid);
    }

    private void RemoveHookTarget(LavalandMadMinerComponent miner, EntityUid target)
    {
        miner.SplitDragOffsets.Remove(target);
        if (miner.HookTargets.Remove(target, out var visual) && Exists(visual))
            QueueDel(visual);
        RemCompDeferred<LavalandMadMinerHookedComponent>(target);
    }

    private void ClearHooks(EntityUid uid, LavalandMadMinerComponent miner)
    {
        foreach (var target in miner.HookTargets.Keys.ToArray())
            RemoveHookTarget(miner, target);
        miner.HookAttacker = null;
        miner.SplitDragOffsets.Clear();
        miner.SplitDragUntil = TimeSpan.Zero;
        if (!TerminatingOrDeleted(uid) && TryComp<HTNComponent>(uid, out var htn))
            _htn.SetHTNEnabled((uid, htn), true);
    }

    private void OnHookedGetVerbs(Entity<LavalandMadMinerHookedComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (args.User != ent.Owner || ent.Comp.Removing)
            return;
        if (!HasComp<LavalandMadMinerComponent>(ent.Comp.Miner))
        {
            RemCompDeferred<LavalandMadMinerHookedComponent>(ent.Owner);
            return;
        }
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("lavaland-mad-miner-remove-hook"),
            Act = () => StartRemovingHook(ent),
        });
    }

    private void OnHookVisualInteract(Entity<LavalandMadMinerHookVisualComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || args.User != ent.Comp.Target ||
            !TryComp<LavalandMadMinerHookedComponent>(ent.Comp.Target, out var hooked))
            return;

        StartRemovingHook((ent.Comp.Target, hooked));
        args.Handled = true;
    }

    private void StartRemovingHook(Entity<LavalandMadMinerHookedComponent> ent)
    {
        if (!TryComp<LavalandMadMinerComponent>(ent.Comp.Miner, out var miner))
            return;
        ent.Comp.Removing = true;
        Dirty(ent);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, ent.Owner, miner.HookRemoveTime,
            new LavalandRemoveMadMinerHookDoAfterEvent(), ent.Owner, target: ent.Owner)
        {
            BreakOnMove = false,
            BreakOnDamage = false,
            NeedHand = false,
        });
    }

    private void OnHookRemoved(Entity<LavalandMadMinerHookedComponent> ent,
        ref LavalandRemoveMadMinerHookDoAfterEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        if (args.Cancelled)
        {
            ent.Comp.Removing = false;
            Dirty(ent);
            return;
        }
        if (TryComp<LavalandMadMinerComponent>(ent.Comp.Miner, out var miner))
            RemoveHookTarget(miner, ent.Owner);
    }

    public bool TryInterceptJaunter(EntityUid user, EntityUid jaunter)
    {
        var query = EntityQueryEnumerator<LavalandMadMinerComponent>();
        while (query.MoveNext(out var uid, out var miner))
        {
            if (!miner.Active || Transform(uid).MapID != Transform(user).MapID ||
                Vector2.DistanceSquared(_transform.GetWorldPosition(uid), _transform.GetWorldPosition(user)) > miner.ChaseRange * miner.ChaseRange)
                continue;
            _containers.TryRemoveFromContainer(jaunter, force: true);
            _transform.SetMapCoordinates(jaunter, _transform.GetMapCoordinates(uid).Offset(new Vector2(0.5f, 0)));
            return true;
        }
        return false;
    }

    /// <summary>
    /// Pulls a falling player out of a chasm to the nearest fighting miner.
    /// Called only from <see cref="LavalandJaunterSystem"/> so the attempt is handled once and after immunity checks.
    /// </summary>
    public bool TryRescueFromChasm(EntityUid tripper, ChasmFallingAttemptEvent args, EntityUid? jaunter = null)
    {
        if (args.Cancelled || !HasComp<ActorComponent>(tripper) || IsDead(tripper) || HasComp<GhostComponent>(tripper))
            return false;

        var tripperCoordinates = _transform.GetMapCoordinates(tripper);
        EntityUid? rescuer = null;
        LavalandMadMinerComponent? rescuerComp = null;
        var best = float.MaxValue;
        var query = EntityQueryEnumerator<LavalandMadMinerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var miner, out var xform))
        {
            if (!miner.Active || xform.MapID != tripperCoordinates.MapId || IsDead(uid))
                continue;
            var distance = Vector2.DistanceSquared(_transform.GetWorldPosition(xform), tripperCoordinates.Position);
            if (distance > miner.ChaseRange * miner.ChaseRange || distance >= best)
                continue;
            best = distance;
            rescuer = uid;
            rescuerComp = miner;
        }

        if (rescuer is not { } minerUid || rescuerComp == null)
            return false;

        args.Cancel();
        var minerCoordinates = _transform.GetMapCoordinates(minerUid);
        if (jaunter is { Valid: true } item && Exists(item))
        {
            _containers.TryRemoveFromContainer(item, force: true);
            _transform.SetMapCoordinates(item, minerCoordinates.Offset(new Vector2(0.5f, 0)));
        }
        _transform.SetMapCoordinates(tripper, minerCoordinates.Offset(new Vector2(2, 0)));
        BeginHook(minerUid, rescuerComp, tripper, _timing.CurTime);
        return true;
    }

    private void OnDamageChanged(Entity<LavalandMadMinerComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || ent.Comp.HookTargets.Count == 0 ||
            args.Origin is not { Valid: true } attacker || attacker == ent.Owner)
            return;
        ent.Comp.HookAttacker = attacker;
    }

    private void OnMobStateChanged(Entity<LavalandMadMinerComponent> ent, ref MobStateChangedEvent args)
    {
        // The necromorph dead for good: the fight is over.
        if (args.NewMobState == MobState.Dead && ent.Comp.Risen)
        {
            EndFight(ent.Owner, ent.Comp);
            return;
        }

        if (args.NewMobState != MobState.Dead || ent.Comp.RewardsSpawned)
            return;
        ent.Comp.RewardsSpawned = true;
        // It falls - and a few seconds later gets up again, as a necromorph.
        if (!ent.Comp.Risen)
            ent.Comp.RiseAt = _timing.CurTime + ent.Comp.RiseDelay;
        SpawnReward(ent.Owner, ent.Comp.ChainHandReward, ent.Comp.ChainHandChance);
        SpawnReward(ent.Owner, ent.Comp.ExplosivePkaReward, ent.Comp.ExplosivePkaChance);
        foreach (var reward in ent.Comp.ExtraRewards)
            SpawnReward(ent.Owner, reward, 1f);
        EndFight(ent.Owner, ent.Comp);
    }

    private void UpdateRising(TimeSpan now)
    {
        var query = EntityQueryEnumerator<LavalandMadMinerComponent>();
        while (query.MoveNext(out var uid, out var miner))
        {
            if (miner.RiseAt is not { } riseAt)
                continue;

            // The body jerks and shakes harder and harder before it gets up.
            if (!miner.RiseShaking && now >= riseAt - miner.RiseShakeTime && IsDead(uid))
            {
                miner.RiseShaking = true;
                _jitter.DoJitter(uid, miner.RiseShakeTime + TimeSpan.FromSeconds(0.3), true, 25f, 12f);
            }

            if (now < riseAt)
                continue;

            miner.RiseAt = null;
            // Gibbed or gone meanwhile: nothing left to rise.
            if (TerminatingOrDeleted(uid) || !IsDead(uid))
                continue;

            Rise((uid, miner));
        }
    }

    /// <summary>
    /// Blood bursts out of the body, and it jerks back up with a necromorph's claws: a plain monster now,
    /// with none of the miner's tricks, that goes for whoever is near.
    /// </summary>
    private void Rise(Entity<LavalandMadMinerComponent> ent)
    {
        var uid = ent.Owner;
        var miner = ent.Comp;
        miner.Risen = true;

        var coordinates = Transform(uid).Coordinates;
        for (var i = 0; i < miner.RisenBloodCount; i++)
            Spawn(miner.RisenBlood, coordinates.Offset(_random.NextVector2(1.3f)));
        if (miner.RisenBloodSound != null)
            _audio.PlayPvs(miner.RisenBloodSound, uid);

        _rejuvenate.PerformRejuvenate(uid);
        _mobThreshold.SetMobStateThreshold(uid, FixedPoint2.New(miner.RisenHealth), MobState.Dead);

        if (TryComp<MeleeWeaponComponent>(uid, out var melee))
        {
            melee.Damage = new DamageSpecifier(miner.RisenDamage);
            melee.Animation = "WeaponArcClaw";
            Dirty(uid, melee);
        }

        if (TryComp<MovementSpeedModifierComponent>(uid, out var speed))
            _speedModifier.ChangeBaseSpeed(uid, miner.RisenSpeed * 0.7f, miner.RisenSpeed, speed.Acceleration, speed);

        _appearance.SetData(uid, LavalandMadMinerVisuals.Necromorph, true);

        // Its arms tear off and fly away - the claws grow in their place.
        EnsureComp<LavalandMadMinerLimbsComponent>(uid);
        Sever(uid, miner, LavalandMadMinerLimb.ArmRight, _random.NextAngle().ToVec());
        Sever(uid, miner, LavalandMadMinerLimb.ArmLeft, _random.NextAngle().ToVec());
        if (TryComp<MovementSpeedModifierComponent>(uid, out var risenSpeed))
        {
            miner.RisenWalkSpeed = risenSpeed.BaseWalkSpeed;
            miner.RisenSprintSpeed = risenSpeed.BaseSprintSpeed;
        }

        // A real necromorph now: what works on necromorphs (the plasma cutter) works on it. Its own claws do the damage.
        if (!HasComp<NecromorfComponent>(uid))
        {
            var strain = new InfectionDeadStrainData { DamageMulty = 0f, SpeedMulty = 1f };
            AddComp(uid, new NecromorfComponent(strain));
        }
        _metaData.SetEntityName(uid, Loc.GetString(miner.RisenName));

        // Its own AI goes for whoever is near.
        if (TryComp<HTNComponent>(uid, out var htn))
        {
            _htn.SetHTNEnabled((uid, htn), true);
            _npc.WakeNPC(uid, htn);
        }

        if (miner.RisenScream != null)
            _audio.PlayPvs(miner.RisenScream, uid);
        _popup.PopupEntity(Loc.GetString("lavaland-mad-miner-risen", ("miner", uid)), uid, PopupType.LargeCaution);

        // The second stage: a fight of its own, with its own health bar and theme.
        miner.HudId = _nextHudId++;
        miner.SelectedMusic = miner.RisenMusic;
        miner.NextBlink = _timing.CurTime + TimeSpan.FromSeconds(1);
    }

    private void ThinkRisen(EntityUid uid, LavalandMadMinerComponent miner, TransformComponent xform, TimeSpan now)
    {
        if (!TryPickTarget(uid, miner, xform, out var target))
        {
            if (miner.Active)
                EndFight(uid, miner);
            return;
        }

        miner.Target = target;
        if (!miner.Active)
            StartFight(uid, miner, now);

        var distance = Vector2.Distance(_transform.GetWorldPosition(xform), _transform.GetWorldPosition(target));
        if (distance > miner.BlinkDistance && now >= miner.NextBlink)
            BlinkBehind(uid, miner, target, now);

        SendHud(uid, miner);
    }

    /// <summary>A plasma cutter tears its legs off one by one; with both gone the cutter just burns it.</summary>
    private void OnPlasmaCutterHit(Entity<LavalandMadMinerComponent> ent, ref NecromorphPlasmaCutterHitAttemptEvent args)
    {
        if (!ent.Comp.Risen || IsDead(ent.Owner) || !TryComp<LavalandMadMinerLimbsComponent>(ent, out var limbs))
            return;

        var leg = (limbs.Severed & LavalandMadMinerLimb.LegRight) == 0 ? LavalandMadMinerLimb.LegRight
            : (limbs.Severed & LavalandMadMinerLimb.LegLeft) == 0 ? LavalandMadMinerLimb.LegLeft
            : LavalandMadMinerLimb.None;
        if (leg == LavalandMadMinerLimb.None)
            return;

        args.Handled = true;
        Sever(ent.Owner, ent.Comp, leg, args.ShotDirection);

        // Half its speed on one leg, crawling on none.
        const LavalandMadMinerLimb legs = LavalandMadMinerLimb.LegRight | LavalandMadMinerLimb.LegLeft;
        var factor = (limbs.Severed & legs) == legs ? 0.1f : 0.5f;
        if (TryComp<MovementSpeedModifierComponent>(ent, out var speed))
        {
            _speedModifier.ChangeBaseSpeed(ent, ent.Comp.RisenWalkSpeed * factor, ent.Comp.RisenSprintSpeed * factor,
                speed.Acceleration, speed);
        }
    }

    /// <summary>Cuts the limb out of its sprite and throws it off as a piece of its own.</summary>
    private void Sever(EntityUid uid, LavalandMadMinerComponent miner, LavalandMadMinerLimb limb, Vector2 direction)
    {
        var limbs = EnsureComp<LavalandMadMinerLimbsComponent>(uid);
        if ((limbs.Severed & limb) != 0)
            return;

        limbs.Severed |= limb;
        Dirty(uid, limbs);

        var piece = Spawn(miner.SeveredLimb, Transform(uid).Coordinates);
        var severed = EnsureComp<LavalandMadMinerSeveredLimbComponent>(piece);
        severed.Limb = limb;
        Dirty(piece, severed);
        if (direction.LengthSquared() > 0.0001f)
            _physics.ApplyLinearImpulse(piece, Vector2.Normalize(direction) * miner.SeveredLimbImpulse);
    }

    private void SpawnReward(EntityUid uid, EntProtoId? reward, float chance)
    {
        if (reward is not { } proto || !_random.Prob(Math.Clamp(chance, 0f, 1f)) || !_prototypes.HasIndex<EntityPrototype>(proto.Id))
            return;
        Spawn(proto.Id, Transform(uid).Coordinates);
    }

    private void SendHud(EntityUid uid, LavalandMadMinerComponent miner)
    {
        var now = _timing.CurTime;
        if (now < miner.NextHud)
            return;
        miner.NextHud = now + miner.HudInterval;

        var maxHealth = miner.Risen ? miner.RisenHealth : miner.MaxHealth;
        var health = maxHealth;
        if (TryComp<DamageableComponent>(uid, out var damageable))
            health = Math.Max(0, maxHealth - damageable.TotalDamage.Float());
        var name = Loc.GetString(miner.Risen ? miner.RisenName : "lavaland-mad-miner-hud-name");
        foreach (var session in GetNearbySessions(uid, miner.ChaseRange))
        {
            // Players who walk into an ongoing fight also get the music, not only those present at its start.
            if (miner.HudRecipients.Add(session) && miner.SelectedMusic != null)
            {
                var resolved = _audio.ResolveSound(miner.SelectedMusic);
                var musicParams = miner.SelectedMusic.Params.WithLoop(true);
                // The necromorph's theme comes in from the middle of the track (the client fades it in).
                if (miner.Risen)
                    musicParams = musicParams.WithPlayOffset((float) _audio.GetAudioLength(resolved).TotalSeconds / 2f);
                RaiseNetworkEvent(new LavalandBossMusicStartEvent(miner.HudId, resolved, musicParams), session.Channel);
            }
            RaiseNetworkEvent(new LavalandBossHudUpdateEvent(miner.HudId, name, health, maxHealth, 1), session.Channel);
        }
    }

    private IEnumerable<ICommonSession> GetNearbySessions(EntityUid uid, float range)
    {
        var origin = _transform.GetWorldPosition(uid);
        var map = Transform(uid).MapID;
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is { Valid: true } player && Transform(player).MapID == map &&
                Vector2.DistanceSquared(origin, _transform.GetWorldPosition(player)) <= range * range)
                yield return session;
        }
    }

    private void BuildPlatform(EntityUid uid)
    {
        if (Transform(uid).GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;
        var center = _map.LocalToTile(gridUid, grid, Transform(uid).Coordinates);
        for (var x = -1; x <= 1; x++)
        for (var y = -1; y <= 1; y++)
        {
            // Keep the existing generated floor; the 3x3 footprint is cleared of blocking terrain by the boss's spawn layer.
            var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, center + new Vector2i(x, y));
            while (anchored.MoveNext(out var entity))
            {
                if (entity is { } obstacle && MetaData(obstacle).EntityPrototype?.ID.StartsWith("WallRockBasalt") == true)
                    QueueDel(obstacle);
            }
        }
    }

    private bool IsDead(EntityUid uid) => TryComp<MobStateComponent>(uid, out var mob) && mob.CurrentState == MobState.Dead;
}
