// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.NPC.HTN;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Atmos.Components;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Lavaland.Bosses;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Lavaland elites ported from tgstation (goliath broodmother, legionnaire, pandora), fought in a temporary
/// arena around the pulsing tumor that spawned them. Unlike tg they are AI-driven.
/// </summary>
public sealed class LavalandEliteSystem : EntitySystem
{
    private const int HudIdBase = 200000;

    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly LavalandTamingSystem _taming = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private int _nextHudId = HudIdBase;
    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();
    private readonly List<DelayedHit> _hits = new();
    private readonly List<(TimeSpan At, EntityUid Uid, EntityCoordinates Target)> _teleports = new();

    private record struct DelayedHit(
        TimeSpan At,
        EntityCoordinates Coordinates,
        DamageSpecifier Damage,
        TimeSpan Stun,
        EntityUid Source,
        string? Visual);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandEliteTumorComponent, InteractHandEvent>(OnTumorTouched);
        SubscribeLocalEvent<LavalandEliteTumorComponent, ComponentShutdown>(OnTumorShutdown);
        SubscribeLocalEvent<LavalandEliteComponent, MapInitEvent>(OnEliteMapInit);
        SubscribeLocalEvent<LavalandEliteComponent, MobStateChangedEvent>(OnEliteMobState);
        SubscribeLocalEvent<LavalandEliteComponent, ComponentShutdown>(OnEliteShutdown);
        SubscribeLocalEvent<LavalandBroodmotherComponent, RefreshMovementSpeedModifiersEvent>(OnBroodmotherSpeed);
        SubscribeLocalEvent<LavalandLegionnaireHeadComponent, MobStateChangedEvent>(OnHeadMobState);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        ProcessDelayedHits(now);
        ProcessTeleports(now);
        UpdateTumors(now);
        UpdateBonfires(now);
        UpdateChildren(now);

        var elites = EntityQueryEnumerator<LavalandEliteComponent, TransformComponent>();
        while (elites.MoveNext(out var uid, out var elite, out var xform))
        {
            if (_mobState.IsDead(uid))
                continue;

            UpdateHeadRegrow(uid, now);
            if (UpdateLegionnaireCharge(uid, xform, now))
                continue;
            UpdateBroodmotherPassive(uid, now);

            if (!TryFindTarget(xform, elite.AggroRange, out var target))
            {
                if (elite.Active)
                    EndFight(elite);
                continue;
            }

            elite.Target = target;
            if (!elite.Active)
            {
                elite.Active = true;
                elite.NextAttack = now + TimeSpan.FromSeconds(1);
            }

            SendHud(uid, elite, now);
            if (now < elite.NextAttack)
                continue;

            elite.NextAttack = now + Attack(uid, xform, target, now);
        }
    }

    #region Tumor

    private void OnTumorTouched(Entity<LavalandEliteTumorComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || ent.Comp.Elite != null || ent.Comp.Elites.Count == 0)
            return;
        args.Handled = true;

        var coordinates = Transform(ent).Coordinates;
        var elite = Spawn(_random.Pick(ent.Comp.Elites), coordinates.Offset(new Vector2(0f, 1f)));
        EnsureComp<LavalandEliteComponent>(elite).Tumor = ent.Owner;
        ent.Comp.Elite = elite;
        ent.Comp.Activator = args.User;
        ent.Comp.AwaySince = null;
        _appearance.SetData(ent, LavalandArtifactVisuals.Active, true);
        BuildArena(ent, coordinates);

        _popup.PopupEntity(Loc.GetString("lavaland-elite-awakens", ("elite", elite)), ent, PopupType.LargeCaution);
        if (ent.Comp.ActivateSound != null)
            _audio.PlayPvs(ent.Comp.ActivateSound, ent);
    }

    private void BuildArena(Entity<LavalandEliteTumorComponent> ent, EntityCoordinates center)
    {
        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var centerTile = _map.TileIndicesFor(gridUid, grid, center);
        var radius = ent.Comp.ArenaRadius;
        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                var distance = MathF.Sqrt(x * x + y * y);
                if (distance < radius - 0.5f || distance > radius + 0.5f)
                    continue;

                var tile = centerTile + new Vector2i(x, y);
                if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty ||
                    _turf.IsTileBlocked(gridUid, tile, CollisionGroup.Impassable, grid))
                {
                    continue;
                }

                ent.Comp.Walls.Add(Spawn(ent.Comp.WallPrototype, _map.GridTileToLocal(gridUid, grid, tile)));
            }
        }
    }

    private void ClearArena(LavalandEliteTumorComponent tumor)
    {
        foreach (var wall in tumor.Walls)
            QueueDel(wall);
        tumor.Walls.Clear();
    }

    private void OnTumorShutdown(Entity<LavalandEliteTumorComponent> ent, ref ComponentShutdown args)
    {
        ClearArena(ent.Comp);
    }

    private void UpdateTumors(TimeSpan now)
    {
        var tumors = EntityQueryEnumerator<LavalandEliteTumorComponent, TransformComponent>();
        while (tumors.MoveNext(out var uid, out var tumor, out var xform))
        {
            if (tumor.Elite is not { } elite)
                continue;

            if (TerminatingOrDeleted(elite) || _mobState.IsDead(elite))
            {
                // The elite is beaten: the tumor bursts, the arena falls.
                ClearArena(tumor);
                QueueDel(uid);
                continue;
            }

            var away = tumor.Activator is not { } activator || TerminatingOrDeleted(activator) ||
                       _mobState.IsDead(activator) ||
                       Transform(activator).MapID != xform.MapID ||
                       Vector2.Distance(_transform.GetWorldPosition(activator), _transform.GetWorldPosition(xform)) >
                       tumor.ArenaRadius + 2;

            if (!away)
            {
                tumor.AwaySince = null;
                continue;
            }

            tumor.AwaySince ??= now;
            if (now - tumor.AwaySince < tumor.LeashTime)
                continue;

            // The challenger fled or fell: the elite goes back to sleep.
            ClearArena(tumor);
            if (TryComp<LavalandEliteComponent>(elite, out var eliteComp))
                eliteComp.RewardsSpawned = true;
            QueueDel(elite);
            tumor.Elite = null;
            tumor.Activator = null;
            tumor.AwaySince = null;
            _appearance.SetData(uid, LavalandArtifactVisuals.Active, false);
        }
    }

    #endregion

    #region Common elite logic

    private void OnEliteMapInit(Entity<LavalandEliteComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.HudId = _nextHudId++;
        ent.Comp.SelectedMusic = ent.Comp.Music.Count == 0 ? null : _random.Pick(ent.Comp.Music);
    }

    private void OnEliteMobState(Entity<LavalandEliteComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || ent.Comp.RewardsSpawned)
            return;

        ent.Comp.RewardsSpawned = true;
        var coordinates = ent.Comp.Tumor is { } tumor && Exists(tumor)
            ? Transform(tumor).Coordinates
            : Transform(ent).Coordinates;
        foreach (var reward in ent.Comp.Rewards)
        {
            if (_random.Prob(Math.Clamp(reward.Chance, 0f, 1f)))
                Spawn(reward.Prototype, coordinates);
        }

        if (TryComp<LavalandBroodmotherComponent>(ent, out var brood))
        {
            foreach (var child in brood.Children)
                QueueDel(child);
            brood.Children.Clear();
        }

        if (TryComp<LavalandLegionnaireComponent>(ent, out var legionnaire))
        {
            if (legionnaire.Head is { } head)
                QueueDel(head);
            if (legionnaire.Bonfire is { } bonfire)
                QueueDel(bonfire);
        }

        EndFight(ent.Comp);
    }

    private void OnEliteShutdown(Entity<LavalandEliteComponent> ent, ref ComponentShutdown args)
    {
        EndFight(ent.Comp);
    }

    private bool TryFindTarget(TransformComponent xform, float range, out EntityUid target)
    {
        target = default;
        var best = range * range;
        var position = _transform.GetWorldPosition(xform);
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } player || TerminatingOrDeleted(player) ||
                HasComp<GhostComponent>(player) || !HasComp<MobStateComponent>(player) || !_mobState.IsAlive(player))
            {
                continue;
            }

            var playerXform = Transform(player);
            if (playerXform.MapID != xform.MapID)
                continue;

            var distance = Vector2.DistanceSquared(position, _transform.GetWorldPosition(playerXform));
            if (distance > best)
                continue;

            best = distance;
            target = player;
        }

        return target.IsValid();
    }

    private void SendHud(EntityUid uid, LavalandEliteComponent elite, TimeSpan now)
    {
        if (now < elite.NextHud)
            return;
        elite.NextHud = now + elite.HudInterval;

        var health = elite.MaxHealth;
        if (TryComp<DamageableComponent>(uid, out var damageable))
            health = Math.Max(0f, elite.MaxHealth - damageable.TotalDamage.Float());

        var name = Loc.GetString(elite.HudName);
        var position = _transform.GetWorldPosition(uid);
        var map = Transform(uid).MapID;
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } player || Transform(player).MapID != map ||
                Vector2.DistanceSquared(position, _transform.GetWorldPosition(player)) > elite.AggroRange * elite.AggroRange * 4)
            {
                continue;
            }

            if (elite.HudRecipients.Add(session) && elite.SelectedMusic != null)
            {
                RaiseNetworkEvent(new LavalandBossMusicStartEvent(elite.HudId,
                    _audio.ResolveSound(elite.SelectedMusic), elite.SelectedMusic.Params.WithLoop(true)), session.Channel);
            }

            RaiseNetworkEvent(new LavalandBossHudUpdateEvent(elite.HudId, name, health, elite.MaxHealth, 1), session.Channel);
        }
    }

    private void EndFight(LavalandEliteComponent elite)
    {
        elite.Active = false;
        elite.Target = null;
        foreach (var session in elite.HudRecipients)
        {
            if (session.Status == SessionStatus.Disconnected)
                continue;
            RaiseNetworkEvent(new LavalandBossHudHideEvent(elite.HudId), session.Channel);
            RaiseNetworkEvent(new LavalandBossMusicStopEvent(elite.HudId), session.Channel);
        }
        elite.HudRecipients.Clear();
    }

    /// <summary>Uses one random attack of the elite and returns its cooldown.</summary>
    private TimeSpan Attack(EntityUid uid, TransformComponent xform, EntityUid target, TimeSpan now)
    {
        var attack = _random.Next(4);
        if (TryComp<LavalandBroodmotherComponent>(uid, out var brood))
            return BroodmotherAttack((uid, brood), target, attack, now);
        if (TryComp<LavalandLegionnaireComponent>(uid, out var legionnaire))
            return LegionnaireAttack((uid, legionnaire), xform, target, attack, now);
        if (TryComp<LavalandPandoraComponent>(uid, out var pandora))
            return PandoraAttack((uid, pandora), xform, target, attack, now);
        return TimeSpan.FromSeconds(2);
    }

    private float HealthFraction(EntityUid uid)
    {
        if (!TryComp<LavalandEliteComponent>(uid, out var elite) || !TryComp<DamageableComponent>(uid, out var damageable))
            return 1f;
        return Math.Clamp(1f - damageable.TotalDamage.Float() / Math.Max(1f, elite.MaxHealth), 0f, 1f);
    }

    private EntityCoordinates TileCenter(EntityCoordinates coordinates)
    {
        return _transform.GetGrid(coordinates) is { } grid && TryComp<MapGridComponent>(grid, out var gridComp)
            ? _map.GridTileToLocal(grid, gridComp, _map.TileIndicesFor(grid, gridComp, coordinates))
            : coordinates;
    }

    private void QueueHit(TimeSpan at, EntityCoordinates coordinates, DamageSpecifier damage, TimeSpan stun,
        EntityUid source, string? visual)
    {
        _hits.Add(new DelayedHit(at, TileCenter(coordinates), damage, stun, source, visual));
    }

    private void ProcessDelayedHits(TimeSpan now)
    {
        for (var i = _hits.Count - 1; i >= 0; i--)
        {
            var hit = _hits[i];
            if (now < hit.At)
                continue;
            _hits.RemoveAt(i);

            if (hit.Visual != null)
                Spawn(hit.Visual, hit.Coordinates);

            // Only a warning mark.
            if (hit.Damage.Empty)
                continue;

            var hitTile = _transform.ToMapCoordinates(hit.Coordinates).Position;
            _mobs.Clear();
            _lookup.GetEntitiesInRange(hit.Coordinates, 0.5f, _mobs);
            foreach (var mob in _mobs)
            {
                if (mob.Owner == hit.Source || HasComp<LavalandEliteComponent>(mob) ||
                    HasComp<LavalandBroodmotherChildComponent>(mob) || HasComp<LavalandLegionnaireHeadComponent>(mob) ||
                    _mobState.IsDead(mob, mob.Comp))
                {
                    continue;
                }

                // Only who stands on this very tile: brushing the neighbouring ones does not count, or one standing
                // on the edge between tiles takes the same blow two to four times over.
                var mobTile = _transform.ToMapCoordinates(TileCenter(Transform(mob).Coordinates)).Position;
                if (Vector2.DistanceSquared(mobTile, hitTile) > 0.01f)
                    continue;

                _damageable.TryChangeDamage(mob.Owner, hit.Damage, origin: Exists(hit.Source) ? hit.Source : null);
                if (hit.Stun > TimeSpan.Zero)
                    _stun.TryUpdateParalyzeDuration(mob, hit.Stun);
            }
        }
    }

    private void ProcessTeleports(TimeSpan now)
    {
        for (var i = _teleports.Count - 1; i >= 0; i--)
        {
            var (at, uid, target) = _teleports[i];
            if (now < at)
                continue;
            _teleports.RemoveAt(i);
            if (!TerminatingOrDeleted(uid))
                _transform.SetCoordinates(uid, target);
        }
    }

    #endregion

    #region Broodmother

    private TimeSpan BroodmotherAttack(Entity<LavalandBroodmotherComponent> ent, EntityUid target, int attack, TimeSpan now)
    {
        ent.Comp.Children.RemoveAll(child => TerminatingOrDeleted(child) || _mobState.IsDead(child));
        switch (attack)
        {
            case 0:
                TentaclePatch(ent, Transform(target).Coordinates, now);
                _popup.PopupEntity(Loc.GetString("lavaland-broodmother-patch", ("elite", ent.Owner)), ent, PopupType.MediumCaution);
                return ent.Comp.PatchCooldown;
            case 1:
                for (var i = 0; i < ent.Comp.ChildrenPerSpawn && ent.Comp.Children.Count < ent.Comp.MaxChildren; i++)
                {
                    var child = Spawn(ent.Comp.ChildPrototype, Transform(ent).Coordinates);
                    EnsureComp<LavalandBroodmotherChildComponent>(child).Mother = ent.Owner;
                    // A tamed mother's children are on her tamer's side too.
                    _taming.InheritSide(ent, child);
                    ent.Comp.Children.Add(child);
                }
                _popup.PopupEntity(Loc.GetString("lavaland-broodmother-children", ("elite", ent.Owner)), ent, PopupType.MediumCaution);
                return ent.Comp.ChildrenCooldown;
            case 2:
                ent.Comp.RageUntil = now + ent.Comp.RageDuration;
                _movement.RefreshMovementSpeedModifiers(ent);
                _popup.PopupEntity(Loc.GetString("lavaland-broodmother-rage", ("elite", ent.Owner)), ent, PopupType.LargeCaution);
                return ent.Comp.RageCooldown;
            default:
                var origin = Transform(ent).Coordinates;
                var index = 0;
                foreach (var child in ent.Comp.Children)
                {
                    var angle = Angle.FromDegrees(45 * index++);
                    _transform.SetCoordinates(child, origin.Offset(angle.ToVec() * 1.2f));
                }
                _popup.PopupEntity(Loc.GetString("lavaland-broodmother-call", ("elite", ent.Owner)), ent, PopupType.MediumCaution);
                return ent.Comp.CallCooldown;
        }
    }

    private void TentaclePatch(Entity<LavalandBroodmotherComponent> ent, EntityCoordinates center, TimeSpan now)
    {
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
                Tentacle(ent, center.Offset(new Vector2(x, y)), now);
        }

        foreach (var direction in new[] { Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY })
            Tentacle(ent, center.Offset(direction * 2), now);
    }

    private void Tentacle(Entity<LavalandBroodmotherComponent> ent, EntityCoordinates coordinates, TimeSpan now)
    {
        Spawn(ent.Comp.TentacleVisual, TileCenter(coordinates));
        QueueHit(now + ent.Comp.TentacleDelay, coordinates, ent.Comp.TentacleDamage, ent.Comp.TentacleStun, ent, null);
    }

    private void UpdateBroodmotherPassive(EntityUid uid, TimeSpan now)
    {
        if (!TryComp<LavalandBroodmotherComponent>(uid, out var brood))
            return;

        if (brood.RageUntil != TimeSpan.Zero && now >= brood.RageUntil)
        {
            brood.RageUntil = TimeSpan.Zero;
            _movement.RefreshMovementSpeedModifiers(uid);
        }

        var health = HealthFraction(uid);
        if (health >= 0.5f || now < brood.NextPassive)
            return;

        brood.NextPassive = now + brood.PassiveInterval;
        var count = health < 0.25f ? 10 : 5;
        var origin = Transform(uid).Coordinates;
        for (var i = 0; i < count; i++)
        {
            var offset = new Vector2(_random.Next(-brood.PassiveRange, brood.PassiveRange + 1),
                _random.Next(-brood.PassiveRange, brood.PassiveRange + 1));
            Tentacle((uid, brood), origin.Offset(offset), now);
        }
    }

    private void OnBroodmotherSpeed(Entity<LavalandBroodmotherComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.RageUntil > _timing.CurTime)
            args.ModifySpeed(ent.Comp.RageSpeed, ent.Comp.RageSpeed);
    }

    private void UpdateChildren(TimeSpan now)
    {
        var children = EntityQueryEnumerator<LavalandBroodmotherChildComponent, TransformComponent>();
        while (children.MoveNext(out var uid, out var child, out var xform))
        {
            if (now < child.NextTentacle || _mobState.IsDead(uid) ||
                child.Mother is not { } mother || !TryComp<LavalandBroodmotherComponent>(mother, out var brood) ||
                !TryFindTarget(xform, child.TentacleRange, out var target))
            {
                continue;
            }

            child.NextTentacle = now + child.TentacleCooldown;
            Tentacle((mother, brood), Transform(target).Coordinates, now);
        }
    }

    #endregion

    #region Legionnaire

    private TimeSpan LegionnaireAttack(Entity<LavalandLegionnaireComponent> ent,
        TransformComponent xform,
        EntityUid target,
        int attack,
        TimeSpan now)
    {
        switch (attack)
        {
            case 0:
                StartLegionnaireCharge(ent, xform, target, now);
                return ent.Comp.ChargeCooldown;
            case 1:
                DetachHead(ent, target);
                return TimeSpan.FromSeconds(1);
            case 2:
                BonfireTeleport(ent, xform);
                return TimeSpan.FromSeconds(0.5);
            default:
                var smokeAt = ent.Comp.Head is { } head && Exists(head) ? Transform(head).Coordinates : xform.Coordinates;
                Spawn(ent.Comp.SmokePrototype, smokeAt);
                _popup.PopupEntity(Loc.GetString("lavaland-legionnaire-smoke", ("elite", ent.Owner)), ent, PopupType.MediumCaution);
                return ent.Comp.ChargeCooldown;
        }
    }

    private void StartLegionnaireCharge(Entity<LavalandLegionnaireComponent> ent, TransformComponent xform, EntityUid target, TimeSpan now)
    {
        var offset = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(xform);
        if (offset.LengthSquared() < 0.01f)
            return;

        ent.Comp.ChargeDirection = Vector2.Normalize(offset);
        ent.Comp.ChargeStartAt = now + ent.Comp.ChargeWindup;
        ent.Comp.ChargeHit.Clear();
        for (var i = 1; i <= ent.Comp.ChargeLength; i++)
            Spawn(ent.Comp.ChargeTelegraph, TileCenter(xform.Coordinates.Offset(ent.Comp.ChargeDirection * i)));

        _popup.PopupEntity(Loc.GetString("lavaland-legionnaire-charge", ("elite", ent.Owner)), ent, PopupType.LargeCaution);
        if (ent.Comp.ChargeSound != null)
            _audio.PlayPvs(ent.Comp.ChargeSound, ent);
        SetAi(ent, false);
    }

    /// <summary>Returns true while the legionnaire is busy with its charge.</summary>
    private bool UpdateLegionnaireCharge(EntityUid uid, TransformComponent xform, TimeSpan now)
    {
        if (!TryComp<LavalandLegionnaireComponent>(uid, out var legionnaire) || !TryComp<PhysicsComponent>(uid, out var physics))
            return false;

        if (legionnaire.ChargeStartAt is { } start)
        {
            if (now < start)
                return true;

            legionnaire.ChargeStartAt = null;
            legionnaire.ChargeEndAt = now + TimeSpan.FromSeconds(legionnaire.ChargeLength / Math.Max(1f, legionnaire.ChargeSpeed));
        }

        if (legionnaire.ChargeEndAt is not { } end)
            return false;

        if (now >= end)
        {
            legionnaire.ChargeEndAt = null;
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
            SetAi(uid, true);
            return false;
        }

        _physics.SetLinearVelocity(uid, legionnaire.ChargeDirection * legionnaire.ChargeSpeed, body: physics);
        _mobs.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, 1f, _mobs);
        foreach (var mob in _mobs)
        {
            if (mob.Owner == uid || HasComp<LavalandLegionnaireHeadComponent>(mob) || _mobState.IsDead(mob, mob.Comp) ||
                !legionnaire.ChargeHit.Add(mob))
            {
                continue;
            }

            _damageable.TryChangeDamage(mob.Owner, legionnaire.ChargeDamage, origin: uid);
            _stun.TryUpdateParalyzeDuration(mob, legionnaire.ChargeStun);
            if (TryComp<PhysicsComponent>(mob, out var victim))
                _physics.SetLinearVelocity(mob, legionnaire.ChargeDirection * legionnaire.ChargeKnockback, body: victim);
        }

        return true;
    }

    private void DetachHead(Entity<LavalandLegionnaireComponent> ent, EntityUid target)
    {
        if (ent.Comp.Head is { } head && Exists(head))
        {
            // Using the ability again recalls the head by destroying it.
            QueueDel(head);
            HeadLost(ent);
            return;
        }

        if (!ent.Comp.HasHead)
            return;

        ent.Comp.HasHead = false;
        _appearance.SetData(ent, LavalandArtifactVisuals.Active, true);
        var newHead = Spawn(ent.Comp.HeadPrototype, Transform(ent).Coordinates);
        EnsureComp<LavalandLegionnaireHeadComponent>(newHead).Body = ent.Owner;
        ent.Comp.Head = newHead;
        _popup.PopupEntity(Loc.GetString("lavaland-legionnaire-head", ("elite", ent.Owner)), ent, PopupType.LargeCaution);
    }

    private void OnHeadMobState(Entity<LavalandLegionnaireHeadComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || ent.Comp.Body is not { } body ||
            !TryComp<LavalandLegionnaireComponent>(body, out var legionnaire))
        {
            return;
        }

        HeadLost((body, legionnaire));
        QueueDel(ent);
    }

    private void HeadLost(Entity<LavalandLegionnaireComponent> ent)
    {
        ent.Comp.Head = null;
        ent.Comp.RegrowAt = _timing.CurTime + ent.Comp.HeadRegrowTime;
    }

    private void UpdateHeadRegrow(EntityUid uid, TimeSpan now)
    {
        if (!TryComp<LavalandLegionnaireComponent>(uid, out var legionnaire) ||
            legionnaire.RegrowAt is not { } regrow || now < regrow)
        {
            return;
        }

        legionnaire.RegrowAt = null;
        legionnaire.HasHead = true;
        _appearance.SetData(uid, LavalandArtifactVisuals.Active, false);
        _popup.PopupEntity(Loc.GetString("lavaland-legionnaire-regrow", ("elite", uid)), uid, PopupType.MediumCaution);
    }

    private void BonfireTeleport(Entity<LavalandLegionnaireComponent> ent, TransformComponent xform)
    {
        if (ent.Comp.Bonfire is not { } bonfire || TerminatingOrDeleted(bonfire))
        {
            var newBonfire = Spawn(ent.Comp.BonfirePrototype, TileCenter(xform.Coordinates));
            EnsureComp<LavalandBonfireComponent>(newBonfire).Legionnaire = ent.Owner;
            ent.Comp.Bonfire = newBonfire;
            _popup.PopupEntity(Loc.GetString("lavaland-legionnaire-bonfire", ("elite", ent.Owner)), ent, PopupType.MediumCaution);
        }
        else
        {
            var bonfireCoordinates = Transform(bonfire).Coordinates;
            var ownCoordinates = TileCenter(xform.Coordinates);
            if (TileCenter(bonfireCoordinates).Equals(ownCoordinates))
            {
                QueueDel(bonfire);
                ent.Comp.Bonfire = null;
                return;
            }

            _transform.SetCoordinates(ent, bonfireCoordinates);
            _transform.SetCoordinates(bonfire, ownCoordinates);
            _popup.PopupEntity(Loc.GetString("lavaland-legionnaire-swap", ("elite", ent.Owner)), ent, PopupType.MediumCaution);
        }

        if (ent.Comp.BonfireSound != null)
            _audio.PlayPvs(ent.Comp.BonfireSound, ent);
    }

    private void UpdateBonfires(TimeSpan now)
    {
        var bonfires = EntityQueryEnumerator<LavalandBonfireComponent, TransformComponent>();
        while (bonfires.MoveNext(out _, out var bonfire, out var xform))
        {
            if (now < bonfire.NextBurn)
                continue;
            bonfire.NextBurn = now + bonfire.Interval;

            _mobs.Clear();
            _lookup.GetEntitiesInRange(xform.Coordinates, 0.5f, _mobs);
            foreach (var mob in _mobs)
            {
                // Only what can burn at all (a living light, say, cannot).
                if (mob.Owner != bonfire.Legionnaire && !_mobState.IsDead(mob, mob.Comp) &&
                    TryComp<FlammableComponent>(mob, out var flammable))
                {
                    _flammable.AdjustFireStacks(mob, bonfire.FireStacks, flammable, ignite: true);
                }
            }
        }
    }

    #endregion

    #region Pandora

    private TimeSpan PandoraAttack(Entity<LavalandPandoraComponent> ent,
        TransformComponent xform,
        EntityUid target,
        int attack,
        TimeSpan now)
    {
        var health = HealthFraction(ent);
        var cooldowns = ent.Comp.Cooldowns;
        var cooldown = cooldowns.Count == 0
            ? TimeSpan.FromSeconds(2)
            : cooldowns[Math.Min(cooldowns.Count - 1, health >= 0.5f ? 0 : health > 0.25f ? 1 : 2)];
        var step = cooldown * 0.1;
        var targetCoordinates = TileCenter(Transform(target).Coordinates);

        switch (attack)
        {
            case 0:
                // A straight line of blasts in one of eight directions.
                var direction = (_transform.GetWorldPosition(target) - _transform.GetWorldPosition(xform)).ToWorldAngle()
                    .GetDir().ToVec();
                var origin = TileCenter(xform.Coordinates);
                for (var i = 1; i <= ent.Comp.ShotLength; i++)
                    PandoraBlast(ent, now + step * i, origin.Offset(direction * i));
                return cooldown * 0.5 + ent.Comp.WarningTime;
            case 1:
                // A box of blasts around the target, sparing only its immediate surroundings.
                for (var x = -3; x <= 3; x++)
                {
                    for (var y = -3; y <= 3; y++)
                    {
                        if (Math.Max(Math.Abs(x), Math.Abs(y)) > 1)
                            PandoraBlast(ent, now, targetCoordinates.Offset(new Vector2(x, y)));
                    }
                }
                return cooldown + ent.Comp.WarningTime;
            case 2:
                if (Vector2.Distance(_transform.GetWorldPosition(xform), _transform.GetWorldPosition(target)) > ent.Comp.TeleportRange)
                    return cooldown;

                var source = TileCenter(xform.Coordinates);
                for (var x = -1; x <= 1; x++)
                {
                    for (var y = -1; y <= 1; y++)
                    {
                        PandoraBlast(ent, now, source.Offset(new Vector2(x, y)));
                        PandoraBlast(ent, now, targetCoordinates.Offset(new Vector2(x, y)));
                    }
                }
                // It moves just as both squares go off.
                _teleports.Add((now + ent.Comp.WarningTime, ent.Owner, targetCoordinates));
                if (ent.Comp.TeleportSound != null)
                    _audio.PlayPvs(ent.Comp.TeleportSound, ent);
                return cooldown * 2;
            default:
                // Rings of blasts spreading out from the target.
                for (var ring = 0; ring <= 3; ring++)
                {
                    for (var x = -ring; x <= ring; x++)
                    {
                        for (var y = -ring; y <= ring; y++)
                        {
                            if (Math.Max(Math.Abs(x), Math.Abs(y)) == ring)
                                PandoraBlast(ent, now + step * ring, targetCoordinates.Offset(new Vector2(x, y)));
                        }
                    }
                }
                return cooldown + ent.Comp.WarningTime;
        }
    }

    /// <summary>The mark shows up on the tile at <paramref name="at"/>, the blast goes off there a warning later.</summary>
    private void PandoraBlast(Entity<LavalandPandoraComponent> ent, TimeSpan at, EntityCoordinates coordinates)
    {
        QueueHit(at, coordinates, NoDamage, TimeSpan.Zero, ent, ent.Comp.TelegraphVisual);
        QueueHit(at + ent.Comp.WarningTime, coordinates, ent.Comp.BlastDamage, TimeSpan.Zero, ent, ent.Comp.BlastVisual);
    }

    private static readonly DamageSpecifier NoDamage = new();

    #endregion

    private void SetAi(EntityUid uid, bool enabled)
    {
        if (TryComp<HTNComponent>(uid, out var htn))
            _htn.SetHTNEnabled((uid, htn), enabled);
    }
}
