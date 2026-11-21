// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.GameTicking.Events;
using Content.Shared.Chat;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.RoundEnd.Credits;
using Content.Shared.DeadSpace.Slippery;
using Content.Shared.Electrocution;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Medical;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Nutrition;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.RoundEnd.Credits;

public sealed partial class RoundEndCreditsStatsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private RoundEndManifestStatsSystem _manifest = default!;

    private const string ScreamEmote = "Scream";
    private static readonly TimeSpan KillCreditWindow = TimeSpan.FromSeconds(30);
    private static readonly int StatCount = Enum.GetValues<RoundEndCreditsStat>().Length;

    private readonly Dictionary<EntityUid, float[]> _stats = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartingEvent>(_ => Reset());
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => Reset());
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<EntitySpokeEvent>(OnEntitySpoke);

        SubscribeLocalEvent<RoundEndCreditsTrackedComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<RoundEndCreditsTrackedComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<RoundEndCreditsTrackedComponent, SlippedEvent>(OnSlipped);
        SubscribeLocalEvent<RoundEndCreditsTrackedComponent, IngestingEvent>(OnIngesting);
        SubscribeLocalEvent<RoundEndCreditsTrackedComponent, TryVomitEvent>(OnVomit);
        SubscribeLocalEvent<RoundEndCreditsTrackedComponent, EmoteEvent>(OnEmote);
        SubscribeLocalEvent<RoundEndCreditsTrackedComponent, ElectrocutedEvent>(OnElectrocuted);
        SubscribeLocalEvent<RoundEndCreditsTrackedComponent, CuffedStateChangeEvent>(OnCuffedStateChange);
    }

    public float GetStat(EntityUid mindId, RoundEndCreditsStat stat)
    {
        return _stats.TryGetValue(mindId, out var stats) ? stats[(int) stat] : 0f;
    }

    public void SetStat(EntityUid mindId, RoundEndCreditsStat stat, float value)
    {
        GetOrCreate(mindId)[(int) stat] = value;
    }

    public void AddStat(EntityUid mindId, RoundEndCreditsStat stat, float amount = 1f)
    {
        GetOrCreate(mindId)[(int) stat] += amount;
    }

    private float[] GetOrCreate(EntityUid mindId)
    {
        if (!_stats.TryGetValue(mindId, out var stats))
        {
            stats = new float[StatCount];
            _stats[mindId] = stats;
        }

        return stats;
    }

    private void Reset()
    {
        _stats.Clear();
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (HasComp<GhostComponent>(args.Entity))
            return;

        EnsureComp<RoundEndCreditsTrackedComponent>(args.Entity);
    }

    private void OnEntitySpoke(EntitySpokeEvent args)
    {
        if (HasComp<GhostComponent>(args.Source) || !TryGetMind(args.Source, out var mindId))
            return;

        AddStat(mindId, RoundEndCreditsStat.Messages);
    }

    private void OnDamageChanged(EntityUid uid, RoundEndCreditsTrackedComponent component, DamageChangedEvent args)
    {
        if (args.DamageDelta == null || !TryGetMind(uid, out var targetMind))
            return;

        var delta = args.DamageDelta.GetTotal().Float();
        var sourceMind = TryGetSourceMind(args.Origin);

        if (args.DamageIncreased && delta > 0f)
        {
            AddStat(targetMind, RoundEndCreditsStat.DamageTaken, delta);

            if (sourceMind == targetMind)
            {
                AddStat(targetMind, RoundEndCreditsStat.SelfDamage, delta);
                return;
            }

            if (sourceMind is not { } attacker)
                return;

            AddStat(attacker, RoundEndCreditsStat.DamageDealt, delta);
            component.LastAttackerMind = attacker;
            component.LastAttackTime = _timing.CurTime;
            component.Attackers[attacker] = _timing.CurTime;
            return;
        }

        if (delta < 0f && sourceMind is { } healer && healer != targetMind)
            AddStat(healer, RoundEndCreditsStat.HealingGiven, -delta);
    }

    private void OnMobStateChanged(EntityUid uid, RoundEndCreditsTrackedComponent component, MobStateChangedEvent args)
    {
        if (!TryGetMind(uid, out var targetMind))
            return;

        var sourceMind = TryGetSourceMind(args.Origin);

        switch (args.NewMobState)
        {
            case MobState.Critical when args.OldMobState == MobState.Alive:
                AddStat(targetMind, RoundEndCreditsStat.Crits);
                break;

            case MobState.Dead when args.OldMobState != MobState.Dead:
                AddStat(targetMind, RoundEndCreditsStat.Deaths);
                OnKilled(component, targetMind, sourceMind);
                break;

            case MobState.Alive or MobState.Critical when args.OldMobState == MobState.Dead:
                if (sourceMind is { } reviver && reviver != targetMind)
                    AddStat(reviver, RoundEndCreditsStat.Revives);
                break;
        }
    }

    private void OnKilled(RoundEndCreditsTrackedComponent component, EntityUid targetMind, EntityUid? sourceMind)
    {
        var killer = sourceMind;
        if ((killer == null || killer == targetMind) &&
            component.LastAttackerMind is { } lastAttacker &&
            _timing.CurTime - component.LastAttackTime <= KillCreditWindow)
        {
            killer = lastAttacker;
        }

        component.LastAttackerMind = null;

        // Everyone else who hurt the body shortly before it died helped with the kill.
        foreach (var (helper, time) in component.Attackers)
        {
            if (helper != killer && helper != targetMind && _timing.CurTime - time <= KillCreditWindow)
                AddStat(helper, RoundEndCreditsStat.Assists);
        }

        component.Attackers.Clear();

        if (killer is not { } killerMind || killerMind == targetMind)
            return;

        AddStat(killerMind, RoundEndCreditsStat.Kills);

        if (!TryComp<MindComponent>(targetMind, out var target) ||
            !TryComp<MindComponent>(killerMind, out var killerComp))
        {
            return;
        }

        if (_manifest.IsAntagPlayerMind(targetMind, target) && !_manifest.IsAntagPlayerMind(killerMind, killerComp))
            AddStat(killerMind, RoundEndCreditsStat.AntagKills);
    }

    private void OnSlipped(EntityUid uid, RoundEndCreditsTrackedComponent component, ref SlippedEvent args)
    {
        AddFor(uid, RoundEndCreditsStat.Slips);
    }

    private void OnIngesting(EntityUid uid, RoundEndCreditsTrackedComponent component, ref IngestingEvent args)
    {
        AddFor(uid, RoundEndCreditsStat.Bites);
    }

    private void OnVomit(EntityUid uid, RoundEndCreditsTrackedComponent component, ref TryVomitEvent args)
    {
        AddFor(uid, RoundEndCreditsStat.Vomits);
    }

    private void OnEmote(EntityUid uid, RoundEndCreditsTrackedComponent component, ref EmoteEvent args)
    {
        AddFor(uid, RoundEndCreditsStat.Emotes);

        if (args.Emote.ID == ScreamEmote)
            AddFor(uid, RoundEndCreditsStat.Screams);
    }

    private void OnElectrocuted(EntityUid uid, RoundEndCreditsTrackedComponent component, ElectrocutedEvent args)
    {
        AddFor(uid, RoundEndCreditsStat.Shocks);
    }

    private void OnCuffedStateChange(EntityUid uid, RoundEndCreditsTrackedComponent component, ref CuffedStateChangeEvent args)
    {
        if (!TryComp<CuffableComponent>(uid, out var cuffable))
            return;

        var cuffed = !cuffable.CanStillInteract;
        if (cuffed && !component.Cuffed)
            AddFor(uid, RoundEndCreditsStat.Cuffed);

        component.Cuffed = cuffed;
    }

    private void AddFor(EntityUid uid, RoundEndCreditsStat stat)
    {
        if (TryGetMind(uid, out var mindId))
            AddStat(mindId, stat);
    }

    private bool TryGetMind(EntityUid uid, out EntityUid mindId)
    {
        return _manifest.TryGetPlayerMind(uid, out mindId, out _);
    }

    private EntityUid? TryGetSourceMind(EntityUid? origin)
    {
        if (!_manifest.TryGetDamageSourceMind(origin, out var mindId, out var mind) ||
            mind.UserId == null && mind.OriginalOwnerUserId == null)
        {
            return null;
        }

        return mindId;
    }
}
