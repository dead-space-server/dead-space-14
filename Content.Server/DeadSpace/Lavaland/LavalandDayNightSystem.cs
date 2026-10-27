// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Chat.Systems;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.DeadSpace.Lavaland;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Runs the lavaland day/night phases from <see cref="LavalandDayCycleConfig"/>: fades the map light,
/// announces the phase and sets the phase flags other systems read through <see cref="LavalandDayCycleSystem"/>.
/// A phase with <see cref="LavalandDayPhase.DeepNight"/> may turn into a deep night: with its one announcement the
/// moon hides and equipment fails at once (effects in <see cref="LavalandDeepNightSystem"/>), then the usual night goes on.
/// </summary>
public sealed class LavalandDayNightSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandMapComponent>();
        while (query.MoveNext(out var uid, out var lavaland))
        {
            if (lavaland.Planet.Id == null || !_prototype.TryIndex(lavaland.Planet, out var planet))
                continue;

            var config = planet.DayCycle;
            if (!config.Enabled || config.Phases.Count == 0)
            {
                if (HasComp<LavalandDayCycleComponent>(uid))
                    RemComp<LavalandDayCycleComponent>(uid);
                continue;
            }

            if (!TryComp<LavalandDayCycleComponent>(uid, out var cycle))
            {
                cycle = AddComp<LavalandDayCycleComponent>(uid);
                StartPhase((uid, cycle), config, Math.Clamp(config.StartPhase, 0, config.Phases.Count - 1), now, true, false);
                continue;
            }

            var phase = config.Phases[Math.Clamp(cycle.Phase, 0, config.Phases.Count - 1)];
            if (cycle.DeepStage != LavalandDeepNightStage.None && phase.DeepNight is { } deep)
                UpdateDeepNight((uid, cycle), config, phase, deep, now);

            if (now >= cycle.PhaseEnd)
                StartPhase((uid, cycle), config, (cycle.Phase + 1) % config.Phases.Count, now, false, false);

            if (cycle.PulseUntil != TimeSpan.Zero)
            {
                UpdatePulse((uid, cycle), config, now);
                continue;
            }

            if (cycle.Fading && now >= cycle.NextLightUpdate)
                UpdateLight((uid, cycle), config, now);
        }
    }

    /// <summary>
    /// Jumps the map to the given phase. Used by the cycle and handy for admins.
    /// <paramref name="forceDeep"/> makes a phase that can turn deep do so.
    /// </summary>
    public void SetPhase(EntityUid map, int phase, bool forceDeep = false)
    {
        if (!TryComp<LavalandMapComponent>(map, out var lavaland) ||
            lavaland.Planet.Id == null || !_prototype.TryIndex(lavaland.Planet, out var planet) ||
            planet.DayCycle.Phases.Count == 0)
        {
            return;
        }

        var cycle = EnsureComp<LavalandDayCycleComponent>(map);
        StartPhase((map, cycle), planet.DayCycle, Math.Clamp(phase, 0, planet.DayCycle.Phases.Count - 1), _timing.CurTime, false, forceDeep);
    }

    private void StartPhase(
        Entity<LavalandDayCycleComponent> ent,
        LavalandDayCycleConfig config,
        int index,
        TimeSpan now,
        bool instant,
        bool forceDeep)
    {
        var phase = config.Phases[index];
        var cycle = ent.Comp;
        cycle.Phase = index;
        cycle.Night = phase.DeepNight != null;
        cycle.CelestialNight = cycle.Night && cycle.Tint != null;
        cycle.PhaseStart = now;
        cycle.PhaseEnd = now + (phase.Duration > TimeSpan.Zero ? phase.Duration : TimeSpan.FromMinutes(1));
        SetFlags(cycle, phase.JamTrackers, phase.BlockJaunter, phase.WispGuide, false, false);
        cycle.DeepStage = LavalandDeepNightStage.None;

        var deep = !instant && phase.DeepNight is { } deepConfig && (forceDeep || _random.Prob(deepConfig.Chance))
            ? phase.DeepNight
            : null;
        if (deep != null)
        {
            // The moon hides right away: everything fails with the announcement, then the usual night.
            cycle.DeepStage = LavalandDeepNightStage.Active;
            cycle.DeepStartAt = now;
            cycle.DeepEndAt = now + deep.Duration;
            cycle.PhaseEnd = cycle.DeepEndAt + deep.AfterDuration;
            SetFlags(cycle, deep.JamTrackers, deep.BlockJaunter, deep.WispGuide, true, deep.SuppressLavaLight);
        }

        Dirty(ent);

        FadeTo(ent, deep?.LightColor ?? phase.LightColor, now);
        if (instant || config.TransitionTime <= TimeSpan.Zero)
            cycle.FadeStart = now - config.TransitionTime;
        UpdateLight(ent, config, now);

        if (instant)
            return;

        if (deep != null)
            Announce(ent, deep.Message, deep.MessageColor, deep.Sound);
        else
            Announce(ent, phase.Message, phase.MessageColor, phase.Sound);
    }

    private void UpdateDeepNight(
        Entity<LavalandDayCycleComponent> ent,
        LavalandDayCycleConfig config,
        LavalandDayPhase phase,
        LavalandDeepNightConfig deep,
        TimeSpan now)
    {
        var cycle = ent.Comp;
        if (cycle.DeepStage == LavalandDeepNightStage.Active && now >= cycle.DeepEndAt)
        {
            // The moon is back: equipment works again and the usual night goes on.
            cycle.DeepStage = LavalandDeepNightStage.None;
            SetFlags(cycle, phase.JamTrackers, phase.BlockJaunter, phase.WispGuide, false, false);
            Dirty(ent);
            FadeTo(ent, phase.LightColor, now);
            UpdateLight(ent, config, now);
            Announce(ent, deep.EndMessage, phase.MessageColor, null);
        }
    }

    private static void SetFlags(
        LavalandDayCycleComponent cycle,
        bool jamTrackers,
        bool blockJaunter,
        bool wispGuide,
        bool deepNight,
        bool suppressLava)
    {
        cycle.JamTrackers = jamTrackers;
        cycle.BlockJaunter = blockJaunter;
        cycle.WispGuide = wispGuide;
        cycle.DeepNight = deepNight;
        cycle.SuppressLavaLight = suppressLava;
    }

    /// <summary>
    /// From now on the nights of this lavaland take the hue and glow as the chromite caves do; the days keep their own
    /// light. If it is night now, it fades into it at once.
    /// </summary>
    public void SetTint(EntityUid map, Color tint, float brightness = 1f, float minBrightness = 0.08f)
    {
        if (!TryComp<LavalandDayCycleComponent>(map, out var cycle))
            return;

        var target = cycle.Fading ? cycle.BaseColor : EnsureComp<MapLightComponent>(map).AmbientLightColor;
        cycle.Tint = tint;
        cycle.TintBrightness = brightness;
        cycle.TintMinBrightness = minBrightness;
        cycle.CelestialNight = cycle.Night;
        Dirty(map, cycle);
        FadeTo((map, cycle), target, _timing.CurTime);
    }

    /// <summary>
    /// The light of the whole map shimmers between two colours for a while, like the chromite caves' pit; then it
    /// fades back to the phase's own colour.
    /// </summary>
    public void Pulse(EntityUid map, Color a, Color b, TimeSpan duration, float period = 2f)
    {
        if (!TryComp<LavalandDayCycleComponent>(map, out var cycle))
            return;

        cycle.PulseUntil = _timing.CurTime + duration;
        cycle.PulseA = a;
        cycle.PulseB = b;
        cycle.PulsePeriod = MathF.Max(0.5f, period);
        cycle.NextLightUpdate = _timing.CurTime;
    }

    private void UpdatePulse(Entity<LavalandDayCycleComponent> ent, LavalandDayCycleConfig config, TimeSpan now)
    {
        var cycle = ent.Comp;
        if (now >= cycle.PulseUntil)
        {
            cycle.PulseUntil = TimeSpan.Zero;
            var phase = config.Phases[Math.Clamp(cycle.Phase, 0, config.Phases.Count - 1)];
            FadeTo(ent, phase.LightColor, now);
            return;
        }

        if (now < cycle.NextLightUpdate)
            return;

        var t = (MathF.Sin((float) now.TotalSeconds * MathF.Tau / cycle.PulsePeriod) + 1f) / 2f;
        var light = EnsureComp<MapLightComponent>(ent);
        light.AmbientLightColor = Color.InterpolateBetween(cycle.PulseA, cycle.PulseB, t);
        Dirty(ent, light);
        cycle.NextLightUpdate = now + PulseUpdateInterval;
    }

    private static readonly TimeSpan PulseUpdateInterval = TimeSpan.FromSeconds(0.1);

    /// <summary>
    /// A night's colour turned towards the tint, as bright as it was times the tint's brightness, but never darker than
    /// its floor; a day's as it is.
    /// </summary>
    private static Color Tinted(LavalandDayCycleComponent cycle, Color color)
    {
        if (cycle.Tint is not { } tint || !cycle.Night)
            return color;

        var brightness = MathF.Max(MathF.Max(color.R, color.G), color.B) * cycle.TintBrightness;
        brightness = Math.Clamp(MathF.Max(brightness, cycle.TintMinBrightness), 0f, 1f);
        var hued = new Color(
            MathF.Min(1f, tint.R * brightness),
            MathF.Min(1f, tint.G * brightness),
            MathF.Min(1f, tint.B * brightness),
            color.A);
        return Color.InterpolateBetween(color, hued, TintStrength);
    }

    private const float TintStrength = 0.75f;

    private void FadeTo(Entity<LavalandDayCycleComponent> ent, Color color, TimeSpan now)
    {
        var light = EnsureComp<MapLightComponent>(ent);
        ent.Comp.FromColor = light.AmbientLightColor;
        ent.Comp.BaseColor = color;
        ent.Comp.ToColor = Tinted(ent.Comp, color);
        ent.Comp.Fading = true;
        ent.Comp.FadeStart = now;
        ent.Comp.NextLightUpdate = now;
    }

    private void Announce(Entity<LavalandDayCycleComponent> ent, LocId? message, Color color, SoundSpecifier? sound)
    {
        var filter = Filter.Empty().AddInMap(Transform(ent).MapID);
        if (message is { } text)
        {
            _chat.DispatchFilteredAnnouncement(
                filter,
                Loc.GetString(text),
                sender: Loc.GetString("lavaland-day-cycle-sender"),
                playSound: false,
                colorOverride: color);
        }

        if (sound != null)
            _audio.PlayGlobal(sound, filter, true);
    }

    private void UpdateLight(Entity<LavalandDayCycleComponent> ent, LavalandDayCycleConfig config, TimeSpan now)
    {
        var cycle = ent.Comp;
        var progress = config.TransitionTime <= TimeSpan.Zero
            ? 1f
            : (float) Math.Clamp((now - cycle.FadeStart) / config.TransitionTime, 0, 1);

        var light = EnsureComp<MapLightComponent>(ent);
        light.AmbientLightColor = Color.InterpolateBetween(cycle.FromColor, cycle.ToColor, progress);
        Dirty(ent, light);

        cycle.NextLightUpdate = now + config.LightUpdateInterval;
        if (progress >= 1f)
            cycle.Fading = false;
    }
}
