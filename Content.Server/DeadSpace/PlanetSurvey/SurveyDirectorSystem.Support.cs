// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Throwing;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.PlanetSurvey;

public sealed partial class SurveyDirectorSystem
{
    private const float MedicReach = 22f;
    private const float PatientHealth = 0.55f;
    private const float PatientDone = 0.85f;
    private const float TouchRange = 2f;
    private const float MedicHeal = 20f;
    private const int RebuildTicks = 3;
    private const float DangerRange = 9f;
    private const float DocRange = 28f;
    private const float ScoutSpotRange = 45f;
    private static readonly TimeSpan GrenadeGap = TimeSpan.FromSeconds(5);

    private static readonly HashSet<string> Lethal = new() { "ExGrenade", "GrenadeShrapnel", "GrenadeIncendiary" };
    private static readonly HashSet<string> Stunning = new() { "GrenadeFlashBang", "GrenadeStinger" };
    private const string Smoke = "SmokeGrenade";
    private const float SafeThrow = 4.5f;
    private const float FlashSafeThrow = 8.5f;
    private const float LethalSafeThrow = 7f;
    private const float LethalAllyRange = 5.5f;
    private const float FlashAllyRange = 7f;
    private const CollisionGroup ThrowMask = CollisionGroup.Impassable | CollisionGroup.HighImpassable;

    [Dependency] private BloodstreamSystem _blood = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private TriggerSystem _trigger = default!;

    private readonly HashSet<EntityUid> _treated = new();
    private readonly HashSet<int> _reissue = new();

    private void SpawnSupport(Entity<SurveyDirectorComponent> ent, SurveyWorldPrototype world, System.Random random, List<SurveySpot> spots)
    {
        var map = ent.Owner;
        var rooms = spots.Where(s => !s.Open && s.Tiles.Count >= 9).ToList();
        if (rooms.Count == 0)
            return;

        if (world.Autodoc is { } autodoc)
        {
            var homes = rooms.Where(s => s.Outpost >= 0).GroupBy(s => s.Outpost).Select(g => g.First())
                .Concat(_guarded.Where(s => !s.Open))
                .Distinct()
                .Take(world.AutodocCount)
                .ToList();

            foreach (var room in homes)
            {
                ent.Comp.Autodocs.Add(SpawnAtPosition(autodoc, new EntityCoordinates(map, FreeTile(map, room, random))));
            }
        }

        if (world.Repair is { } repair)
        {
            var bays = rooms.Where(s => s.Tag == "repair").ToList();
            for (var i = 0; i < world.RepairCount; i++)
            {
                var room = bays.Count > 0 ? bays[i % bays.Count] : rooms[random.Next(rooms.Count)];
                SpawnMember(ent, repair, room, -1, random);
            }
        }

        if (world.Scout is { } scout)
        {
            for (var i = 0; i < world.ScoutCount; i++)
            {
                var room = rooms[random.Next(rooms.Count)];
                var uid = SpawnMember(ent, scout, room, -1, random);
                ent.Comp.Scouts.Add(uid);
            }
        }
    }

    private void TickSupport(Entity<SurveyDirectorComponent> ent)
    {
        if (ent.Comp.Kind is not (SurveyKind.Raiders or SurveyKind.Pirates or SurveyKind.Factory))
            return;

        _reissue.Clear();
        TickDodges(ent);
        TickRecovery(ent);
        TickDocs(ent);
        TickSelfCare(ent);
        TickMedics(ent);
        TickRecovered(ent);
        TickGrenades(ent);
        TickBlockedShooters(ent);
        TickScouts(ent);

        foreach (var index in _reissue)
        {
            if (index < 0 || index >= ent.Comp.Squads.Count)
                continue;

            if (ent.Comp.Kind == SurveyKind.Factory)
                continue;

            Issue(ent, ent.Comp.Squads[index], ent.Comp.Kind == SurveyKind.Pirates);
        }
    }

    private bool Tending(EntityUid uid)
    {
        return TryComp<SurveyMemberComponent>(uid, out var member) && Busy(member);
    }

    private Vector2? DocFor(SurveyDirectorComponent director, Vector2 from, bool robot)
    {
        Vector2? best = null;
        var bestDist = DocRange;
        foreach (var doc in director.Autodocs)
        {
            if (TerminatingOrDeleted(doc) || !TryComp<SurveyAutodocComponent>(doc, out var comp) || comp.Robots != robot)
                continue;

            if (director.Blackout && InBase(director, Pos(doc)))
                continue;

            var d = Vector2.Distance(Pos(doc), from);
            if (d >= bestDist)
                continue;

            bestDist = d;
            best = Pos(doc);
        }

        return best;
    }

    private void TickDocs(Entity<SurveyDirectorComponent> ent)
    {
        var director = ent.Comp;
        director.Autodocs.RemoveAll(d => TerminatingOrDeleted(d));

        foreach (var doc in director.Autodocs)
        {
            if (!TryComp<SurveyAutodocComponent>(doc, out var comp) || director.Blackout && InBase(director, Pos(doc)))
                continue;

            var docPos = Pos(doc);
            foreach (var uid in _lookup.GetEntitiesInRange<SurveyMemberComponent>(Transform(doc).Coordinates, comp.Range))
            {
                if (uid.Comp.Robot != comp.Robots || _mobState.IsDead(uid) || Health(uid) >= 0.99f)
                    continue;

                Treat(uid, comp.Heal);
                if (Vector2.Distance(Pos(uid), docPos) <= comp.Range)
                    _treated.Add(uid);
            }
        }
    }

    private void Treat(EntityUid patient, float amount)
    {
        var heal = new DamageSpecifier(_proto.Index(Brute), -amount * 3f);
        heal += new DamageSpecifier(_proto.Index(Burn), -amount * 2f);
        _damage.TryChangeDamage(patient, heal, true, interruptsDoAfters: false);
        _blood.TryModifyBleedAmount(patient, -1f);
    }

    private void TickMedics(Entity<SurveyDirectorComponent> ent)
    {
        var map = ent.Owner;
        var director = ent.Comp;
        var claimed = new HashSet<EntityUid>();

        var query = EntityQueryEnumerator<SurveyMemberComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var medic, out var xform))
        {
            if (!medic.Medic || xform.MapUid != map || _timing.CurTime < medic.DodgeUntil)
                continue;

            if (!Alive(uid))
            {
                medic.Patient = null;
                medic.Dragging = false;
                continue;
            }

            var rebuild = medic.Robot && !director.CoreDown;
            if (medic.Patient is { } current && (TerminatingOrDeleted(current) ||
                                                 _mobState.IsDead(current) && !(rebuild && !Comp<SurveyMemberComponent>(current).Rebuilt) ||
                                                 _mobState.IsAlive(current) && Health(current) >= PatientDone))
            {
                Release(uid, medic);
            }

            var pos = Pos(uid);
            if (medic.Patient == null && FindPatient(map, uid, medic, pos, claimed, rebuild) is { } found)
            {
                medic.Patient = found;
                if (!_mobState.IsAlive(found))
                    Radio(ent, uid, "medic", ("where", Where(director, Pos(found))));
            }
            if (medic.Patient is not { } patient)
            {
                if (Board(uid) is { } idle && idle.GetValueOrDefault<SurveyRole>(SurveyKeys.Role, EntityManager) == SurveyRole.Errand)
                {
                    idle.SetValue(SurveyKeys.Role, SurveyRole.None);
                    _reissue.Add(medic.Squad);
                    Replan(uid);
                }

                continue;
            }

            claimed.Add(patient);
            var patientPos = Pos(patient);
            var goal = patientPos;

            if (medic.Dragging && TryComp<PullableComponent>(patient, out var pullable) && pullable.Puller == uid)
                _pulling.TryStopPull(patient, pullable, uid);

            medic.Dragging = false;

            if (Vector2.Distance(pos, patientPos) <= TouchRange)
            {
                if (medic.Robot && _mobState.IsDead(patient))
                {
                    goal = pos;
                    Treat(patient, MedicHeal);
                    if (++medic.RepairProgress >= RebuildTicks)
                    {
                        medic.RepairProgress = 0;
                        Comp<SurveyMemberComponent>(patient).Rebuilt = true;
                        _mobState.ChangeMobState(patient, Content.Shared.Mobs.MobState.Alive);
                        Radio(ent, uid, "rebuilt", ("where", Where(director, patientPos)));
                        Release(uid, medic);
                    }
                }
                else
                {
                    goal = pos;
                    Treat(patient, MedicHeal);
                    _blood.TryModifyBleedAmount(patient, -10f);
                    if (_random.Prob(0.08f))
                        Bark(uid, "patch");
                }
            }

            if (Board(uid) is not { } board)
                continue;

            var errand = At(map, goal);
            var stale = !board.TryGetValue<EntityCoordinates>(SurveyKeys.Errand, out var old, EntityManager) ||
                        Vector2.Distance(old.Position, errand.Position) > 1.5f ||
                        board.GetValueOrDefault<SurveyRole>(SurveyKeys.Role, EntityManager) != SurveyRole.Errand;

            if (!stale)
                continue;

            board.SetValue(SurveyKeys.Errand, errand);
            board.SetValue(SurveyKeys.Role, SurveyRole.Errand);
            Replan(uid);
        }
    }

    private EntityUid? FindPatient(EntityUid map, EntityUid medic, SurveyMemberComponent comp, Vector2 from, HashSet<EntityUid> claimed, bool rebuild)
    {
        EntityUid? best = null;
        var bestScore = float.MaxValue;
        var query = EntityQueryEnumerator<SurveyMemberComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var other, out var xform))
        {
            if (uid == medic || xform.MapUid != map || other.Robot != comp.Robot || other.Scout || claimed.Contains(uid) || _treated.Contains(uid))
                continue;

            if (_mobState.IsDead(uid) && !(rebuild && !other.Rebuilt))
                continue;

            var down = !_mobState.IsAlive(uid);
            if (!down && Health(uid) >= PatientHealth)
                continue;

            var d = Vector2.Distance(_xform.GetWorldPosition(xform), from);
            if (d > MedicReach)
                continue;

            var score = down ? d - 10f : d;
            if (score >= bestScore)
                continue;

            bestScore = score;
            best = uid;
        }

        if (best != null)
            Bark(medic, "medic");

        return best;
    }

    private void Release(EntityUid medic, SurveyMemberComponent comp)
    {
        if (comp.Patient is { } patient && comp.Dragging && TryComp<PullableComponent>(patient, out var pullable) && pullable.Puller == medic)
            _pulling.TryStopPull(patient, pullable, medic);

        comp.Patient = null;
        comp.Dragging = false;
        comp.Haven = null;
    }

    private void TickRecovered(Entity<SurveyDirectorComponent> ent)
    {
        _treated.Clear();
        for (var i = 0; i < ent.Comp.Squads.Count; i++)
        {
            var squad = ent.Comp.Squads[i];
            if (squad.Wounded.RemoveWhere(m => Alive(m) && Health(m) >= PatientDone && !Crippled(m)) > 0)
                _reissue.Add(i);
        }
    }

    private void TickGrenades(Entity<SurveyDirectorComponent> ent)
    {
        var director = ent.Comp;
        var now = _timing.CurTime;
        if (now < director.NextSquadGrenade || director.Kind == SurveyKind.Factory)
            return;

        if (director.Kind == SurveyKind.Pirates && HoldsFire(director))
            return;

        foreach (var squad in director.Squads)
        {
            if (!squad.Engaged || squad.Target is not { } target || !Alive(target))
                continue;

            var targetPos = Pos(target);
            foreach (var member in squad.Members)
            {
                if (!Alive(member) || !TryComp<SurveyMemberComponent>(member, out var comp) ||
                    comp.Grenades.Count == 0 || now < comp.NextGrenade || comp.Patient != null)
                {
                    continue;
                }

                var pos = Pos(member);
                var distance = Vector2.Distance(pos, targetPos);
                if (distance < 4f || distance > 13f)
                    continue;

                var hidden = !_examine.InRangeUnOccluded(member, target, distance + 1f);
                var bunched = PlayersNear(targetPos, 3f) >= 2;
                var falling = squad.Withdrawn || squad.Wounded.Contains(member) || squad.Health < 0.5f;

                string? pick = null;
                if (falling)
                    pick = comp.Grenades.FirstOrDefault(g => g.Id == Smoke).Id;

                if (pick == null && (hidden || bunched))
                    pick = comp.Grenades.FirstOrDefault(g => Lethal.Contains(g.Id)).Id ?? comp.Grenades.FirstOrDefault(g => Stunning.Contains(g.Id)).Id;

                if (pick == null && _random.Prob(0.3f))
                    pick = comp.Grenades.FirstOrDefault(g => g.Id != Smoke).Id;

                if (pick == null)
                    continue;

                var flash = pick == "GrenadeFlashBang";
                var keepAway = pick == Smoke ? 0f : flash ? FlashSafeThrow : Lethal.Contains(pick) ? LethalSafeThrow : SafeThrow;
                if (ThrowPoint(ent.Owner, member, pos, pick == Smoke ? pos + (targetPos - pos) * 0.45f : targetPos, keepAway) is not { } aim)
                    continue;

                if (pick != Smoke && AlliesNear(director, aim, flash ? FlashAllyRange : Lethal.Contains(pick) ? LethalAllyRange : 2f))
                    continue;

                Lob(ent.Owner, member, pick, aim);
                comp.Grenades.Remove(comp.Grenades.First(g => g.Id == pick));
                comp.NextGrenade = now + TimeSpan.FromSeconds(_random.NextFloat(14f, 24f));
                director.NextSquadGrenade = now + GrenadeGap;
                Bark(member, pick == Smoke ? "smoke" : "grenade");
                return;
            }
        }
    }

    private Vector2? ThrowPoint(EntityUid map, EntityUid thrower, Vector2 from, Vector2 wanted, float keepAway)
    {
        for (var i = 0; i < 9; i++)
        {
            var angle = i * MathF.Tau / 8f;
            var point = i == 0 ? wanted : wanted + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 2f;
            var distance = Vector2.Distance(from, point);
            if (distance < keepAway)
                continue;

            if (_interaction.InRangeUnobstructed(thrower, new EntityCoordinates(map, point), distance + 0.5f, ThrowMask))
                return point;
        }

        return null;
    }

    private bool AlliesNear(SurveyDirectorComponent director, Vector2 at, float range)
    {
        foreach (var squad in director.Squads)
        {
            foreach (var ally in squad.Members)
            {
                if (Alive(ally) && Vector2.Distance(Pos(ally), at) < range)
                    return true;
            }
        }

        return false;
    }

    private void Lob(EntityUid map, EntityUid thrower, string proto, Vector2 at)
    {
        var grenade = SpawnAtPosition(proto, Transform(thrower).Coordinates);
        _trigger.ActivateTimerTrigger(grenade, thrower);
        _throwing.TryThrow(grenade, new EntityCoordinates(map, at), 9f, thrower, pushbackRatio: 0f, compensateFriction: true);
    }

    private void TickScouts(Entity<SurveyDirectorComponent> ent)
    {
        var director = ent.Comp;
        var map = ent.Owner;
        var now = _timing.CurTime;
        director.Scouts.RemoveAll(s => !Alive(s));

        foreach (var scout in director.Scouts)
        {
            if (!TryComp<SurveyMemberComponent>(scout, out var comp) || now < comp.DodgeUntil || Board(scout) is not { } board)
                continue;

            var pos = Pos(scout);
            var seen = NearestSeen(director, pos, comp.Sight, false);
            if (seen is { } player && !_examine.InRangeUnOccluded(scout, player, comp.Sight))
                seen = null;

            Vector2 goal;
            if (seen is { } spotted)
            {
                var playerPos = Pos(spotted);
                var back = pos - playerPos;
                if (back.LengthSquared() < 0.01f)
                    back = Vector2.UnitX;

                goal = Walkable(map, playerPos + Vector2.Normalize(back) * 7f);
                if (comp.Spotted != spotted || now >= comp.NextReport)
                {
                    comp.Spotted = spotted;
                    comp.NextReport = now + TimeSpan.FromSeconds(20);
                    Report(ent, spotted, playerPos);
                }
            }
            else
            {
                comp.Spotted = null;
                var route = ScoutRoute(director);
                if (route.Count == 0)
                    continue;

                if (board.TryGetValue<EntityCoordinates>(SurveyKeys.Errand, out var current, EntityManager) &&
                    Vector2.Distance(current.Position, pos) > 2f && now < comp.NextShift)
                {
                    continue;
                }

                comp.NextShift = now + TimeSpan.FromSeconds(40);
                comp.Waypoint = (comp.Waypoint + 1 + _random.Next(2)) % route.Count;
                goal = Walkable(map, route[comp.Waypoint]);
            }

            var errand = At(map, goal);
            if (board.TryGetValue<EntityCoordinates>(SurveyKeys.Errand, out var old, EntityManager) &&
                Vector2.Distance(old.Position, errand.Position) <= 1.5f &&
                board.GetValueOrDefault<SurveyRole>(SurveyKeys.Role, EntityManager) == SurveyRole.Errand)
            {
                continue;
            }

            board.SetValue(SurveyKeys.Errand, errand);
            board.SetValue(SurveyKeys.Role, SurveyRole.Errand);
            Replan(scout);
        }
    }

    private List<Vector2> ScoutRoute(SurveyDirectorComponent director)
    {
        var route = director.Pois.Select(p => p.Pos).ToList();
        route.AddRange(director.Outposts.Select(o => o.Pos));
        route.Add(director.BaseCentre);
        if (director.Anchors.Count > 0)
            route.AddRange(director.Anchors.Where((_, i) => i % 4 == 0));

        return route;
    }

    private void Report(Entity<SurveyDirectorComponent> ent, EntityUid player, Vector2 at)
    {
        var director = ent.Comp;
        director.LastContact = _timing.CurTime;

        var callers = director.Squads
            .Where(s => s.Alive > 0 && !s.Withdrawn && !s.Ambush)
            .Select(s => (Squad: s, Dist: Vector2.Distance(Centroid(s, out _), at)))
            .Where(s => s.Dist < ScoutSpotRange)
            .OrderBy(s => s.Dist)
            .Take(2)
            .ToList();

        if (callers.Count == 0)
            return;

        RaiseAlarm(ent, callers[0].Squad);
        BarkOne(callers[0].Squad, "scout", at);
        Radio(ent, Speaker(callers[0].Squad), "scout", ("where", Where(director, at)), ("count", PlayersNear(at, 10f)));

        if (director.CommsCut && director.Kind != SurveyKind.Factory)
            return;

        foreach (var (squad, _) in callers)
        {
            if (squad.Engaged && squad.Target != null)
                continue;

            squad.Engaged = true;
            squad.Target = player;
            squad.LastSeen = _timing.CurTime + TimeSpan.FromSeconds(25);

            if (director.Kind == SurveyKind.Factory)
                continue;

            ArmUp(director, squad);
            Issue(ent, squad, director.Kind == SurveyKind.Pirates);
        }
    }
}
