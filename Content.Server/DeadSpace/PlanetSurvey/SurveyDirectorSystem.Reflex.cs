// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks.Operators;
using Content.Shared.Chemistry.Components;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Trigger.Components;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.PlanetSurvey;

public sealed partial class SurveyDirectorSystem
{
    private const float DodgeRadius = 5.5f;
    private const float DodgeHop = 14f;
    private const float ToughFactor = 0.6f;
    private static readonly TimeSpan DodgeTime = TimeSpan.FromSeconds(3.5);
    private static readonly TimeSpan ReflexRate = TimeSpan.FromSeconds(0.25);
    private static readonly TimeSpan SelfHealTime = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ToughTime = TimeSpan.FromSeconds(40);
    private static readonly SoundSpecifier HypoSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");
    private static readonly SoundSpecifier PatchStart = new SoundPathSpecifier("/Audio/Items/Medical/brutepack_begin.ogg");
    private static readonly SoundSpecifier PatchEnd = new SoundPathSpecifier("/Audio/Items/Medical/brutepack_end.ogg");

    [Dependency] private SharedPhysicsSystem _physics = default!;

    private const float RunAndGunRange = 13f;
    private const float RunAndGunSpread = 0.5f;
    private const float RunAndGunDrift = 0.09f;
    private const float MovingSpeed = 0.4f;
    private const float StandSpread = 0.2f;
    private const float StandDrift = 0.04f;
    private const float DodgeSpeed = 4.5f;
    private const float RegrabRange = 2.5f;
    private const float DoorSense = 1.4f;

    [Dependency] private SharedDoorSystem _doors = default!;

    private readonly HashSet<Entity<SurveyNativeDoorComponent>> _nearDoors = new();
    private const float StuckDrift = 0.4f;
    private static readonly TimeSpan SidearmDelay = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan StuckTime = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan DodgeRushTime = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan RunAndGunRate = TimeSpan.FromSeconds(0.1);

    [Dependency] private LimbWoundSystem _limbs = default!;

    private readonly HashSet<Entity<SurveyMemberComponent>> _nearGrenade = new();
    private TimeSpan _nextReflex;
    private TimeSpan _nextRunAndGun;

    private void InitializeReflex()
    {
        SubscribeLocalEvent<SurveyMemberComponent, DamageModifyEvent>(OnMemberDamage);
        SubscribeLocalEvent<SurveyMemberComponent, ComponentStartup>(OnMemberStartup);
    }

    private void OnMemberStartup(Entity<SurveyMemberComponent> ent, ref ComponentStartup args)
    {
        RemCompDeferred<Content.Shared.Movement.Components.MobCollisionComponent>(ent);
    }

    private void OnMemberDamage(Entity<SurveyMemberComponent> ent, ref DamageModifyEvent args)
    {
        if (_timing.CurTime < ent.Comp.ToughUntil)
            args.Damage *= ToughFactor;
    }

    private bool Busy(SurveyMemberComponent member)
    {
        var now = _timing.CurTime;
        return member.Patient != null || member.SelfHealAt != null || now < member.DodgeUntil || now < member.CoverUntil;
    }

    private void UpdateReflex(TimeSpan now)
    {
        if (now < _nextReflex)
            return;

        _nextReflex = now + ReflexRate;
        OpenNativeDoors();
        var grenades = EntityQueryEnumerator<ActiveTimerTriggerComponent, TransformComponent>();
        while (grenades.MoveNext(out var grenade, out _, out var xform))
        {
            if (xform.MapUid is not { } map || !HasComp<SurveyDirectorComponent>(map))
                continue;

            var id = MetaData(grenade).EntityPrototype?.ID;
            if (id == null || id.Contains("Smoke"))
                continue;

            var blast = _xform.GetWorldPosition(xform);
            _nearGrenade.Clear();
            _lookup.GetEntitiesInRange(xform.Coordinates, DodgeRadius, _nearGrenade);
            var warned = false;

            foreach (var member in _nearGrenade)
            {
                if (member.Comp.Dodged == grenade || !Alive(member) || Board(member) is not { } board)
                    continue;

                member.Comp.Dodged = grenade;
                var pos = Pos(member);
                var away = pos - blast;
                if (away.LengthSquared() < 0.01f)
                    away = _random.NextVector2(1f);

                away = Vector2.Normalize(away);
                var flee = KitePoint(map, member, pos, blast) ?? Walkable(map, pos + away * 4f);

                if (TryComp<PhysicsComponent>(member, out var body))
                    _physics.ApplyLinearImpulse(member, away * body.Mass * DodgeHop, body: body);

                member.Comp.DodgeUntil = now + DodgeTime;
                member.Comp.DodgeTo = flee;
                member.Comp.DodgeRush = now + DodgeRushTime;
                board.SetValue(SurveyKeys.Errand, At(map, flee));
                board.SetValue(SurveyKeys.Role, SurveyRole.Errand);
                Replan(member, true);

                if (!warned && !member.Comp.Robot && member.Comp.Voice.Length > 0 &&
                    RadioLine($"survey-bark-{member.Comp.Voice}-incoming", Array.Empty<(string, object)>()) is { } shout)
                {
                    _chat.TrySendInGameICMessage(member, shout, InGameICChatType.Speak, false);
                    warned = true;
                }
            }
        }
    }

    private void UpdateRunAndGun(TimeSpan now)
    {
        if (now < _nextRunAndGun)
            return;

        _nextRunAndGun = now + RunAndGunRate;
        var query = EntityQueryEnumerator<SurveyMemberComponent, HTNComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var member, out var htn, out var body, out var xform))
        {
            if (member.ReplanPending && now >= member.NextReplan)
                Replan(uid);

            if (member.DodgeTo is { } dodge && now < member.DodgeRush)
            {
                var run = dodge - _xform.GetWorldPosition(xform);
                if (run.LengthSquared() > 0.25f && _mobState.IsAlive(uid))
                    _physics.SetLinearVelocity(uid, Vector2.Normalize(run) * DodgeSpeed, body: body);
                continue;
            }

            if (member.Medic || member.Scout || member.SelfHealAt != null || now < member.CoverUntil || xform.MapUid is not { } map ||
                !TryComp<SurveyDirectorComponent>(map, out var director))
            {
                continue;
            }

            if (htn.Plan is { } plan && plan.Index < plan.Tasks.Count &&
                plan.Tasks[plan.Index].Operator is Content.Server.NPC.HTN.PrimitiveTasks.Operators.Combat.Ranged.GunOperator)
            {
                continue;
            }

            if (!_mobState.IsAlive(uid))
                continue;

            var moving = body.LinearVelocity.LengthSquared() >= MovingSpeed * MovingSpeed;

            if (!htn.Blackboard.TryGetValue<EntityUid>(SurveyKeys.Target, out var target, EntityManager) || !Alive(target))
                continue;

            if (!_gun.TryGetGun(uid, out var gun) || now < gun.Comp.NextFire)
                continue;

            var pos = _xform.GetWorldPosition(xform);
            var targetPos = Pos(target);
            var distance = Vector2.Distance(pos, targetPos);
            if (distance > RunAndGunRange || !ClearShot(_xform.GetMapCoordinates(xform), _xform.GetMapCoordinates(target), uid, target) ||
                AllyInLine(director, uid, pos, targetPos))
            {
                continue;
            }

            var drift = moving ? RunAndGunSpread + distance * RunAndGunDrift : StandSpread + distance * StandDrift;
            var aimAt = targetPos + _random.NextVector2(drift);
            _gun.AttemptShoot(uid, gun, new EntityCoordinates(map, aimAt), target);
        }
    }

    private const float MovingSlip = 0.35f;

    private void AimLimb(EntityUid shooter, Entity<GunComponent> gun)
    {
        if (Board(shooter) is not { } board ||
            !board.TryGetValue<EntityUid>(SurveyKeys.Target, out var target, EntityManager) || !Exists(target) ||
            TryComp<SurveyMemberComponent>(shooter, out var member) && member.Blind)
        {
            return;
        }

        var zone = board.TryGetValue<LimbZone>(SurveyKeys.AimZone, out var picked, EntityManager) ? picked : LimbZone.Torso;
        if (TryComp<PhysicsComponent>(shooter, out var body) &&
            body.LinearVelocity.LengthSquared() >= MovingSpeed * MovingSpeed &&
            _random.Prob(MovingSlip))
        {
            var near = LimbAim.Near.Where(n => n.Called == zone).Select(n => n.Natural).ToList();
            if (near.Count > 0)
                zone = _random.Pick(near);
        }

        var aim = EnsureComp<LimbAimComponent>(shooter);
        aim.Assisted = true;
        aim.CalledZone = zone;
        if (!aim.Aiming)
            _limbs.SetAiming(shooter, true, aim);
    }

    private void OpenNativeDoors()
    {
        var members = EntityQueryEnumerator<SurveyMemberComponent, TransformComponent>();
        while (members.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid is not { } map || !HasComp<SurveyDirectorComponent>(map) || !_mobState.IsAlive(uid))
                continue;

            _nearDoors.Clear();
            _lookup.GetEntitiesInRange(xform.Coordinates, DoorSense, _nearDoors);
            foreach (var door in _nearDoors)
            {
                if (TryComp<DoorComponent>(door, out var doorComp) && doorComp.State == DoorState.Closed)
                    _doors.TryOpen(door, doorComp, uid, quiet: true);
            }
        }
    }

    private void TickRecovery(Entity<SurveyDirectorComponent> ent)
    {
        var now = _timing.CurTime;
        foreach (var squad in ent.Comp.Squads)
        {
            foreach (var member in squad.Members)
            {
                if (!Alive(member) || !TryComp<SurveyMemberComponent>(member, out var comp))
                    continue;

                if (!comp.Robot && HasComp<Content.Shared.Hands.Components.HandsComponent>(member))
                    Regrab(member, comp, now);

                if (squad.Engaged && squad.Target is { } enemy && Alive(enemy) && !Busy(comp))
                    Unstick(ent, member, comp, enemy, now);
            }
        }
    }

    private void Regrab(EntityUid member, SurveyMemberComponent comp, TimeSpan now)
    {
        foreach (var held in _hands.EnumerateHeld(member))
        {
            if (HasComp<SurveyNativeWeaponComponent>(held))
            {
                comp.DisarmedAt = null;
                return;
            }
        }

        comp.DisarmedAt ??= now;
        foreach (var gun in _lookup.GetEntitiesInRange<SurveyNativeWeaponComponent>(Transform(member).Coordinates, RegrabRange))
        {
            if (_containers.IsEntityInContainer(gun) || !_hands.TryPickupAnyHand(member, gun))
                continue;

            comp.DisarmedAt = null;
            Replan(member);
            return;
        }

        if (now - comp.DisarmedAt < SidearmDelay)
            return;

        comp.DisarmedAt = null;
        Rearm(member);
        Replan(member);
    }

    private void Unstick(Entity<SurveyDirectorComponent> ent, EntityUid member, SurveyMemberComponent comp, EntityUid enemy, TimeSpan now)
    {
        var pos = Pos(member);
        if (comp.StuckPos is not { } before || Vector2.Distance(before, pos) > StuckDrift)
        {
            comp.StuckPos = pos;
            comp.StuckSince = now;
            return;
        }

        if (now - comp.StuckSince < StuckTime || Board(member) is not { } board)
            return;

        if (TryComp<HTNComponent>(member, out var htn) && htn.Plan is { } plan && plan.Index < plan.Tasks.Count &&
            plan.Tasks[plan.Index].Operator is Content.Server.NPC.HTN.PrimitiveTasks.Operators.Combat.Ranged.GunOperator &&
            !ShotBlocked(member, enemy))
        {
            comp.StuckSince = now;
            return;
        }

        comp.StuckSince = now;
        var map = ent.Owner;
        var enemyPos = Pos(enemy);
        var side = _random.Prob(0.5f) ? 1f : -1f;
        var goal = ReachableFlank(map, member, pos, enemyPos, side) ??
                   (KitePoint(map, member, pos, enemyPos) is { } back ? At(map, back) : At(map, Walkable(map, pos + _random.NextVector2(2f, 3.5f))));

        comp.Approach = false;
        board.SetValue(SurveyKeys.Flank, goal);
        board.SetValue(SurveyKeys.Role, SurveyRole.Flank);
        board.SetValue(SurveyKeys.TargetCoords, At(map, enemyPos));
        Replan(member, true);
    }

    private void TickDodges(Entity<SurveyDirectorComponent> ent)
    {
        var now = _timing.CurTime;
        for (var i = 0; i < ent.Comp.Squads.Count; i++)
        {
            var squad = ent.Comp.Squads[i];
            foreach (var member in squad.Members)
            {
                if (!TryComp<SurveyMemberComponent>(member, out var comp) || comp.DodgeUntil == TimeSpan.Zero || now < comp.DodgeUntil)
                    continue;

                comp.DodgeUntil = TimeSpan.Zero;
                if (!Alive(member) || comp.Patient != null || comp.SelfHealAt != null || Board(member) is not { } board)
                    continue;

                board.SetValue(SurveyKeys.Role, SurveyRole.None);
                Replan(member);
                squad.NextOrders = TimeSpan.Zero;
                _reissue.Add(i);
            }
        }
    }

    private void TickSelfCare(Entity<SurveyDirectorComponent> ent)
    {
        var director = ent.Comp;
        if (director.Kind is not (SurveyKind.Raiders or SurveyKind.Pirates))
            return;

        var now = _timing.CurTime;
        for (var i = 0; i < director.Squads.Count; i++)
        {
            var squad = director.Squads[i];
            foreach (var member in squad.Members)
            {
                if (!Alive(member) || !TryComp<SurveyMemberComponent>(member, out var comp) || comp.Robot)
                    continue;

                if (comp.SelfHealAt is { } done)
                {
                    if (now < done)
                        continue;

                    comp.SelfHealAt = null;
                    Treat(member, 7f);
                    _audio.PlayPvs(PatchEnd, member);
                    if (Board(member) is { } healed)
                    {
                        healed.SetValue(SurveyKeys.Role, SurveyRole.None);
                        Replan(member);
                    }

                    _reissue.Add(i);
                    continue;
                }

                if (now < comp.NextSelfCare || Busy(comp))
                    continue;

                var hp = Health(member);
                var pos = Pos(member);
                var threat = NearestPlayer(pos, 14f, out var distance);
                var exposed = threat is { } enemy && _examine.InRangeUnOccluded(member, enemy, 14f);

                if (Boost(director, squad, member, comp, hp, exposed, distance))
                {
                    comp.NextSelfCare = now + TimeSpan.FromSeconds(6);
                    continue;
                }

                if (comp.Medkits <= 0 || hp >= 0.55f || exposed && hp >= 0.25f || Board(member) is not { } board)
                    continue;

                comp.Medkits--;
                comp.SelfHealAt = now + SelfHealTime;
                comp.NextSelfCare = now + TimeSpan.FromSeconds(12);
                board.SetValue(SurveyKeys.Errand, At(ent.Owner, pos));
                board.SetValue(SurveyKeys.Role, SurveyRole.Errand);
                _audio.PlayPvs(PatchStart, member);
                Bark(member, "selfheal");
            }
        }
    }

    private bool Boost(SurveyDirectorComponent director, SurveySquad squad, EntityUid member, SurveyMemberComponent comp, float hp, bool exposed, float distance)
    {
        if (comp.Stims.Count == 0 || !director.Alarm && director.Threat < 1.1f)
            return false;

        string? pick = null;
        if (hp < 0.6f && exposed && comp.Stims.Contains("tough"))
            pick = "tough";
        else if (hp < 0.35f && comp.Stims.Contains("adrenal"))
            pick = "adrenal";
        else if (comp.Stims.Contains("speed") && (squad.Withdrawn || squad.Wounded.Contains(member) || squad.Engaged && exposed && distance < 6f))
            pick = "speed";
        else if (comp.Stims.Contains("speed") && squad.Engaged && director.Threat >= 1.5f && _random.Prob(0.15f))
            pick = "speed";

        if (pick == null)
            return false;

        comp.Stims.Remove(pick);
        switch (pick)
        {
            case "tough":
                comp.ToughUntil = _timing.CurTime + ToughTime;
                break;
            case "adrenal":
                _blood.TryAddToBloodstream(member, new Solution("Epinephrine", 10));
                Treat(member, 4f);
                break;
            default:
                _blood.TryAddToBloodstream(member, new Solution("Stimulants", 10));
                break;
        }

        _audio.PlayPvs(HypoSound, member);
        Bark(member, "stim");
        return true;
    }
}
