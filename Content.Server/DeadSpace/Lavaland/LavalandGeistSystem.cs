// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.DeadSpace.Traps;
using Content.Shared.Damage.Systems;
using Content.Shared.Chasm;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Lavaland.Bosses;
using Content.Shared.SSDIndicator;
using Content.Shared.DeadSpace.Traps;
using Content.Shared.Ghost;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// The geists of the Circle haunting lavaland: bear traps hidden in the flowers, a geist glimpsed at the edge of sight
/// that runs off when approached, and a geist rising half out of a hole to throw traps around a player. None of them
/// can be harmed; a shot while the ambusher aims only drops it back into its hole.
/// </summary>
public sealed class LavalandGeistSystem : EntitySystem
{
    [Dependency] private readonly BearTrapSystem _bearTrap = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly Content.Shared.Examine.ExamineSystemShared _examine = default!;
    [Dependency] private readonly Robust.Shared.Audio.Systems.SharedAudioSystem _audio = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly TimeSpan TriggerInterval = TimeSpan.FromSeconds(0.4);
    private static readonly Robust.Shared.Prototypes.EntProtoId HoleDust = "LavalandGeistHoleDust";
    private static readonly Robust.Shared.Prototypes.EntProtoId VanishSmoke = "LavalandWellTeleportSmoke";


    private TimeSpan _nextTriggerCheck;

    private readonly List<(EntityUid Uid, MapId Map, Vector2 Position)> _players2 = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandGeistPhantomComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<LavalandGeistPhantomComponent, MapInitEvent>(OnPhantomMapInit);
        SubscribeLocalEvent<LavalandGeistPhantomComponent, ChasmFallingAttemptEvent>(OnPhantomChasm);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        LayFlowerTraps();

        if (now >= _nextTriggerCheck)
        {
            _nextTriggerCheck = now + TriggerInterval;
            CollectPlayers();
            UpdateWatchTriggers(now);
            UpdateAmbushTriggers(now);
        }

        UpdatePhantoms(now);
        UpdateLandingTraps(now);
    }

    /// <summary>A geist stands among people as a player would - but nobody plays it: no "disconnected" mark over it.</summary>
    private void OnPhantomMapInit(Entity<LavalandGeistPhantomComponent> ent, ref MapInitEvent args)
    {
        RemComp<SSDIndicatorComponent>(ent);
        RemComp<Content.Shared.Strip.Components.StrippableComponent>(ent);
        // It doesn't breathe: no gasping in the ash storms, no suffocating.
        RemComp<Content.Server.Body.Components.RespiratorComponent>(ent);
    }

    /// <summary>A geist touching a chasm is simply gone.</summary>
    private void OnPhantomChasm(Entity<LavalandGeistPhantomComponent> ent, ref ChasmFallingAttemptEvent args)
    {
        args.Cancel();
        if (!ent.Comp.Ambusher)
            Vanish(ent);
    }

    /// <summary>Gone in a puff of smoke: hidden at once, deleted a moment later under the smoke.</summary>
    private void Vanish(Entity<LavalandGeistPhantomComponent> ent)
    {
        if (TerminatingOrDeleted(ent) || ent.Comp.Stage == LavalandGeistStage.Vanishing)
            return;

        Spawn(VanishSmoke, Transform(ent).Coordinates);
        Hide(ent, _timing.CurTime);
    }

    /// <summary>
    /// Stops drawing it, then deletes it a little later: deleted while still drawn, its clothes could go a moment
    /// before its body and leave a bare doll standing there.
    /// </summary>
    private void Hide(Entity<LavalandGeistPhantomComponent> ent, TimeSpan now)
    {
        ent.Comp.Stage = LavalandGeistStage.Vanishing;
        ent.Comp.StageEnd = now + TimeSpan.FromSeconds(0.4);
        SetHole(ent, LavalandGeistHolePhase.Hidden, TimeSpan.Zero, now);
        if (TryComp<InputMoverComponent>(ent, out var mover))
            mover.CurTickSprintMovement = Vector2.Zero;
    }

    /// <summary>How far a shot throws it back as it drops (tiles).</summary>
    private const float KnockDistance = 0.2f;

    private void SetHole(EntityUid uid, LavalandGeistHolePhase phase, TimeSpan duration, TimeSpan now)
    {
        var hole = EnsureComp<LavalandGeistHoleComponent>(uid);
        hole.Knock = Vector2.Zero;
        hole.Phase = phase;
        hole.PhaseStart = now;
        hole.Duration = (float) duration.TotalSeconds;
        Dirty(uid, hole);
    }

    #region Hidden traps

    /// <summary>A marker in the flowers lays an ordinary bear trap there, armed, and is gone.</summary>
    private void LayFlowerTraps()
    {
        var query = EntityQueryEnumerator<LavalandGeistTrapSpawnerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var spawner, out var xform))
        {
            if (xform.GridUid == null)
                continue;

            LayTrap(spawner.Trap, _transform.GetMapCoordinates(xform));
            QueueDel(uid);
        }
    }

    /// <summary>An ordinary bear trap, armed by nobody (anyone stepping on it is caught).</summary>
    private EntityUid LayTrap(EntProtoId prototype, MapCoordinates at)
    {
        var trap = Spawn(prototype, at);
        if (TryComp<BearTrapComponent>(trap, out var bearTrap))
            _bearTrap.Arm((trap, bearTrap), null);
        return trap;
    }

    #endregion

    #region Triggers

    private void CollectPlayers()
    {
        _players2.Clear();
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } mob || HasComp<GhostComponent>(mob) ||
                !HasComp<MobStateComponent>(mob) || !_mobState.IsAlive(mob))
            {
                continue;
            }

            var coords = _transform.GetMapCoordinates(mob);
            _players2.Add((mob, coords.MapId, coords.Position));
        }
    }

    private bool TryFindPlayerNear(TransformComponent xform, float radius, out (EntityUid Uid, MapId Map, Vector2 Position) player)
    {
        var position = _transform.GetWorldPosition(xform);
        foreach (var candidate in _players2)
        {
            if (candidate.Map == xform.MapID && Vector2.DistanceSquared(candidate.Position, position) <= radius * radius)
            {
                player = candidate;
                return true;
            }
        }

        player = default;
        return false;
    }

    /// <summary>A geist somewhere at the edge of the player's sight, standing and watching him.</summary>
    private void UpdateWatchTriggers(TimeSpan now)
    {
        var query = EntityQueryEnumerator<LavalandGeistWatchTriggerComponent, TransformComponent>();
        while (query.MoveNext(out _, out var trigger, out var xform))
        {
            if (now < trigger.NextTrigger || !TryFindPlayerNear(xform, trigger.TriggerRadius, out var player))
                continue;

            for (var attempt = 0; attempt < 16; attempt++)
            {
                var direction = _random.NextAngle().ToVec();
                var toEdge = MathF.Min(
                    MathF.Abs(direction.X) > 0.001f ? trigger.ViewHalfSize.X / MathF.Abs(direction.X) : float.MaxValue,
                    MathF.Abs(direction.Y) > 0.001f ? trigger.ViewHalfSize.Y / MathF.Abs(direction.Y) : float.MaxValue);
                // Just inside the edge of his screen: seen, but far off.
                var spot = new MapCoordinates(player.Position + direction * MathF.Max(4f, toEdge - 1.5f), player.Map);
                if (!IsVisibleSpot(new MapCoordinates(player.Position, player.Map), spot))
                    continue;

                trigger.NextTrigger = now + trigger.Cooldown;
                var phantom = Spawn(trigger.Phantom, spot);
                var comp = EnsureComp<LavalandGeistPhantomComponent>(phantom);
                comp.Player = player.Uid;
                comp.Stage = LavalandGeistStage.Watching;
                comp.StageEnd = now + comp.WatchTime;
                break;
            }
        }
    }

    /// <summary>A geist rises half out of a hole by the player to throw its traps.</summary>
    private void UpdateAmbushTriggers(TimeSpan now)
    {
        var query = EntityQueryEnumerator<LavalandGeistAmbushTriggerComponent, TransformComponent>();
        while (query.MoveNext(out _, out var trigger, out var xform))
        {
            if (now < trigger.NextTrigger || !TryFindPlayerNear(xform, trigger.TriggerRadius, out var player))
                continue;

            // Out of one of the chasms around him that he can see - no chasm about, no ambush.
            var playerCoords = new MapCoordinates(player.Position, player.Map);
            if (!TryFindChasmNear(playerCoords, trigger.ChasmRange, out var spot))
                continue;

            trigger.NextTrigger = now + trigger.Cooldown;
            var phantom = Spawn(trigger.Phantom, spot);
            var comp = EnsureComp<LavalandGeistPhantomComponent>(phantom);
            comp.Ambusher = true;
            comp.Player = player.Uid;
            comp.Trap = trigger.Trap;
            comp.TrapCount = trigger.TrapCount;
            comp.TrapRadius = trigger.TrapRadius;
            comp.Stage = LavalandGeistStage.Rising;
            comp.StageEnd = now + comp.RiseTime;
            SetHole(phantom, LavalandGeistHolePhase.Rising, comp.RiseTime, now);

            var dust = Spawn(HoleDust, spot);
            EnsureComp<TimedDespawnComponent>(dust).Lifetime =
                (float) (comp.RiseTime + comp.AimTime + comp.SinkTime).TotalSeconds + 0.6f;
            comp.HoleParts.Add(dust);
        }
    }

    /// <summary>The middle of a chasm tile near the player, in his sight, not right next to him.</summary>
    private bool TryFindChasmNear(MapCoordinates player, float range, out MapCoordinates spot)
    {
        spot = default;
        if (!_mapManager.TryFindGridAt(player, out var gridUid, out var grid))
            return false;

        var center = _map.TileIndicesFor(gridUid, grid, player);
        var radius = (int) MathF.Ceiling(range);
        var found = new List<MapCoordinates>();
        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                var tile = center + new Vector2i(dx, dy);
                var position = _map.GridTileToWorldPos(gridUid, grid, tile);
                var distance = Vector2.Distance(position, player.Position);
                if (distance < 2f || distance > range || !HasChasm(gridUid, grid, tile))
                    continue;

                var coords = new MapCoordinates(position, player.MapId);
                if (_examine.InRangeUnOccluded(player, coords, distance + 1f, null))
                    found.Add(coords);
            }
        }

        if (found.Count == 0)
            return false;

        spot = _random.Pick(found);
        return true;
    }

    private bool HasChasm(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<ChasmComponent>(uid))
                return true;
        }

        return false;
    }

    #endregion

    #region Geists

    private void UpdatePhantoms(TimeSpan now)
    {
        var query = EntityQueryEnumerator<LavalandGeistPhantomComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var geist, out var xform))
        {
            if (geist.Stage == LavalandGeistStage.Vanishing)
            {
                if (now >= geist.StageEnd)
                    QueueDel(uid);
                continue;
            }

            // Spawned by hand (not by a trigger): it just stands there.
            if (geist.Player == null)
                continue;

            switch (geist.Stage)
            {
                case LavalandGeistStage.Watching:
                    Watch((uid, geist), xform, now);
                    break;

                case LavalandGeistStage.Fleeing:
                    Flee((uid, geist), xform, now);
                    break;

                case LavalandGeistStage.Rising when now >= geist.StageEnd:
                    StartAiming((uid, geist), now);
                    break;

                case LavalandGeistStage.Aiming when now >= geist.StageEnd:
                    ThrowTraps((uid, geist));
                    StartSinking((uid, geist), now);
                    break;

                case LavalandGeistStage.Sinking when now >= geist.StageEnd:
                    Hide((uid, geist), now);
                    break;

            }
        }
    }

    /// <summary>It stands and watches; anyone coming close sends it running.</summary>
    private void Watch(Entity<LavalandGeistPhantomComponent> ent, TransformComponent xform, TimeSpan now)
    {
        if (now >= ent.Comp.StageEnd)
        {
            Vanish(ent);
            return;
        }

        var position = _transform.GetWorldPosition(xform);
        foreach (var player in _players2)
        {
            if (player.Map != xform.MapID ||
                Vector2.DistanceSquared(player.Position, position) > ent.Comp.FleeDistance * ent.Comp.FleeDistance)
            {
                continue;
            }

            ent.Comp.Player = player.Uid;
            ent.Comp.Stage = LavalandGeistStage.Fleeing;
            ent.Comp.StageEnd = now + ent.Comp.FleeTime;
            return;
        }
    }

    /// <summary>Running flat out away from the player; a few seconds of it, and it is gone.</summary>
    private void Flee(Entity<LavalandGeistPhantomComponent> ent, TransformComponent xform, TimeSpan now)
    {
        if (now >= ent.Comp.StageEnd || ent.Comp.Player is not { } player || TerminatingOrDeleted(player))
        {
            Vanish(ent);
            return;
        }

        if (!TryComp<InputMoverComponent>(ent, out var mover))
            return;

        // Up against a wall: it is not getting anywhere - gone.
        var position = _transform.GetWorldPosition(xform);
        if (now >= ent.Comp.NextStuckCheck)
        {
            var moved = Vector2.Distance(position, ent.Comp.LastPosition);
            var firstCheck = ent.Comp.NextStuckCheck == TimeSpan.Zero;
            ent.Comp.LastPosition = position;
            ent.Comp.NextStuckCheck = now + TimeSpan.FromSeconds(0.3);
            if (!firstCheck && moved < 0.25f)
            {
                Vanish(ent);
                return;
            }
        }

        var away = position - _transform.GetWorldPosition(player);

        // Off the edge of the player's sight: gone there, before it leaves his view range altogether.
        if (away.LengthSquared() > VanishDistance * VanishDistance)
        {
            Vanish(ent);
            return;
        }

        if (away.LengthSquared() < 0.01f)
            away = _random.NextAngle().ToVec();

        mover.CurTickSprintMovement = Vector2.Normalize(away);
        mover.LastInputTick = _timing.CurTick;
        mover.LastInputSubTick = ushort.MaxValue;
    }

    /// <summary>How far from the player a fleeing geist gets before it is gone (tiles): about the edge of the screen.</summary>
    private const float VanishDistance = 12f;

    /// <summary>Half out of its hole, it marks the tiles around the player its traps will land on.</summary>
    private void StartAiming(Entity<LavalandGeistPhantomComponent> ent, TimeSpan now)
    {
        var geist = ent.Comp;
        geist.Stage = LavalandGeistStage.Aiming;
        geist.StageEnd = now + geist.AimTime;
        geist.TrapSpots.Clear();
        geist.Marks.Clear();

        if (geist.Player is not { } player || TerminatingOrDeleted(player))
            return;

        var center = _transform.GetMapCoordinates(player);
        if (!_mapManager.TryFindGridAt(center, out var gridUid, out var grid))
            return;

        var origin = _map.TileIndicesFor(gridUid, grid, center);
        var picked = new HashSet<Vector2i>();
        for (var attempt = 0; attempt < geist.TrapCount * 6 && picked.Count < geist.TrapCount; attempt++)
        {
            var tile = origin + new Vector2i(_random.Next(-geist.TrapRadius, geist.TrapRadius + 1),
                _random.Next(-geist.TrapRadius, geist.TrapRadius + 1));
            var coords = new MapCoordinates(_map.GridTileToWorldPos(gridUid, grid, tile), center.MapId);
            if (!picked.Add(tile) || !IsOpenGround(coords))
                continue;

            geist.TrapSpots.Add(coords);
        }

        // No marks: it only winds up, shaking, its traps in hand (the shake is drawn by the client with the hole).
        SetHole(ent, LavalandGeistHolePhase.Holding, TimeSpan.Zero, now);
    }

    /// <summary>It tosses its traps up out of the hole: each flies in an arc and lands on its tile.</summary>
    private void ThrowTraps(Entity<LavalandGeistPhantomComponent> ent)
    {
        var now = _timing.CurTime;
        var from = _transform.GetWorldPosition(ent) + new Vector2(0f, 0.3f);
        foreach (var spot in ent.Comp.TrapSpots)
        {
            var trap = LayTrap(ent.Comp.Trap, spot);
            var flight = 0.45f + Vector2.Distance(from, spot.Position) * 0.06f;
            var motion = EnsureComp<LavalandSpriteMotionComponent>(trap);
            motion.Start = now;
            motion.Duration = flight;
            motion.From = from - spot.Position;
            motion.To = Vector2.Zero;
            motion.Arc = 1.6f + _random.NextFloat(0f, 0.8f);
            motion.Spin = _random.NextFloat(8f, 14f);
            motion.EndRotation = 0f;
            Dirty(trap, motion);
            _landingTraps.Add((trap, now + TimeSpan.FromSeconds(flight + 0.1f)));
        }

        if (ent.Comp.TrapSpots.Count > 0)
            _audio.PlayPvs(ThrowSound, ent);
        ent.Comp.TrapSpots.Clear();
    }

    private static readonly Robust.Shared.Audio.SoundSpecifier ThrowSound =
        new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");

    private readonly List<(EntityUid Trap, TimeSpan Until)> _landingTraps = new();

    private void UpdateLandingTraps(TimeSpan now)
    {
        for (var i = _landingTraps.Count - 1; i >= 0; i--)
        {
            var (trap, until) = _landingTraps[i];
            if (now < until)
                continue;

            _landingTraps.RemoveAt(i);
            if (!TerminatingOrDeleted(trap))
                RemComp<LavalandSpriteMotionComponent>(trap);
        }
    }

    /// <summary>Back down the hole it came from.</summary>
    private void StartSinking(Entity<LavalandGeistPhantomComponent> ent, TimeSpan now)
    {
        ent.Comp.Stage = LavalandGeistStage.Sinking;
        // Deleted only a moment after it has faded out completely, so nothing of it is seen going.
        ent.Comp.StageEnd = now + ent.Comp.SinkTime + TimeSpan.FromSeconds(0.3);
        SetHole(ent, LavalandGeistHolePhase.Sinking, ent.Comp.SinkTime, now);
    }

    /// <summary>
    /// Nothing harms a geist. A shot at the ambusher while it aims makes it drop back into its hole at once,
    /// its traps never thrown.
    /// </summary>
    private void OnBeforeDamage(Entity<LavalandGeistPhantomComponent> ent, ref BeforeDamageChangedEvent args)
    {
        args.Cancelled = true;
        if (!ent.Comp.Ambusher ||
            ent.Comp.Stage is not (LavalandGeistStage.Rising or LavalandGeistStage.Aiming) ||
            args.Origin is not { } origin || origin == ent.Owner || HasComp<LavalandGeistPhantomComponent>(origin))
        {
            return;
        }

        ent.Comp.TrapSpots.Clear();
        foreach (var mark in ent.Comp.Marks)
        {
            if (!TerminatingOrDeleted(mark))
                QueueDel(mark);
        }

        ent.Comp.Marks.Clear();
        _popup.PopupEntity(Loc.GetString("lavaland-geist-ambush-shot"), ent, PopupType.MediumCaution);
        StartSinking(ent, _timing.CurTime);

        // Knocked back a little, away from whoever hit it, as it drops.
        var away = _transform.GetWorldPosition(ent) - _transform.GetWorldPosition(origin);
        if (away.LengthSquared() < 0.0001f)
            away = _random.NextAngle().ToVec();
        if (TryComp<LavalandGeistHoleComponent>(ent, out var hole))
        {
            hole.Knock = Vector2.Normalize(away) * KnockDistance;
            Dirty(ent, hole);
        }
    }

    #endregion

    /// <summary>A floor tile with nothing solid on it.</summary>
    private bool IsOpenGround(MapCoordinates coords)
    {
        if (!_mapManager.TryFindGridAt(coords, out var gridUid, out var grid))
            return false;

        var tile = _map.TileIndicesFor(gridUid, grid, coords);
        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return false;

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<ChasmComponent>(uid) ||
                TryComp<PhysicsComponent>(uid, out var body) && body.Hard && body.CanCollide)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Open ground the player can actually see from where he stands: not behind a wall or a rock.</summary>
    private bool IsVisibleSpot(MapCoordinates player, MapCoordinates spot)
    {
        return IsOpenGround(spot) &&
               _examine.InRangeUnOccluded(player, spot, Vector2.Distance(player.Position, spot.Position) + 1f, null);
    }
}
