// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.NPC;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Hands.Components;
using Content.Shared.Movement.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.PlanetSurvey;

public sealed partial class SurveyDirectorSystem
{
    private static readonly TimeSpan ForgetAfter = TimeSpan.FromSeconds(20);
    private const float WoundedHealth = 0.45f;
    private const string Armor = "ClothingOuterArmorBasic";
    private const string Helmet = "ClothingHeadHelmetBasic";
    private const string Smg = "SurveyNpcSmg";
    private const string Rifle = "SurveyNpcRifle";

    private void SpawnHumans(Entity<SurveyDirectorComponent> ent, SurveyWorldPrototype world, System.Random random, List<SurveySpot> spots)
    {
        if (world.Squads.Count == 0 || spots.Count == 0)
            return;

        var near = spots.Where(s => s.Outpost < 0).OrderBy(s => Vector2.DistanceSquared(s.Center, ent.Comp.Landing)).ToList();
        var deep = spots.Where(s => s.Outpost < 0).OrderBy(_ => random.Next()).ToList();
        if (near.Count == 0)
        {
            near = spots.ToList();
            deep = spots.ToList();
        }

        var garrisons = world.Squads.Where(s => !s.Ambush).ToList();
        SurveySpot? first = null;

        for (var i = 0; i < world.SquadCount; i++)
        {
            var outpostIndex = i - 1;
            var garrison = outpostIndex >= 0 && outpostIndex < ent.Comp.Outposts.Count && garrisons.Count > 0;
            var entry = i == 0 ? world.Squads[0]
                : garrison ? garrisons[random.Next(garrisons.Count)]
                : world.Squads[random.Next(world.Squads.Count)];

            var spot = garrison
                ? NextSpot(spots.Where(s => s.Outpost == outpostIndex).ToList() is { Count: > 0 } rooms ? rooms : deep, random)
                : NextSpot(entry.Ambush ? near : deep, random);
            first ??= spot;

            var squad = new SurveySquad
            {
                Home = new EntityCoordinates(ent.Owner, FreeTile(ent.Owner, spot, random)),
                Ambush = entry.Ambush,
                NextChatter = _timing.CurTime + TimeSpan.FromSeconds(random.Next(20, 60)),
            };

            foreach (var proto in entry.Members)
            {
                squad.Members.Add(SpawnMember(ent, proto, spot, ent.Comp.Squads.Count, random));
            }

            if (!entry.Ambush)
                _guarded.Add(spot);

            squad.Size = squad.Members.Count;
            squad.Alive = squad.Size;
            ent.Comp.Squads.Add(squad);

            if (garrison)
                ent.Comp.Outposts[outpostIndex].Squad = squad;
        }

        if (ent.Comp.Kind == SurveyKind.Pirates && world.Stash is { } stash && first != null)
            ent.Comp.Stash = SpawnAtPosition(stash, new EntityCoordinates(ent.Owner, FreeTile(ent.Owner, first, random)));
    }

    private void TickHumans(Entity<SurveyDirectorComponent> ent, bool pirates)
    {
        var director = ent.Comp;
        var now = _timing.CurTime;

        if (pirates)
            TickDeal(ent);

        foreach (var squad in director.Squads)
        {
            var reissue = director.ReissueAll;
            var centre = Centroid(squad, out var alive);

            if (alive < squad.Alive)
            {
                director.Kills += squad.Alive - alive;
                reissue = true;
                BarkOne(squad, "loss");
                if (director.Kills % 5 == 0)
                    director.Heat++;

                if (alive > 0 && squad.Engaged && squad.Target is { } foe && Alive(foe))
                    Assist(ent, squad, foe, centre);
            }

            var wiped = alive == 0 && squad.Alive > 0;
            squad.Alive = alive;
            if (wiped)
                CallQrf(ent, squad);

            if (alive == 0)
                continue;

            squad.LastPos = centre;
            var health = 0f;
            Vector2? struck = null;
            foreach (var member in squad.Members)
            {
                if (!Alive(member) || !TryComp<SurveyMemberComponent>(member, out var comp))
                    continue;

                var hp = Health(member);
                health += hp;
                var hit = hp < comp.LastHealth - 0.01f;
                if (hit)
                    struck = Pos(member);

                if (!squad.Wounded.Contains(member) && !squad.Withdrawn)
                    Maneuver(ent, squad, member, comp, hit);

                if (pirates && HoldsFire(director) && hp < comp.LastHealth - 0.02f)
                    Betray(ent);

                if ((hp < WoundedHealth || Crippled(member)) && squad.Wounded.Add(member))
                {
                    reissue = true;
                    Bark(member, "hurt");
                }

                comp.LastHealth = hp;
            }

            squad.Health = health / alive;

            var alert = squad.Ambush ? 11f : 13f;
            var reach = squad.Engaged ? alert * 2f : alert;
            var target = struck is { } hitAt
                ? NearestPlayer(hitAt, 40f, out _)
                : SquadSees(director, squad, reach);

            if (squad.Target is { } old && !Alive(old))
            {
                BarkOne(squad, "kill");
                squad.Target = null;
                reissue = true;
            }

            if (target is { } seen)
            {
                if (!squad.Engaged)
                {
                    squad.Engaged = true;
                    reissue = true;

                    if (pirates && director.Phase == SurveyPhase.Quiet && !director.DealPaid)
                        StartDeal(ent, squad, centre);
                    else
                    {
                        BarkOne(squad, "seen", Pos(seen));
                        Radio(ent, Speaker(squad, Pos(seen)), "contact", ("where", Where(director, Pos(seen))), ("count", PlayersNear(Pos(seen), 10f)));
                    }

                    if (!(pirates && director.Phase == SurveyPhase.Deal))
                        RaiseAlarm(ent, squad);
                    ArmUp(director, squad);
                    if (struck != null)
                        Assist(ent, squad, seen, centre);
                }

                director.LastContact = now;

                if (squad.Target != seen)
                {
                    squad.Target = seen;
                    reissue = true;
                }

                squad.LastSeen = now;
            }
            else if (squad.Engaged && now - squad.LastSeen > ForgetAfter)
            {
                squad.Engaged = false;
                squad.Target = null;
                reissue = true;
            }

            var withdraw = alive * 3 <= squad.Size || pirates && director.Phase == SurveyPhase.Withdraw;
            if (withdraw && !squad.Withdrawn)
            {
                squad.Withdrawn = true;
                reissue = true;
                BarkOne(squad, "retreat");
            }

            if (reissue)
            {
                Issue(ent, squad, pirates);
            }
            else if (squad.Engaged && squad.Target is { } aimed)
            {
                RefreshAim(ent.Owner, squad, aimed);
            }
            else if (!squad.Engaged && now >= squad.NextChatter)
            {
                squad.NextChatter = now + TimeSpan.FromSeconds(_random.Next(35, 80));
                if (PlayersNear(centre, 18f) > 0)
                    BarkOne(squad, "idle");
            }
        }
    }

    private void Assist(Entity<SurveyDirectorComponent> ent, SurveySquad caller, EntityUid enemy, Vector2 from)
    {
        BarkOne(caller, "help");
        if (ent.Comp.CommsCut)
            return;

        Radio(ent, Speaker(caller, from), "help", ("where", Where(ent.Comp, from)));

        var helpers = ent.Comp.Squads
            .Where(s => s != caller && !s.Engaged && !s.Withdrawn && s.Alive > 0 && !s.Ambush)
            .Select(s => (Squad: s, Dist: Vector2.Distance(Centroid(s, out _), from)))
            .Where(s => s.Dist < 60f)
            .OrderBy(s => s.Dist)
            .Take(2)
            .ToList();

        foreach (var (helper, _) in helpers)
        {
            helper.Engaged = true;
            helper.Target = enemy;
            helper.LastSeen = _timing.CurTime + TimeSpan.FromSeconds(30);
            ArmUp(ent.Comp, helper);
            BarkOne(helper, "reinforce");
            Radio(ent, Speaker(helper), "moving", ("where", Where(ent.Comp, from)));
            Issue(ent, helper, ent.Comp.Kind == SurveyKind.Pirates);
        }
    }

    private void ArmUp(SurveyDirectorComponent director, SurveySquad squad)
    {
        if (squad.Armed)
            return;

        squad.Armed = true;
        var upgrade = director.Threat >= 1.25f || director.Alarm;
        var pirates = director.Kind == SurveyKind.Pirates;
        var first = true;
        foreach (var member in squad.Members)
        {
            if (!Alive(member) || !HasComp<Content.Shared.Inventory.InventoryComponent>(member))
                continue;

            Wear(member, "outerClothing", upgrade ? _random.Pick(HeavyArmor) : Armor);
            Wear(member, "head", upgrade ? _random.Pick(HeavyHelmets) : Helmet);

            if (!upgrade)
                continue;

            if (TryComp<SurveyMemberComponent>(member, out var comp) && comp.Grenades.Count < 2)
                comp.Grenades.Add(_random.Prob(0.6f) ? (pirates ? "GrenadeShrapnel" : "ExGrenade") : "SmokeGrenade");

            if (comp != null)
            {
                comp.Medkits = Math.Max(comp.Medkits, 1);
                if (comp.Stims.Count < 2)
                    comp.Stims.Add(_random.Prob(0.5f) ? "speed" : "tough");
            }

            if (!first && !_random.Prob(0.6f) || comp is { Medic: true })
                continue;

            var better = first ? (pirates ? Smg : "SurveyNpcLmg") : _random.Pick(BetterGuns);
            first = false;
            foreach (var held in _hands.EnumerateHeld(member).ToList())
            {
                if (!HasComp<SurveyNativeWeaponComponent>(held) || !WeakGuns.Contains(MetaData(held).EntityPrototype?.ID ?? string.Empty))
                    continue;

                QueueDel(held);
                var gun = SpawnAtPosition(better, Transform(member).Coordinates);
                _hands.PickupOrDrop(member, gun);
                break;
            }
        }
    }

    private static readonly string[] HeavyArmor = { "ClothingOuterArmorBulletproof", "ClothingOuterVestWebMerc", "ClothingOuterArmorBasic", "ClothingOuterArmorRiot" };
    private static readonly string[] HeavyHelmets = { "ClothingHeadHelmetSwat", "ClothingHeadHelmetMerc", "ClothingHeadHelmetBasic", "ClothingHeadHelmetRiot" };
    private static readonly string[] BetterGuns = { "SurveyNpcAk", "SurveyNpcAk", "SurveyNpcBulldog", "SurveyNpcVector", Rifle, "SurveyNpcCarbine" };
    private static readonly HashSet<string> WeakGuns = new() { "SurveyNpcPistol", "SurveyNpcRevolver", "SurveyNpcShotgun", "SurveyNpcCobra", "SurveyNpcC20r", "SurveyNpcSmg" };

    private void Wear(EntityUid uid, string slot, string proto)
    {
        if (_inventory.TryGetSlotEntity(uid, slot, out var worn))
        {
            if (HasComp<Content.Shared.Armor.ArmorComponent>(worn) || !_inventory.TryUnequip(uid, slot, silent: true, force: true))
                return;

            QueueDel(worn.Value);
        }

        var item = SpawnAtPosition(proto, Transform(uid).Coordinates);
        if (!_inventory.TryEquip(uid, item, slot, silent: true, force: true))
            QueueDel(item);
    }

    private void Issue(Entity<SurveyDirectorComponent> ent, SurveySquad squad, bool pirates)
    {
        var director = ent.Comp;
        var map = ent.Owner;
        var target = squad.Engaged ? squad.Target : null;
        var targetPos = target is { } t ? Pos(t) : Vector2.Zero;

        _scratch.Clear();
        foreach (var member in squad.Members)
        {
            if (Alive(member))
                _scratch.Add(member);
        }

        if (target != null)
            _scratch.Sort((a, b) => Vector2.DistanceSquared(Pos(a), targetPos).CompareTo(Vector2.DistanceSquared(Pos(b), targetPos)));

        var fit = _scratch.Count(m => !squad.Wounded.Contains(m));
        var confident = target != null && squad.Health > 0.6f && fit >= PlayersNear(targetPos, 10f) + 1;
        var holdFire = pirates && HoldsFire(director);
        var rush = pirates && director.Phase == SurveyPhase.Betrayal ? RushPoint(ent, squad.Home.Position) : null;
        var side = 1f;

        for (var k = 0; k < _scratch.Count; k++)
        {
            var member = _scratch[k];
            if (Board(member) is not { } board || Tending(member))
                continue;

            var pos = Pos(member);
            if (TryComp<SurveyMemberComponent>(member, out var orders))
                orders.Approach = false;

            var retreat = squad.Withdrawn || squad.Wounded.Contains(member);
            board.SetValue(SurveyKeys.Retreat, retreat);
            board.SetValue(SurveyKeys.HoldFire, holdFire);
            board.SetValue(SurveyKeys.Confident, confident);
            board.SetValue(SurveyKeys.Cover, squad.Wounded.Contains(member) && DocFor(director, pos, false) is { } doc
                ? At(map, Walkable(map, doc))
                : CoverPoint(map, squad, pos, target != null ? targetPos : null));
            if (target is not { } enemy)
            {
                board.SetValue(SurveyKeys.Role, SurveyRole.None);
                board.Remove<EntityUid>(SurveyKeys.Target);
                Replan(member);
                continue;
            }

            board.SetValue(SurveyKeys.Target, enemy);
            SurveyRole role;

            if (squad.Ambush)
            {
                role = SurveyRole.Ambush;
            }
            else if (rush is { } point && k == 0)
            {
                role = SurveyRole.Rush;
                board.SetValue(SurveyKeys.Rush, point);
            }
            else if (k == 0 && _scratch.Count > 1)
            {
                role = SurveyRole.Suppress;
            }
            else if (k % 3 == 1 && ReachableFlank(map, member, pos, targetPos, side) is { } flank)
            {
                role = SurveyRole.Flank;
                board.SetValue(SurveyKeys.Flank, flank);
                side = -side;
            }
            else
            {
                role = SurveyRole.Hold;
            }

            board.SetValue(SurveyKeys.Role, role);
            WriteZones(board, map, enemy, confident);
            Replan(member);
        }

        _scratch.Clear();
    }

    private void RefreshAim(EntityUid map, SurveySquad squad, EntityUid target)
    {
        foreach (var member in squad.Members)
        {
            if (Alive(member) && Board(member) is { } board)
                WriteZones(board, map, target, board.GetValueOrDefault<bool>(SurveyKeys.Confident, EntityManager));
        }
    }

    private void WriteZones(NPCBlackboard board, EntityUid map, EntityUid target, bool confident)
    {
        board.SetValue(SurveyKeys.TargetCoords, At(map, Pos(target)));
        var armed = ArmedHand(target);
        board.SetValue(SurveyKeys.AimZone, PickZone(target, armed, confident));

        if (armed is { } hand)
            board.SetValue(SurveyKeys.MeleeZone, hand);
        else
            board.Remove<LimbZone>(SurveyKeys.MeleeZone);
    }

    private LimbZone PickZone(EntityUid target, LimbZone? armed, bool confident)
    {
        if (armed is { } hand && _random.Prob(0.6f))
            return hand;

        if (TryComp<InputMoverComponent>(target, out var mover) && mover.HasDirectionalMovement)
        {
            if (mover.Sprinting)
                return _random.Prob(0.5f) ? LimbZone.LegLeft : LimbZone.LegRight;
        }
        else if (confident)
        {
            return _random.Prob(0.3f) ? LimbZone.Eyes : LimbZone.Head;
        }

        return LimbZone.Torso;
    }

    private LimbZone? ArmedHand(EntityUid target)
    {
        if (!_gun.TryGetGun(target, out _) || !TryComp<HandsComponent>(target, out var hands))
            return null;

        return hands.ActiveHandId?.Contains("left", StringComparison.OrdinalIgnoreCase) == true
            ? LimbZone.HandLeft
            : LimbZone.HandRight;
    }

    private EntityCoordinates CoverPoint(EntityUid map, SurveySquad squad, Vector2 from, Vector2? threat)
    {
        var home = squad.Home.Position;
        if (threat is not { } danger || Vector2.Distance(home, danger) > 6f && Vector2.Distance(home, from) < 25f)
            return At(map, home);

        var away = from - danger;
        if (away.LengthSquared() < 0.01f)
            away = Vector2.UnitY;

        return At(map, Walkable(map, from + Vector2.Normalize(away) * 9f));
    }

    private EntityCoordinates FlankPoint(EntityUid map, Vector2 from, Vector2 target, float side)
    {
        var dir = target - from;
        if (dir.LengthSquared() < 0.01f)
            dir = Vector2.UnitX;

        dir = Vector2.Normalize(dir);
        var perp = new Vector2(-dir.Y, dir.X) * side;
        return At(map, Walkable(map, target + perp * 6f - dir * 2f));
    }

    private bool HoldsFire(SurveyDirectorComponent director)
    {
        return director.Phase == SurveyPhase.Deal || director.Phase == SurveyPhase.Quiet && director.DealPaid;
    }

    private void StartDeal(Entity<SurveyDirectorComponent> ent, SurveySquad squad, Vector2 centre)
    {
        ent.Comp.Phase = SurveyPhase.Deal;
        ent.Comp.DealEnds = _timing.CurTime + TimeSpan.FromSeconds(90);
        ent.Comp.ReissueAll = true;
        BarkOne(squad, "deal", centre);
    }

    private void TickDeal(Entity<SurveyDirectorComponent> ent)
    {
        var director = ent.Comp;
        var now = _timing.CurTime;

        switch (director.Phase)
        {
            case SurveyPhase.Deal:
                if (TakeTribute(ent))
                {
                    director.DealPaid = true;
                    director.Phase = SurveyPhase.Quiet;
                    director.ReissueAll = true;
                    SquadBark(director, "paid");

                    var wounded = _players.Count(p => Health(p.Uid) < 0.7f);
                    var chance = 0.3f + 0.15f * wounded + 0.1f * director.Heat;
                    if (_random.Prob(Math.Clamp(chance, 0f, 0.9f)))
                        director.BetrayAt = now + TimeSpan.FromSeconds(_random.Next(30, 120));
                }
                else if (now >= director.DealEnds)
                {
                    Betray(ent);
                }
                break;

            case SurveyPhase.Quiet when director.BetrayAt is { } at && now >= at:
                Betray(ent);
                break;

            case SurveyPhase.Betrayal:
                var alive = 0;
                var size = 0;
                var health = 0f;
                foreach (var squad in director.Squads)
                {
                    size += squad.Size;
                    alive += squad.Alive;
                    health += squad.Health * squad.Alive;
                }

                var bleeding = alive * 2 <= size || alive > 0 && health / alive < 0.5f;
                if (bleeding || director.Cargo.Count == 0)
                {
                    director.Phase = SurveyPhase.Withdraw;
                    director.ReissueAll = true;
                }
                break;
        }
    }

    private void Betray(Entity<SurveyDirectorComponent> ent)
    {
        if (ent.Comp.Phase is SurveyPhase.Betrayal or SurveyPhase.Withdraw)
            return;

        ent.Comp.Phase = SurveyPhase.Betrayal;
        ent.Comp.BetrayAt = null;
        ent.Comp.ReissueAll = true;
        SquadBark(ent.Comp, "betray");
        if (ent.Comp.Squads.FirstOrDefault(s => s.Alive > 0) is { } loud)
            RaiseAlarm(ent, loud);
    }

    private void SquadBark(SurveyDirectorComponent director, string pool)
    {
        foreach (var squad in director.Squads)
        {
            if (squad.Alive > 0)
                BarkOne(squad, pool);
        }
    }

    private bool TakeTribute(Entity<SurveyDirectorComponent> ent)
    {
        if (ent.Comp.Stash is not { } stash || !Exists(stash))
            return false;

        var at = Pos(stash);
        foreach (var cargo in ent.Comp.Cargo)
        {
            var xform = Transform(cargo);
            if (xform.ParentUid != ent.Owner || Vector2.Distance(_xform.GetWorldPosition(xform), at) > 2.5f)
                continue;

            ent.Comp.Cargo.Remove(cargo);
            ent.Comp.CargoHolders.Remove(cargo);
            QueueDel(cargo);
            return true;
        }

        return false;
    }

    private EntityCoordinates? RushPoint(Entity<SurveyDirectorComponent> ent, Vector2 from)
    {
        EntityUid? best = null;
        var bestDist = float.MaxValue;

        foreach (var cargo in ent.Comp.Cargo)
        {
            var d = Vector2.DistanceSquared(Pos(cargo), from);
            if (d >= bestDist)
                continue;

            bestDist = d;
            best = cargo;
        }

        if (best == null)
        {
            foreach (var shuttle in ent.Comp.Shuttles)
            {
                if (!Exists(shuttle) || Transform(shuttle).MapUid != ent.Owner)
                    continue;

                var d = Vector2.DistanceSquared(Pos(shuttle), from);
                if (d >= bestDist)
                    continue;

                bestDist = d;
                best = shuttle;
            }
        }

        return best is { } uid ? At(ent.Owner, Pos(uid)) : null;
    }
}
