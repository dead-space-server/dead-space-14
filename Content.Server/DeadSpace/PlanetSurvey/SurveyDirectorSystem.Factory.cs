// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.NPC.HTN;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.PlanetSurvey;

public sealed partial class SurveyDirectorSystem
{
    private const string CrowdTask = "SurveyCrowdCompound";
    private const float GuardRange = 9f;

    private void SpawnFactory(Entity<SurveyDirectorComponent> ent, SurveyWorldPrototype world, System.Random random, List<SurveySpot> spots)
    {
        if (world.Stamp is not { } stampProto || spots.Count == 0)
            return;

        var order = spots.Where(s => s.Tag == "assembly").Concat(spots.Where(s => s.Tag == null && s.Tiles.Count >= 12).OrderBy(_ => random.Next())).ToList();
        if (order.Count == 0)
            order = spots;

        if (spots.FirstOrDefault(s => s.Tag == "core") is { } coreHall && world.Elites.Count > 0)
        {
            coreHall.Taken = true;
            _guarded.Add(coreHall);
            var guard = new SurveySquad { Home = At(ent.Owner, coreHall.Center) };
            for (var e = 0; e < world.ElitesPerStamp + 1; e++)
            {
                var proto = e == 0 && world.Veterans.Count > 0 ? world.Veterans[0] : world.Elites[random.Next(world.Elites.Count)];
                var elite = SpawnMember(ent, proto, coreHall, ent.Comp.Squads.Count, random);
                if (Board(elite) is { } board)
                    board.SetValue(SurveyKeys.Post, guard.Home);
                guard.Members.Add(elite);
            }

            guard.Size = guard.Members.Count;
            guard.Alive = guard.Size;
            ent.Comp.Squads.Add(guard);
        }

        for (var i = 0; i < world.StampCount; i++)
        {
            var spot = NextSpot(order, random);
            var at = FreeTile(ent.Owner, spot, random);
            _guarded.Add(spot);

            var stamp = SpawnAtPosition(stampProto, At(ent.Owner, at));
            var comp = EnsureComp<SurveyStampComponent>(stamp);
            comp.Crowd = world.Crowd;
            comp.Pool.AddRange(world.CrowdPool);
            comp.Interval = world.StampInterval;
            comp.NextPrint = _timing.CurTime + TimeSpan.FromSeconds(random.Next(5, 20));
            ent.Comp.Stamps.Add(stamp);

            var squad = new SurveySquad { Home = At(ent.Owner, at) };
            for (var e = 0; e < world.ElitesPerStamp && world.Elites.Count > 0; e++)
            {
                var proto = world.Elites[random.Next(world.Elites.Count)];
                var elite = SpawnMember(ent, proto, spot, ent.Comp.Squads.Count, random);
                if (Board(elite) is { } board)
                    board.SetValue(SurveyKeys.Post, squad.Home);
                squad.Members.Add(elite);
            }

            squad.Size = squad.Members.Count;
            squad.Alive = squad.Size;
            ent.Comp.Squads.Add(squad);
        }

        RebalanceStamps(ent, world);
    }

    private void TickFactory(Entity<SurveyDirectorComponent> ent)
    {
        var director = ent.Comp;
        var now = _timing.CurTime;

        director.Stamps.RemoveAll(s => TerminatingOrDeleted(s));
        if (_proto.TryIndex(director.World, out var world))
            RebalanceStamps(ent, world);

        foreach (var squad in director.Squads)
        {
            Centroid(squad, out var alive);
            if (alive < squad.Alive)
            {
                director.Kills += squad.Alive - alive;
                if (alive == 0)
                    CallQrf(ent, squad);
            }

            squad.Alive = alive;
            if (alive == 0)
                continue;

            var home = squad.Home.Position;
            var stampAlive = director.Stamps.Exists(s => Vector2.Distance(Pos(s), home) < 2f);
            var intruder = stampAlive ? NearestPlayer(home, GuardRange, out _) : null;
            var seen = intruder ?? SquadSees(director, squad, squad.Engaged ? 24f : 14f);
            var previous = squad.Target;

            if (seen is { } spotted)
            {
                if (!squad.Engaged)
                {
                    squad.NextOrders = TimeSpan.Zero;
                    Radio(ent, Speaker(squad, Pos(spotted)), "contact", ("where", Where(director, Pos(spotted))), ("count", PlayersNear(Pos(spotted), 10f)));
                }

                squad.Engaged = true;
                squad.Target = spotted;
                squad.LastSeen = now;
                squad.LastKnown = Pos(spotted);
                director.LastContact = now;
                RaiseAlarm(ent, squad);
            }
            else if (squad.Engaged && (squad.Target is not { } chased || !Alive(chased) || now - squad.LastSeen > ForgetAfter))
            {
                squad.Engaged = false;
                squad.Target = null;
                squad.LastKnown = null;
                squad.NextOrders = TimeSpan.Zero;
            }
            else if (squad.Engaged && squad.Target is { } trail &&
                     (now < squad.LastSeen || squad.LastKnown is not { } known || Vector2.Distance(Pos(trail), known) < TrackRange))
            {
                squad.LastKnown = Pos(trail);
            }

            if (intruder != null && now >= director.NextAlarm && world?.AlarmSound is { } alarm)
            {
                director.NextAlarm = now + TimeSpan.FromSeconds(15);
                _audio.PlayPvs(alarm, squad.Home, AudioParams.Default.WithVolume(2f).WithMaxDistance(30f));
            }

            if (squad.Target != previous || now >= squad.NextOrders || director.ReissueAll)
            {
                squad.NextOrders = now + RobotOrderGap;
                RobotOrders(ent, squad, seen != null);
            }
        }
    }

    private static readonly TimeSpan RobotOrderGap = TimeSpan.FromSeconds(2);
    private const float KiteRange = 4.5f;
    private const float TrackRange = 30f;
    private const int CrowdLimit = 16;
    private const float KiteDistance = 7f;

    private void RobotOrders(Entity<SurveyDirectorComponent> ent, SurveySquad squad, bool visible)
    {
        var map = ent.Owner;
        var chase = squad.Engaged ? squad.LastKnown : null;
        var k = 0;

        foreach (var robot in squad.Members)
        {
            if (!Alive(robot) || !TryComp<SurveyMemberComponent>(robot, out var member) || member.Blind || member.Medic || member.Scout || Busy(member))
                continue;

            if (SensorDead(robot))
            {
                member.Blind = true;
                Dumb(robot);
                continue;
            }

            if (Board(robot) is not { } board)
                continue;

            var oldRole = board.GetValueOrDefault<SurveyRole>(SurveyKeys.Role, EntityManager);
            var oldCoords = board.TryGetValue<EntityCoordinates>(SurveyKeys.TargetCoords, out var coords, EntityManager) ? coords.Position : (Vector2?) null;
            var oldFlank = board.TryGetValue<EntityCoordinates>(SurveyKeys.Flank, out var flankWas, EntityManager) ? flankWas.Position : (Vector2?) null;

            if (chase is not { } goal || squad.Target is not { } target)
            {
                board.Remove<EntityUid>(SurveyKeys.Target);
                board.SetValue(SurveyKeys.Role, SurveyRole.None);
                if (oldRole != SurveyRole.None)
                    Replan(robot);
                continue;
            }

            var pos = Pos(robot);
            var role = SurveyRole.Rush;
            if (HasComp<GunComponent>(robot))
            {
                role = SurveyRole.Hold;
                var away = pos - goal;
                if (visible && away.Length() < KiteRange && KitePoint(map, robot, pos, goal) is { } back)
                {
                    board.SetValue(SurveyKeys.Flank, At(map, back));
                    role = SurveyRole.Flank;
                }
                else if (away.Length() < FireReach && ShotBlocked(robot, target))
                {
                    var reroute = FirePoint(map, robot, pos, target) is { } step ? At(map, step) : At(map, goal);

                    board.SetValue(SurveyKeys.Flank, reroute);
                    role = SurveyRole.Flank;
                }
                else if (k % 3 == 1 && ReachableFlank(map, robot, pos, goal, k % 2 == 0 ? 1f : -1f) is { } flank)
                {
                    board.SetValue(SurveyKeys.Flank, flank);
                    role = SurveyRole.Flank;
                }

                k++;
            }

            board.SetValue(SurveyKeys.Target, target);
            board.SetValue(SurveyKeys.AimZone, RobotZone(target));
            board.SetValue(SurveyKeys.TargetCoords, At(map, goal));
            board.SetValue(SurveyKeys.Role, role);

            var flankMoved = role == SurveyRole.Flank &&
                             board.TryGetValue<EntityCoordinates>(SurveyKeys.Flank, out var newFlank, EntityManager) &&
                             (oldFlank is not { } prevFlank || Vector2.Distance(prevFlank, newFlank.Position) > 2.5f);

            if (role != oldRole || oldCoords is not { } was || Vector2.Distance(was, goal) > 2.5f || flankMoved)
                Replan(robot);
        }
    }

    private EntityCoordinates? PrintSpot(EntityUid map, TransformComponent stamp)
    {
        var origin = stamp.Coordinates;
        if (!TryComp<Robust.Shared.Map.Components.MapGridComponent>(map, out var grid))
            return null;

        var tile = _map.TileIndicesFor(map, grid, origin);
        foreach (var offset in PrintOffsets)
        {
            var cell = tile + offset;
            if (_map.TryGetTileRef(map, grid, cell, out var tileRef) && !tileRef.Tile.IsEmpty &&
                _anchorable.TileFree((map, grid), cell, Blocking, Blocking) && !Cluttered(map, cell))
            {
                return new EntityCoordinates(map, cell + new Vector2(0.5f, 0.5f));
            }
        }

        return null;
    }

    private static readonly Vector2i[] PrintOffsets =
    {
        new(0, -1), new(1, 0), new(-1, 0), new(0, 1), new(1, -1), new(-1, -1), new(1, 1), new(-1, 1), new(0, -2), new(2, 0),
    };

    private void OnCoreDown(Entity<SurveyFactoryCoreComponent> ent, ref ComponentShutdown args)
    {
        if (Transform(ent).MapUid is not { } map || TerminatingOrDeleted(map) ||
            !TryComp<SurveyDirectorComponent>(map, out var director) || director.CoreDown)
        {
            return;
        }

        director.CoreDown = true;
        var robots = EntityQueryEnumerator<SurveyMemberComponent, TransformComponent>();
        while (robots.MoveNext(out var uid, out var member, out var xform))
        {
            if (member.Robot && xform.MapUid == map && Alive(uid))
                _mobState.ChangeMobState(uid, Content.Shared.Mobs.MobState.Dead);
        }

        Progress(map, "core");
        Warn(map, Loc.GetString("survey-core-down"));
    }

    private void TickCore(Entity<SurveyDirectorComponent> ent)
    {
        var director = ent.Comp;
        if (director.CoreDown || director.CoreSpoke || director.Kind != SurveyKind.Factory)
            return;

        if (NearestPlayer(director.BaseCentre, director.BaseRadius, out _) == null)
            return;

        director.CoreSpoke = true;
        Warn(ent.Owner, Loc.GetString("survey-core-greet"));
    }

    private LimbZone RobotZone(EntityUid target)
    {
        if (ArmedHand(target) is { } hand)
            return _random.Prob(0.5f) ? hand : (hand == LimbZone.HandLeft ? LimbZone.ArmLeft : LimbZone.ArmRight);

        return _random.Next(3) switch
        {
            0 => _random.Prob(0.5f) ? LimbZone.LegLeft : LimbZone.LegRight,
            1 => LimbZone.Eyes,
            _ => _random.Prob(0.5f) ? LimbZone.ArmLeft : LimbZone.ArmRight,
        };
    }

    private void Dumb(EntityUid elite)
    {
        if (!TryComp<HTNComponent>(elite, out var htn))
            return;

        RemComp<LimbAimComponent>(elite);
        htn.RootTask = new HTNCompoundTask { Task = CrowdTask };
        htn.Blackboard.Remove<EntityUid>(SurveyKeys.Target);
        htn.Blackboard.SetValue(SurveyKeys.Role, SurveyRole.None);
        _htn.Replan(htn);
    }

    private void RebalanceStamps(Entity<SurveyDirectorComponent> ent, SurveyWorldPrototype world)
    {
        if (ent.Comp.Stamps.Count == 0)
            return;

        var alarm = ent.Comp.Alarm ? 1.5f : 1f;
        var cap = Math.Min(CrowdLimit, (int) ((world.CrowdCap + 2 * _players.Count + ent.Comp.Heat) * ent.Comp.Threat * alarm));
        var each = Math.Max(1, cap / ent.Comp.Stamps.Count);
        foreach (var stamp in ent.Comp.Stamps)
        {
            if (TryComp<SurveyStampComponent>(stamp, out var comp))
                comp.Cap = each;
        }
    }

    private void UpdateStamps(TimeSpan now)
    {
        var query = EntityQueryEnumerator<SurveyStampComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var stamp, out var xform))
        {
            if (now < stamp.NextPrint || xform.MapUid is not { } map)
                continue;

            var crowd = stamp.Pool.Count > 0 ? _random.Pick(stamp.Pool) : stamp.Crowd;
            if (crowd == null)
                continue;

            if (TryComp<SurveyDirectorComponent>(map, out var planet) && (planet.CoreDown || planet.Blackout && InBase(planet, _xform.GetWorldPosition(xform))))
                continue;

            stamp.NextPrint = now + TimeSpan.FromSeconds(stamp.Interval);
            stamp.Printed.RemoveWhere(p => !Alive(p));
            if (stamp.Printed.Count >= stamp.Cap)
                continue;

            if (PrintSpot(map, xform) is not { } spot)
                continue;

            var drone = SpawnAtPosition(crowd.Value, spot);
            var member = EnsureComp<SurveyMemberComponent>(drone);
            member.Planet = map;
            stamp.Printed.Add(drone);
            if (Board(drone) is { } board)
                board.SetValue(SurveyKeys.Post, At(map, Walkable(map, spot.Position + _random.NextVector2(2f, 4f))));

            var index = planet?.Squads.FindIndex(s => Vector2.Distance(s.Home.Position, xform.LocalPosition) < 2.5f) ?? -1;
            if (planet != null && index >= 0)
            {
                member.Squad = index;
                planet.Squads[index].Members.Add(drone);
                planet.Squads[index].Size++;
                planet.Squads[index].Alive++;
                planet.Squads[index].NextOrders = TimeSpan.Zero;
            }
        }
    }
}
