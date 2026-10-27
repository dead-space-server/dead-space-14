// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Gatherable;
using Content.Shared.Buckle.Components;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Borgs;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.DeadSpace.Lavaland.AshWolf;
using Content.Shared.DeadSpace.Lavaland.Bosses;
using Content.Shared.DeadSpace.Weapons.Parry;
using Content.Shared.Ghost;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// AshWolf LQ-829 (<see cref="LavalandAshWolfComponent"/>), the secret boss of the strong storm. It lies near the
/// outpost until someone hits it, gets up in a scene of its own and fights with rushes, a shield, leaps, saws from the
/// sky, a retreat with saws and a pounce with a quick time event. Everything it does is meant to be parried. Beaten,
/// it falls and becomes a borg with ordinary laws, a brain slot and room for any four modules.
/// </summary>
public sealed partial class LavalandAshWolfSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly Content.Server.Ghost.Roles.ToggleableGhostRoleSystem _ghostRoles = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GatherableSystem _gatherable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly LavalandFaunaRaidSystem _raids = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ParrySystem _parry = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _camera = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly TimeSpan HudInterval = TimeSpan.FromSeconds(0.25);

    private const float ContactRange = 0.85f;
    private const float SawHitRange = 0.6f;

    private int _nextHudId = 300000;
    private int _nextQteId = 1;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandAshWolfComponent, MapInitEvent>(OnMapInit,
            after: [typeof(Content.Server.NameIdentifier.NameIdentifierSystem), typeof(Content.Server.RandomMetadata.RandomMetadataSystem)]);
        SubscribeLocalEvent<LavalandAshWolfComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<LavalandAshWolfComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<LavalandAshWolfComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<LavalandAshWolfComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeNetworkEvent<LavalandAshWolfQteResultEvent>(OnQteResult);

        InitializeTamed();
    }

    private void OnMapInit(Entity<LavalandAshWolfComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.HudId = _nextHudId++;
        ent.Comp.SelectedMusic = ent.Comp.Music.Count == 0 ? null : _random.Pick(ent.Comp.Music);
        SetVisual(ent, ent.Comp.Tamed ? LavalandAshWolfVisualState.Normal : LavalandAshWolfVisualState.Dormant);

        // The borg is no mount: it carries no one on its back.
        if (ent.Comp.Tamed)
        {
            RemComp<StrapComponent>(ent);
            RemComp<BorgRideComponent>(ent);

            // AshWolf LQ-829, not a borg's random name or serial number.
            RemComp<Content.Shared.NameIdentifier.NameIdentifierComponent>(ent);
            _metaData.SetEntityName(ent, Loc.GetString(ent.Comp.Name));
        }
        // The boss is not dragged around, asleep (to be woken in the brig) or awake.
        else
        {
            RemComp<PullableComponent>(ent);

            // The health bar over its head counts from its own health, not the thresholds it never reaches.
            var bar = EnsureComp<LavalandHealthBarComponent>(ent);
            bar.MaxHealth = ent.Comp.MaxHealth;
            Dirty(ent, bar);
        }
    }

    private void OnShutdown(Entity<LavalandAshWolfComponent> ent, ref ComponentShutdown args)
    {
        // Deleted mid-fight or mid-scene: nobody keeps its health bar, music, saws or a frozen body.
        StopQte(ent.Comp);
        ReleaseVictim(ent.Comp);
        EndFight(ent.Comp);
        ReleaseWatchers(ent.Comp);
        DeleteShadows(ent.Comp);
        DeleteSaws(ent.Owner);
    }

    #region Damage

    private void OnBeforeDamage(Entity<LavalandAshWolfComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (ent.Comp.Tamed || !args.Damage.AnyPositive())
            return;

        switch (ent.Comp.Stage)
        {
            case LavalandAshWolfStage.Dormant:
                args.Cancelled = true;
                // Only a player's blow wakes it: not heat or pressure, and not the fauna of a raid beating at
                // everything near the outpost.
                if (args.Origin is not { } origin || origin == ent.Owner || !HasComp<ActorComponent>(origin))
                    break;

                // While the fauna raids the outpost it lies behind a force field: no waking it to finish it off.
                if (Transform(ent).MapUid is { } map && _raids.IsRaidRunning(map))
                {
                    if (_timing.CurTime >= ent.Comp.NextShieldPopup)
                    {
                        ent.Comp.NextShieldPopup = _timing.CurTime + TimeSpan.FromSeconds(1);
                        _popup.PopupEntity(Loc.GetString("lavaland-ash-wolf-raid-shield"), ent, origin);
                    }
                    break;
                }

                Wake(ent);
                break;

            case LavalandAshWolfStage.Waking:
            case LavalandAshWolfStage.Intro:
            case LavalandAshWolfStage.Dying:
                args.Cancelled = true;
                break;
        }
    }

    private void OnDamageModify(EntityUid uid, LavalandAshWolfComponent comp, DamageModifyEvent args)
    {
        // Up in the sky only its own parried saws reach it.
        if (comp.Untouchable && !(args.Origin is { } origin && TryComp<LavalandAshWolfSawComponent>(origin, out var saw) && saw.Wolf == uid))
        {
            args.Damage *= 0f;
            return;
        }

        if (comp.Reduction > 0f)
            args.Damage *= Math.Clamp(1f - comp.Reduction, 0f, 1f);

        // The borg's attacks are made to dive into melee: it is well armoured while in one.
        if (comp.Tamed && comp.Attack != LavalandAshWolfAttack.None)
            args.Damage *= Math.Clamp(1f - comp.TamedAttackGuard, 0f, 1f);
    }

    private void OnDamageChanged(EntityUid uid, LavalandAshWolfComponent comp, DamageChangedEvent args)
    {
        if (comp.Tamed || comp.Stage != LavalandAshWolfStage.Fight || !args.DamageIncreased)
            return;

        var now = _timing.CurTime;
        var health = GetHealth(uid, comp);
        if (health <= 0f)
        {
            StartDying((uid, comp), now);
            return;
        }

        if (!comp.SecondPhaseReached && health <= comp.MaxHealth * comp.SecondPhase)
        {
            comp.SecondPhaseReached = true;
            _popup.PopupEntity(Loc.GetString("lavaland-ash-wolf-phase-popup"), uid, PopupType.LargeCaution);
        }

        // Left open by a parried rush of the second phase: a blow now knocks it away.
        if (comp.KnockOnHit && now < comp.StaggerUntil && args.Origin is { } attacker && attacker != uid)
        {
            comp.KnockOnHit = false;
            comp.GrindLeft = 0;
            KnockBack((uid, comp), Position(attacker), comp.KnockbackDistance, now);
            Recover((uid, comp), now, 0.7f);
        }
    }

    private float GetHealth(EntityUid uid, LavalandAshWolfComponent comp)
    {
        return TryComp<DamageableComponent>(uid, out var damageable)
            ? Math.Max(0f, comp.MaxHealth - damageable.TotalDamage.Float())
            : comp.MaxHealth;
    }

    #endregion

    private readonly List<Entity<LavalandAshWolfComponent>> _wolves = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        // A snapshot: a beaten wolf spawns its borg (one more wolf component) and deletes itself mid-loop.
        _wolves.Clear();
        var query = EntityQueryEnumerator<LavalandAshWolfComponent>();
        while (query.MoveNext(out var uid, out var comp))
            _wolves.Add((uid, comp));

        foreach (var ent in _wolves)
        {
            var (uid, comp) = ent;
            if (TerminatingOrDeleted(uid))
                continue;

            if (comp.Tamed)
            {
                UpdateTamed(ent, now);
                continue;
            }

            UpdateStars(ent, now);

            switch (comp.Stage)
            {
                case LavalandAshWolfStage.Waking when now >= comp.StageTime:
                    StartIntro(ent, now);
                    break;

                case LavalandAshWolfStage.Intro:
                    UpdateIntro(ent, now);
                    break;

                case LavalandAshWolfStage.Fight:
                    UpdateFight(ent, now, frameTime);
                    break;

                case LavalandAshWolfStage.Dying:
                    UpdateDying(ent, now);
                    break;
            }
        }

        UpdateShadows(now);
        UpdateSaws(now, frameTime);
    }

    #region Fight

    private void StartFight(Entity<LavalandAshWolfComponent> ent, TimeSpan now)
    {
        ent.Comp.Active = true;
        ent.Comp.NextAttack = now + TimeSpan.FromSeconds(1);
        ent.Comp.NextHud = TimeSpan.Zero;
        ent.Comp.LastSeenPlayer = now;
    }

    /// <summary>
    /// The fauna raids the outpost mid-fight: it jumps back, blows up a little and shuts itself down, lying as the storm
    /// left it, until somebody strikes it again (then it gets up at once, no scene). Once a raid: woken during it, it
    /// fights on. True while it is shutting down.
    /// </summary>
    private bool UpdateRaidShutdown(Entity<LavalandAshWolfComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;
        var raid = Transform(ent).MapUid is { } map && _raids.IsRaidRunning(map);
        if (!raid)
            comp.ShutDownForRaid = false;

        if (comp.ShutdownAt == TimeSpan.Zero)
        {
            if (!raid || comp.ShutDownForRaid || !comp.Active)
                return false;

            comp.ShutDownForRaid = true;
            EndAttack(ent, now);
            var players = GetPlayers(ent.Owner, ChaseRange(comp));
            var awayFrom = players.Count > 0 ? Position(Nearest(ent.Owner, players)) : Position(ent) - _transform.GetWorldRotation(ent).ToWorldVec();
            KnockBack(ent, awayFrom, comp.KnockbackDistance, now);
            comp.ShutdownAt = now + TimeSpan.FromSeconds(0.6);
            return true;
        }

        if (now < comp.ShutdownAt || comp.Sliding)
            return true;

        comp.ShutdownAt = TimeSpan.Zero;
        Blast(ent, Position(ent));
        EndFight(comp);
        DeleteShadows(comp);
        comp.Stage = LavalandAshWolfStage.Dormant;
        comp.Target = null;
        SetVisual(ent, LavalandAshWolfVisualState.Dormant);
        return true;
    }

    private void EndFight(LavalandAshWolfComponent comp)
    {
        comp.Active = false;
        foreach (var session in comp.HudRecipients)
        {
            if (session.Status is SessionStatus.Disconnected or SessionStatus.Zombie)
                continue;

            RaiseNetworkEvent(new LavalandBossHudHideEvent(comp.HudId), session.Channel);
            RaiseNetworkEvent(new LavalandBossMusicStopEvent(comp.HudId), session.Channel);
        }

        comp.HudRecipients.Clear();
    }

    private void UpdateFight(Entity<LavalandAshWolfComponent> ent, TimeSpan now, float frameTime)
    {
        var comp = ent.Comp;
        UpdateSlide(ent, now);
        UpdateGrind(ent, now);

        if (UpdateRaidShutdown(ent, now))
            return;

        var players = GetPlayers(ent.Owner, ChaseRange(comp));
        if (players.Count == 0)
        {
            // Everyone ran or died: it waits, and after a while the fight is over.
            if (comp.Active && now - comp.LastSeenPlayer > comp.LoseInterest)
            {
                EndAttack(ent, now);
                EndFight(comp);
            }

            return;
        }

        comp.LastSeenPlayer = now;
        if (!comp.Active)
            StartFight(ent, now);

        SendHud(ent, now);

        if (comp.Attack != LavalandAshWolfAttack.None)
        {
            UpdateAttack(ent, players, now);
            return;
        }

        if (now < comp.StaggerUntil || comp.Sliding)
            return;

        var target = Nearest(ent.Owner, players);
        comp.Target = target;
        WalkTowards(ent.Owner, Position(target), comp.WalkSpeed * Haste(comp) * frameTime, 2.5f);

        if (now >= comp.NextAttack)
            ChooseAttack(ent, now);
    }

    private void ChooseAttack(Entity<LavalandAshWolfComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;
        var options = new List<(LavalandAshWolfAttack Attack, float Weight)>
        {
            (LavalandAshWolfAttack.Rush, 3f),
            (LavalandAshWolfAttack.Shield, 2f),
            (LavalandAshWolfAttack.Leap, 3f),
            (LavalandAshWolfAttack.Retreat, 2f),
        };

        if (now >= comp.NextSky)
            options.Add((LavalandAshWolfAttack.Sky, 2f));
        if (now >= comp.NextPounce)
            options.Add((LavalandAshWolfAttack.Pounce, 2f));

        options.RemoveAll(o => o.Attack == comp.LastAttack);
        var total = options.Sum(o => o.Weight);
        var roll = _random.NextFloat(0f, total);
        var chosen = options[^1].Attack;
        foreach (var (attack, weight) in options)
        {
            roll -= weight;
            if (roll > 0f)
                continue;

            chosen = attack;
            break;
        }

        BeginAttack(ent, chosen, now);
    }

    private void BeginAttack(Entity<LavalandAshWolfComponent> ent, LavalandAshWolfAttack attack, TimeSpan now)
    {
        var comp = ent.Comp;
        comp.Attack = attack;
        comp.LastAttack = attack;
        comp.Step = 0;
        comp.StepTime = now;
        ReleaseVictim(comp);

        if (attack == LavalandAshWolfAttack.Pounce)
            comp.NextPounce = now + comp.PounceCooldown / (comp.Tamed ? 1f : Haste(comp));
        else if (attack == LavalandAshWolfAttack.Sky)
            comp.NextSky = now + comp.SkyCooldown / Haste(comp);

        if (comp.Tamed)
            EnsureComp<LavalandAshWolfDashingComponent>(ent);
    }

    /// <summary>
    /// Off its victim. While it sat on them they could not stand up (no room), and the knockdown then stops trying on
    /// its own: they stand up again once it is gone.
    /// </summary>
    private void ReleaseVictim(LavalandAshWolfComponent comp)
    {
        if (comp.Victim is { } victim && !TerminatingOrDeleted(victim))
            _stun.SetAutoStand(victim, true);

        comp.Victim = null;
    }

    /// <summary>The attack is over: a short breath, then the next.</summary>
    private void Recover(Entity<LavalandAshWolfComponent> ent, TimeSpan now, float seconds)
    {
        ent.Comp.Step = RecoverStep;
        ent.Comp.StepTime = now + TimeSpan.FromSeconds(seconds);
        ent.Comp.Reduction = 0f;
    }

    private void EndAttack(Entity<LavalandAshWolfComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;
        if (comp.Attack == LavalandAshWolfAttack.None)
            return;

        StopQte(comp);
        comp.Attack = LavalandAshWolfAttack.None;
        comp.Step = 0;
        comp.Reduction = 0f;
        comp.Untouchable = false;
        SetUpInSky(ent, false);
        ReleaseVictim(comp);
        comp.KnockOnHit = false;
        comp.GrindLeft = 0;
        comp.PendingSlash = TimeSpan.Zero;
        RemComp<LavalandSkyDropComponent>(ent);
        Land(ent);
        DeleteShadows(comp);
        SetVisual(ent, LavalandAshWolfVisualState.Normal);

        // Each pattern leaves it stunned for a moment, stars over its head.
        if (!comp.Tamed && comp.PatternStun > 0f)
        {
            var stun = now + TimeSpan.FromSeconds(comp.PatternStun / Haste(comp));
            if (stun > comp.StaggerUntil)
                comp.StaggerUntil = stun;
        }

        var pause = comp.Frenzied ? comp.FrenzyPause : comp.SecondPhaseReached ? 0.8f : 1.3f;
        var rested = comp.StaggerUntil > now ? comp.StaggerUntil : now;
        comp.NextAttack = rested + TimeSpan.FromSeconds(MathF.Max(0f, pause + _random.NextFloat(-0.3f, 0.3f)));

        if (comp.Tamed)
            RemComp<LavalandAshWolfDashingComponent>(ent);
    }

    private void SendHud(Entity<LavalandAshWolfComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;
        if (now < comp.NextHud)
            return;

        comp.NextHud = now + HudInterval;
        var health = GetHealth(ent, comp);
        var name = Loc.GetString(comp.Name);
        // The client fills in the keys as they are bound there.
        var hint = comp.Hint.Id;
        var sessions = GetNearbySessions(ent, ChaseRange(comp)).ToList();
        foreach (var session in sessions)
        {
            // Players who walk into the fight late get the music too.
            if (comp.HudRecipients.Add(session) && comp.SelectedMusic != null)
            {
                var resolved = _audio.ResolveSound(comp.SelectedMusic);
                RaiseNetworkEvent(new LavalandBossMusicStartEvent(comp.HudId, resolved, comp.SelectedMusic.Params.WithLoop(true)),
                    session.Channel);
            }

            RaiseNetworkEvent(new LavalandBossHudUpdateEvent(comp.HudId, name, health, comp.MaxHealth, sessions.Count) { Hint = hint },
                session.Channel);
        }
    }

    #endregion

    #region Helpers

    private const int RecoverStep = 99;

    /// <summary>Stunned (after a pattern, a parry, an escape from under it): stars over its head.</summary>
    private void UpdateStars(Entity<LavalandAshWolfComponent> ent, TimeSpan now)
    {
        var stars = ent.Comp.Stage == LavalandAshWolfStage.Fight && now < ent.Comp.StaggerUntil;
        if (stars == ent.Comp.SeeingStars)
            return;

        ent.Comp.SeeingStars = stars;
        _appearance.SetData(ent, SharedStunSystem.StunVisuals.SeeingStars, stars);
    }

    /// <summary>How much faster than usual it does everything: frenzied, a lot.</summary>
    private static float Haste(LavalandAshWolfComponent comp)
    {
        return comp.Frenzied && !comp.Tamed ? comp.FrenzyHaste : 1f;
    }

    private static float ChaseRange(LavalandAshWolfComponent comp)
    {
        return comp.Frenzied ? MathF.Max(comp.ChaseRange, comp.FrenzyChaseRange) : comp.ChaseRange;
    }

    /// <summary>
    /// The strong storm has died down: if it is fighting, it is frenzied for the rest of its life, as if at its last
    /// breath - second phase at once, everything far faster, hardly a pause. If nobody woke it, it lies on.
    /// </summary>
    public void Frenzy(Entity<LavalandAshWolfComponent> ent)
    {
        var comp = ent.Comp;
        if (comp.Tamed || comp.Frenzied || comp.Stage == LavalandAshWolfStage.Dying)
            return;

        // Asleep when the storm died down: it lies on, but whoever wakes it later meets it frenzied, not an easy prey.
        if (comp.Stage == LavalandAshWolfStage.Dormant)
        {
            comp.Frenzied = true;
            comp.SecondPhaseReached = true;
            return;
        }

        comp.Frenzied = true;
        comp.SecondPhaseReached = true;
        comp.NextAttack = TimeSpan.Zero;
        _jitter.AddJitter(ent, 8f, 14f);
        _popup.PopupEntity(Loc.GetString("lavaland-ash-wolf-frenzy-popup"), ent, PopupType.LargeCaution);
    }

    private void SetVisual(EntityUid uid, LavalandAshWolfVisualState state)
    {
        _appearance.SetData(uid, LavalandAshWolfVisuals.State, state);
    }

    private Vector2 Position(EntityUid uid) => _transform.GetWorldPosition(uid);

    private void Face(EntityUid uid, Vector2 direction)
    {
        if (direction.LengthSquared() > 0.0001f)
            _transform.SetWorldRotation(uid, direction.ToWorldAngle());
    }

    private void MoveTo(EntityUid uid, Vector2 position)
    {
        _transform.SetWorldPosition(uid, position);
        if (TryComp<PhysicsComponent>(uid, out var body))
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
    }

    private void WalkTowards(EntityUid uid, Vector2 goal, float step, float stopAt)
    {
        var position = Position(uid);
        var delta = goal - position;
        var distance = delta.Length();
        Face(uid, delta);
        if (distance <= stopAt)
            return;

        MoveTo(uid, position + delta / distance * MathF.Min(step, distance - stopAt));
    }

    private bool IsAliveTarget(EntityUid? uid)
    {
        return uid is { } target && !TerminatingOrDeleted(target) && IsStanding(target);
    }

    /// <summary>Still on their feet, if barely (pre-critical too): it finishes them off to critical, then leaves them.</summary>
    private bool IsStanding(EntityUid target, MobStateComponent? mob = null)
    {
        return _mobState.IsAlive(target, mob) || _mobState.IsPreCritical(target, mob);
    }

    /// <summary>Living players on its map within the range, ghosts aside.</summary>
    private List<EntityUid> GetPlayers(EntityUid uid, float range)
    {
        var result = new List<EntityUid>();
        var xform = Transform(uid);
        var origin = _transform.GetWorldPosition(xform);
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } player ||
                player == uid ||
                HasComp<GhostComponent>(player) ||
                !IsStanding(player) ||
                Transform(player).MapID != xform.MapID ||
                Vector2.DistanceSquared(origin, Position(player)) > range * range)
            {
                continue;
            }

            result.Add(player);
        }

        return result;
    }

    private IEnumerable<ICommonSession> GetNearbySessions(EntityUid uid, float range)
    {
        foreach (var player in GetPlayers(uid, range))
        {
            if (_players.TryGetSessionByEntity(player, out var session))
                yield return session;
        }
    }

    private EntityUid Nearest(EntityUid uid, List<EntityUid> candidates)
    {
        var origin = Position(uid);
        var best = candidates[0];
        var bestDistance = float.MaxValue;
        foreach (var candidate in candidates)
        {
            var distance = Vector2.DistanceSquared(origin, Position(candidate));
            if (distance >= bestDistance)
                continue;

            best = candidate;
            bestDistance = distance;
        }

        return best;
    }

    /// <summary>
    /// Who its blows and saws can hit around a point: the boss hurts players only; the tamed borg anyone alive but
    /// itself.
    /// </summary>
    private List<EntityUid> GetVictims(EntityUid wolf, LavalandAshWolfComponent comp, MapCoordinates at, float range)
    {
        var result = new List<EntityUid>();
        if (!comp.Tamed)
        {
            foreach (var player in GetPlayers(wolf, float.MaxValue))
            {
                if (Vector2.DistanceSquared(Position(player), at.Position) <= range * range)
                    result.Add(player);
            }

            return result;
        }

        _mobs.Clear();
        _lookup.GetEntitiesInRange(at, range, _mobs);
        foreach (var mob in _mobs)
        {
            if (mob.Owner != wolf && IsStanding(mob, mob.Comp) && !HasComp<GhostComponent>(mob))
                result.Add(mob.Owner);
        }

        return result;
    }

    private void PlaySound(SoundSpecifier? sound, EntityUid at, float volume = 0f)
    {
        if (sound != null)
            _audio.PlayPvs(sound, at, AudioParams.Default.WithVolume(volume));
    }

    #endregion
}
