// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Lavaland.AshWolf;
using Content.Shared.DeadSpace.Lavaland.Bosses;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>The six attacks of the Ash Wolf, each a little machine of steps.</summary>
public sealed partial class LavalandAshWolfSystem
{
    private void UpdateAttack(Entity<LavalandAshWolfComponent> ent, List<EntityUid> players, TimeSpan now)
    {
        var comp = ent.Comp;
        if (comp.Step == RecoverStep)
        {
            if (now >= comp.StepTime && !comp.Sliding)
                EndAttack(ent, now);
            return;
        }

        // Whoever it went for is gone: another one, if anyone is left.
        if (!comp.Tamed && !IsAliveTarget(comp.Target) && players.Count > 0)
            comp.Target = Nearest(ent, players);

        switch (comp.Attack)
        {
            case LavalandAshWolfAttack.Rush:
                UpdateRush(ent, now);
                break;
            case LavalandAshWolfAttack.Shield:
                UpdateShield(ent, players, now);
                break;
            case LavalandAshWolfAttack.Leap:
                UpdateLeap(ent, players, now);
                break;
            case LavalandAshWolfAttack.Sky:
                UpdateSky(ent, players, now);
                break;
            case LavalandAshWolfAttack.Retreat:
                UpdateRetreat(ent, players, now);
                break;
            case LavalandAshWolfAttack.Pounce:
                UpdatePounce(ent, now);
                break;
        }
    }

    /// <summary>Where the attack goes: its target for the boss, the aimed point for the tamed borg.</summary>
    private Vector2 AimAt(Entity<LavalandAshWolfComponent> ent)
    {
        if (ent.Comp.Tamed)
            return ent.Comp.AimPoint;

        return ent.Comp.Target is { } target && !TerminatingOrDeleted(target) ? Position(target) : Position(ent);
    }

    /// <summary>The tamed borg reaches no further than this with its dashes.</summary>
    private Vector2 Reach(Entity<LavalandAshWolfComponent> ent, Vector2 goal, float max)
    {
        if (!ent.Comp.Tamed)
            return goal;

        var from = Position(ent);
        var delta = goal - from;
        return delta.Length() > max ? from + delta.Normalized() * max : goal;
    }

    #region Slides and blows

    private void Slide(Entity<LavalandAshWolfComponent> ent, Vector2 to, float duration, LavalandAshWolfContact contact, TimeSpan now)
    {
        var comp = ent.Comp;
        comp.Sliding = true;
        comp.SlideFrom = Position(ent);
        comp.SlideTo = to;
        comp.SlideStart = now;
        comp.SlideDuration = MathF.Max(0.05f, duration);
        comp.SlideContact = contact;
        Face(ent, to - comp.SlideFrom);
    }

    private void UpdateSlide(Entity<LavalandAshWolfComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;
        if (!comp.Sliding)
            return;

        var t = Math.Clamp((float) (now - comp.SlideStart).TotalSeconds / comp.SlideDuration, 0f, 1f);
        var position = Vector2.Lerp(comp.SlideFrom, comp.SlideTo, t);
        MoveTo(ent, position);

        if (comp.SlideContact != LavalandAshWolfContact.None)
        {
            var coordinates = new MapCoordinates(position, Transform(ent).MapID);
            foreach (var victim in GetVictims(ent, comp, coordinates, ContactRange))
            {
                comp.Sliding = false;
                OnContact(ent, victim, now);
                return;
            }
        }

        if (t >= 1f)
            comp.Sliding = false;
    }

    /// <summary>A rush or a pounce lands on someone.</summary>
    private void OnContact(Entity<LavalandAshWolfComponent> ent, EntityUid victim, TimeSpan now)
    {
        var comp = ent.Comp;
        comp.Victim = victim;
        comp.ContactResolve = now + TimeSpan.FromSeconds(comp.ParryGrace);
        SitOn(ent, victim);

        if (comp.SlideContact == LavalandAshWolfContact.Pounce)
        {
            comp.Step = 3;
            _stun.TryKnockdown(victim, TimeSpan.FromSeconds(comp.QteDuration + 2.5f), refresh: true, autoStand: true, drop: false, force: true);
            StartQte(ent, victim, now);
            return;
        }

        comp.Step = 3;
    }

    private void SitOn(Entity<LavalandAshWolfComponent> ent, EntityUid victim)
    {
        var at = Position(victim);
        var from = Position(ent);
        var delta = from - at;
        MoveTo(ent, delta.LengthSquared() > 0.01f ? at + delta.Normalized() * 0.35f : at);
        Face(ent, at - from);
    }

    private void KnockBack(Entity<LavalandAshWolfComponent> ent, Vector2 awayFrom, float distance, TimeSpan now)
    {
        var position = Position(ent);
        var direction = position - awayFrom;
        if (direction.LengthSquared() < 0.01f)
            direction = _random.NextAngle().ToVec();

        Slide(ent, position + direction.Normalized() * distance, 0.25f + distance * 0.04f, LavalandAshWolfContact.None, now);
        // It flies back facing whoever threw it.
        Face(ent, -direction);
        SetVisual(ent, LavalandAshWolfVisualState.Normal);
    }

    /// <summary>It sits on its victim, the blades going round: a few cuts in a row.</summary>
    private void StartGrind(Entity<LavalandAshWolfComponent> ent, EntityUid victim, TimeSpan now)
    {
        var comp = ent.Comp;
        comp.Victim = victim;
        comp.GrindLeft = comp.GrindTicks;
        comp.NextGrind = now;
        SetVisual(ent, LavalandAshWolfVisualState.Roll);
        _stun.TryKnockdown(victim, TimeSpan.FromSeconds(comp.GrindTicks * comp.GrindInterval + 0.6f), refresh: true,
            autoStand: true, drop: false, force: true);
    }

    private void UpdateGrind(Entity<LavalandAshWolfComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;
        if (comp.GrindLeft <= 0 || now < comp.NextGrind)
            return;

        if (!IsAliveTarget(comp.Victim))
        {
            comp.GrindLeft = 0;
            return;
        }

        var victim = comp.Victim!.Value;
        SitOn(ent, victim);
        _damage.TryChangeDamage(victim, comp.GrindDamage, origin: ent);
        PlaySound(comp.SawSound, victim, -4f);
        comp.GrindLeft--;
        comp.NextGrind = now + TimeSpan.FromSeconds(comp.GrindInterval);
    }

    /// <summary>Done with someone: it jumps back off them.</summary>
    private void JumpBack(Entity<LavalandAshWolfComponent> ent, TimeSpan now)
    {
        var from = ent.Comp.Victim is { } victim && !TerminatingOrDeleted(victim) ? Position(victim) : AimAt(ent);
        KnockBack(ent, from, ent.Comp.KnockbackDistance, now);
        Recover(ent, now, 0.6f);
    }

    #endregion

    #region 1. Rush

    private void UpdateRush(Entity<LavalandAshWolfComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;
        switch (comp.Step)
        {
            // Curls up into a blade wheel.
            case 0:
                SetVisual(ent, LavalandAshWolfVisualState.Roll);
                PlaySound(comp.RushSound, ent);
                comp.StepTime = now + TimeSpan.FromSeconds(comp.Tamed ? 0.25f : comp.RushWindup / Haste(comp));
                comp.Step = 1;
                break;

            case 1 when now >= comp.StepTime:
            {
                var from = Position(ent);
                var goal = Reach(ent, AimAt(ent), 7f);
                var direction = goal - from;
                if (direction.LengthSquared() < 0.01f)
                    direction = _transform.GetWorldRotation(ent).ToWorldVec();

                var to = goal + direction.Normalized() * comp.RushOvershoot;
                SetVisual(ent, LavalandAshWolfVisualState.Roll);
                Slide(ent, to, Vector2.Distance(from, to) / (comp.RushSpeed * Haste(comp)), LavalandAshWolfContact.Rush, now);
                if (comp.SecondPhaseReached && !comp.Tamed)
                    SpawnShadows(ent, goal, now);
                comp.Step = 2;
                break;
            }

            // Rolled past everyone.
            case 2 when !comp.Sliding:
                SetVisual(ent, LavalandAshWolfVisualState.Normal);
                Recover(ent, now, 0.5f);
                break;

            // On someone: a parry in time, or the blades.
            case 3:
            {
                if (comp.Victim is not { } victim || !IsAliveTarget(victim))
                {
                    Recover(ent, now, 0.4f);
                    break;
                }

                if (_parry.TryConsumeParry(victim, ent))
                {
                    if (comp.SecondPhaseReached && !comp.Tamed)
                    {
                        // Parried, but it stays on them: one blow now throws it away, or the blades come anyway.
                        comp.KnockOnHit = true;
                        comp.StaggerUntil = now + TimeSpan.FromSeconds(1.2);
                        SetVisual(ent, LavalandAshWolfVisualState.Normal);
                        _popup.PopupEntity(Loc.GetString("lavaland-ash-wolf-open-popup"), ent, PopupType.Large);
                        comp.Step = 4;
                        break;
                    }

                    KnockBack(ent, Position(victim), comp.KnockbackDistance, now);
                    comp.StaggerUntil = now + TimeSpan.FromSeconds(0.9);
                    Recover(ent, now, 0.9f);
                    break;
                }

                if (now < comp.ContactResolve)
                {
                    SitOn(ent, victim);
                    break;
                }

                StartGrind(ent, victim, now);
                comp.Step = 5;
                break;
            }

            // Left open (second phase): no blow in time - the blades.
            case 4 when now >= comp.StaggerUntil:
                comp.KnockOnHit = false;
                if (comp.Victim is { } stillVictim && IsAliveTarget(stillVictim))
                {
                    StartGrind(ent, stillVictim, now);
                    comp.Step = 5;
                }
                else
                {
                    Recover(ent, now, 0.4f);
                }

                break;

            case 5 when comp.GrindLeft <= 0:
                JumpBack(ent, now);
                break;
        }
    }

    /// <summary>Three shadow copies rush at the same spot from other sides; only the real one has eyes.</summary>
    private void SpawnShadows(Entity<LavalandAshWolfComponent> ent, Vector2 goal, TimeSpan now)
    {
        var comp = ent.Comp;
        var mapId = Transform(ent).MapID;
        var from = Position(ent);
        var baseAngle = (from - goal).ToWorldAngle();
        for (var i = 1; i <= 3; i++)
        {
            var angle = baseAngle + Angle.FromDegrees(90 * i + _random.NextFloat(-20f, 20f));
            var start = goal + angle.ToWorldVec() * Vector2.Distance(from, goal);
            var to = goal + (goal - start).Normalized() * comp.RushOvershoot;
            var shadow = Spawn(comp.Shadow, new MapCoordinates(start, mapId));
            _transform.SetWorldRotation(shadow, (to - start).ToWorldAngle());
            var data = EnsureComp<LavalandAshWolfShadowComponent>(shadow);
            data.From = start;
            data.To = to;
            data.Start = now;
            data.Duration = MathF.Max(0.1f, Vector2.Distance(start, to) / (comp.RushSpeed * Haste(comp)));
            comp.Shadows.Add(shadow);
        }
    }

    private void UpdateShadows(TimeSpan now)
    {
        var query = EntityQueryEnumerator<LavalandAshWolfShadowComponent>();
        while (query.MoveNext(out var uid, out var shadow))
        {
            var t = (float) (now - shadow.Start).TotalSeconds / shadow.Duration;
            if (t >= 1f)
            {
                QueueDel(uid);
                continue;
            }

            _transform.SetWorldPosition(uid, Vector2.Lerp(shadow.From, shadow.To, Math.Clamp(t, 0f, 1f)));
        }
    }

    private void DeleteShadows(LavalandAshWolfComponent comp)
    {
        foreach (var shadow in comp.Shadows)
        {
            if (!TerminatingOrDeleted(shadow))
                QueueDel(shadow);
        }

        comp.Shadows.Clear();
    }

    #endregion

    #region 2. Shield

    private void UpdateShield(Entity<LavalandAshWolfComponent> ent, List<EntityUid> players, TimeSpan now)
    {
        var comp = ent.Comp;
        switch (comp.Step)
        {
            case 0:
                SetVisual(ent, LavalandAshWolfVisualState.Shield);
                comp.Reduction = comp.ShieldReduction;
                comp.AttackEnd = now + TimeSpan.FromSeconds(comp.ShieldDuration);
                comp.NextBlink = now + TimeSpan.FromSeconds(0.6);
                comp.PendingSlash = TimeSpan.Zero;
                comp.ShieldDashing = false;
                comp.Step = 1;
                break;

            case 1:
                // Mid-dash: it lands behind the back, and swings a moment later.
                if (comp.ShieldDashing)
                {
                    if (comp.Sliding)
                        break;

                    comp.ShieldDashing = false;
                    SetVisual(ent, LavalandAshWolfVisualState.Shield);
                    if (comp.Target is { } dashTarget && !TerminatingOrDeleted(dashTarget))
                        Face(ent, Position(dashTarget) - Position(ent));
                    DustPuff(ent, Position(ent));
                    comp.PendingSlash = now + TimeSpan.FromSeconds(0.3);
                    break;
                }

                if (now >= comp.AttackEnd)
                {
                    // The shield goes down in a great ring of dust; it is lost in it for a moment.
                    comp.Reduction = 0f;
                    DustRing(ent, Position(ent), 2.6f, 22, 8);
                    SetVisual(ent, LavalandAshWolfVisualState.Hidden);
                    comp.StepTime = now + TimeSpan.FromSeconds(1.1);
                    comp.Step = 2;
                    break;
                }

                if (comp.PendingSlash != TimeSpan.Zero && now >= comp.PendingSlash)
                {
                    comp.PendingSlash = TimeSpan.Zero;
                    ShieldSlash(ent, now);
                    break;
                }

                if (now >= comp.NextBlink && players.Count > 0)
                {
                    comp.Target = _random.Pick(players);
                    DashBehind(ent, comp.Target.Value, now);
                    comp.NextBlink = now + TimeSpan.FromSeconds(comp.ShieldBlinkInterval / Haste(comp));
                }

                break;

            // Its eyes light up in the dust...
            case 2 when now >= comp.StepTime:
                SetVisual(ent, LavalandAshWolfVisualState.EyesOnly);
                comp.StepTime = now + TimeSpan.FromSeconds(0.9);
                comp.Step = 3;
                break;

            // ...and it rushes out of it.
            case 3 when now >= comp.StepTime:
                comp.Attack = LavalandAshWolfAttack.Rush;
                comp.Step = 1;
                comp.StepTime = now;
                SetVisual(ent, LavalandAshWolfVisualState.Roll);
                PlaySound(comp.RushSound, ent);
                break;
        }
    }

    /// <summary>A sharp dash round to the back of the player, rolled up, with a puff of dust where it was.</summary>
    private void DashBehind(Entity<LavalandAshWolfComponent> ent, EntityUid target, TimeSpan now)
    {
        var comp = ent.Comp;
        var behind = Position(target) - _transform.GetWorldRotation(target).ToWorldVec() * 1.4f;
        var distance = Vector2.Distance(Position(ent), behind);
        DustPuff(ent, Position(ent));
        SetVisual(ent, LavalandAshWolfVisualState.Roll);
        PlaySound(comp.RushSound, ent);
        Slide(ent, behind, MathF.Max(0.1f, distance / comp.ShieldDashSpeed), LavalandAshWolfContact.None, now);
        comp.ShieldDashing = true;
    }

    private void ShieldSlash(Entity<LavalandAshWolfComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;
        if (comp.Target is not { } target || !IsAliveTarget(target) ||
            Vector2.Distance(Position(target), Position(ent)) > 1.8f)
        {
            return;
        }

        if (_parry.TryConsumeParry(target, ent))
        {
            KnockBack(ent, Position(target), 2f, now);
            return;
        }

        _damage.TryChangeDamage(target, comp.ShieldSlashDamage, origin: ent);
        PlaySound(comp.SawSound, target);
    }

    #endregion

    #region 3. Leap

    private void UpdateLeap(Entity<LavalandAshWolfComponent> ent, List<EntityUid> players, TimeSpan now)
    {
        var comp = ent.Comp;
        switch (comp.Step)
        {
            // A mark over the one it will jump at.
            case 0:
                if (!comp.Tamed && players.Count > 0)
                {
                    comp.Target = _random.Pick(players);
                    Spawn(comp.LeapMark, new EntityCoordinates(comp.Target.Value, Vector2.Zero));
                    comp.StepTime = now + TimeSpan.FromSeconds(comp.LeapWarning / Haste(comp));
                }
                else
                {
                    comp.StepTime = now;
                }

                comp.Step = 1;
                break;

            case 1 when now >= comp.StepTime:
                SkyMove(ent, now, comp.LeapRise, rising: true);
                comp.Untouchable = true;
                comp.StepTime = now + TimeSpan.FromSeconds(comp.LeapRise);
                comp.Step = 2;
                break;

            case 2 when now >= comp.StepTime:
                MoveTo(ent, Reach(ent, AimAt(ent), 8f));
                SkyMove(ent, now, comp.LeapFall, rising: false);
                comp.StepTime = now + TimeSpan.FromSeconds(comp.LeapFall);
                comp.Step = 3;
                break;

            case 3 when now >= comp.StepTime:
            {
                RemComp<LavalandSkyDropComponent>(ent);
                comp.Untouchable = false;
                var at = Position(ent);
                Land(ent, at, heavy: true);
                var angle = _random.NextFloat(0f, MathF.Tau);
                SpawnSpiralSaw(ent, at, angle, now);
                SpawnSpiralSaw(ent, at, angle + MathF.PI, now);
                Recover(ent, now, 0.7f);
                break;
            }
        }
    }

    private void SkyMove(EntityUid uid, TimeSpan now, float duration, bool rising)
    {
        var drop = EnsureComp<LavalandSkyDropComponent>(uid);
        drop.Start = now;
        drop.Duration = MathF.Max(0.05f, duration);
        drop.Height = 12f;
        drop.Rising = rising;
        drop.Sinking = false;
        drop.Slant = 0f;
        Dirty(uid, drop);
    }

    /// <summary>
    /// It comes down: the ground shakes the harder the nearer one is, takes the breath (stamina) of those close and
    /// hurts whoever it lands on.
    /// </summary>
    private void Land(Entity<LavalandAshWolfComponent> ent, Vector2 at, bool heavy)
    {
        var comp = ent.Comp;
        var mapId = Transform(ent).MapID;
        var coordinates = new MapCoordinates(at, mapId);
        Spawn(comp.Landing, coordinates);
        PlaySound(comp.LandSound, ent);
        DustRing(ent, at, heavy ? 1.8f : 1.1f, heavy ? 12 : 6, 2);

        var reach = MathF.Max(comp.LeapShakeRadius, comp.LeapStaminaRadius);
        foreach (var victim in GetVictims(ent, comp, coordinates, reach))
        {
            var distance = Vector2.Distance(Position(victim), at);
            if (distance <= comp.LeapShakeRadius)
            {
                var strength = (1f - distance / comp.LeapShakeRadius) * (heavy ? 1.4f : 0.6f);
                _camera.KickCamera(victim, _random.NextAngle().ToVec() * strength);
            }

            if (!heavy)
                continue;

            if (distance <= comp.LeapStaminaRadius)
                _stamina.TakeStaminaDamage(victim, comp.LeapStamina * (1f - distance / comp.LeapStaminaRadius), source: ent);

            if (distance <= 1.3f)
                _damage.TryChangeDamage(victim, comp.LeapDamage, origin: ent);
        }
    }

    #endregion

    #region 4. Sky

    private void UpdateSky(Entity<LavalandAshWolfComponent> ent, List<EntityUid> players, TimeSpan now)
    {
        var comp = ent.Comp;
        switch (comp.Step)
        {
            case 0:
                SkyMove(ent, now, 0.5f, rising: true);
                comp.Untouchable = true;
                comp.StepTime = now + TimeSpan.FromSeconds(0.5);
                comp.Step = 1;
                break;

            // Warning signs over everyone.
            case 1 when now >= comp.StepTime:
                SetVisual(ent, LavalandAshWolfVisualState.Hidden);
                foreach (var player in players)
                {
                    Spawn(comp.SkyMark, new EntityCoordinates(player, Vector2.Zero));
                }

                comp.StepTime = now + TimeSpan.FromSeconds(1);
                comp.Step = 2;
                break;

            // A saw falls by each of them and hunts them.
            case 2 when now >= comp.StepTime:
                comp.SkySaws.Clear();
                foreach (var player in players)
                {
                    var at = Position(player) + _random.NextAngle().ToVec() * 1.5f;
                    comp.SkySaws.Add(SpawnHomingSaw(ent, at, player, now));
                }

                comp.AttackEnd = now + TimeSpan.FromSeconds(0.5f + comp.SkyDuration);
                comp.Step = 3;
                break;

            // Down when every saw is parried or gone, or the time is up.
            case 3:
                comp.SkySaws.RemoveAll(saw => TerminatingOrDeleted(saw) ||
                                              TryComp<LavalandAshWolfSawComponent>(saw, out var data) &&
                                              data.Kind == LavalandAshWolfSawKind.Returning);
                if (comp.SkySaws.Count > 0 && now < comp.AttackEnd)
                    break;

                foreach (var saw in comp.SkySaws)
                {
                    QueueDel(saw);
                }

                comp.SkySaws.Clear();
                if (players.Count > 0)
                {
                    var nearest = Position(Nearest(ent, players));
                    var from = Position(ent);
                    var side = from - nearest;
                    MoveTo(ent, nearest + (side.LengthSquared() > 0.01f ? side.Normalized() : _random.NextAngle().ToVec()) * 3f);
                }

                SetVisual(ent, LavalandAshWolfVisualState.Normal);
                SkyMove(ent, now, 0.4f, rising: false);
                comp.StepTime = now + TimeSpan.FromSeconds(0.4);
                comp.Step = 4;
                break;

            case 4 when now >= comp.StepTime:
                RemComp<LavalandSkyDropComponent>(ent);
                comp.Untouchable = false;
                Land(ent, Position(ent), heavy: false);
                Recover(ent, now, 0.8f);
                break;
        }
    }

    #endregion

    #region 5. Retreat

    private void UpdateRetreat(Entity<LavalandAshWolfComponent> ent, List<EntityUid> players, TimeSpan now)
    {
        var comp = ent.Comp;
        switch (comp.Step)
        {
            case 0:
            {
                var from = Position(ent);
                var threat = comp.Tamed ? comp.AimPoint : players.Count > 0 ? Position(Nearest(ent, players)) : from;
                var away = from - threat;
                if (away.LengthSquared() < 0.01f)
                    away = -_transform.GetWorldRotation(ent).ToWorldVec();

                SetVisual(ent, LavalandAshWolfVisualState.Normal);
                Slide(ent, from + away.Normalized() * comp.RetreatDistance, 0.35f, LavalandAshWolfContact.None, now);
                Face(ent, -away);
                comp.StepTime = now + TimeSpan.FromSeconds(0.4);
                comp.Step = 1;
                break;
            }

            // Three saws straight at each of them, a tile of room between them.
            case 1 when now >= comp.StepTime:
            {
                var from = Position(ent);
                var aims = new List<Vector2>();
                if (comp.Tamed)
                    aims.Add(comp.AimPoint);
                else
                    aims.AddRange(players.ConvertAll(Position));

                foreach (var aim in aims)
                {
                    var direction = aim - from;
                    if (direction.LengthSquared() < 0.01f)
                        continue;

                    direction = direction.Normalized();
                    Face(ent, direction);
                    var across = new Vector2(-direction.Y, direction.X);
                    for (var k = -1; k <= 1; k++)
                    {
                        SpawnStraightSaw(ent, from + across * (2f * k), direction * comp.RetreatSawSpeed * Haste(comp), now);
                    }
                }

                PlaySound(comp.SawSound, ent);
                Recover(ent, now, 0.8f);
                break;
            }
        }
    }

    #endregion

    #region 6. Pounce

    private void UpdatePounce(Entity<LavalandAshWolfComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;
        switch (comp.Step)
        {
            // Crouches behind its shield.
            case 0:
                SetVisual(ent, LavalandAshWolfVisualState.Shield);
                comp.Reduction = comp.PounceShield;
                comp.StepTime = now + TimeSpan.FromSeconds(comp.Tamed ? 0.3f : 0.7f);
                comp.Step = 1;
                break;

            case 1 when now >= comp.StepTime:
            {
                var from = Position(ent);
                var goal = Reach(ent, AimAt(ent), 6f);
                var direction = goal - from;
                if (direction.LengthSquared() < 0.01f)
                    direction = _transform.GetWorldRotation(ent).ToWorldVec();

                var to = goal + direction.Normalized();
                SetVisual(ent, LavalandAshWolfVisualState.Roll);
                Slide(ent, to, Vector2.Distance(from, to) / (comp.PounceSpeed * Haste(comp)), LavalandAshWolfContact.Pounce, now);
                comp.Step = 2;
                break;
            }

            case 2 when !comp.Sliding:
                SetVisual(ent, LavalandAshWolfVisualState.Normal);
                Recover(ent, now, 0.6f);
                break;

            // Pinned under it: the quick time event decides.
            case 3:
            {
                if (comp.Victim is not { } victim || !IsAliveTarget(victim))
                {
                    StopQte(comp);
                    Recover(ent, now, 0.4f);
                    break;
                }

                SitOn(ent, victim);
                if (comp.QteResult == true)
                {
                    StopQte(comp);
                    comp.Reduction = 0f;
                    SetVisual(ent, LavalandAshWolfVisualState.Normal);
                    _popup.PopupEntity(Loc.GetString("lavaland-ash-wolf-qte-success-popup"), victim, PopupType.Large);
                    if (!comp.Tamed)
                        _damage.TryChangeDamage(ent.Owner, comp.QteSuccessDamage, ignoreResistances: true, origin: victim);
                    KnockBack(ent, Position(victim), comp.QteKnockback, now);
                    comp.StaggerUntil = now + TimeSpan.FromSeconds(0.4f + comp.QteStun);
                    Recover(ent, now, 0.4f + comp.QteStun);
                    break;
                }

                if (comp.QteResult == false || now >= comp.QteDeadline)
                {
                    StopQte(comp);
                    comp.Reduction = 0f;
                    StartGrind(ent, victim, now);
                    comp.Step = 4;
                }

                break;
            }

            case 4 when comp.GrindLeft <= 0:
                JumpBack(ent, now);
                break;
        }
    }

    #endregion

    #region Dust

    private static readonly Robust.Shared.Prototypes.EntProtoId DustPuffParticle = "LavalandIntroSmokeParticle";

    private void DustPuff(Entity<LavalandAshWolfComponent> ent, Vector2 at)
    {
        var mapId = Transform(ent).MapID;
        for (var i = 0; i < 4; i++)
        {
            var dust = Spawn(DustPuffParticle, new MapCoordinates(at, mapId));
            Drift(dust, _random.NextAngle().ToVec() * _random.NextFloat(0.3f, 0.9f), 1.2f);
        }
    }

    /// <summary>A ring of thick dust rolling out from a spot, with some in the middle.</summary>
    private void DustRing(Entity<LavalandAshWolfComponent> ent, Vector2 at, float radius, int ring, int middle)
    {
        var mapId = Transform(ent).MapID;
        for (var i = 0; i < ring; i++)
        {
            var angle = MathF.Tau * i / ring + _random.NextFloat(-0.1f, 0.1f);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var dust = Spawn(ent.Comp.Dust, new MapCoordinates(at + direction * radius * 0.5f, mapId));
            Drift(dust, direction * radius * 0.6f, 3.4f);
        }

        for (var i = 0; i < middle; i++)
        {
            var dust = Spawn(ent.Comp.Dust, new MapCoordinates(at + _random.NextVector2(0.5f), mapId));
            Drift(dust, _random.NextVector2(0.4f), 3.6f);
        }
    }

    private void Drift(EntityUid particle, Vector2 to, float duration)
    {
        var motion = EnsureComp<LavalandSpriteMotionComponent>(particle);
        motion.Start = _timing.CurTime;
        motion.Duration = duration;
        motion.From = Vector2.Zero;
        motion.To = to;
        motion.Spin = _random.NextFloat(-1f, 1f);
        motion.FadeOut = true;
        Dirty(particle, motion);
    }

    #endregion
}
