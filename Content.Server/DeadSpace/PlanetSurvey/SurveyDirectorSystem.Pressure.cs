// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Shared.Mobs;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.PlanetSurvey;

public sealed partial class SurveyDirectorSystem
{
    private static readonly TimeSpan JudgeEvery = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan Memory = TimeSpan.FromSeconds(150);
    private const float ThreatMin = 0.5f;
    private const float ThreatMax = 2.5f;

    private void InitializePressure()
    {
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobState);
        SubscribeLocalEvent<SurveyNativeWeaponComponent, ShotAttemptedEvent>(OnNativeShot);
        SubscribeLocalEvent<SurveyMemberComponent, ShotAttemptedEvent>(OnRobotShot);
        SubscribeLocalEvent<SurveyNativeMineComponent, StepTriggerAttemptEvent>(OnNativeMine);
    }

    private void OnNativeShot(Entity<SurveyNativeWeaponComponent> ent, ref ShotAttemptedEvent args)
    {
        if (!HasComp<SurveyMemberComponent>(args.User) || FriendlyLine(args.User, args.Used))
        {
            args.Cancel();
            return;
        }

        AimLimb(args.User, args.Used);
    }

    private void OnRobotShot(Entity<SurveyMemberComponent> ent, ref ShotAttemptedEvent args)
    {
        if (FriendlyLine(args.User, args.Used))
        {
            args.Cancel();
            return;
        }

        AimLimb(args.User, args.Used);
    }

    private bool FriendlyLine(EntityUid shooter, Entity<GunComponent> gun)
    {
        if (gun.Comp.ShootCoordinates is not { } aim || Transform(shooter).MapUid is not { } map ||
            !TryComp<SurveyDirectorComponent>(map, out var director))
        {
            return false;
        }

        return AllyInLine(director, shooter, Pos(shooter), _xform.ToMapCoordinates(aim).Position);
    }

    private void OnNativeMine(Entity<SurveyNativeMineComponent> ent, ref StepTriggerAttemptEvent args)
    {
        if (HasComp<SurveyMemberComponent>(args.Tripper))
            args.Cancelled = true;
    }

    private bool InArena(SurveyDirectorComponent director, Vector2 pos)
    {
        return director.ArenaRadius <= 0f || Vector2.Distance(pos, director.BaseCentre) < director.ArenaRadius - 4f;
    }

    private void OnMobState(MobStateChangedEvent ev)
    {
        if (ev.NewMobState == MobState.Alive)
        {
            Rearm(ev.Target);
            return;
        }

        var map = Transform(ev.Target).MapUid;
        if (map == null || !TryComp<SurveyDirectorComponent>(map, out var director))
            return;

        if (HasComp<ActorComponent>(ev.Target))
        {
            director.DownLog.Enqueue(_timing.CurTime);
            if (director.Storm)
                director.StormLoss = true;
            return;
        }

        if (!HasComp<SurveyMemberComponent>(ev.Target))
            return;

        BreakWeapons(ev.Target);
        if (ev.NewMobState == MobState.Dead)
        {
            director.KillLog.Enqueue(_timing.CurTime);
            if (director.Kind is SurveyKind.Raiders or SurveyKind.Pirates && !Comp<SurveyMemberComponent>(ev.Target).Robot)
                DropRadioKey(ev.Target);
        }
    }

    private void Rearm(EntityUid body)
    {
        if (!TryComp<SurveyMemberComponent>(body, out var member) || member.Robot ||
            !HasComp<Content.Shared.Hands.Components.HandsComponent>(body))
        {
            return;
        }

        foreach (var held in _hands.EnumerateHeld(body))
        {
            if (HasComp<SurveyNativeWeaponComponent>(held))
                return;
        }

        var gun = SpawnAtPosition(member.Sidearm ?? "SurveyNpcPistol", Transform(body).Coordinates);
        _hands.PickupOrDrop(body, gun);
    }

    private void BreakWeapons(EntityUid body)
    {
        foreach (var held in _hands.EnumerateHeld(body).ToList())
        {
            if (!TryComp<SurveyNativeWeaponComponent>(held, out var weapon))
                continue;

            if (weapon.Drop is { } drop && _random.Prob(weapon.DropChance))
                SpawnAtPosition(drop, Transform(body).Coordinates);

            QueueDel(held);
        }
    }

    private void TickHunt(Entity<SurveyDirectorComponent> ent)
    {
        var director = ent.Comp;
        var now = _timing.CurTime;

        if (_players.Count == 0 || director.Kind == SurveyKind.DeadElement)
            return;

        if (director.Kind is SurveyKind.Raiders or SurveyKind.Pirates && !director.Alarm)
            return;

        director.Arrived ??= now;
        var quiet = TimeSpan.FromSeconds(70 / director.Threat);
        if (now - director.Arrived < TimeSpan.FromSeconds(45) || now - director.LastContact < quiet)
            return;

        var (prey, preyPos) = _players.OrderBy(p => PlayersNear(p.Pos, 12f)).First();
        director.LastContact = now;

        var spotPos = HiddenPoint(director, preyPos);
        if (spotPos == null)
            return;

        var spot = OpenSpot(ent.Owner, spotPos.Value, 2);
        var random = new System.Random(_random.Next());

        switch (director.Kind)
        {
            case SurveyKind.Raiders:
            case SurveyKind.Pirates:
            case SurveyKind.Beasts:
                SurveySquad? best = null;
                var bestDist = float.MaxValue;
                foreach (var squad in director.Squads)
                {
                    if (squad.Engaged || squad.Withdrawn || squad.Ambush || squad.Alive == 0 ||
                        squad.Members.Count == 0 || TryComp<SurveyMemberComponent>(squad.Members[0], out var lead) && lead.Prey)
                    {
                        continue;
                    }

                    var d = Vector2.Distance(Centroid(squad, out _), preyPos);
                    if (d >= bestDist)
                        continue;

                    bestDist = d;
                    best = squad;
                }

                if (best == null)
                    return;

                if (bestDist > 26f)
                    Relocate(ent, best.Members, spot, random);

                if (director.Kind == SurveyKind.Beasts)
                {
                    if (best.Members.FirstOrDefault(Alive) is { Valid: true } caller)
                        Call(ent, best, caller, preyPos, _proto.TryIndex(director.World, out var world) ? world.CallSound : null);
                    break;
                }

                best.Engaged = true;
                best.Target = prey;
                best.LastSeen = now + TimeSpan.FromSeconds(30);
                ArmUp(director, best);
                Issue(ent, best, director.Kind == SurveyKind.Pirates);
                break;

            case SurveyKind.Factory:
                var drones = new List<EntityUid>();
                foreach (var stamp in director.Stamps)
                {
                    if (TryComp<SurveyStampComponent>(stamp, out var comp))
                        drones.AddRange(comp.Printed.Where(Alive).Take(2));
                }

                Relocate(ent, drones.Take(3).ToList(), spot, random);
                break;
        }
    }

    private Vector2? HiddenPoint(SurveyDirectorComponent director, Vector2 near, float min = 15f, float max = 19f)
    {
        for (var i = 0; i < 8; i++)
        {
            var angle = _random.NextFloat(0f, MathF.Tau);
            var pos = near + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * _random.NextFloat(min, max);
            if (!NearPad(director, pos, PadClearance) && InArena(director, pos) && NearestPlayer(pos, min - 2f, out _) == null)
                return pos;
        }

        return null;
    }

    private void Relocate(Entity<SurveyDirectorComponent> ent, List<EntityUid> members, SurveySpot spot, System.Random random)
    {
        foreach (var member in members)
        {
            if (!Alive(member))
                continue;

            var at = FreeTile(ent.Owner, spot, random);
            _xform.SetCoordinates(member, new EntityCoordinates(ent.Owner, at));
            if (Board(member) is { } board)
                board.SetValue(SurveyKeys.Post, new EntityCoordinates(ent.Owner, at));
            Replan(member);
        }
    }

    private void TickPressure(Entity<SurveyDirectorComponent> ent)
    {
        var director = ent.Comp;
        var now = _timing.CurTime;

        while (director.KillLog.TryPeek(out var kill) && now - kill > Memory)
        {
            director.KillLog.Dequeue();
        }

        while (director.DownLog.TryPeek(out var down) && now - down > Memory)
        {
            director.DownLog.Dequeue();
        }

        if (_players.Count == 0 || now < director.NextJudge)
            return;

        director.NextJudge = now + JudgeEvery;

        var health = _players.Average(p => Health(p.Uid));
        var kills = director.KillLog.Count;
        var downs = director.DownLog.Count;

        if (downs > 0 || health < 0.45f)
        {
            director.Threat = Math.Max(ThreatMin, director.Threat - 0.25f);
            var breather = now + TimeSpan.FromSeconds(120);
            if (director.NextWave < breather)
                director.NextWave = breather;
            return;
        }

        if (kills >= 2 + _players.Count && health > 0.6f)
            director.Threat = Math.Min(ThreatMax, director.Threat + 0.25f);
        else if (kills == 0 && health > 0.8f)
            director.Threat = Math.Min(ThreatMax, director.Threat + 0.05f);

        if (director.Threat < 1.25f || now < director.NextWave || director.Waves >= 3 + _players.Count)
            return;

        if (director.Kind is SurveyKind.Raiders or SurveyKind.Pirates or SurveyKind.Factory &&
            (!director.Alarm || director.CommsCut))
        {
            return;
        }

        if (Reinforce(ent))
        {
            director.Waves++;
            director.NextWave = now + TimeSpan.FromSeconds(150 / director.Threat);
        }
    }

    private bool Reinforce(Entity<SurveyDirectorComponent> ent)
    {
        var director = ent.Comp;
        if (!_proto.TryIndex(director.World, out var world))
            return false;

        var (victim, victimPos) = _random.Pick(_players);
        var angle = _random.NextFloat(0f, MathF.Tau);
        var at = HiddenPoint(director, victimPos) ?? victimPos + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * _random.NextFloat(18f, 24f);
        if (!InArena(director, at))
            at = director.BaseCentre + Vector2.Normalize(at - director.BaseCentre) * (director.ArenaRadius - 8f);
        var spot = OpenSpot(ent.Owner, at, 2);
        var random = new System.Random(_random.Next());
        var size = (int) MathF.Round(1f + director.Threat * (0.6f + 0.4f * _players.Count));

        switch (director.Kind)
        {
            case SurveyKind.Raiders:
            case SurveyKind.Pirates:
                if (world.Squads.Count == 0)
                    return false;

                var members = _random.Pick(world.Squads).Members;
                var squad = NewSquad(ent, spot);
                for (var i = 0; i < Math.Max(2, size); i++)
                {
                    squad.Members.Add(SpawnMember(ent, members[i % members.Count], spot, director.Squads.Count - 1, random));
                }

                squad.Size = squad.Members.Count;
                squad.Alive = squad.Size;
                squad.Engaged = true;
                squad.Target = victim;
                squad.LastSeen = _timing.CurTime + TimeSpan.FromSeconds(30);
                director.ReissueAll = true;
                BarkOne(squad, "reinforce");
                break;

            case SurveyKind.Factory:
                if (world.Elites.Count == 0)
                    return false;

                var guard = NewSquad(ent, spot);
                for (var i = 0; i < Math.Max(1, size - 1); i++)
                {
                    guard.Members.Add(SpawnMember(ent, _random.Pick(world.Elites), spot, director.Squads.Count - 1, random));
                }

                guard.Size = guard.Members.Count;
                guard.Alive = guard.Size;
                director.Heat++;
                break;

            case SurveyKind.Beasts:
                if (world.Predators.Count == 0)
                    return false;

                var pack = NewSquad(ent, spot);
                var beast = _random.Pick(world.Predators);
                for (var i = 0; i < Math.Max(2, size); i++)
                {
                    pack.Members.Add(SpawnMember(ent, beast, spot, director.Squads.Count - 1, random));
                }

                pack.Size = pack.Members.Count;
                pack.Alive = pack.Size;
                Call(ent, pack, pack.Members[0], victimPos, world.CallSound);
                break;

            case SurveyKind.DeadElement:
                if (director.Storm)
                    return false;

                var soon = _timing.CurTime + TimeSpan.FromSeconds(45);
                if (director.StormAt > soon)
                    director.StormAt = soon;
                director.Heat++;
                break;
        }

        director.NativesTotal = director.Squads.Sum(s => s.Size);
        return true;
    }

    private SurveySquad NewSquad(Entity<SurveyDirectorComponent> ent, SurveySpot spot)
    {
        var squad = new SurveySquad
        {
            Home = new EntityCoordinates(ent.Owner, spot.Center),
            NextChatter = _timing.CurTime + TimeSpan.FromSeconds(60),
        };
        ent.Comp.Squads.Add(squad);
        return squad;
    }
}
