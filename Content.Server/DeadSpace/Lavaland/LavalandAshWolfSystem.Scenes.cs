// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Lavaland.AshWolf;
using Content.Shared.DeadSpace.Lavaland.Bosses;
using Content.Shared.DeadSpace.Lavaland.SecretBoss;
using Content.Shared.Jittering;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>Its waking scene, its quick time event and its fall.</summary>
public sealed partial class LavalandAshWolfSystem
{
    private static readonly EntProtoId IntroRoll = "LavalandIntroAshWolfRoll";
    private static readonly EntProtoId IntroDust = "LavalandIntroAshWolfDust";
    private static readonly EntProtoId IntroFlash = "LavalandIntroFlash";
    private static readonly EntProtoId IntroFire = "LavalandIntroFireParticle";
    private static readonly EntProtoId IntroSmoke = "LavalandIntroSmokeParticle";

    // The moments of the scene, seconds from its start.
    private const float SceneGetUp = 2f;
    private const float SceneCircle = 2.6f;
    private const float SceneCircleEnd = 7.4f;
    private const float SceneDust = 7.6f;
    private const float SceneEyes = 8.2f;
    private const float SceneReveal = 10.2f;
    private const float SceneLength = 13.4f;

    #region Waking and the scene

    private void Wake(Entity<LavalandAshWolfComponent> ent)
    {
        var comp = ent.Comp;
        comp.Stage = LavalandAshWolfStage.Waking;
        comp.StageTime = _timing.CurTime + comp.WakeDelay;
        _jitter.AddJitter(ent, 6f, 10f);
        PlaySound(comp.WakeSound, ent);
        _popup.PopupEntity(Loc.GetString("lavaland-ash-wolf-wake-popup"), ent, PopupType.LargeCaution);
    }

    /// <summary>
    /// The camera goes to it lying and twitching; it gets up, circles the players in a storm of dust, and is lost in
    /// it; the camera comes back to it - only its eyes glow; the dust settles, and there it stands with its name.
    /// </summary>
    private void StartIntro(Entity<LavalandAshWolfComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;
        comp.Stage = LavalandAshWolfStage.Intro;
        comp.StageTime = now;
        comp.Step = 0;

        var bossPosition = Position(ent);
        var watchers = GetPlayers(ent, comp.WatchRange);
        var filter = Filter.Empty();
        var ev = new LavalandBossIntroEvent
        {
            Title = Loc.GetString(comp.Name),
            Subtitle = Loc.GetString(comp.Subtitle),
            TitleColor = Color.FromHex("#ff7a2e"),
            Duration = SceneLength,
            TitleDelay = SceneReveal,
            TitleDuration = 2.6f,
            ReturnTime = 1.2f,
            StartZoom = 0.6f,
        };

        foreach (var watcher in watchers)
        {
            if (_players.TryGetSessionByEntity(watcher, out var session))
                filter.AddPlayer(session);

            ev.Watchers.Add(GetNetEntity(watcher));
            if (!HasComp<LavalandPitFrozenComponent>(watcher))
            {
                AddComp<LavalandPitFrozenComponent>(watcher);
                comp.Watchers.Add(watcher);
            }
        }

        // The middle of the players, or of it if nobody is about.
        var center = bossPosition;
        if (watchers.Count > 0)
        {
            center = Vector2.Zero;
            foreach (var watcher in watchers)
            {
                center += Position(watcher);
            }

            center /= watchers.Count;
        }

        var spread = 0f;
        foreach (var watcher in watchers)
        {
            spread = MathF.Max(spread, Vector2.Distance(center, Position(watcher)));
        }

        var radius = MathF.Max(2.6f, spread + 2f);

        ev.Shots.Add(new LavalandIntroShot(0f, false, bossPosition, 0.6f));
        ev.Shots.Add(new LavalandIntroShot(SceneCircle, false, center, MathF.Max(1f, radius / 3.2f)));
        ev.Shots.Add(new LavalandIntroShot(SceneDust, false, bossPosition, 0.7f));
        ev.Dolls.Add(new LavalandIntroDollCue(SceneCircle, LavalandIntroDollAction.LookAround));
        ev.Dolls.Add(new LavalandIntroDollCue(SceneDust, LavalandIntroDollAction.None));
        ev.Shakes.Add(new Vector2(SceneGetUp, 0.15f));
        ev.Shakes.Add(new Vector2(SceneCircle, 0.25f));
        ev.Shakes.Add(new Vector2(SceneDust, 0.5f));

        if (comp.RushSound != null)
            ev.Sounds.Add(new LavalandIntroSound(SceneCircle, comp.RushSound));
        if (comp.LandSound != null)
            ev.Sounds.Add(new LavalandIntroSound(SceneDust, comp.LandSound));

        PlanCircle(ev, bossPosition, center, radius);
        PlanDustCloud(ev, bossPosition);

        if (comp.SelectedMusic != null)
        {
            ev.Music = _audio.ResolveSound(comp.SelectedMusic);
            ev.MusicVolume += comp.SelectedMusic.Params.Volume;
        }

        RaiseNetworkEvent(ev, filter);
    }

    /// <summary>A copy of it rolling round the players twice, throwing up huge dust as it goes.</summary>
    private void PlanCircle(LavalandBossIntroEvent ev, Vector2 bossPosition, Vector2 center, float radius)
    {
        const float segment = 0.12f;
        var startAngle = (bossPosition - center).ToWorldAngle().Theta - MathF.PI / 2f;
        var turns = 2f;
        var segments = (int) MathF.Ceiling((SceneCircleEnd - SceneCircle - 0.3f) / segment);

        Vector2 PointAt(float along)
        {
            var angle = (float) startAngle + MathF.Tau * turns * along;
            return center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }

        // From where it lies out onto the circle.
        var first = PointAt(0f);
        ev.Effects.Add(new LavalandIntroFx(SceneCircle, IntroRoll, bossPosition, Vector2.Zero, first - bossPosition, 0.3f)
        {
            Lifetime = 0.32f,
        });

        for (var i = 0; i < segments; i++)
        {
            var time = SceneCircle + 0.3f + i * segment;
            var from = PointAt((float) i / segments);
            var to = PointAt((float) (i + 1) / segments);
            ev.Effects.Add(new LavalandIntroFx(time, IntroRoll, from, Vector2.Zero, to - from, segment) { Lifetime = segment + 0.02f });

            for (var k = 0; k < 2; k++)
            {
                var drift = (from - center).Normalized() * _random.NextFloat(0.4f, 1.4f) + new Vector2(0f, _random.NextFloat(0.2f, 0.7f));
                ev.Effects.Add(new LavalandIntroFx(time, IntroDust, from + _random.NextVector2(0.3f), Vector2.Zero, drift,
                    _random.NextFloat(1.6f, 2.6f), spin: _random.NextFloat(-1.5f, 1.5f), fadeOut: true));
            }
        }
    }

    /// <summary>It rolls back to where it lay into a thick cloud, which settles when it is shown.</summary>
    private void PlanDustCloud(LavalandBossIntroEvent ev, Vector2 bossPosition)
    {
        ev.Effects.Add(new LavalandIntroFx(SceneDust, IntroFlash, bossPosition));
        for (var i = 0; i < 26; i++)
        {
            var angle = _random.NextFloat(0f, MathF.Tau);
            var start = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * _random.NextFloat(0f, 1.6f);
            ev.Effects.Add(new LavalandIntroFx(SceneDust, IntroDust, bossPosition + start, Vector2.Zero,
                start * 0.4f + new Vector2(0f, _random.NextFloat(0.1f, 0.5f)), SceneReveal - SceneDust + 0.3f,
                spin: _random.NextFloat(-0.6f, 0.6f), fadeOut: true)
            {
                Lifetime = SceneReveal - SceneDust + 0.3f,
            });
        }
    }

    private void UpdateIntro(Entity<LavalandAshWolfComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;
        var t = (float) (now - comp.StageTime).TotalSeconds;
        if (comp.Step == 0 && t >= SceneGetUp)
        {
            RemComp<JitteringComponent>(ent);
            SetVisual(ent, LavalandAshWolfVisualState.Normal);
            comp.Step = 1;
        }

        // Its copy does the circling.
        if (comp.Step == 1 && t >= SceneCircle)
        {
            SetVisual(ent, LavalandAshWolfVisualState.Hidden);
            comp.Step = 2;
        }

        if (comp.Step == 2 && t >= SceneEyes)
        {
            SetVisual(ent, LavalandAshWolfVisualState.EyesOnly);
            comp.Step = 3;
        }

        if (comp.Step == 3 && t >= SceneReveal)
        {
            SetVisual(ent, LavalandAshWolfVisualState.Normal);
            comp.Step = 4;
        }

        if (t < SceneLength)
            return;

        ReleaseWatchers(comp);
        comp.Stage = LavalandAshWolfStage.Fight;
        comp.Step = 0;
        StartFight(ent, now);
    }

    private void ReleaseWatchers(LavalandAshWolfComponent comp)
    {
        foreach (var watcher in comp.Watchers)
        {
            if (!TerminatingOrDeleted(watcher))
                RemComp<LavalandPitFrozenComponent>(watcher);
        }

        comp.Watchers.Clear();
    }

    #endregion

    #region Quick time event

    private void StartQte(Entity<LavalandAshWolfComponent> ent, EntityUid victim, TimeSpan now)
    {
        var comp = ent.Comp;
        comp.QteId = _nextQteId++;
        comp.QteResult = null;
        comp.QteDeadline = now + TimeSpan.FromSeconds(comp.QteDuration + comp.QteGrace);

        // Nobody to press anything: the blades.
        if (!_players.TryGetSessionByEntity(victim, out var session))
        {
            comp.QteResult = false;
            return;
        }

        var ev = new LavalandAshWolfQteStartEvent
        {
            Id = comp.QteId,
            Duration = comp.QteDuration,
            FirstDelay = 0.35f,
            TurnTime = 1.1f,
            Gap = 0.35f,
            ZoneWidth = 0.14f,
        };

        for (var i = 0; i < 3; i++)
        {
            ev.ZoneStarts.Add(_random.NextFloat(0.3f, 0.78f));
        }

        RaiseNetworkEvent(ev, session.Channel);
        _popup.PopupEntity(Loc.GetString("lavaland-ash-wolf-qte-popup"), victim, victim, PopupType.LargeCaution);
    }

    private void StopQte(LavalandAshWolfComponent comp)
    {
        if (comp.QteId == 0)
            return;

        if (comp.Victim is { } victim && _players.TryGetSessionByEntity(victim, out var session))
            RaiseNetworkEvent(new LavalandAshWolfQteStopEvent(comp.QteId), session.Channel);

        comp.QteId = 0;
    }

    /// <summary>Only the pinned player's answer counts, and only in time (the client plays it on its own clock).</summary>
    private void OnQteResult(LavalandAshWolfQteResultEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } sender)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandAshWolfComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (comp.QteId != ev.Id || comp.Victim != sender || comp.QteResult != null || now > comp.QteDeadline)
                continue;

            comp.QteResult = ev.Success;
            return;
        }
    }

    #endregion

    #region Defeat

    private const float DyingShake = 6f;
    private const float DyingFall = 1.6f;

    private void StartDying(Entity<LavalandAshWolfComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;
        EndAttack(ent, now);
        comp.Sliding = false;
        DeleteSaws(ent);
        comp.Stage = LavalandAshWolfStage.Dying;
        comp.StageTime = now + TimeSpan.FromSeconds(DyingShake);
        comp.Step = 0;
        comp.Untouchable = true;
        comp.NextSpark = now;
        comp.NextBlast = now + TimeSpan.FromSeconds(0.6);
        _jitter.AddJitter(ent, 14f, 12f);
        _popup.PopupEntity(Loc.GetString("lavaland-ash-wolf-dying-popup"), ent, PopupType.LargeCaution);
        comp.NextHud = TimeSpan.Zero;
        SendHud(ent, now);
    }

    /// <summary>It shakes, sparks fly every way, now and then it blows up a little; then it falls, and is a borg.</summary>
    private void UpdateDying(Entity<LavalandAshWolfComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;
        var at = Position(ent);
        var mapId = Transform(ent).MapID;

        if (comp.Step == 0)
        {
            if (now >= comp.NextSpark)
            {
                comp.NextSpark = now + TimeSpan.FromSeconds(_random.NextFloat(0.12f, 0.25f));
                var spark = Spawn(comp.Sparks, new MapCoordinates(at + _random.NextVector2(0.3f), mapId));
                Drift(spark, _random.NextAngle().ToVec() * _random.NextFloat(0.8f, 2f), 0.45f);
            }

            if (now >= comp.NextBlast)
            {
                comp.NextBlast = now + TimeSpan.FromSeconds(_random.NextFloat(0.8f, 1.6f));
                Blast(ent, at + _random.NextVector2(0.6f));
            }

            if (now < comp.StageTime)
                return;

            // Down it goes.
            RemComp<JitteringComponent>(ent);
            Blast(ent, at);
            SetVisual(ent, LavalandAshWolfVisualState.Dormant);
            PlaySound(comp.LandSound, ent);
            comp.Step = 1;
            comp.StageTime = now + TimeSpan.FromSeconds(DyingFall);
            return;
        }

        if (now >= comp.StageTime)
            BecomeBorg(ent);
    }

    /// <summary>A small blast like the colossus's in its scene: a flash, fire, smoke and a jolt to those near.</summary>
    private void Blast(Entity<LavalandAshWolfComponent> ent, Vector2 at)
    {
        var mapId = Transform(ent).MapID;
        Spawn(IntroFlash, new MapCoordinates(at, mapId));
        for (var i = 0; i < 6; i++)
        {
            var fire = Spawn(IntroFire, new MapCoordinates(at, mapId));
            Drift(fire, _random.NextAngle().ToVec() * _random.NextFloat(0.4f, 1.2f) + new Vector2(0f, 0.3f), _random.NextFloat(0.35f, 0.6f));
        }

        for (var i = 0; i < 8; i++)
        {
            var smoke = Spawn(IntroSmoke, new MapCoordinates(at, mapId));
            Drift(smoke, _random.NextAngle().ToVec() * _random.NextFloat(0.6f, 1.6f) + new Vector2(0f, _random.NextFloat(0.4f, 1f)),
                _random.NextFloat(1.2f, 2f));
        }

        PlaySound(ent.Comp.BlastSound, ent, -2f);
        foreach (var player in GetPlayers(ent, 9f))
        {
            var strength = 0.5f * (1f - Vector2.Distance(Position(player), at) / 9f);
            if (strength > 0f)
                _camera.KickCamera(player, _random.NextAngle().ToVec() * strength);
        }
    }

    /// <summary>The boss is gone; the borg it was stays where it fell, with ordinary laws and an empty mind.</summary>
    private void BecomeBorg(Entity<LavalandAshWolfComponent> ent)
    {
        var xform = Transform(ent);
        var coordinates = xform.Coordinates;
        var rotation = _transform.GetWorldRotation(xform);
        EndFight(ent.Comp);

        var borg = Spawn(ent.Comp.Borg, _transform.ToMapCoordinates(coordinates));
        _transform.SetWorldRotation(borg, rotation);
        _damage.TryChangeDamage(borg, ent.Comp.BorgDamage, ignoreResistances: true);
        _popup.PopupEntity(Loc.GetString("lavaland-ash-wolf-tamed-popup"), borg, PopupType.Large);
        QueueDel(ent);
    }

    #endregion
}
