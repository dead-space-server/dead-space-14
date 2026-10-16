// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.DeadSpace.Lavaland.Bosses;
using Content.Shared.DeadSpace.Lavaland.SecretBoss;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Boss intros: a scripted scene played once when the fight would start (the Legion: when its gate is knocked on).
/// Everything seen in it but the players (particles, blasts, blood, a copy of the boss coming in) and its sounds
/// are spawned by the clients on the clock of the scene, so they are on time whatever the lag. The server only
/// freezes and moves the players, breaks the gate and brings out the real boss where its copy stands when the
/// scene ends.
/// </summary>
public sealed partial class LavalandBossArenaSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly LavalandNecropolisGateSystem _gates = default!;
    [Dependency] private readonly LavalandLegionMegafaunaSystem _legion = default!;

    private static readonly EntProtoId FireParticle = "LavalandIntroFireParticle";
    private static readonly EntProtoId SmokeParticle = "LavalandIntroSmokeParticle";
    private static readonly EntProtoId RockParticle = "LavalandIntroRockParticle";
    private static readonly EntProtoId BloodParticle = "LavalandIntroBloodParticle";
    private static readonly EntProtoId BloodTrail = "LavalandIntroBloodTrail";
    private static readonly EntProtoId Flash = "LavalandIntroFlash";
    private static readonly EntProtoId BubblegumShadow = "LavalandIntroBubblegumShadow";
    private static readonly EntProtoId GateDoorPiece = "LavalandIntroGateDoorPiece";
    private static readonly EntProtoId LegionFistLeft = "LavalandIntroLegionFistLeft";
    private static readonly EntProtoId LegionFistRight = "LavalandIntroLegionFistRight";
    private static readonly EntProtoId LegionHandLeft = "LavalandIntroLegionHandLeft";
    private static readonly EntProtoId LegionHandRight = "LavalandIntroLegionHandRight";

    /// <summary>Where the Legion's real hands float by its sides (as in its hands component).</summary>
    private static readonly Vector2 LegionHandRest = new(1.6f, 0.2f);

    /// <summary>How long the intro's hands outlive the scene, until the real ones are there.</summary>
    private const float LegionHandsOverlap = 1.6f;

    private const float GateWatchRange = 14f;

    /// <summary>The intro starts when a player comes this close to the middle of the arena.</summary>
    private const float IntroStartDistance = 15f;
    private const float LegionWakeRange = 40f;

    /// <summary>The copy of the boss stays this long after the scene, until the real one is there.</summary>
    private const float CopyOverlap = 0.6f;

    /// <summary>The scene plays this far from the players towards the middle of the arena (near the entrance).</summary>
    private const float StageDistance = 7f;

    /// <summary>A thrown doll flies this long into its corner.</summary>
    private const float ThrowFlight = 1.1f;

    /// <summary>The moment the Hierophant reaches the players in its intro.</summary>
    private const float BowlingHit = 5.9f;

    #region Start and end

    /// <summary>Starts the intro of an arena whose fight is starting. The players inside watch it.</summary>
    private void BeginIntro(Entity<LavalandBossArenaComponent> arena, LavalandBossIntroConfig intro)
    {
        var watchers = new List<EntityUid>();
        foreach (var participant in arena.Comp.Participants)
        {
            if (_players.TryGetSessionById(participant, out var session) &&
                session.AttachedEntity is { Valid: true } mob)
            {
                watchers.Add(mob);
            }
        }

        RunIntro(arena, intro, watchers, null, null);
    }

    /// <summary>
    /// The Legion gate of this arena was knocked on: the gate-break intro plays for the players around it.
    /// False if the arena has no such intro (the gate then opens as usual).
    /// </summary>
    public bool TryBeginGateIntro(EntityUid arenaUid, Entity<LavalandNecropolisGateComponent> gate, EntityUid? knocker)
    {
        if (!TryComp<LavalandBossArenaComponent>(arenaUid, out var arena) ||
            arena.Ended ||
            arena.IntroPlayed ||
            arena.Intro is not { Style: LavalandBossIntroStyle.GateBreak } intro)
        {
            return false;
        }

        var gateCoordinates = _transform.GetMapCoordinates(gate);
        var watchers = new List<EntityUid>();
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } mob || !_mobState.IsAlive(mob))
                continue;

            var coordinates = _transform.GetMapCoordinates(mob);
            if (coordinates.MapId == gateCoordinates.MapId &&
                Vector2.Distance(coordinates.Position, gateCoordinates.Position) <= GateWatchRange)
            {
                watchers.Add(mob);
            }
        }

        if (knocker is { } knockerUid && !watchers.Contains(knockerUid) && Exists(knockerUid))
            watchers.Add(knockerUid);

        RunIntro((arenaUid, arena), intro, watchers, gate, knocker);
        return true;
    }

    private void RunIntro(
        Entity<LavalandBossArenaComponent> arena,
        LavalandBossIntroConfig intro,
        List<EntityUid> watchers,
        Entity<LavalandNecropolisGateComponent>? gate,
        EntityUid? knocker)
    {
        if (!TryComp<MapGridComponent>(arena.Owner, out var grid))
            return;

        arena.Comp.IntroPlayed = true;
        arena.Comp.IntroKnocker = knocker;
        arena.Comp.IntroGate = gate?.Owner;

        var filter = Filter.Empty();
        var ev = new LavalandBossIntroEvent
        {
            Title = GetIntroTitle(arena.Comp),
            Subtitle = intro.Subtitle is { } subtitleId ? Loc.GetString(subtitleId) : null,
            TitleColor = intro.TitleColor,
            ReturnTime = intro.ReturnTime,
            StartZoom = intro.CameraZoom,
        };

        foreach (var watcher in watchers)
        {
            if (_players.TryGetSessionByEntity(watcher, out var session))
                filter.AddPlayer(session);

            ev.Watchers.Add(GetNetEntity(watcher));
            Freeze(arena.Comp, watcher);
        }

        var plan = new IntroPlan(arena, grid, intro, watchers, filter, gate, ev);
        switch (intro.Style)
        {
            case LavalandBossIntroStyle.BloodRitual:
                PlanBloodRitual(plan);
                break;
            case LavalandBossIntroStyle.Blast:
                PlanBlast(plan);
                break;
            case LavalandBossIntroStyle.Bowling:
                PlanBowling(plan);
                break;
            case LavalandBossIntroStyle.GateBreak:
                PlanGateBreak(plan);
                break;
            default:
                PlanFall(plan);
                break;
        }

        ev.Music = ResolveIntroMusic(arena, out var musicVolume);
        // A track mixed quieter (the Hierophant, Pandora) carries its own boost in the boss music: keep it here too.
        ev.MusicVolume += musicVolume;
        arena.Comp.IntroEndsAt = _timing.CurTime + TimeSpan.FromSeconds(Math.Max(0.5f, ev.Duration));
        RaiseNetworkEvent(ev, filter);
    }

    /// <summary>
    /// The fight theme of the boss, for the background of its scene. A boss that only comes out after the scene has
    /// none chosen yet: it is chosen here from its prototype and kept for the fight.
    /// </summary>
    private ResolvedSoundSpecifier? ResolveIntroMusic(Entity<LavalandBossArenaComponent> arena, out float volume)
    {
        volume = 0f;
        SoundSpecifier? music = null;
        if (TryComp<LavalandBossComponent>(arena.Comp.Boss, out var boss))
        {
            music = boss.SelectedMusic;
        }
        else if (arena.Comp.PendingBoss is { } pending &&
                 _prototype.TryIndex(pending, out var proto) &&
                 proto.TryGetComponent<LavalandBossComponent>(out var bossProto, EntityManager.ComponentFactory))
        {
            music = bossProto.MusicPool.Count > 0 ? _random.Pick(bossProto.MusicPool) : bossProto.Music;
            arena.Comp.IntroMusic = music;
        }

        if (music == null)
            return null;

        volume = music.Params.Volume;
        return _audio.ResolveSound(music);
    }

    /// <summary>The scene is over: the real boss stands where its copy is, everyone is released, the fight begins.</summary>
    private void FinishIntro(Entity<LavalandBossArenaComponent> arena)
    {
        var style = arena.Comp.Intro?.Style;
        var gate = arena.Comp.IntroGate;
        var knocker = arena.Comp.IntroKnocker;
        var bossSpot = arena.Comp.IntroBossSpot;

        // The bowled-over players really are where their dolls were knocked to.
        foreach (var (watcher, knockSpot) in arena.Comp.IntroKnockSpots)
            MoveTo(watcher, knockSpot);
        arena.Comp.IntroKnockSpots.Clear();

        ReleaseIntroWatchers(arena.Comp);

        if (TrySummonBoss(arena.Owner) && Exists(arena.Comp.Boss) && bossSpot is { } spot)
        {
            _transform.SetMapCoordinates(arena.Comp.Boss, spot);
            if (TryComp<PhysicsComponent>(arena.Comp.Boss, out var body))
                _physics.SetLinearVelocity(arena.Comp.Boss, Vector2.Zero, body: body);
        }

        if (style == LavalandBossIntroStyle.GateBreak)
        {
            // The Legion wakes up and lunges at whoever knocked.
            var wakeAt = gate is { } gateUid && Exists(gateUid)
                ? _transform.GetMapCoordinates(gateUid)
                : _transform.GetMapCoordinates(arena.Comp.Boss);
            _legion.WakeNear(wakeAt, LegionWakeRange, knocker);
            return;
        }

        StartFight(arena);
    }

    private void Freeze(LavalandBossArenaComponent arena, EntityUid uid)
    {
        if (HasComp<LavalandPitFrozenComponent>(uid))
            return;

        AddComp<LavalandPitFrozenComponent>(uid);
        arena.IntroWatchers.Add(uid);
    }

    private void ReleaseIntroWatchers(LavalandBossArenaComponent arena)
    {
        arena.IntroEndsAt = null;
        foreach (var watcher in arena.IntroWatchers)
        {
            if (!TerminatingOrDeleted(watcher))
                RemComp<LavalandPitFrozenComponent>(watcher);
        }

        arena.IntroWatchers.Clear();
    }

    /// <summary>Name of the boss for the intro: it is not spawned yet, so it comes from the prototype.</summary>
    /// <summary>
    /// The boss's own name for the title: the scene plays before the boss is spawned, so it is taken from the boss
    /// prototype first; the arena's name of the boss is still the placeholder then.
    /// </summary>
    private string GetIntroTitle(LavalandBossArenaComponent arena)
    {
        if (arena.PendingBoss is { } pending && _prototype.TryIndex<EntityPrototype>(pending, out var prototype))
        {
            return prototype.TryGetComponent<LavalandBossComponent>(out var boss, EntityManager.ComponentFactory) &&
                   IsRealName(boss.BossName)
                ? boss.BossName
                : prototype.Name;
        }

        if (IsRealName(arena.BossName))
            return arena.BossName;

        return arena.Boss is { } spawned && !TerminatingOrDeleted(spawned) ? Name(spawned) : string.Empty;
    }

    /// <summary>Not empty and not the "boss" placeholder of the components.</summary>
    private static bool IsRealName(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) && !name.Equals("boss", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region The plans

    private sealed class IntroPlan(
        Entity<LavalandBossArenaComponent> arena,
        MapGridComponent grid,
        LavalandBossIntroConfig config,
        List<EntityUid> watchers,
        Filter filter,
        Entity<LavalandNecropolisGateComponent>? gate,
        LavalandBossIntroEvent ev)
    {
        public readonly Entity<LavalandBossArenaComponent> Arena = arena;
        public readonly MapGridComponent Grid = grid;
        public readonly LavalandBossIntroConfig Config = config;
        public readonly List<EntityUid> Watchers = watchers;
        public readonly Filter Filter = filter;
        public readonly Entity<LavalandNecropolisGateComponent>? Gate = gate;
        public readonly LavalandBossIntroEvent Event = ev;
    }

    /// <summary>Ash drake: walk in, look around, a roar, it falls near them in a burst of fire, smoke and rocks.</summary>
    private void PlanFall(IntroPlan plan)
    {
        var c = plan.Config;
        var ev = plan.Event;
        var stage = Stage(plan);
        ev.Duration = c.Duration;
        ev.TitleDelay = c.TitleDelay;
        ev.TitleDuration = c.TitleDuration;
        ev.Shots.Add(new LavalandIntroShot(0f, true, c.CameraOffset, c.CameraZoom));
        ev.Shots.Add(new LavalandIntroShot(c.FallDelay - 0.6f, false, stage.Position, c.ArenaZoom));
        ev.Dolls.Add(new LavalandIntroDollCue(0f, LavalandIntroDollAction.WalkIn, new Vector2(-c.WalkInDistance, 0f), c.WalkInTime));
        ev.Dolls.Add(new LavalandIntroDollCue(c.WalkInTime, LavalandIntroDollAction.LookAround));
        ev.Dolls.Add(new LavalandIntroDollCue(c.RoarDelay, LavalandIntroDollAction.LookUp));

        var landAt = c.FallDelay + c.FallDuration;
        ev.Shakes.Add(new Vector2(landAt, 0.5f));
        Sound(plan, c.RoarSound, c.RoarDelay);
        Sound(plan, c.LandSound, landAt);

        BossCopy(plan, c.FallDelay, stage, new Vector2(0f, c.FallHeight), Vector2.Zero, c.FallDuration, accelerate: true);
        Burst(plan, landAt, stage, FireParticle, 22, 0.4f, 2.6f, 1.6f, 3.4f, 0.8f, 1.6f);
        Burst(plan, landAt, stage, SmokeParticle, 16, 0.6f, 3.2f, 2f, 4f, 1.6f, 2.8f);
        Debris(plan, landAt, stage, 14, 2.8f);
    }

    /// <summary>
    /// Bubblegum: blood runs from each corner towards the players (one corner a second); where it meets, earth and
    /// blood burst up, a hand digs out, a shadow flies up and it falls down.
    /// </summary>
    private void PlanBloodRitual(IntroPlan plan)
    {
        var c = plan.Config;
        var ev = plan.Event;
        var stage = Stage(plan);
        var corners = Corners(plan, 2.5f);
        // Every gush of the running blood splashes, quieter than the burst where it all meets.
        var splash = new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Bubblegum/splat.ogg",
            AudioParams.Default.WithVolume(-5f).WithVariation(0.15f));
        ev.Duration = 14f;
        ev.TitleDelay = 10.6f;
        ev.TitleDuration = 2.8f;

        for (var i = 0; i < corners.Count; i++)
        {
            var corner = corners[i];
            ev.Shots.Add(new LavalandIntroShot(i, false, Vector2.Lerp(corner.Position, stage.Position, 0.25f), 0.75f));

            // The trail runs most of the way while its corner is on screen, the rest when all meet.
            for (var step = 0; step < 14; step++)
            {
                var along = step / 14f * 0.7f;
                var time = i + step * 0.07f;
                Trail(plan, time, corner, stage, along);
                if (step % 2 == 0)
                    Burst(plan, time, Along(corner, stage, along), BloodParticle, 1, 0f, 0.3f, 0.3f, 0.8f, 0.4f, 0.7f);
                if (step % 4 == 0)
                    Sound(plan, splash, time);
            }

            for (var step = 0; step < 8; step++)
            {
                Trail(plan, 4f + step * 0.15f, corner, stage, 0.7f + step / 8f * 0.3f);
                // The four trails run in together: one splash per moment for all of them.
                if (i == 0 && step % 3 == 0)
                    Sound(plan, splash, 4f + step * 0.15f);
            }
        }

        ev.Shots.Add(new LavalandIntroShot(4f, false, stage.Position, c.ArenaZoom));
        ev.Dolls.Add(new LavalandIntroDollCue(0f, LavalandIntroDollAction.LookAround));
        ev.Dolls.Add(new LavalandIntroDollCue(8.1f, LavalandIntroDollAction.LookUp));
        ev.Shakes.Add(new Vector2(5.3f, 0.25f));
        ev.Shakes.Add(new Vector2(6.8f, 0.35f));
        ev.Shakes.Add(new Vector2(9.9f, 0.7f));

        // Where the blood meets, earth and blood burst up.
        Debris(plan, 5.3f, stage, 14, 2.2f);
        Burst(plan, 5.3f, stage, BloodParticle, 18, 0.3f, 2.6f, 0.8f, 2.2f, 0.6f, 1.2f);
        Sound(plan, new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Bubblegum/splat.ogg"), 5.3f);

        // A hand digs out of the ground, rocks flying.
        Fx(plan, new LavalandIntroFx(6.8f, "LavalandBubblegumRightPaw", stage.Position));
        Debris(plan, 6.8f, stage, 16, 2.2f);
        Burst(plan, 6.8f, stage, BloodParticle, 8, 0.2f, 1.2f, 0.6f, 1.4f, 0.5f, 0.9f);
        Sound(plan, new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Bubblegum/enter_blood.ogg"), 6.8f);

        // A dark shadow flies up, then Bubblegum comes down.
        Fx(plan, new LavalandIntroFx(8.1f, BubblegumShadow, stage.Position, Vector2.Zero, new Vector2(0f, 14f), 0.8f, fadeOut: true));
        Sound(plan, new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Bubblegum/exit_blood.ogg"), 8.1f);
        BossCopy(plan, 9.2f, stage, new Vector2(0f, 14f), Vector2.Zero, 0.7f, accelerate: true);
        Burst(plan, 9.9f, stage, BloodParticle, 18, 0.4f, 3f, 1f, 2.4f, 0.7f, 1.2f);
        Debris(plan, 9.9f, stage, 12, 2.6f);
        Sound(plan, c.LandSound, 9.9f);
    }

    /// <summary>
    /// Colossus: a blast behind each player throws it forward into a corner of the arena, the camera following;
    /// it tries to get up, then a great blast near the entrance and the enraged colossus shows through the smoke.
    /// </summary>
    private void PlanBlast(IntroPlan plan)
    {
        var c = plan.Config;
        var ev = plan.Event;
        var stage = Stage(plan);
        ev.Duration = 11.5f;
        ev.TitleDelay = 7.6f;
        ev.TitleDuration = 2.8f;
        ev.Shots.Add(new LavalandIntroShot(0f, true, Vector2.Zero, 0.6f));
        ev.Shots.Add(new LavalandIntroShot(4.6f, false, stage.Position, 0.95f));
        ev.Dolls.Add(new LavalandIntroDollCue(0f, LavalandIntroDollAction.LookAround));
        ev.Dolls.Add(new LavalandIntroDollCue(1f + ThrowFlight, LavalandIntroDollAction.Lie));
        ev.Dolls.Add(new LavalandIntroDollCue(2.4f, LavalandIntroDollAction.StandUp, length: 2f));
        ev.Dolls.Add(new LavalandIntroDollCue(4.6f, LavalandIntroDollAction.None));
        ev.Shakes.Add(new Vector2(1f, 0.6f));
        ev.Shakes.Add(new Vector2(1f + ThrowFlight, 0.3f));
        ev.Shakes.Add(new Vector2(5.4f, 1.1f));

        // A blast behind each player throws it into a corner.
        foreach (var watcher in plan.Watchers)
        {
            var behind = _transform.GetMapCoordinates(watcher).Offset(new Vector2(0f, -0.8f));
            Blast(plan, 1f, behind, 1.2f, 10, 6);
        }

        Sound(plan, c.ImpactSound, 1f);
        Later(plan, 1f, () => ThrowToCorners(plan));

        // The great blast; the colossus shows through the smoke, enraged.
        Blast(plan, 5.4f, stage, 3f, 30, 22);
        for (var k = 0; k < 5; k++)
        {
            var angle = MathF.Tau * k / 5f + 0.3f;
            Blast(plan, 5.4f + 0.12f * k, stage.Offset(new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 1.8f), 1.4f, 8, 6);
        }

        Burst(plan, 5.6f, stage, SmokeParticle, 26, 0.2f, 3f, 0.4f, 1.8f, 2.4f, 3.6f);
        Debris(plan, 5.4f, stage, 12, 3f);
        BossCopy(plan, 5.6f, stage, Vector2.Zero, Vector2.Zero, 2.4f, fadeIn: true, state: "colossus_angry");
        Sound(plan, c.LandSound, 5.4f);
    }

    /// <summary>
    /// Hierophant: everyone gathers in the middle and looks around, its warning flares under them,
    /// then it slides in from the side like a bowling ball and knocks them into the corners.
    /// </summary>
    private void PlanBowling(IntroPlan plan)
    {
        var c = plan.Config;
        var ev = plan.Event;
        var landing = Landing(plan);
        ev.Duration = 12f;
        ev.TitleDelay = 8.4f;
        ev.TitleDuration = 2.8f;
        ev.Shots.Add(new LavalandIntroShot(0f, true, Vector2.Zero, 0.6f));
        ev.Shots.Add(new LavalandIntroShot(2.6f, false, landing.Position, 0.65f));
        ev.Shots.Add(new LavalandIntroShot(6f, false, landing.Position, 0.72f));
        ev.Dolls.Add(new LavalandIntroDollCue(0f, LavalandIntroDollAction.WalkIn, new Vector2(-2.5f, 0f), 1.2f));
        ev.Dolls.Add(new LavalandIntroDollCue(1.2f, LavalandIntroDollAction.LookAround));
        // The very moment it reaches them they scatter like pins - on the client's clock, not a server timer.
        ev.Dolls.Add(new LavalandIntroDollCue(BowlingHit, LavalandIntroDollAction.Knocked, length: ThrowFlight));
        ev.Dolls.Add(new LavalandIntroDollCue(BowlingHit + ThrowFlight, LavalandIntroDollAction.Lie));
        ev.Dolls.Add(new LavalandIntroDollCue(7.4f, LavalandIntroDollAction.StandUp, length: 1f));
        ev.Dolls.Add(new LavalandIntroDollCue(8.4f, LavalandIntroDollAction.None));
        ev.Shakes.Add(new Vector2(3.75f, 0.3f));
        ev.Shakes.Add(new Vector2(BowlingHit, 0.8f));

        // Everyone comes to the middle; its warning flares under them where they stand.
        var spots = new List<MapCoordinates>();
        for (var i = 0; i < plan.Watchers.Count; i++)
        {
            var angle = MathF.Tau * i / Math.Max(1, plan.Watchers.Count);
            spots.Add(plan.Watchers.Count == 1
                ? landing.Offset(new Vector2(0f, -1.5f))
                : landing.Offset(new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 1.6f));
        }

        Later(plan, 0f, () =>
        {
            for (var i = 0; i < plan.Watchers.Count; i++)
                MoveTo(plan.Watchers[i], spots[i]);
        });

        foreach (var spot in spots)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                    Fx(plan, new LavalandIntroFx(3f, "LavalandHierophantTelegraph", spot.Position + new Vector2(dx, dy)));
            }

            Fx(plan, new LavalandIntroFx(3.75f, "LavalandHierophantSquares", spot.Position));
            Fx(plan, new LavalandIntroFx(5.9f, "LavalandHierophantBlast", spot.Position));
        }

        Sound(plan, c.RoarSound, 3f);

        // It slides in from the side, upright, grinding over the floor, and bowls them over.
        const float slideTime = 0.9f;
        BossCopy(plan, BowlingHit - slideTime, landing, new Vector2(-12f, 0f), Vector2.Zero, slideTime);
        Sound(plan, new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Hierophant/mechmove04.ogg",
            AudioParams.Default.WithVolume(4f)), BowlingHit - slideTime);
        Sound(plan, c.ImpactSound, BowlingHit);
        Sound(plan, new SoundPathSpecifier("/Audio/Effects/hit_kick.ogg", AudioParams.Default.WithVolume(3f)), BowlingHit);
        Sound(plan, new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg"), BowlingHit + 0.05f);

        // Each one flies into a corner of the arena. The real body is put there the moment it lands, not at the end
        // of the scene: no standing before the Hierophant and then popping into the corner.
        var corners = Corners(plan, 2.5f);
        _random.Shuffle(corners);
        plan.Arena.Comp.IntroKnockSpots.Clear();
        for (var i = 0; i < plan.Watchers.Count; i++)
        {
            var corner = corners[i % corners.Count];
            ev.Knocks.Add(new LavalandIntroKnock(GetNetEntity(plan.Watchers[i]), corner.Position));
            plan.Arena.Comp.IntroKnockSpots.Add((plan.Watchers[i], corner));
        }

        Later(plan, BowlingHit + ThrowFlight, () =>
        {
            foreach (var (watcher, knockSpot) in plan.Arena.Comp.IntroKnockSpots)
                MoveTo(watcher, knockSpot);
        });
    }

    /// <summary>
    /// Legion: the players back off from the gate as something hammers it, the screen shaking harder with every
    /// blow; the fifth blow throws the doors apart and the Legion spins in the doorway before it charges.
    /// </summary>
    private void PlanGateBreak(IntroPlan plan)
    {
        var c = plan.Config;
        var ev = plan.Event;
        if (plan.Gate is not { } gate)
            return;

        var gateSpot = _transform.GetMapCoordinates(gate);
        var inside = Landing(plan).Position - gateSpot.Position;
        var inward = inside.LengthSquared() > 0.01f ? Vector2.Normalize(inside) : new Vector2(0f, 1f);
        const float backOff = 2.5f;
        const float backOffTime = 3.5f;

        ev.Duration = 10f;
        ev.TitleDelay = 5.6f;
        ev.TitleDuration = 2.4f;
        ev.Shots.Add(new LavalandIntroShot(0f, false, gateSpot.Position - inward * 3f, 0.8f));
        ev.Shots.Add(new LavalandIntroShot(5f, false, gateSpot.Position, 0.9f));

        // The dolls back off; at the end the players really stand there and stay.
        ev.Dolls.Add(new LavalandIntroDollCue(0.3f, LavalandIntroDollAction.StepBack, -inward * backOff, backOffTime));
        ev.Dolls.Add(new LavalandIntroDollCue(0.3f + backOffTime, LavalandIntroDollAction.None));
        Later(plan, 0.3f + backOffTime, () =>
        {
            foreach (var watcher in plan.Watchers)
            {
                if (!TerminatingOrDeleted(watcher))
                    MoveTo(watcher, FreeSpotAlong(_transform.GetMapCoordinates(watcher), -inward, backOff));
            }
        });

        // Three knocks on the doors.
        for (var hit = 1; hit <= 3; hit++)
        {
            var at = (float) hit;
            ev.Shakes.Add(new Vector2(at, 0.25f + hit * 0.15f));
            Sound(plan, c.ImpactSound, at);
            Sound(plan, c.LandSound, at + 0.15f);
        }

        var side = new Vector2(inward.Y, -inward.X);
        PlanFistThroughWall(plan, gateSpot, inward, side);

        // The fifth blow: the doors fly apart, the Legion bursts out with its right fist raised.
        ev.Shakes.Add(new Vector2(5f, 1.4f));
        Sound(plan, c.ImpactSound, 5f);
        Sound(plan, c.LandSound, 5.15f);
        Fx(plan, new LavalandIntroFx(5f, GateDoorPiece, gateSpot.Position, Vector2.Zero, side * 14f + new Vector2(0f, 2f), 1.2f, spin: 7f, fadeOut: true));
        Fx(plan, new LavalandIntroFx(5f, GateDoorPiece, gateSpot.Position, Vector2.Zero, -side * 14f + new Vector2(0f, 2f), 1.2f, spin: -7f, fadeOut: true));
        Debris(plan, 5f, gateSpot, 14, 3f);
        Burst(plan, 5f, gateSpot, SmokeParticle, 12, 0.5f, 3f, 1.5f, 3f, 1.4f, 2.4f);
        Later(plan, 5f, () =>
        {
            if (!TerminatingOrDeleted(gate))
                _gates.Smash(gate);
        });

        // Out of the depths into the broken doorway.
        var bossSpot = gateSpot.Offset(inward * 1.5f);
        BossCopy(plan, 5f, bossSpot, inward * 1.2f, Vector2.Zero, 0.35f);
        // It bursts out punching: the right fist thrown up over its head, the left one still clenched from the doors.
        const float punchFor = 0.8f;
        var rightRaised = bossSpot.Position + new Vector2(1.4f, 1.1f);
        var leftClenched = bossSpot.Position + new Vector2(-1.5f, 0.1f);
        Fx(plan, new LavalandIntroFx(5f, LegionFistRight, rightRaised, new Vector2(-0.4f, -1.2f), Vector2.Zero, 0.3f)
            { Lifetime = punchFor });
        Fx(plan, new LavalandIntroFx(5f, LegionFistLeft, leftClenched, new Vector2(0.3f, 0.2f), Vector2.Zero, 0.3f)
            { Lifetime = punchFor });

        // Then the fists open into plain hands and drift to where the real ones float, and stay there until the
        // real Legion and its hands take over at the very same spots.
        var openAt = 5f + punchFor;
        // Held a little past the end of the scene: the real hands come with the real Legion a moment later.
        var holdFor = MathF.Max(0.1f, ev.Duration - openAt + LegionHandsOverlap);
        var rightRest = bossSpot.Position + LegionHandRest;
        var leftRest = bossSpot.Position + new Vector2(-LegionHandRest.X, LegionHandRest.Y);
        Fx(plan, new LavalandIntroFx(openAt, LegionHandRight, rightRest, rightRaised - rightRest, Vector2.Zero, 0.4f)
            { Lifetime = holdFor });
        Fx(plan, new LavalandIntroFx(openAt, LegionHandLeft, leftRest, leftClenched - leftRest, Vector2.Zero, 0.4f)
            { Lifetime = holdFor });
        Sound(plan, c.RoarSound, 5f);
    }

    /// <summary>
    /// After the third knock the left fist punches out through the wall beside the doors, grabs a door from outside
    /// and tugs at it, trying to wrench the necropolis open.
    /// </summary>
    private void PlanFistThroughWall(IntroPlan plan, MapCoordinates gateSpot, Vector2 inward, Vector2 side)
    {
        var c = plan.Config;
        var ev = plan.Event;
        var wall = gateSpot.Position + side * 1.3f;
        var outside = -inward * 0.7f;

        // The punch: out of the wall in a blink, chips and dust flying.
        const float punch = 3.45f;
        Segment(plan, punch, LegionFistLeft, wall, inward * 0.6f, outside, 0.15f, 0.4f, accelerate: true, spin: 9f);
        ev.Shakes.Add(new Vector2(punch + 0.15f, 0.9f));
        Sound(plan, c.ImpactSound, punch + 0.15f);
        var hole = new MapCoordinates(wall + outside, gateSpot.MapId);
        Debris(plan, punch + 0.15f, hole, 10, 1.8f);
        Burst(plan, punch + 0.15f, hole, SmokeParticle, 6, 0.3f, 1.4f, 0.8f, 1.6f, 0.9f, 1.4f);

        // It gropes along the wall to the door's edge...
        var edge = gateSpot.Position + side * 0.55f + outside;
        Segment(plan, punch + 0.4f, LegionFistLeft, Vector2.Zero, wall + outside, edge, 0.35f, 0.35f);

        // ...and wrenches at it, back and forth, the doors shuddering.
        var pulled = edge - inward * 0.35f + side * 0.15f;
        var time = punch + 0.75f;
        for (var tug = 0; tug < 4; tug++)
        {
            var (from, to) = tug % 2 == 0 ? (edge, pulled) : (pulled, edge);
            var last = tug == 3;
            // Twisting the door back and forth as it wrenches at it.
            Segment(plan, time, LegionFistLeft, Vector2.Zero, from, to, 0.18f, last ? 5f - time : 0.2f,
                spin: tug % 2 == 0 ? 3.5f : -3.5f);
            if (tug % 2 == 0)
            {
                ev.Shakes.Add(new Vector2(time + 0.18f, 0.35f));
                Sound(plan, c.LandSound, time + 0.18f);
            }

            time += 0.2f;
        }
    }

    /// <summary>One stretch of a moving prop: it appears at the start and is gone when the next one takes over.</summary>
    private static void Segment(IntroPlan plan, float time, string prototype, Vector2 origin, Vector2 from, Vector2 to,
        float duration, float lifetime, bool accelerate = false, float spin = 0f)
    {
        // The entity stands at the end point; the sprite moves there from the start.
        Fx(plan, new LavalandIntroFx(time, prototype, origin + to, from - to, Vector2.Zero, duration, spin: spin)
        {
            Accelerate = accelerate,
            Lifetime = MathF.Max(0.05f, lifetime),
        });
    }

    #endregion

    #region Helpers

    private MapCoordinates Landing(IntroPlan plan)
    {
        return _transform.ToMapCoordinates(_map.GridTileToLocal(plan.Arena.Owner, plan.Grid, plan.Arena.Comp.BossSpawnTile));
    }

    /// <summary>Where the boss comes in: near the players, a few tiles from them towards the middle.</summary>
    private MapCoordinates Stage(IntroPlan plan)
    {
        var landing = Landing(plan);
        var sum = Vector2.Zero;
        var count = 0;
        foreach (var watcher in plan.Watchers)
        {
            if (TerminatingOrDeleted(watcher))
                continue;

            var at = _transform.GetMapCoordinates(watcher);
            if (at.MapId != landing.MapId)
                continue;

            sum += at.Position;
            count++;
        }

        if (count == 0)
            return landing;

        var players = sum / count;
        var toMiddle = landing.Position - players;
        var length = toMiddle.Length();
        return length <= StageDistance
            ? landing
            : new MapCoordinates(players + toMiddle / length * StageDistance, landing.MapId);
    }

    /// <summary>The four corners of the fight area, <paramref name="inset"/> tiles inside.</summary>
    private List<MapCoordinates> Corners(IntroPlan plan, float inset)
    {
        var arena = plan.Arena.Comp;
        var halfWidth = MathF.Max(1f, arena.Width / 2f - inset);
        var halfHeight = MathF.Max(1f, arena.Height / 2f - inset);
        var origin = new Vector2(arena.Origin.X + 0.5f, arena.Origin.Y + 0.5f);
        var corners = new List<MapCoordinates>();
        foreach (var sign in new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
        {
            var local = origin + sign * new Vector2(halfWidth, halfHeight);
            corners.Add(_transform.ToMapCoordinates(new EntityCoordinates(plan.Arena.Owner, local)));
        }

        return corners;
    }

    private static MapCoordinates Along(MapCoordinates from, MapCoordinates to, float along)
    {
        return new MapCoordinates(Vector2.Lerp(from.Position, to.Position, along), to.MapId);
    }

    /// <summary>A real action of the scene on the server (moving players, breaking the gate).</summary>
    private void Later(IntroPlan plan, float delay, Action action)
    {
        var arenaUid = plan.Arena.Owner;
        Timer.Spawn(TimeSpan.FromSeconds(Math.Max(0f, delay)), () =>
        {
            if (TryComp<LavalandBossArenaComponent>(arenaUid, out var arena) && !arena.Ended)
                action();
        });
    }

    private static void Fx(IntroPlan plan, LavalandIntroFx fx)
    {
        plan.Event.Effects.Add(fx);
    }

    private static void Sound(IntroPlan plan, SoundSpecifier? sound, float time)
    {
        if (sound != null)
            plan.Event.Sounds.Add(new LavalandIntroSound(time, sound));
    }

    /// <summary>
    /// The copy of the boss the watchers see coming in; it stays until the real boss is brought out at the
    /// same spot when the scene ends.
    /// </summary>
    private void BossCopy(IntroPlan plan, float time, MapCoordinates at, Vector2 from, Vector2 to, float duration,
        bool accelerate = false, bool fadeIn = false, float spin = 0f, string? state = null)
    {
        if (plan.Arena.Comp.PendingBoss is not { } boss)
            return;

        plan.Arena.Comp.IntroBossSpot = at;
        Fx(plan, new LavalandIntroFx(time, boss, at.Position, from, to, duration, spin: spin, fadeIn: fadeIn)
        {
            Accelerate = accelerate,
            Lifetime = MathF.Max(0.1f, plan.Event.Duration - time + CopyOverlap),
            State = state,
        });
    }

    private void Trail(IntroPlan plan, float time, MapCoordinates corner, MapCoordinates stage, float along)
    {
        var position = Vector2.Lerp(corner.Position, stage.Position, along) + _random.NextVector2(0.25f);
        Fx(plan, new LavalandIntroFx(time, BloodTrail, position));
    }

    /// <summary>A blast made of particles: a flash, fire bursting out, smoke rolling out after it.</summary>
    private void Blast(IntroPlan plan, float time, MapCoordinates at, float radius, int fire, int smoke)
    {
        Fx(plan, new LavalandIntroFx(time, Flash, at.Position));
        for (var i = 0; i < fire; i++)
        {
            var angle = _random.NextFloat(0f, MathF.Tau);
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            Fx(plan, new LavalandIntroFx(time, FireParticle, at.Position, Vector2.Zero,
                dir * _random.NextFloat(0.5f, radius) + new Vector2(0f, 0.3f), _random.NextFloat(0.35f, 0.65f),
                spin: _random.NextFloat(-3f, 3f), fadeOut: true));
        }

        for (var i = 0; i < smoke; i++)
        {
            var angle = _random.NextFloat(0f, MathF.Tau);
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            Fx(plan, new LavalandIntroFx(time + 0.1f, SmokeParticle, at.Position, Vector2.Zero,
                dir * _random.NextFloat(0.6f, radius * 1.3f) + new Vector2(0f, _random.NextFloat(0.4f, 1.2f)),
                _random.NextFloat(1.2f, 2.2f), spin: _random.NextFloat(-1f, 1f), fadeOut: true));
        }
    }

    /// <summary>Particles thrown up from around a spot: fire, smoke, blood.</summary>
    private void Burst(IntroPlan plan, float time, MapCoordinates at, EntProtoId particle, int count,
        float minRadius, float maxRadius, float minRise, float maxRise, float minTime, float maxTime)
    {
        for (var i = 0; i < count; i++)
        {
            var angle = _random.NextFloat(0f, MathF.Tau);
            var start = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * _random.NextFloat(minRadius, maxRadius);
            var drift = new Vector2(_random.NextFloat(-0.6f, 0.6f), _random.NextFloat(minRise, maxRise));
            Fx(plan, new LavalandIntroFx(time, particle, at.Position + start, Vector2.Zero, drift,
                _random.NextFloat(minTime, maxTime), spin: _random.NextFloat(-2f, 2f), fadeOut: true));
        }
    }

    /// <summary>Rocks and earth thrown up and falling back around a spot.</summary>
    private void Debris(IntroPlan plan, float time, MapCoordinates at, int count, float spread)
    {
        for (var i = 0; i < count; i++)
        {
            var angle = _random.NextFloat(0f, MathF.Tau);
            var to = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * _random.NextFloat(0.8f, spread);
            Fx(plan, new LavalandIntroFx(time, RockParticle, at.Position, Vector2.Zero, to, _random.NextFloat(0.6f, 1.1f),
                _random.NextFloat(1.2f, 2.8f), _random.NextFloat(-9f, 9f), fadeOut: true));
        }
    }

    /// <summary>
    /// Each watcher flies into its own corner of the arena: it is put there at once and its sprite flies
    /// over from where it stood, spinning, so the camera following it glides along.
    /// </summary>
    private void ThrowToCorners(IntroPlan plan)
    {
        var corners = Corners(plan, 2.5f);
        _random.Shuffle(corners);
        for (var i = 0; i < plan.Watchers.Count; i++)
        {
            var watcher = plan.Watchers[i];
            if (TerminatingOrDeleted(watcher))
                continue;

            var from = _transform.GetMapCoordinates(watcher);
            var corner = corners[i % corners.Count];
            MoveTo(watcher, corner);

            var flight = EnsureComp<LavalandSpriteMotionComponent>(watcher);
            flight.Start = _timing.CurTime;
            flight.Duration = ThrowFlight;
            flight.From = from.Position - corner.Position;
            flight.To = Vector2.Zero;
            flight.Arc = 2.5f;
            flight.Spin = 12f;
            // Lands lying, as the next cue has it, and only then gets up.
            flight.EndRotation = 90f;
            Dirty(watcher, flight);

            var thrown = watcher;
            Timer.Spawn(TimeSpan.FromSeconds(ThrowFlight + 0.05f), () =>
            {
                if (!TerminatingOrDeleted(thrown))
                    RemComp<LavalandSpriteMotionComponent>(thrown);
            });
        }
    }

    private void MoveTo(EntityUid uid, MapCoordinates spot)
    {
        if (TerminatingOrDeleted(uid))
            return;

        _transform.SetMapCoordinates(uid, spot);
        if (TryComp<PhysicsComponent>(uid, out var body))
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
    }

    /// <summary>The farthest free spot up to <paramref name="distance"/> tiles from a point, stopping at walls.</summary>
    private MapCoordinates FreeSpotAlong(MapCoordinates from, Vector2 direction, float distance)
    {
        var best = from;
        for (var step = 0.5f; step <= distance + 0.01f; step += 0.5f)
        {
            var candidate = from.Offset(direction * step);
            if (!_mapManager.TryFindGridAt(candidate, out var gridUid, out var grid))
                break;

            var tile = _map.TileIndicesFor(gridUid, grid, candidate);
            if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty ||
                _turf.IsTileBlocked(gridUid, tile, CollisionGroup.Impassable, grid))
            {
                break;
            }

            best = candidate;
        }

        return best;
    }

    #endregion
}
