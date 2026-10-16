// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.NPC.HTN;
using Content.Shared.Chasm;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Content.Shared.DeadSpace.Lavaland.SecretBoss;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Physics;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland.ChromiteCaves;

/// <summary>Runs <see cref="LavalandPitJumperComponent"/>: spot, whistle, dive into a chasm, jump out by the player.</summary>
public sealed class LavalandPitJumperSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly LavalandPitLandingSystem _landing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.3);

    private readonly HashSet<Entity<ChasmComponent>> _chasms = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandPitJumperComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<LavalandPitJumperComponent, ChasmFallingAttemptEvent>(OnChasmFallingAttempt);
    }

    /// <summary>
    /// It never just falls: stepping onto a chasm it dives in (the same dive as on spotting someone) and comes out
    /// by the nearest player, or back where it went in.
    /// </summary>
    private void OnChasmFallingAttempt(Entity<LavalandPitJumperComponent> ent, ref ChasmFallingAttemptEvent args)
    {
        args.Cancel();
        if (ent.Comp.Stage != LavalandPitJumperStage.Idle || HasComp<LavalandPitLandingComponent>(ent) ||
            _mobState.IsDead(ent))
        {
            return;
        }

        var xform = Transform(ent);
        var now = _timing.CurTime;
        // Wandering over a chasm with nobody about: the chasm just holds it, no pointless dive.
        if (!TryFindPlayer(ent, xform, out var target, ent.Comp.NoticeRange * 2f, false))
            return;

        ent.Comp.Target = target;
        ent.Comp.ChasmPosition = _transform.GetWorldPosition(args.Chasm);
        // Comes back out at the chasm's edge if nobody is there to jump at.
        ent.Comp.Home = new MapCoordinates(
            _transform.GetWorldPosition(xform) - Vector2.Normalize(ent.Comp.ChasmPosition - _transform.GetWorldPosition(xform) + new Vector2(0.001f, 0f)) * 0.8f,
            xform.MapID);
        SetAi(ent, false);
        Dive(ent, now);
    }

    /// <summary>It starts wandering about, not running at the first player it sees.</summary>
    private void OnMapInit(Entity<LavalandPitJumperComponent> ent, ref MapInitEvent args)
    {
        Wander(ent);
    }

    /// <summary>Waiting: it ambles about by itself (see <see cref="Stroll"/>), never going for anybody.</summary>
    private void Wander(Entity<LavalandPitJumperComponent> ent)
    {
        ent.Comp.Hunting = false;
        ent.Comp.Prey = null;
        SetAi(ent, false);
    }

    /// <summary>A few steps in some direction, a pause, a few steps again.</summary>
    private void Stroll(EntityUid uid, LavalandPitJumperComponent jumper, TimeSpan now)
    {
        if (HasComp<LavalandPitLandingComponent>(uid) || !TryComp<InputMoverComponent>(uid, out var mover))
            return;

        if (now >= jumper.NextStroll)
        {
            jumper.StrollDirection = _random.NextAngle().ToVec();
            jumper.StrollUntil = now + TimeSpan.FromSeconds(_random.NextFloat(0.6f, 1.6f));
            jumper.NextStroll = jumper.StrollUntil + TimeSpan.FromSeconds(_random.NextFloat(1.5f, 4f));
        }

        if (now >= jumper.StrollUntil)
            return;

        // Walking, not running.
        mover.CurTickSprintMovement = jumper.StrollDirection * 0.5f;
        mover.LastInputTick = _timing.CurTick;
        mover.LastInputSubTick = ushort.MaxValue;
    }

    /// <summary>Hunting: its AI off, it runs at its prey and bites by itself (see <see cref="Chase"/>).</summary>
    private void Hunt(Entity<LavalandPitJumperComponent> ent)
    {
        ent.Comp.Hunting = true;
        if (ent.Comp.Prey == null && TryFindPlayer(ent, Transform(ent), out var prey, ent.Comp.NoticeRange * 1.5f, false))
            ent.Comp.Prey = prey;
        SetAi(ent, false);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandPitJumperComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var jumper, out var xform))
        {
            if (_mobState.IsDead(uid))
            {
                if (jumper.Stage != LavalandPitJumperStage.Idle)
                    Reset((uid, jumper));
                continue;
            }

            switch (jumper.Stage)
            {
                case LavalandPitJumperStage.Idle:
                    if (jumper.Hunting)
                        Chase(uid, jumper, xform);
                    else
                        Stroll(uid, jumper, now);

                    if (now < jumper.NextCheck)
                        break;

                    jumper.NextCheck = now + CheckInterval;
                    if (jumper.Hunting)
                        UpdateHunt((uid, jumper), xform, now);
                    else
                        TrySpot((uid, jumper), xform, now);
                    break;

                case LavalandPitJumperStage.Alert when now >= jumper.StageEnd:
                    Dive((uid, jumper), now);
                    break;

                case LavalandPitJumperStage.Diving when now >= jumper.StageEnd:
                    Hide((uid, jumper), now);
                    break;

                case LavalandPitJumperStage.Hidden when now >= jumper.StageEnd:
                    Emerge((uid, jumper), now);
                    break;
            }
        }
    }

    /// <summary>While fighting: once nobody has been near for a while, it stops, AI off, and waits again.</summary>
    private void UpdateHunt(Entity<LavalandPitJumperComponent> ent, TransformComponent xform, TimeSpan now)
    {
        if (TryFindPlayer(ent, xform, out var prey, ent.Comp.NoticeRange * 1.5f, false))
        {
            ent.Comp.Prey = prey;
            ent.Comp.LastPlayerNear = now;
            return;
        }

        ent.Comp.Prey = null;
        if (now - ent.Comp.LastPlayerNear < ent.Comp.CalmDownTime)
            return;

        Wander(ent);
    }

    /// <summary>
    /// Hunting: it runs straight at its prey and bites when close. Done here, not by its AI: the caves are one huge
    /// biome grid its pathfinding gets lost in. Running onto a chasm on the way makes it dive.
    /// </summary>
    private void Chase(EntityUid uid, LavalandPitJumperComponent jumper, TransformComponent xform)
    {
        if (jumper.Prey is not { } prey || TerminatingOrDeleted(prey) || !_mobState.IsAlive(prey) ||
            HasComp<LavalandPitLandingComponent>(uid) || !TryComp<InputMoverComponent>(uid, out var mover))
        {
            return;
        }

        var offset = _transform.GetWorldPosition(prey) - _transform.GetWorldPosition(xform);
        var distance = offset.Length();
        if (distance > jumper.BiteRange)
        {
            mover.CurTickSprintMovement = offset / distance;
            mover.LastInputTick = _timing.CurTick;
            mover.LastInputSubTick = ushort.MaxValue;
            return;
        }

        if (_melee.TryGetWeapon(uid, out var weaponUid, out var weapon) && weapon.NextAttack <= _timing.CurTime)
            _melee.AttemptLightAttack(uid, weaponUid, weapon, prey);
    }

    private void StartHunt(Entity<LavalandPitJumperComponent> ent, TimeSpan now)
    {
        ent.Comp.LastPlayerNear = now;
        Hunt(ent);
    }

    /// <summary>Wandering and spotting a player: with a chasm at hand the whistle and the dive, else a plain attack.</summary>
    private void TrySpot(Entity<LavalandPitJumperComponent> ent, TransformComponent xform, TimeSpan now)
    {
        if (!HasComp<LavalandChromiteCavesComponent>(xform.MapUid) || HasComp<LavalandPitLandingComponent>(ent))
            return;

        if (!TryFindPlayer(ent, xform, out var target, ent.Comp.NoticeRange, true))
            return;

        // No chasm to dive into (or still catching its breath after the last dive): it just goes for him.
        if (now < ent.Comp.NextDive || !TryFindChasm(xform, ent.Comp.ChasmRange, out var chasm))
        {
            StartHunt(ent, now);
            return;
        }

        ent.Comp.Target = target;
        ent.Comp.Home = _transform.GetMapCoordinates(xform);
        ent.Comp.ChasmPosition = chasm;
        ent.Comp.Stage = LavalandPitJumperStage.Alert;
        ent.Comp.StageEnd = now + ent.Comp.AlertTime;
        SetAi(ent, false);

        var alert = EnsureComp<LavalandPitJumperAlertComponent>(ent);
        alert.Duration = (float) ent.Comp.AlertTime.TotalSeconds;
        Dirty(ent, alert);
        if (ent.Comp.WhistleSound != null)
            _audio.PlayPvs(ent.Comp.WhistleSound, ent);
    }

    private bool TryFindPlayer(Entity<LavalandPitJumperComponent> ent, TransformComponent xform, out EntityUid target,
        float range, bool needSight)
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
                needSight && !_interaction.InRangeUnobstructed((ent.Owner, xform), (player, playerXform), range))
            {
                continue;
            }

            best = distance;
            target = player;
        }

        return target.IsValid();
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

    /// <summary>Gone below: waits out of sight, far away on the caves map.</summary>
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
    /// Jumps out next to the player - unless he ran far enough that the spot it dived from is off his screen:
    /// then it climbs back out of its own chasm and stands there again.
    /// </summary>
    private void Emerge(Entity<LavalandPitJumperComponent> ent, TimeSpan now)
    {
        var home = ent.Comp.Home;
        var spot = home;
        var from = ent.Comp.ChasmPosition - home.Position;

        if (ent.Comp.Target is { } target && !TerminatingOrDeleted(target) && _mobState.IsAlive(target))
        {
            var targetCoords = _transform.GetMapCoordinates(target);
            var delta = home.Position - targetCoords.Position;
            var stillSeen = targetCoords.MapId == home.MapId &&
                            MathF.Abs(delta.X) <= ent.Comp.ViewHalfSize.X &&
                            MathF.Abs(delta.Y) <= ent.Comp.ViewHalfSize.Y;

            if (stillSeen)
            {
                // Always out of a chasm: one close by him, else any a bit farther; never out of plain ground.
                if (TryFindPitNear(targetCoords, ent.Comp, ent.Comp.EmergePitRange, out var near, out var pit) ||
                    TryFindPitNear(targetCoords, ent.Comp, ent.Comp.EmergePitRange * 2f, out near, out pit))
                {
                    spot = near;
                    from = pit - near.Position;
                }
            }
        }

        _transform.SetMapCoordinates(ent, spot);
        _landing.StartLanding(ent, ent.Comp.EmergeTime, from);

        ent.Comp.Stage = LavalandPitJumperStage.Idle;
        ent.Comp.Target = null;
        ent.Comp.NextDive = now + ent.Comp.Cooldown;
        ent.Comp.LastEmerge = now;

        // Out by the player: the fight is on. Back out of its own chasm: it wanders and waits again.
        if (spot.Position == home.Position && spot.MapId == home.MapId)
            Wander(ent);
        else
            StartHunt(ent, now);
    }

    private static readonly Vector2[] Around =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(-1, 1), new(1, -1), new(-1, -1),
    };

    /// <summary>
    /// The edge of a chasm near the player to climb out of: a random one of the chasms around him, so he never knows
    /// which hole it comes from.
    /// </summary>
    private bool TryFindPitNear(MapCoordinates target, LavalandPitJumperComponent jumper, float range, out MapCoordinates spot, out Vector2 pit)
    {
        spot = default;
        pit = default;
        _chasms.Clear();
        _lookup.GetEntitiesInRange(target, range, _chasms, LookupFlags.Static);
        var chasms = new List<Entity<ChasmComponent>>(_chasms);
        _random.Shuffle(chasms);

        foreach (var chasm in chasms)
        {
            var chasmPosition = _transform.GetWorldPosition(chasm);
            // Not the chasm it dived into (nor its neighbours): that would be no trick at all.
            if (Vector2.DistanceSquared(chasmPosition, jumper.ChasmPosition) < 3f * 3f)
                continue;

            foreach (var step in Around)
            {
                var candidate = new MapCoordinates(chasmPosition + step, target.MapId);
                // Not right on top of him either.
                if (Vector2.DistanceSquared(candidate.Position, target.Position) < 1f || !IsFreeFloor(candidate))
                    continue;

                spot = candidate;
                pit = chasmPosition;
                return true;
            }
        }

        return false;
    }

    /// <summary>Plain floor: not a wall, not a chasm, not in the void.</summary>
    private bool IsFreeFloor(MapCoordinates coords)
    {
        if (!_turf.TryGetTileRef(_transform.ToCoordinates(coords), out var tile) || _turf.IsSpace(tile.Value) ||
            _turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable) ||
            !TryComp<MapGridComponent>(tile.Value.GridUid, out var grid))
        {
            return false;
        }

        var anchored = _map.GetAnchoredEntitiesEnumerator(tile.Value.GridUid, grid, tile.Value.GridIndices);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<ChasmComponent>(uid))
                return false;
        }

        return true;
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
}
