// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.PlanetSurvey;

public sealed partial class SurveyDirectorSystem
{
    private static readonly TimeSpan QrfDelay = TimeSpan.FromSeconds(18);
    private static readonly TimeSpan ShiftCooldown = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan KiteCooldown = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CoverTime = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PeekTime = TimeSpan.FromSeconds(3);
    private const float CoverAlert = 16f;
    private const int CoverMin = 1;
    private const int CoverMax = 6;
    private const float LineClearance = 0.7f;

    private void Maneuver(Entity<SurveyDirectorComponent> ent, SurveySquad squad, EntityUid member, SurveyMemberComponent comp, bool hit)
    {
        var now = _timing.CurTime;
        if (!squad.Engaged || squad.Target is not { } enemy || !Alive(enemy) || Board(member) is not { } board)
            return;

        var pos = Pos(member);
        var enemyPos = Pos(enemy);
        var away = pos - enemyPos;

        if (comp.CoverUntil != TimeSpan.Zero)
        {
            if (now < comp.CoverUntil)
                return;

            comp.CoverUntil = TimeSpan.Zero;
            comp.NextCover = now + PeekTime + TimeSpan.FromSeconds(_random.NextFloat(0f, 1.5f));
            comp.NextShift = comp.NextCover;
            if (FirePoint(ent.Owner, member, pos, enemy) is { } peek)
            {
                board.SetValue(SurveyKeys.Flank, At(ent.Owner, peek));
                board.SetValue(SurveyKeys.Role, SurveyRole.Flank);
            }
            else
            {
                board.SetValue(SurveyKeys.Role, SurveyRole.Hold);
            }

            board.SetValue(SurveyKeys.TargetCoords, At(ent.Owner, enemyPos));
            Replan(member, true);
            return;
        }

        if (Busy(comp))
            return;

        if (now >= comp.NextCover && UnderFire(member, enemy, hit) && TakeCover(ent, squad, member, comp, board, pos, enemyPos, enemy))
            return;

        if (comp.Approach)
        {
            if (away.Length() < FireReach && !ShotBlocked(member, enemy))
            {
                comp.Approach = false;
                comp.NextShift = now + ShiftCooldown;
                board.SetValue(SurveyKeys.Role, SurveyRole.Hold);
                board.SetValue(SurveyKeys.TargetCoords, At(ent.Owner, enemyPos));
                Replan(member);
                return;
            }

            if (now >= comp.NextShift)
            {
                comp.NextShift = now + ShiftCooldown;
                board.SetValue(SurveyKeys.Flank, At(ent.Owner, enemyPos));
                Replan(member);
            }

            return;
        }

        if (now < comp.NextShift)
            return;

        var close = away.Length() < KiteRange && !Brawler(member);
        var blocked = !hit && !close && away.Length() < FireReach && ShotBlocked(member, enemy);
        if (!hit && !close && !blocked && !AllyInLine(ent.Comp, member, pos, enemyPos))
            return;

        comp.NextShift = now + ShiftCooldown + TimeSpan.FromSeconds(_random.NextFloat(0f, 2f));
        var side = _random.Prob(0.5f) ? 1f : -1f;
        EntityCoordinates? goal;
        if (blocked)
        {
            if (FirePoint(ent.Owner, member, pos, enemy) is { } step)
            {
                goal = At(ent.Owner, step);
            }
            else
            {
                comp.Approach = true;
                goal = At(ent.Owner, enemyPos);
            }
        }
        else if (close)
        {
            comp.NextShift = now + KiteCooldown;
            goal = KitePoint(ent.Owner, member, pos, enemyPos) is { } back
                ? At(ent.Owner, back)
                : ReachableFlank(ent.Owner, member, pos, enemyPos, side);

            if (goal != null && comp.Grenades.FindIndex(g => g.Id == Smoke) is >= 0 and var smoke && _random.Prob(0.5f))
            {
                comp.Grenades.RemoveAt(smoke);
                Lob(ent.Owner, member, Smoke, pos);
                Bark(member, "smoke");
            }
        }
        else
        {
            goal = ReachableFlank(ent.Owner, member, pos, enemyPos, side) ??
                   (FirePoint(ent.Owner, member, pos, enemy) is { } sidestep ? At(ent.Owner, sidestep) : null);
        }

        if (goal is not { } flankPoint)
            return;

        board.SetValue(SurveyKeys.Flank, flankPoint);
        board.SetValue(SurveyKeys.Role, SurveyRole.Flank);
        board.SetValue(SurveyKeys.TargetCoords, At(ent.Owner, enemyPos));
        Replan(member);
    }

    private bool UnderFire(EntityUid member, EntityUid enemy, bool hit)
    {
        if (hit)
            return true;

        return _noisy.Contains(enemy) && Vector2.Distance(Pos(member), Pos(enemy)) < CoverAlert && !ShotBlocked(enemy, member);
    }

    private bool TakeCover(Entity<SurveyDirectorComponent> ent, SurveySquad squad, EntityUid member, SurveyMemberComponent comp,
        Content.Server.NPC.NPCBlackboard board, Vector2 pos, Vector2 enemyPos, EntityUid enemy)
    {
        if (board.GetValueOrDefault<SurveyRole>(SurveyKeys.Role, EntityManager) == SurveyRole.Suppress)
            return false;

        var hiding = 0;
        var alive = 0;
        foreach (var other in squad.Members)
        {
            if (!Alive(other))
                continue;

            alive++;
            if (TryComp<SurveyMemberComponent>(other, out var otherComp) && otherComp.CoverUntil > _timing.CurTime)
                hiding++;
        }

        if (alive > 1 && hiding * 2 >= alive)
            return false;

        if (CoverFrom(ent.Owner, member, pos, enemyPos, enemy) is not { } cover)
            return false;

        var now = _timing.CurTime;
        comp.Approach = false;
        comp.CoverUntil = now + CoverTime + TimeSpan.FromSeconds(_random.NextFloat(0f, 2f));
        board.SetValue(SurveyKeys.Errand, At(ent.Owner, cover));
        board.SetValue(SurveyKeys.Role, SurveyRole.Errand);
        Replan(member, true);
        if (_random.Prob(0.3f))
            Bark(member, "cover");

        return true;
    }

    private Vector2? CoverFrom(EntityUid map, EntityUid member, Vector2 pos, Vector2 enemyPos, EntityUid enemy)
    {
        var mapId = Transform(map).MapID;
        var shooter = _xform.GetMapCoordinates(enemy);
        var origin = new Vector2i((int) MathF.Floor(pos.X), (int) MathF.Floor(pos.Y));
        var current = Vector2.Distance(pos, enemyPos);

        for (var ring = CoverMin; ring <= CoverMax; ring++)
        {
            Vector2? best = null;
            var bestDist = float.MaxValue;
            for (var x = -ring; x <= ring; x++)
            {
                for (var y = -ring; y <= ring; y++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) != ring)
                        continue;

                    var cell = origin + new Vector2i(x, y) + new Vector2(0.5f, 0.5f);
                    if (Vector2.Distance(cell, enemyPos) < current - 1.5f || Walkable(map, cell, 0) != cell)
                        continue;

                    if (ClearShot(shooter, new MapCoordinates(cell, mapId), enemy, member))
                        continue;

                    if (!_interaction.InRangeUnobstructed(member, new EntityCoordinates(map, cell), ring + 1.5f, ThrowMask))
                        continue;

                    var d = Vector2.DistanceSquared(cell, pos);
                    if (d >= bestDist)
                        continue;

                    bestDist = d;
                    best = cell;
                }
            }

            if (best != null)
                return best;
        }

        return null;
    }

    private void TickBlockedShooters(Entity<SurveyDirectorComponent> ent)
    {
        foreach (var squad in ent.Comp.Squads)
        {
            if (!squad.Engaged || squad.Target is not { } enemy || !Alive(enemy))
                continue;

            foreach (var member in squad.Members)
            {
                if (!Alive(member) || !TryComp<SurveyMemberComponent>(member, out var comp) || Busy(comp) ||
                    !TryComp<Content.Server.NPC.HTN.HTNComponent>(member, out var htn))
                {
                    continue;
                }

                var firing = htn.Plan is { } plan && plan.Index < plan.Tasks.Count &&
                             plan.Tasks[plan.Index].Operator is Content.Server.NPC.HTN.PrimitiveTasks.Operators.Combat.Ranged.GunOperator;

                if (!firing || !ShotBlocked(member, enemy))
                {
                    comp.BlockedTicks = 0;
                    continue;
                }

                if (++comp.BlockedTicks < 2)
                    continue;

                comp.BlockedTicks = 0;
                var pos = Pos(member);
                var step = FirePoint(ent.Owner, member, pos, enemy);
                comp.Approach = step == null;
                htn.Blackboard.SetValue(SurveyKeys.Flank, At(ent.Owner, step ?? Pos(enemy)));
                htn.Blackboard.SetValue(SurveyKeys.Role, SurveyRole.Flank);
                htn.Blackboard.SetValue(SurveyKeys.TargetCoords, At(ent.Owner, Pos(enemy)));
                comp.NextShift = _timing.CurTime + ShiftCooldown;
                squad.NextOrders = _timing.CurTime + ShiftCooldown;
                Replan(member);
            }
        }
    }

    private static readonly HashSet<string> CloseGuns = new() { "SurveyNpcShotgun", "SurveyNpcBulldog" };
    private static readonly float[] KiteAngles = { 0f, 0.6f, -0.6f, 1.2f, -1.2f, 1.9f, -1.9f };
    private static readonly float[] KiteSteps = { KiteDistance, 5f, 3.5f };

    private const float FireReach = 12f;
    private const int FireStepReach = 3;

    private bool ClearShot(MapCoordinates from, MapCoordinates to, EntityUid shooter, EntityUid target)
    {
        var range = (to.Position - from.Position).Length() + 1f;
        return _interaction.InRangeUnobstructed(from, to, range, CollisionGroup.BulletImpassable,
            e => e == shooter || e == target || HasComp<Content.Shared.Mobs.Components.MobStateComponent>(e));
    }

    private bool ShotBlocked(EntityUid shooter, EntityUid target)
    {
        return !ClearShot(_xform.GetMapCoordinates(shooter), _xform.GetMapCoordinates(target), shooter, target);
    }

    private Vector2? FirePoint(EntityUid map, EntityUid shooter, Vector2 pos, EntityUid target)
    {
        var mapId = Transform(map).MapID;
        var goal = _xform.GetMapCoordinates(target);
        var origin = new Vector2i((int) MathF.Floor(pos.X), (int) MathF.Floor(pos.Y));

        for (var ring = 1; ring <= FireStepReach; ring++)
        {
            for (var x = -ring; x <= ring; x++)
            {
                for (var y = -ring; y <= ring; y++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) != ring)
                        continue;

                    var cell = origin + new Vector2i(x, y) + new Vector2(0.5f, 0.5f);
                    if (Walkable(map, cell, 0) != cell)
                        continue;

                    if (!_interaction.InRangeUnobstructed(shooter, new EntityCoordinates(map, cell), ring + 1.5f, ThrowMask))
                        continue;

                    if (ClearShot(new MapCoordinates(cell, mapId), goal, shooter, target))
                        return cell;
                }
            }
        }

        return null;
    }

    private EntityCoordinates? ReachableFlank(EntityUid map, EntityUid member, Vector2 pos, Vector2 enemy, float side)
    {
        foreach (var turn in new[] { side, -side })
        {
            var flank = FlankPoint(map, pos, enemy, turn);
            var distance = Vector2.Distance(pos, flank.Position);
            if (distance > 1.5f && _interaction.InRangeUnobstructed(member, flank, distance + 1.5f, ThrowMask))
                return flank;
        }

        return null;
    }

    private Vector2? KitePoint(EntityUid map, EntityUid member, Vector2 pos, Vector2 enemy)
    {
        var away = pos - enemy;
        if (away.LengthSquared() < 0.01f)
            away = _random.NextVector2(1f);

        var heading = MathF.Atan2(away.Y, away.X);
        var current = away.Length();
        foreach (var step in KiteSteps)
        {
            foreach (var turn in KiteAngles)
            {
                var angle = heading + turn;
                var point = Walkable(map, pos + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * step, 1);
                if (Vector2.Distance(point, enemy) < current + 1.5f)
                    continue;

                if (_interaction.InRangeUnobstructed(member, new EntityCoordinates(map, point), step + 1.5f, ThrowMask))
                    return point;
            }
        }

        return null;
    }

    private bool Brawler(EntityUid member)
    {
        foreach (var held in _hands.EnumerateHeld(member))
        {
            if (CloseGuns.Contains(MetaData(held).EntityPrototype?.ID ?? string.Empty))
                return true;
        }

        return false;
    }

    private bool AllyInLine(SurveyDirectorComponent director, EntityUid shooter, Vector2 from, Vector2 to)
    {
        var line = to - from;
        var length = line.Length();
        if (length < 1.5f)
            return false;

        var dir = line / length;
        foreach (var squad in director.Squads)
        {
            foreach (var ally in squad.Members)
            {
                if (ally == shooter || !Alive(ally))
                    continue;

                var rel = Pos(ally) - from;
                var along = Vector2.Dot(rel, dir);
                if (along <= 0.5f || along >= length - 0.5f)
                    continue;

                var off = rel - dir * along;
                if (off.LengthSquared() < LineClearance * LineClearance)
                    return true;
            }
        }

        return false;
    }

    private void CallQrf(Entity<SurveyDirectorComponent> ent, SurveySquad lost)
    {
        var director = ent.Comp;
        if (director.CommsCut || director.QrfAt != null)
            return;

        if (director.QrfBudget < 0)
            director.QrfBudget = 2 + _players.Count;

        if (director.QrfBudget == 0)
            return;

        director.QrfAt = _timing.CurTime + QrfDelay;
        director.QrfPos = lost.LastPos;

        SurveySquad? radio = null;
        var best = float.MaxValue;
        foreach (var squad in director.Squads)
        {
            if (squad == lost || squad.Alive == 0)
                continue;

            var d = Vector2.Distance(squad.Home.Position, lost.LastPos);
            if (d >= best)
                continue;

            best = d;
            radio = squad;
        }

        if (radio != null)
        {
            BarkOne(radio, "qrf");
            Radio(ent, Speaker(radio), "qrf", ("where", Where(director, lost.LastPos)));
            RaiseAlarm(ent, radio);
        }
    }

    private void TickQrf(Entity<SurveyDirectorComponent> ent)
    {
        var director = ent.Comp;
        if (director.QrfAt is not { } at || _timing.CurTime < at)
            return;

        director.QrfAt = null;
        if (!_proto.TryIndex(director.World, out var world))
            return;

        var pool = world.Veterans.Count > 0 ? world.Veterans
            : world.Squads.Count > 0 ? world.Squads[0].Members
            : world.Elites;
        if (pool.Count == 0)
            return;

        var target = NearestPlayer(director.QrfPos, 70f, out _);
        var anchor = target is { } t ? Pos(t) : director.QrfPos;
        var point = HiddenPoint(director, anchor, 12f, 16f) ?? anchor + new Vector2(_random.NextFloat(-14f, 14f), _random.NextFloat(-14f, 14f));
        if (!InArena(director, point))
            point = director.BaseCentre + Vector2.Normalize(point - director.BaseCentre) * (director.ArenaRadius - 8f);

        var spot = OpenSpot(ent.Owner, point, 2);
        var random = new System.Random(_random.Next());
        var squad = NewSquad(ent, spot);
        var count = 3 + (director.Threat >= 1.5f ? 1 : 0) + (_players.Count > 2 ? 1 : 0);
        for (var i = 0; i < count; i++)
        {
            squad.Members.Add(SpawnMember(ent, pool[i % pool.Count], spot, director.Squads.Count - 1, random));
        }

        squad.Size = squad.Members.Count;
        squad.Alive = squad.Size;
        squad.Armed = true;
        director.QrfBudget--;
        director.NativesTotal = director.Squads.Sum(s => s.Size);

        if (target is not { } enemy)
            return;

        squad.Engaged = true;
        squad.Target = enemy;
        squad.LastSeen = _timing.CurTime + TimeSpan.FromSeconds(30);
        Radio(ent, Speaker(squad), "arrive", ("where", Where(director, Pos(enemy))));

        if (director.Kind is SurveyKind.Raiders or SurveyKind.Pirates)
        {
            Issue(ent, squad, director.Kind == SurveyKind.Pirates);
            BarkOne(squad, "reinforce");
            return;
        }

        foreach (var member in squad.Members)
        {
            if (Board(member) is not { } board)
                continue;

            board.SetValue(SurveyKeys.Target, enemy);
            board.SetValue(SurveyKeys.TargetCoords, At(ent.Owner, Pos(enemy)));
            board.SetValue(SurveyKeys.Role, SurveyRole.Hold);
            Replan(member);
        }
    }
}
