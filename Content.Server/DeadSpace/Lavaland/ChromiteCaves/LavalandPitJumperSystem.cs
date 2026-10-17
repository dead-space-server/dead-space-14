// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Chasm;
using Content.Shared.CombatMode;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Content.Shared.DeadSpace.Lavaland.SecretBoss;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.NPC;
using Content.Shared.Physics;
using Content.Shared.Weapons.Melee;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland.ChromiteCaves;

/// <summary>
/// Runs <see cref="LavalandPitJumperComponent"/> - the argocytes of the chromite caves. All of it is done here, with
/// their AI off: their pathfinding gets lost on the huge caves grid.
/// <list type="bullet">
/// <item>Waiting: it ambles about, never stepping into a chasm.</item>
/// <item>Spotting a player with a chasm at hand: whistle, dive in, sit below a few seconds, crawl out of
/// another chasm by the player and go for him.</item>
/// <item>Hunting: it runs at him and bites. A chasm in its way: it dives through it, out of a chasm by him;
/// still catching its breath after the last dive - it goes round the chasm. It never walks over one.</item>
/// <item>The player gone far or dead: it crawls back out of its own chasm and waits again.</item>
/// </list>
/// </summary>
public sealed class LavalandPitJumperSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly LavalandPitLandingSystem _landing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.3);

    /// <summary>A player this close keeps it awake (asleep its movement is not processed at all).</summary>
    private const float AwakeRange = 30f;

    /// <summary>How far ahead it looks for a chasm in its way.</summary>
    private const float LookAhead = 0.9f;

    /// <summary>Farther than this from the player when it comes back up, it gives up and climbs out of its own chasm.</summary>
    private const float GiveUpRange = 24f;

    private static readonly float[] DetourAngles = { 0f, 35f, -35f, 70f, -70f, 105f, -105f };

    private static readonly Vector2i[] Around =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(-1, 1), new(1, -1), new(-1, -1),
    };

    private readonly HashSet<Entity<ChasmComponent>> _chasms = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandPitJumperComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<LavalandPitJumperComponent, ChasmFallingAttemptEvent>(OnChasmFallingAttempt);
    }

    private void OnMapInit(Entity<LavalandPitJumperComponent> ent, ref MapInitEvent args)
    {
        Wander(ent);
    }

    /// <summary>
    /// It never falls. Pushed onto a chasm anyway: with its dive ready and somebody to go for it dives through it,
    /// otherwise the chasm just holds it until it walks off.
    /// </summary>
    private void OnChasmFallingAttempt(Entity<LavalandPitJumperComponent> ent, ref ChasmFallingAttemptEvent args)
    {
        args.Cancel();
        if (ent.Comp.Stage != LavalandPitJumperStage.Idle || HasComp<LavalandPitLandingComponent>(ent) ||
            _mobState.IsDead(ent) || _timing.CurTime < ent.Comp.NextDive)
        {
            return;
        }

        var xform = Transform(ent);
        if (!TryGetQuarry(ent, xform, out var target))
            return;

        StartDive(ent, xform, target, _transform.GetWorldPosition(args.Chasm), false);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandPitJumperComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var jumper, out var xform))
        {
            var ent = (uid, jumper);
            if (_mobState.IsDead(uid))
            {
                if (jumper.Stage != LavalandPitJumperStage.Idle)
                    Reset(ent);
                continue;
            }

            // Spawned anchored (by a biome layer): it could never move.
            if (xform.Anchored)
                _transform.Unanchor(uid, xform);

            switch (jumper.Stage)
            {
                case LavalandPitJumperStage.Idle:
                    UpdateIdle(ent, xform, now);
                    break;

                case LavalandPitJumperStage.Alert when now >= jumper.StageEnd:
                    Dive(ent, now);
                    break;

                case LavalandPitJumperStage.Diving when now >= jumper.StageEnd:
                    Hide(ent, now);
                    break;

                case LavalandPitJumperStage.Hidden when now >= jumper.StageEnd:
                    Emerge(ent, now);
                    break;
            }
        }
    }

    private void UpdateIdle(Entity<LavalandPitJumperComponent> ent, TransformComponent xform, TimeSpan now)
    {
        var jumper = ent.Comp;
        if (now >= jumper.NextCheck)
        {
            jumper.NextCheck = now + CheckInterval;

            // Nobody about: it stays put.
            if (!KeepAwake(ent, xform))
                return;

            if (jumper.Hunting)
                UpdateHunt(ent, xform, now);
            else
                TrySpot(ent, xform, now);
        }

        // Out of a chasm just now: let it climb out first.
        if (HasComp<LavalandPitLandingComponent>(ent) || jumper.Stage != LavalandPitJumperStage.Idle)
            return;

        if (jumper.Hunting)
            Chase(ent, xform, now);
        else
            Stroll(ent, xform, now);
    }

    #region Waiting

    private void Wander(Entity<LavalandPitJumperComponent> ent)
    {
        ent.Comp.Hunting = false;
        ent.Comp.Prey = null;
        SetAi(ent, false);
    }

    /// <summary>A few steps in some direction, a pause, a few steps again - never into a chasm.</summary>
    private void Stroll(Entity<LavalandPitJumperComponent> ent, TransformComponent xform, TimeSpan now)
    {
        var jumper = ent.Comp;
        if (!TryComp<InputMoverComponent>(ent, out var mover))
            return;

        if (now >= jumper.NextStroll)
        {
            jumper.StrollDirection = _random.NextAngle().ToVec();
            jumper.StrollUntil = now + TimeSpan.FromSeconds(_random.NextFloat(0.6f, 1.6f));
            jumper.NextStroll = jumper.StrollUntil + TimeSpan.FromSeconds(_random.NextFloat(1.5f, 4f));
        }

        if (now >= jumper.StrollUntil)
            return;

        var position = _transform.GetWorldPosition(xform);
        if (IsChasmAt(new MapCoordinates(position + jumper.StrollDirection * LookAhead, xform.MapID)))
        {
            // A chasm ahead: this stroll is over.
            jumper.StrollUntil = now;
            return;
        }

        Move(mover, jumper.StrollDirection * 0.5f);
    }

    /// <summary>A player in sight: with a chasm at hand and its dive ready, the whistle and the dive; else straight at him.</summary>
    private void TrySpot(Entity<LavalandPitJumperComponent> ent, TransformComponent xform, TimeSpan now)
    {
        if (!HasComp<LavalandChromiteCavesComponent>(xform.MapUid) ||
            !TryFindPlayer(xform, ent.Comp.NoticeRange, true, out var target))
        {
            return;
        }

        if (now >= ent.Comp.NextDive && TryFindChasm(xform, ent.Comp.ChasmRange, out var chasm))
        {
            StartDive(ent, xform, target, chasm, true);
            return;
        }

        StartHunt(ent, target, now);
    }

    #endregion

    #region Hunting

    private void StartHunt(Entity<LavalandPitJumperComponent> ent, EntityUid prey, TimeSpan now)
    {
        ent.Comp.Hunting = true;
        ent.Comp.Prey = prey;
        ent.Comp.LastPlayerNear = now;
        SetAi(ent, false);
    }

    /// <summary>Keeps its prey (or the nearest player); nobody near for a while - it goes back to waiting.</summary>
    private void UpdateHunt(Entity<LavalandPitJumperComponent> ent, TransformComponent xform, TimeSpan now)
    {
        if (IsValidPrey(ent.Comp.Prey, xform, ent.Comp.NoticeRange * 2f))
        {
            ent.Comp.LastPlayerNear = now;
            return;
        }

        if (TryFindPlayer(xform, ent.Comp.NoticeRange * 1.5f, false, out var prey))
        {
            ent.Comp.Prey = prey;
            ent.Comp.LastPlayerNear = now;
            return;
        }

        ent.Comp.Prey = null;
        if (now - ent.Comp.LastPlayerNear >= ent.Comp.CalmDownTime)
            Wander(ent);
    }

    /// <summary>
    /// Runs at its prey and bites when it is in reach. A chasm right ahead: with its dive ready it dives through,
    /// out by the prey; otherwise it takes the nearest way round. Boxed in by chasms it waits for its dive.
    /// </summary>
    private void Chase(Entity<LavalandPitJumperComponent> ent, TransformComponent xform, TimeSpan now)
    {
        var jumper = ent.Comp;
        if (jumper.Prey is not { } prey || !IsValidPrey(prey, xform, float.MaxValue) ||
            !TryComp<InputMoverComponent>(ent, out var mover))
        {
            return;
        }

        var position = _transform.GetWorldPosition(xform);
        var offset = _transform.GetWorldPosition(prey) - position;
        var distance = offset.Length();

        // Bites as soon as he is in its reach - a big argocyte stops farther off than its bite range.
        if (_melee.TryGetWeapon(ent, out var weaponUid, out var weapon) &&
            distance <= MathF.Max(jumper.BiteRange, weapon.Range) && weapon.NextAttack <= now)
        {
            // A blow needs combat mode on; with its AI off nothing else turns it on.
            _combatMode.SetInCombatMode(ent, true);
            _melee.AttemptLightAttack(ent, weaponUid, weapon, prey);
        }

        if (distance <= jumper.BiteRange || distance < 0.01f)
            return;

        var direction = offset / distance;
        var ahead = new MapCoordinates(position + direction * LookAhead, xform.MapID);
        if (TryGetChasmAt(ahead, out var chasm))
        {
            if (now >= jumper.NextDive)
            {
                StartDive(ent, xform, prey, chasm, false);
                return;
            }

            if (!TryDetour(position, direction, xform.MapID, out direction))
                return;
        }

        Move(mover, direction);
    }

    /// <summary>The nearest way round a chasm ahead: turned more and more to either side until the way is clear.</summary>
    private bool TryDetour(Vector2 position, Vector2 direction, MapId map, out Vector2 detour)
    {
        foreach (var degrees in DetourAngles)
        {
            detour = new Angle(MathHelper.DegreesToRadians(degrees)).RotateVec(direction);
            if (!IsChasmAt(new MapCoordinates(position + detour * LookAhead, map)))
                return true;
        }

        detour = direction;
        return false;
    }

    #endregion

    #region Diving

    /// <summary>
    /// Off into the chasm after <paramref name="target"/>: after the whistle when it only just spotted him
    /// (<paramref name="whistle"/>), straight away when a chasm is in its way.
    /// </summary>
    private void StartDive(Entity<LavalandPitJumperComponent> ent, TransformComponent xform, EntityUid target,
        Vector2 chasm, bool whistle)
    {
        var jumper = ent.Comp;
        var position = _transform.GetWorldPosition(xform);
        jumper.Target = target;
        jumper.ChasmPosition = chasm;
        // Where it climbs back out if he gets away: the edge of the chasm it dives into, on its side.
        var back = position - chasm;
        jumper.Home = new MapCoordinates(
            back.LengthSquared() > 0.01f ? chasm + Vector2.Normalize(back) * 1.1f : position,
            xform.MapID);
        SetAi(ent, false);

        if (!whistle)
        {
            Dive(ent, _timing.CurTime);
            return;
        }

        jumper.Stage = LavalandPitJumperStage.Alert;
        jumper.StageEnd = _timing.CurTime + jumper.AlertTime;
        var alert = EnsureComp<LavalandPitJumperAlertComponent>(ent);
        alert.Duration = (float) jumper.AlertTime.TotalSeconds;
        Dirty(ent, alert);
        if (jumper.WhistleSound != null)
            _audio.PlayPvs(jumper.WhistleSound, ent);
    }

    /// <summary>Leaps into the chasm with the same dive the players do.</summary>
    private void Dive(Entity<LavalandPitJumperComponent> ent, TimeSpan now)
    {
        RemComp<LavalandPitJumperAlertComponent>(ent);
        var jumping = EnsureComp<LavalandPitJumpingComponent>(ent);
        jumping.Style = _random.Pick(Enum.GetValues<LavalandPitJumpStyle>());
        jumping.Target = ent.Comp.ChasmPosition - _transform.GetWorldPosition(ent);
        jumping.Duration = (float) ent.Comp.DiveTime.TotalSeconds;
        Dirty(ent, jumping);

        ent.Comp.Stage = LavalandPitJumperStage.Diving;
        ent.Comp.StageEnd = now + ent.Comp.DiveTime;
        if (ent.Comp.DiveSound != null)
            _audio.PlayPvs(ent.Comp.DiveSound, ent);
    }

    /// <summary>Gone below: it waits out of sight, far away on the caves map.</summary>
    private void Hide(Entity<LavalandPitJumperComponent> ent, TimeSpan now)
    {
        RemComp<LavalandPitJumpingComponent>(ent);
        if (!TryComp<LavalandChromiteCavesComponent>(Transform(ent).MapUid, out var caves))
        {
            Reset(ent);
            return;
        }

        // Away from the shaft where falling players wait.
        _transform.SetMapCoordinates(ent, caves.Holding.Offset(new Vector2(40f + _random.NextFloat(0f, 20f), 0f)));

        // Dancing the same player around the chasms makes every next trip below shorter, down to the minimum.
        var jumper = ent.Comp;
        if (jumper.StreakTarget != jumper.Target || now - jumper.LastEmerge > jumper.StreakWindow)
            jumper.Streak = 0;
        jumper.StreakTarget = jumper.Target;

        var hidden = jumper.HiddenTime - jumper.HiddenTimeReduction * jumper.Streak;
        if (hidden < jumper.MinHiddenTime)
            hidden = jumper.MinHiddenTime;

        jumper.Streak++;
        jumper.Stage = LavalandPitJumperStage.Hidden;
        jumper.StageEnd = now + hidden;
    }

    /// <summary>
    /// Crawls out of a chasm by the player and goes for him. Only if he is gone (dead, another map, far away) it climbs
    /// back out of its own chasm and waits again.
    /// </summary>
    private void Emerge(Entity<LavalandPitJumperComponent> ent, TimeSpan now)
    {
        var jumper = ent.Comp;
        var home = jumper.Home;
        var spot = home;
        var pit = jumper.ChasmPosition;
        var byPlayer = false;

        if (jumper.Target is { } target && !TerminatingOrDeleted(target) && _mobState.IsAlive(target))
        {
            var targetCoords = _transform.GetMapCoordinates(target);
            if (targetCoords.MapId == home.MapId &&
                Vector2.Distance(targetCoords.Position, home.Position) <= GiveUpRange &&
                TryFindEmergeSpot(targetCoords, jumper, out var near, out var nearPit))
            {
                spot = near;
                pit = nearPit;
                byPlayer = true;
            }
        }

        _transform.SetMapCoordinates(ent, spot);
        _landing.StartLanding(ent, jumper.EmergeTime, pit - spot.Position);

        jumper.Stage = LavalandPitJumperStage.Idle;
        jumper.LastEmerge = now;

        if (byPlayer && jumper.Target is { } prey)
        {
            // A short breath before the next dive: enough to get to him and bite, not to vanish again at once.
            jumper.NextDive = now + jumper.EmergeTime + jumper.HuntDiveCooldown;
            StartHunt(ent, prey, now);
        }
        else
        {
            jumper.NextDive = now + jumper.Cooldown;
            Wander(ent);
        }

        jumper.Target = null;
    }

    /// <summary>
    /// The edge of a chasm by the player to crawl out of: one of the closest to him, picked at random so he never knows
    /// which hole it comes from - not the one it dived into. Found tile by tile around him (the chasms are anchored
    /// on the caves grid). No free edge at all: right out of the chasm nearest to him.
    /// </summary>
    private bool TryFindEmergeSpot(MapCoordinates target, LavalandPitJumperComponent jumper, out MapCoordinates spot,
        out Vector2 pit)
    {
        spot = default;
        pit = default;
        if (!_mapManager.TryFindGridAt(target, out var gridUid, out var grid))
        {
            Log.Warning($"Argocyte: no grid under the player at {target}, back out of its own chasm.");
            return false;
        }

        var center = _map.TileIndicesFor(gridUid, grid, target);
        var radius = (int) MathF.Ceiling(jumper.EmergePitRange * 2f);
        var edges = new List<(float Distance, Vector2 Spot, Vector2 Pit)>();
        var nearestPit = (Distance: float.MaxValue, Pit: Vector2.Zero);
        var pits = 0;

        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                var tile = center + new Vector2i(dx, dy);
                if (!HasChasm(gridUid, grid, tile))
                    continue;

                var chasmPosition = _map.GridTileToWorldPos(gridUid, grid, tile);
                // Not out of the very hole it went into.
                if (Vector2.DistanceSquared(chasmPosition, jumper.ChasmPosition) < 0.5f)
                    continue;

                pits++;
                var pitDistance = Vector2.Distance(chasmPosition, target.Position);
                if (pitDistance >= 1.2f && pitDistance < nearestPit.Distance)
                    nearestPit = (pitDistance, chasmPosition);

                foreach (var step in Around)
                {
                    var edge = tile + step;
                    if (!IsFloorTile(gridUid, grid, edge))
                        continue;

                    var edgePosition = _map.GridTileToWorldPos(gridUid, grid, edge);
                    var toPlayer = Vector2.Distance(edgePosition, target.Position);
                    // Next to him, not right on top of him.
                    if (toPlayer < 1.2f)
                        continue;

                    edges.Add((toPlayer, edgePosition, chasmPosition));
                }
            }
        }

        if (edges.Count > 0)
        {
            var pick = _random.Pick(edges.OrderBy(e => e.Distance).Take(4).ToList());
            spot = new MapCoordinates(pick.Spot, target.MapId);
            pit = pick.Pit;
            return true;
        }

        // No free edge by any chasm: straight out of the nearest one (it never falls in).
        if (nearestPit.Distance < float.MaxValue)
        {
            spot = new MapCoordinates(nearestPit.Pit, target.MapId);
            pit = nearestPit.Pit;
            return true;
        }

        Log.Warning($"Argocyte: no chasm within {radius} tiles of the player ({pits} checked), back out of its own chasm.");
        return false;
    }

    #endregion

    #region Helpers

    private void Move(InputMoverComponent mover, Vector2 direction)
    {
        mover.CurTickSprintMovement = direction;
        mover.LastInputTick = _timing.CurTick;
        mover.LastInputSubTick = ushort.MaxValue;
    }

    /// <summary>The player it is after: its prey while hunting, else whoever is near.</summary>
    private bool TryGetQuarry(Entity<LavalandPitJumperComponent> ent, TransformComponent xform, out EntityUid target)
    {
        if (ent.Comp.Hunting && ent.Comp.Prey is { } prey && IsValidPrey(prey, xform, float.MaxValue))
        {
            target = prey;
            return true;
        }

        return TryFindPlayer(xform, ent.Comp.NoticeRange * 2f, false, out target);
    }

    private bool IsValidPrey(EntityUid? prey, TransformComponent xform, float range)
    {
        if (prey is not { } uid || TerminatingOrDeleted(uid) || !_mobState.IsAlive(uid) || HasComp<GhostComponent>(uid))
            return false;

        var preyXform = Transform(uid);
        return preyXform.MapID == xform.MapID &&
               (range == float.MaxValue ||
                Vector2.Distance(_transform.GetWorldPosition(preyXform), _transform.GetWorldPosition(xform)) <= range);
    }

    private bool TryFindPlayer(TransformComponent xform, float range, bool needSight, out EntityUid target)
    {
        target = default;
        var position = _transform.GetWorldPosition(xform);
        var best = range * range;
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } player || HasComp<GhostComponent>(player) ||
                !HasComp<MobStateComponent>(player) || !_mobState.IsAlive(player))
            {
                continue;
            }

            var playerXform = Transform(player);
            if (playerXform.MapID != xform.MapID)
                continue;

            var distance = Vector2.DistanceSquared(position, _transform.GetWorldPosition(playerXform));
            if (distance > best ||
                needSight && !_interaction.InRangeUnobstructed((xform.Owner, xform), (player, playerXform), range))
            {
                continue;
            }

            best = distance;
            target = player;
        }

        return target.IsValid();
    }

    /// <summary>With a player about it must be awake: asleep, its movement is not processed at all.</summary>
    private bool KeepAwake(EntityUid uid, TransformComponent xform)
    {
        if (!TryFindPlayer(xform, AwakeRange, false, out _))
            return false;

        if (!HasComp<ActiveNPCComponent>(uid))
            _npc.WakeNPC(uid);
        return true;
    }

    private bool TryFindChasm(TransformComponent xform, float range, out Vector2 position)
    {
        position = default;
        var origin = _transform.GetWorldPosition(xform);
        var best = float.MaxValue;
        _chasms.Clear();
        _lookup.GetEntitiesInRange(_transform.GetMapCoordinates(xform), range, _chasms, LookupFlags.Static);
        foreach (var chasm in _chasms)
        {
            var chasmPosition = _transform.GetWorldPosition(chasm);
            var distance = Vector2.DistanceSquared(origin, chasmPosition);
            if (distance >= best)
                continue;

            best = distance;
            position = chasmPosition;
        }

        return best < float.MaxValue;
    }

    private bool IsChasmAt(MapCoordinates coords)
    {
        return TryGetChasmAt(coords, out _);
    }

    /// <summary>A chasm on the tile at these coordinates, and where its middle is.</summary>
    private bool TryGetChasmAt(MapCoordinates coords, out Vector2 chasm)
    {
        chasm = default;
        if (!_mapManager.TryFindGridAt(coords, out var gridUid, out var grid))
            return false;

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, _map.TileIndicesFor(gridUid, grid, coords));
        while (anchored.MoveNext(out var uid))
        {
            if (!HasComp<ChasmComponent>(uid))
                continue;

            chasm = _transform.GetWorldPosition(uid.Value);
            return true;
        }

        return false;
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

    /// <summary>A floor tile to stand on: a tile, no chasm on it, no wall or rock.</summary>
    private bool IsFloorTile(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty ||
            HasChasm(gridUid, grid, tile))
        {
            return false;
        }

        return !_turf.IsTileBlocked(tileRef, CollisionGroup.Impassable);
    }

    private void Reset(Entity<LavalandPitJumperComponent> ent)
    {
        RemComp<LavalandPitJumperAlertComponent>(ent);
        RemComp<LavalandPitJumpingComponent>(ent);
        if (ent.Comp.Stage == LavalandPitJumperStage.Hidden && ent.Comp.Home.MapId != MapId.Nullspace)
            _transform.SetMapCoordinates(ent, ent.Comp.Home);

        ent.Comp.Stage = LavalandPitJumperStage.Idle;
        ent.Comp.Target = null;
        Wander(ent);
    }

    private void SetAi(EntityUid uid, bool enabled)
    {
        if (TryComp<HTNComponent>(uid, out var htn))
            _htn.SetHTNEnabled((uid, htn), enabled, enabled ? 0.3f : 0f);
    }

    #endregion
}
