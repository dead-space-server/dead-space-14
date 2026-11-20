// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Access.Components;
using Content.Server.Administration.Logs;
using Content.Server.Body.Systems;
using Content.Server.Popups;
using Content.Server.Traits.Assorted;
using Content.Shared.Access.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.Psychiatry;
using Content.Shared.FixedPoint;
using Content.Shared.Damage.Components;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Inventory;
using Content.Shared.Medical;
using Content.Shared.Mind;
using Content.Shared.PDA;
using Content.Shared.Popups;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.Psychiatry;

public sealed partial class PsychiatrySystem : SharedPsychiatrySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private VomitSystem _vomit = default!;
    [Dependency] private ParacusiaSystem _paracusia = default!;
    [Dependency] private InternalsSystem _internals = default!;
    [Dependency] private InventorySystem _inventory = default!;

    private static readonly SoundSpecifier HallucinationSounds = new SoundCollectionSpecifier("Paracusia");

    private static readonly ProtoId<PsychiatryPhrasesPrototype> DefaultPhrases = "PsychiatryDefault";
    private static readonly ProtoId<ReagentPrototype> Schizotoxin = SpecialPillReagentId;
    private static readonly ProtoId<ReagentPrototype> PsychogenLatent = "PsychogenLatent";
    private static readonly ProtoId<ReagentPrototype> PsychiatryRemedy = "PsychiatryRemedy";
    private static readonly ProtoId<ReagentPrototype> PsychiatryStageStep = "PsychiatryStageStep";

    private float _accum;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SchizophreniaComponent, ComponentStartup>(OnIllnessStartup);
        SubscribeLocalEvent<SchizophreniaComponent, ComponentShutdown>(OnIllnessShutdown);
        SubscribeLocalEvent<SchizophreniaComponent, MobStateChangedEvent>(OnMobState);
        SubscribeLocalEvent<PsychiatryHeadTraumaComponent, ComponentStartup>(OnHeadTrauma);
        SubscribeLocalEvent<RoleAddedEvent>(OnRoleAdded);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_cfg.GetCVar(CCCCVars.PsychiatryEnabled))
            return;

        _accum += frameTime;
        if (_accum < 1f)
            return;
        _accum = 0f;

        TickMindShields();
        ScanReagents();
        TickAutoEscalateAndWhispers();
    }

    private void ScanReagents()
    {
        var query = EntityQueryEnumerator<BloodstreamComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var blood, out _))
        {
            if (IsPositronic(uid))
            {
                if (GetReagentUnits(uid, blood, Schizotoxin) > FixedPoint2.Zero
                    || GetReagentUnits(uid, blood, PsychogenLatent) > FixedPoint2.Zero
                    || GetReagentUnits(uid, blood, PsychiatryRemedy) > FixedPoint2.Zero
                    || GetReagentUnits(uid, blood, PsychiatryStageStep) > FixedPoint2.Zero)
                {
                    RemoveReagent(uid, blood, Schizotoxin);
                    RemoveReagent(uid, blood, PsychogenLatent);
                    RemoveReagent(uid, blood, PsychiatryRemedy);
                    RemoveReagent(uid, blood, PsychiatryStageStep);
                    _popup.PopupEntity(Loc.GetString("psychiatry-pill-positronic"), uid, uid);
                }

                continue;
            }

            if (GetReagentUnits(uid, blood, PsychiatryRemedy) > FixedPoint2.Zero)
            {
                ClearIllness(uid, "PsychiatryRemedy");
                RemoveReagent(uid, blood, PsychiatryRemedy);
                continue;
            }

            if (GetReagentUnits(uid, blood, Schizotoxin) > FixedPoint2.Zero)
            {
                TryApplyOrEscalate(uid, SchizophreniaStage.Acute, pillForced: true, ignoreCooldown: true, reason: "Schizotoxin");
                RemoveReagent(uid, blood, Schizotoxin);
            }

            if (GetReagentUnits(uid, blood, PsychiatryStageStep) > FixedPoint2.Zero)
            {
                TryStepStage(uid);
                RemoveReagent(uid, blood, PsychiatryStageStep);
            }
        }
    }

    private void TickAutoEscalateAndWhispers()
    {
        var q = EntityQueryEnumerator<SchizophreniaComponent>();
        while (q.MoveNext(out var uid, out var schizo))
        {
            if (schizo.CourseSpoiled)
            {
                if (!TryComp<BloodstreamComponent>(uid, out var blood)
                    || GetReagentUnits(uid, blood, ClarityReagentId) <= FixedPoint2.Zero)
                    schizo.CourseSpoiled = false;
            }

            if (Timing.CurTime >= schizo.NextAutoEscalate && schizo.Stage < SchizophreniaStage.Acute)
            {
                if (IsMindShielded(uid))
                    ScheduleAutoEscalate(schizo, uid);
                else if (!IsAntagImmune(uid, pillForced: false))
                    AdjustStage(uid, +1, "auto-escalate");
                else
                    ScheduleAutoEscalate(schizo, uid);
                continue;
            }

            if (schizo.Stage >= SchizophreniaStage.Latent)
                TryWhisper(uid, schizo);

            TickPhantom(uid, schizo);
        }
    }

    public bool IsPsychogenBlocked(EntityUid uid)
    {
        if (_internals.AreInternalsWorking(uid))
            return true;

        if (!_inventory.TryGetContainerSlotEnumerator(uid, out var slots, SlotFlags.HEAD | SlotFlags.MASK))
            return false;

        while (slots.NextItem(out var item))
        {
            if (!HasComp<PsychogenFilterComponent>(item))
                continue;

            if (TryComp<BreathToolComponent>(item, out var breath) && breath.IsFunctional)
                return true;
        }

        return false;
    }

    public bool TryInhalePsychogen(EntityUid uid)
    {
        if (!_cfg.GetCVar(CCCCVars.PsychiatryEnabled))
            return false;

        if (IsPositronic(uid) || IsMindShielded(uid) || IsAntagImmune(uid, pillForced: false, gas: true))
            return false;

        if (IsPsychogenBlocked(uid))
            return false;

        if (OnTreatmentHold(uid))
            return false;

        var tracker = EnsureComp<SchizophreniaOnsetTrackerComponent>(uid);
        if (Timing.CurTime < tracker.NextAllowedGasOnset)
            return false;

        if (TryComp<SchizophreniaComponent>(uid, out var existing))
        {
            if (existing.Kind != PsychiatryIllnessKind.Schizophrenia || existing.Stage >= SchizophreniaStage.Simple)
            {
                tracker.NextAllowedGasOnset = Timing.CurTime + GasCooldown();
                return false;
            }

            AdjustStage(uid, +1, "psychogen-gas");
            tracker.NextAllowedGasOnset = Timing.CurTime + GasCooldown();
            return true;
        }

        ApplyNew(uid, SchizophreniaStage.Latent, pillForced: false, "psychogen-gas");
        tracker.NextAllowedGasOnset = Timing.CurTime + GasCooldown();
        return true;
    }

    public bool TryOnsetOrEscalate(EntityUid uid, SchizophreniaStage suggested, string reason, bool ignoreCooldown = false, bool harm = false)
    {
        if (TryComp<SchizophreniaComponent>(uid, out var existing)
            && existing.Kind == PsychiatryIllnessKind.Schizophrenia
            && existing.Stage >= SchizophreniaStage.Simple)
            return false;

        return TryApplyOrEscalate(uid, suggested, pillForced: false, ignoreCooldown, reason, harm);
    }

    public bool TryApplyOrEscalate(EntityUid uid, SchizophreniaStage suggested, bool pillForced, bool ignoreCooldown, string reason, bool harm = false)
    {
        if (!_cfg.GetCVar(CCCCVars.PsychiatryEnabled))
            return false;

        if (OnTreatmentHold(uid))
            return false;

        if (IsPositronic(uid) || IsMindShielded(uid))
            return false;

        if (IsAntagImmune(uid, pillForced))
            return false;

        var tracker = EnsureComp<SchizophreniaOnsetTrackerComponent>(uid);
        var cd = TimeSpan.FromSeconds(_cfg.GetCVar(CCCCVars.PsychiatryOnsetCooldownSec));
        if (!ignoreCooldown && Timing.CurTime < tracker.NextAllowedOnset)
            return false;
        if (harm && Timing.CurTime < tracker.NextHarmStage)
            return false;

        if (TryComp<SchizophreniaComponent>(uid, out var existing))
        {
            if (existing.Stage >= SchizophreniaStage.Acute)
            {
                if (!ignoreCooldown)
                    tracker.NextAllowedOnset = Timing.CurTime + cd;
                return false;
            }

            if (pillForced)
                existing.PillForced = true;

            AdjustStage(uid, +1, reason);
            if (!ignoreCooldown)
                tracker.NextAllowedOnset = Timing.CurTime + cd;
            if (harm)
                StampHarm(tracker);
            return true;
        }

        ApplyNew(uid, suggested, pillForced, reason);
        if (!ignoreCooldown)
            tracker.NextAllowedOnset = Timing.CurTime + cd;
        if (harm)
            StampHarm(tracker);
        return true;
    }

    public bool TryApplyCyber(EntityUid uid, SchizophreniaStage suggested, string reason, bool ignoreCooldown = false, bool harm = false, bool forced = false)
    {
        if (!_cfg.GetCVar(CCCCVars.PsychiatryEnabled) || !IsPositronic(uid))
            return false;

        if (OnTreatmentHold(uid))
            return false;

        if (!forced && IsAntagImmune(uid, pillForced: false))
            return false;

        var tracker = EnsureComp<SchizophreniaOnsetTrackerComponent>(uid);
        var cd = TimeSpan.FromSeconds(_cfg.GetCVar(CCCCVars.PsychiatryOnsetCooldownSec));
        if (!ignoreCooldown && Timing.CurTime < tracker.NextAllowedOnset)
            return false;
        if (harm && Timing.CurTime < tracker.NextHarmStage)
            return false;

        if (TryComp<SchizophreniaComponent>(uid, out var existing))
        {
            if (existing.Kind != PsychiatryIllnessKind.Cyberpsychosis
                || existing.Stage >= SchizophreniaStage.Acute
                || (existing.Stage >= SchizophreniaStage.Simple && !forced))
            {
                if (!ignoreCooldown)
                    tracker.NextAllowedOnset = Timing.CurTime + cd;
                return false;
            }

            AdjustStage(uid, +1, reason);
            if (!ignoreCooldown)
                tracker.NextAllowedOnset = Timing.CurTime + cd;
            if (harm)
                StampHarm(tracker);
            return true;
        }

        ApplyNew(uid, suggested, pillForced: false, reason, PsychiatryIllnessKind.Cyberpsychosis);
        if (!ignoreCooldown)
            tracker.NextAllowedOnset = Timing.CurTime + cd;
        if (harm)
            StampHarm(tracker);
        return true;
    }

    public void ApplyNew(EntityUid uid, SchizophreniaStage stage, bool pillForced, string reason, PsychiatryIllnessKind kind = PsychiatryIllnessKind.Schizophrenia)
    {
        if (stage <= SchizophreniaStage.None)
            return;

        if (kind == PsychiatryIllnessKind.Schizophrenia && (IsPositronic(uid) || IsMindShielded(uid)))
            return;

        if (kind == PsychiatryIllnessKind.Cyberpsychosis && !IsPositronic(uid))
            return;

        var comp = EnsureComp<SchizophreniaComponent>(uid);
        comp.Kind = kind;
        comp.Stage = ClampStage((int) stage);
        comp.StageHealth = 1f;
        comp.PillForced = pillForced || comp.PillForced;
        comp.CourseNeeded = 0;
        comp.CourseTaken = 0;
        comp.CourseMetabolized = 0f;
        comp.CourseSpoiled = false;
        if (comp.Seed == 0)
            comp.Seed = _random.Next();
        ScheduleAutoEscalate(comp, uid);
        ScheduleWhisper(comp, uid);
        SyncHallucinations(uid, comp.Stage);
        Dirty(uid, comp);

        _adminLog.Add(LogType.Damaged, LogImpact.Medium,
            $"{ToPrettyString(uid):player} got {comp.Kind}, stage {comp.Stage}, from {reason}, pillForced={comp.PillForced}");
    }

    public void ApplyClarityDose(EntityUid uid, float units)
    {
        if (units <= 0f || IsPositronic(uid))
            return;
        if (!TryComp<SchizophreniaComponent>(uid, out var comp))
            return;
        if (comp.Kind != PsychiatryIllnessKind.Schizophrenia)
            return;

        var pill = _cfg.GetCVar(CCCCVars.PsychiatryCoursePillUnits);
        if (pill <= 0f)
            return;

        if (TryComp<BloodstreamComponent>(uid, out var blood)
            && (float) GetReagentUnits(uid, blood, ClarityReagentId) > pill)
        {
            if (!comp.CourseSpoiled)
                _vomit.Vomit(uid);
            comp.CourseSpoiled = true;
            comp.CourseMetabolized = 0f;
            return;
        }

        if (comp.CourseSpoiled)
            return;

        if (comp.CourseNeeded <= 0)
            comp.CourseNeeded = CourseTablets(comp.Stage);

        comp.CourseMetabolized += units;
        while (comp.CourseMetabolized + 0.001f >= pill)
        {
            comp.CourseMetabolized = Math.Max(0f, comp.CourseMetabolized - pill);
            comp.CourseTaken++;
            if (comp.CourseTaken >= comp.CourseNeeded)
            {
                ClearIllness(uid, "NeuroClarity");
                return;
            }
        }
    }

    public bool TryStepStage(EntityUid uid)
    {
        if (IsPositronic(uid) || IsMindShielded(uid))
            return false;

        if (!TryComp<SchizophreniaComponent>(uid, out var schizo))
        {
            ApplyNew(uid, SchizophreniaStage.Latent, pillForced: false, "admin-step");
            return HasComp<SchizophreniaComponent>(uid);
        }

        if (schizo.Kind != PsychiatryIllnessKind.Schizophrenia || schizo.Stage >= SchizophreniaStage.Acute)
            return false;

        AdjustStage(uid, +1, "admin-step");
        return true;
    }

    public void ApplyPsychogenDose(EntityUid uid, float units)
    {
        if (units <= 0f || IsPositronic(uid) || IsMindShielded(uid) || OnTreatmentHold(uid))
            return;

        if (TryComp<SchizophreniaComponent>(uid, out var existing) && existing.Stage >= SchizophreniaStage.Acute)
            return;

        var dose = EnsureComp<PsychogenDoseComponent>(uid);
        dose.Units += units;
        while (dose.Units >= 5f)
        {
            if (!TryApplyOrEscalate(uid, SchizophreniaStage.Latent, pillForced: false, ignoreCooldown: true, reason: "PsychogenLatent"))
            {
                dose.Units = Math.Min(dose.Units, 4.99f);
                break;
            }

            dose.Units -= 5f;
        }
    }

    public void HoldOnset(EntityUid uid)
    {
        var tracker = EnsureComp<SchizophreniaOnsetTrackerComponent>(uid);
        var until = Timing.CurTime + TimeSpan.FromSeconds(_cfg.GetCVar(CCCCVars.PsychiatryOnsetCooldownSec));
        tracker.TreatmentHoldUntil = until;
        tracker.NextAllowedOnset = until;
        tracker.NextAllowedGasOnset = until;
        tracker.NextAsphyxiationRoll = until;
        tracker.NextRadiationRoll = until;
        tracker.NextShockRoll = until;
        tracker.NextAlcoholRoll = until;
        tracker.NextHarmStage = until;
        RemComp<PsychogenDoseComponent>(uid);
    }

    private void StampHarm(SchizophreniaOnsetTrackerComponent tracker)
    {
        tracker.NextHarmStage = Timing.CurTime + TimeSpan.FromSeconds(_cfg.GetCVar(CCCCVars.PsychiatryHarmStageCooldownSec));
    }

    private void OnHeadTrauma(Entity<PsychiatryHeadTraumaComponent> ent, ref ComponentStartup args)
    {
        if (IsPositronic(ent.Owner) || HasComp<SchizophreniaComponent>(ent.Owner))
            return;

        if (IsMindShielded(ent.Owner))
        {
            ent.Comp.Suppressed = true;
            return;
        }

        ApplyNew(ent.Owner, SchizophreniaStage.Incipient, pillForced: false, reason: "head-trauma");
    }

    private void TickMindShields()
    {
        var clear = new List<EntityUid>();
        var ill = EntityQueryEnumerator<SchizophreniaComponent, MindShieldComponent>();
        while (ill.MoveNext(out var uid, out var schizo, out _))
        {
            if (schizo.Kind == PsychiatryIllnessKind.Schizophrenia)
                clear.Add(uid);
        }

        foreach (var uid in clear)
        {
            if (TryComp<PsychiatryHeadTraumaComponent>(uid, out var trauma))
                trauma.Suppressed = true;
            ClearIllness(uid, "mind-shield");
        }

        var restore = new List<EntityUid>();
        var traumaQuery = EntityQueryEnumerator<PsychiatryHeadTraumaComponent>();
        while (traumaQuery.MoveNext(out var uid, out var trauma))
        {
            if (!trauma.Suppressed || HasComp<MindShieldComponent>(uid) || HasComp<SchizophreniaComponent>(uid) || IsPositronic(uid))
                continue;
            restore.Add(uid);
        }

        foreach (var uid in restore)
        {
            if (!TryComp<PsychiatryHeadTraumaComponent>(uid, out var trauma))
                continue;
            trauma.Suppressed = false;
            ApplyNew(uid, SchizophreniaStage.Incipient, pillForced: false, "head-trauma");
        }
    }

    private bool IsMindShielded(EntityUid uid)
    {
        return HasComp<MindShieldComponent>(uid);
    }

    private void OnRoleAdded(RoleAddedEvent args)
    {
        if (args.Mind.OwnedEntity is not { } body)
            return;
        if (!IsAntagImmune(body, pillForced: false))
            return;

        ClearIllness(body, "antag");
    }

    private bool OnTreatmentHold(EntityUid uid)
    {
        return TryComp<SchizophreniaOnsetTrackerComponent>(uid, out var tracker)
               && Timing.CurTime < tracker.TreatmentHoldUntil;
    }

    public void ClearIllness(EntityUid uid, string reason)
    {
        if (!TryComp<SchizophreniaComponent>(uid, out var comp))
            return;

        var kind = comp.Kind;
        var stage = comp.Stage;
        RemComp<SchizophreniaComponent>(uid);
        _adminLog.Add(LogType.Healed, LogImpact.High,
            $"{ToPrettyString(uid):player} {kind} from stage {stage} to stage {SchizophreniaStage.None} ({reason})");
    }

    public void AdjustStage(EntityUid uid, int delta, string reason)
    {
        if (!TryComp<SchizophreniaComponent>(uid, out var comp))
            return;

        var from = comp.Stage;
        var kind = comp.Kind;
        var next = delta < 0
            ? LowerStage(from, -delta)
            : ClampStage((int) from + delta);
        if (next == SchizophreniaStage.None)
        {
            RemComp<SchizophreniaComponent>(uid);
            _adminLog.Add(LogType.Healed, LogImpact.Medium,
                $"{ToPrettyString(uid):player} {kind} from stage {from} to stage {next} ({reason})");
            return;
        }
        if (next == from && delta > 0)
            return;

        if (delta < 0)
        {
            comp.CourseNeeded = 0;
            comp.CourseTaken = 0;
            comp.CourseMetabolized = 0f;
            comp.CourseSpoiled = false;
        }

        comp.Stage = next;
        comp.StageHealth = 1f;
        ScheduleAutoEscalate(comp, uid);
        SyncHallucinations(uid, comp.Stage);
        Dirty(uid, comp);
        _adminLog.Add(delta < 0 ? LogType.Healed : LogType.Damaged, LogImpact.Medium,
            $"{ToPrettyString(uid):player} {kind} from stage {from} to stage {next} ({reason})");
    }

    private void ScheduleAutoEscalate(SchizophreniaComponent comp, EntityUid uid)
    {
        var min = _cfg.GetCVar(CCCCVars.PsychiatryAutoEscalateMinSec);
        var max = _cfg.GetCVar(CCCCVars.PsychiatryAutoEscalateMaxSec);
        comp.NextAutoEscalate = Timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(min, max));
        Dirty(uid, comp);
    }

    private void ScheduleWhisper(SchizophreniaComponent comp, EntityUid uid)
    {
        var min = _cfg.GetCVar(CCCCVars.PsychiatryWhisperMinSec);
        var max = _cfg.GetCVar(CCCCVars.PsychiatryWhisperMaxSec);
        if (comp.Stage >= SchizophreniaStage.Acute)
        {
            min *= _cfg.GetCVar(CCCCVars.PsychiatryWhisperAcuteMinScale);
            max *= _cfg.GetCVar(CCCCVars.PsychiatryWhisperAcuteMaxScale);
        }

        comp.NextWhisper = Timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(min, max));
        Dirty(uid, comp);
    }

    private void TryWhisper(EntityUid uid, SchizophreniaComponent schizo)
    {
        if (Timing.CurTime < schizo.NextWhisper)
            return;

        ScheduleWhisper(schizo, uid);

        if (!TryComp<ActorComponent>(uid, out var actor))
            return;
        if (actor.PlayerSession.AttachedEntity != uid)
            return;
        if (!_proto.TryIndex(DefaultPhrases, out PsychiatryPhrasesPrototype? phrases))
            return;

        List<string> pool;
        if (schizo.Stage >= SchizophreniaStage.Acute && phrases.Crime.Count > 0)
            pool = phrases.Crime;
        else if (_random.Prob(_cfg.GetCVar(CCCCVars.PsychiatryWhisperCrimeChance)) && phrases.Crime.Count > 0)
            pool = phrases.Crime;
        else if (phrases.Mockery.Count > 0)
            pool = phrases.Mockery;
        else
            pool = phrases.Neutral;

        var victim = Identity.Name(uid, EntityManager);
        var asRadio = schizo.Stage >= SchizophreniaStage.Simple
                      && phrases.Radio.Count > 0
                      && _random.Prob(schizo.Stage >= SchizophreniaStage.Acute
                          ? _cfg.GetCVar(CCCCVars.PsychiatryWhisperRadioAcute)
                          : _cfg.GetCVar(CCCCVars.PsychiatryWhisperRadioSimple));

        string speaker;
        string message;
        var job = "";
        var jobColor = "#32cd32";
        if (asRadio && TryPickCrew(uid, out speaker, out job, out jobColor))
        {
            message = Loc.GetString(_random.Pick(phrases.Radio), ("name", victim));
        }
        else
        {
            asRadio = false;
            pool = new List<string>(pool);
            if (phrases.Addressed.Count > 0)
                pool.AddRange(phrases.Addressed);
            if (pool.Count == 0)
                return;

            var locId = _random.Pick(pool);
            message = Loc.GetString(locId, ("name", victim));
            if ((phrases.Crime.Contains(locId) || phrases.Mockery.Contains(locId))
                && TryPickCrew(uid, out speaker, out job, out jobColor))
            {
                asRadio = true;
            }
            else
            {
                speaker = phrases.FakeNames.Count > 0
                    ? Loc.GetString(_random.Pick(phrases.FakeNames))
                    : Loc.GetString("psychiatry-fake-name-default");
                job = "";
                jobColor = "#32cd32";
            }
        }

        var whisper = new PsychiatryWhisperEvent(speaker, message, null)
        {
            AsRadio = asRadio,
            Job = job,
            JobColor = jobColor,
        };
        RaiseNetworkEvent(whisper, Filter.SinglePlayer(actor.PlayerSession));
        var heard = new PsychiatryBrainActivityEvent(PsychiatryBrainRegion.Hearing, _cfg.GetCVar(CCCCVars.PsychiatryUnrealHearing));
        RaiseLocalEvent(uid, ref heard);
        var fear = new PsychiatryBrainActivityEvent(PsychiatryBrainRegion.Fear, _cfg.GetCVar(CCCCVars.PsychiatryUnrealFear));
        RaiseLocalEvent(uid, ref fear);
    }

    private bool TryPickCrew(EntityUid victim, out string speaker, out string job, out string jobColor)
    {
        speaker = "";
        job = "";
        jobColor = "#32cd32";

        var crew = new List<EntityUid>();
        var query = EntityQueryEnumerator<ActorComponent, HumanoidAppearanceComponent>();
        while (query.MoveNext(out var crewUid, out _, out _))
        {
            if (crewUid == victim)
                continue;

            crew.Add(crewUid);
        }

        if (crew.Count == 0)
            return false;

        var pick = _random.Pick(crew);
        speaker = Identity.Name(pick, EntityManager);
        if (!_inventory.TryGetSlotEntity(pick, "id", out var idUid))
            return true;

        if (TryComp<PdaComponent>(idUid, out var pda) && pda.ContainedId is { } card)
            idUid = card;

        if (!TryComp<IdCardComponent>(idUid, out var id))
            return true;

        if (!string.IsNullOrWhiteSpace(id.LocalizedJobTitle))
            job = id.LocalizedJobTitle;

        if (TryDepartmentColor(id, idUid.Value, out var hex))
            jobColor = hex;
        else if (_inventory.TryGetSlotEntity(pick, "ears", out var ears)
                 && TryComp<HeadsetComponent>(ears, out var headset))
            jobColor = headset.Color.ToHexNoAlpha();

        return true;
    }

    /// <summary>
    /// Department colors are the radio colors: cargo brown, medical blue, security blue.
    /// Preset cards fill <see cref="IdCardComponent.JobDepartments"/> and leave JobPrototype empty.
    /// </summary>
    private bool TryDepartmentColor(IdCardComponent id, EntityUid idUid, out string hex)
    {
        if (PickDepartment(id.JobDepartments, out var department))
        {
            hex = department.Color.ToHexNoAlpha();
            return true;
        }

        ProtoId<JobPrototype>? jobId = id.JobPrototype;
        if (jobId == null && TryComp<PresetIdCardComponent>(idUid, out var preset))
            jobId = preset.JobName;

        if (jobId is { } job
            && (_jobs.TryGetPrimaryDepartment(job, out department)
                || _jobs.TryGetDepartment(job, out department)))
        {
            hex = department.Color.ToHexNoAlpha();
            return true;
        }

        hex = "";
        return false;
    }

    private bool PickDepartment(List<ProtoId<DepartmentPrototype>> departments, out DepartmentPrototype department)
    {
        DepartmentPrototype? primary = null;
        DepartmentPrototype? fallback = null;
        foreach (var departmentId in departments)
        {
            if (!_proto.TryIndex(departmentId, out DepartmentPrototype? proto))
                continue;

            if (proto.Primary)
            {
                primary = proto;
                break;
            }

            fallback ??= proto;
        }

        department = (primary ?? fallback)!;
        return primary != null || fallback != null;
    }
    private TimeSpan GasCooldown()
    {
        return TimeSpan.FromSeconds(_cfg.GetCVar(CCCCVars.PsychiatryGasOnsetSec));
    }

    private FixedPoint2 GetReagentUnits(EntityUid uid, BloodstreamComponent blood, ProtoId<ReagentPrototype> reagent)
    {
        Entity<SolutionComponent>? soln = null;
        if (!_solutions.ResolveSolution(uid, blood.BloodSolutionName, ref soln, out var solution))
            return FixedPoint2.Zero;
        return solution.GetTotalPrototypeQuantity(reagent);
    }

    private void OnMobState(Entity<SchizophreniaComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            ClearPhantom(ent);
        SyncHallucinations(ent.Owner, ent.Comp.Stage);
    }

    private void TickPhantom(EntityUid uid, SchizophreniaComponent schizo)
    {
        if (schizo.Stage < SchizophreniaStage.Simple || IsDead(uid))
        {
            if (schizo.PhantomAmount > 0f)
                ClearPhantom((uid, schizo));
            return;
        }

        if (schizo.PhantomAmount > 0f && Timing.CurTime >= schizo.PhantomUntil)
            ClearPhantom((uid, schizo));

        if (schizo.NextPhantom == TimeSpan.Zero)
        {
            schizo.NextPhantom = Timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(12f, 28f));
            return;
        }

        if (Timing.CurTime < schizo.NextPhantom || schizo.PhantomAmount > 0f)
            return;

        schizo.NextPhantom = Timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(18f, 36f));
        if (!TryComp<DamageableComponent>(uid, out var damage))
            return;
        if (!Thresholds.TryGetIncapThreshold(uid, out var incap) || incap <= 0)
            return;

        var room = (float) (incap.Value - damage.TotalDamage) - 1f;
        if (room < _cfg.GetCVar(CCCCVars.PsychiatryPhantomMinRoom))
            return;

        if (!_proto.TryIndex(DefaultPhrases, out PsychiatryPhrasesPrototype? phrases) || phrases.PhantomTypes.Count == 0)
            return;

        var minFraction = _cfg.GetCVar(CCCCVars.PsychiatryPhantomMinFraction);
        var maxFraction = MathF.Max(minFraction, _cfg.GetCVar(CCCCVars.PsychiatryPhantomMaxFraction));
        var type = _random.Pick(phrases.PhantomTypes);
        schizo.PhantomType = type;
        schizo.PhantomAmount = room * _random.NextFloat(minFraction, maxFraction);
        schizo.PhantomUntil = Timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(7f, 14f));
        Dirty(uid, schizo);
        Thresholds.VerifyThresholds(uid);
    }

    private void ClearPhantom(Entity<SchizophreniaComponent> ent)
    {
        if (ent.Comp.PhantomAmount <= 0f && ent.Comp.PhantomType.Length == 0)
            return;

        ent.Comp.PhantomAmount = 0f;
        ent.Comp.PhantomType = "";
        Dirty(ent);
        Thresholds.VerifyThresholds(ent.Owner);
    }

    public bool TryGarbleSpeech(EntityUid listener, string message, string wrapped, out string heard, out string heardWrapped)
    {
        heard = message;
        heardWrapped = wrapped;
        if (!TryComp<SchizophreniaComponent>(listener, out var schizo) || schizo.Stage < SchizophreniaStage.Simple)
            return false;
        if (IsDead(listener) || HasComp<GhostComponent>(listener))
            return false;

        heard = GarbleWords(message);
        var from = FormattedMessage.EscapeText(message);
        var to = FormattedMessage.EscapeText(heard);
        heardWrapped = from.Length > 0 && wrapped.Contains(from) ? wrapped.Replace(from, to) : wrapped;
        return true;
    }

    private string GarbleWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var parts = text.Split(' ');
        for (var i = 0; i < parts.Length; i++)
        {
            var letters = 0;
            foreach (var ch in parts[i])
            {
                if (char.IsLetter(ch))
                    letters++;
            }

            if (letters == 0)
                continue;

            parts[i] = FitSyllables(Math.Max(2, letters));
        }

        return string.Join(' ', parts);
    }

    private string FitSyllables(int length)
    {
        if (!_proto.TryIndex(DefaultPhrases, out PsychiatryPhrasesPrototype? phrases) || phrases.Syllables.Count == 0)
            return "";

        var built = "";
        while (built.Length < length)
            built += _random.Pick(phrases.Syllables);

        return built.Length == length ? built : built.Substring(0, length);
    }

    private bool IsDead(EntityUid uid)
    {
        return TryComp<MobStateComponent>(uid, out var mob) && mob.CurrentState == MobState.Dead;
    }

    private void OnIllnessStartup(Entity<SchizophreniaComponent> ent, ref ComponentStartup args)
    {
        SyncHallucinations(ent.Owner, ent.Comp.Stage);
    }

    private void OnIllnessShutdown(Entity<SchizophreniaComponent> ent, ref ComponentShutdown args)
    {
        if (!HasComp<PsychiatryParacusiaComponent>(ent.Owner))
            return;

        RemComp<ParacusiaComponent>(ent.Owner);
        RemComp<PsychiatryParacusiaComponent>(ent.Owner);
    }

    private void SyncHallucinations(EntityUid uid, SchizophreniaStage stage)
    {
        if (stage < SchizophreniaStage.Acute || IsDead(uid))
        {
            if (!HasComp<PsychiatryParacusiaComponent>(uid))
                return;

            RemComp<ParacusiaComponent>(uid);
            RemComp<PsychiatryParacusiaComponent>(uid);
            return;
        }

        if (HasComp<ParacusiaComponent>(uid) && !HasComp<PsychiatryParacusiaComponent>(uid))
            return;

        EnsureComp<PsychiatryParacusiaComponent>(uid);
        var paracusia = EnsureComp<ParacusiaComponent>(uid);
        _paracusia.SetSounds(uid, HallucinationSounds, paracusia);
        _paracusia.SetDistance(uid, _cfg.GetCVar(CCCCVars.PsychiatryParacusiaDistance), paracusia);
        _paracusia.SetTime(
            uid,
            _cfg.GetCVar(CCCCVars.PsychiatryParacusiaAcuteMinSec),
            _cfg.GetCVar(CCCCVars.PsychiatryParacusiaAcuteMaxSec),
            paracusia);
    }

    private void RemoveReagent(EntityUid uid, BloodstreamComponent blood, ProtoId<ReagentPrototype> reagent)
    {
        Entity<SolutionComponent>? soln = null;
        if (!_solutions.ResolveSolution(uid, blood.BloodSolutionName, ref soln, out _))
            return;
        _solutions.RemoveReagent(soln.Value, reagent, FixedPoint2.New(1000));
    }
}
