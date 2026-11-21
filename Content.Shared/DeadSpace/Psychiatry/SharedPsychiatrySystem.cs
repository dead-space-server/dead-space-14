// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using CVars = Content.Shared.DeadSpace.CCCCVars.CCCCVars;
using Content.Shared.Alert;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Psychiatry;

public abstract partial class SharedPsychiatrySystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedRoleSystem _roles = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] protected MobThresholdSystem Thresholds = default!;
    [Dependency] protected IGameTiming Timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SchizophreniaComponent, BeforeAlertSeverityCheckEvent>(OnPhantomAlert);
    }

    public const string SpecialPillReagentId = "Schizotoxin";
    public const string ClarityReagentId = "NeuroClarity";
    public const string PositronicSpecies = "IPC";

    public bool IsPositronic(EntityUid uid)
    {
        return TryComp<HumanoidAppearanceComponent>(uid, out var humanoid)
               && humanoid.Species == PositronicSpecies;
    }

    public bool IsAntagImmune(EntityUid uid, bool pillForced, bool gas = false)
    {
        var mode = (PsychiatryAntagImmunity) _cfg.GetCVar(CVars.PsychiatryAntagImmunityMode);
        if (!Enum.IsDefined(mode))
            mode = PsychiatryAntagImmunity.Partial;
        if (mode == PsychiatryAntagImmunity.None)
            return false;
        if (!_mind.TryGetMind(uid, out var mindId, out _))
            return false;
        if (!_roles.MindIsAntagonist(mindId))
            return false;
        if (mode == PsychiatryAntagImmunity.Full)
            return true;

        return !pillForced && !gas;
    }

    public static int CourseTablets(SchizophreniaStage stage) => stage switch
    {
        SchizophreniaStage.Incipient => 1,
        SchizophreniaStage.Latent => 1,
        SchizophreniaStage.Simple => 2,
        SchizophreniaStage.Acute => 3,
        _ => 1,
    };

    public static SchizophreniaStage ClampStage(int stage) =>
        (SchizophreniaStage) Math.Clamp(stage, 0, (int) SchizophreniaStage.Acute);

    public static SchizophreniaStage LowerStage(SchizophreniaStage current, int by)
    {
        var next = (int) current - by;
        return next <= 0 ? SchizophreniaStage.None : ClampStage(next);
    }

    private void OnPhantomAlert(Entity<SchizophreniaComponent> ent, ref BeforeAlertSeverityCheckEvent ev)
    {
        if (ent.Comp.Stage < SchizophreniaStage.Simple || ent.Comp.PhantomAmount <= 0f)
            return;
        if (!TryComp<MobStateComponent>(ent, out var mob) || mob.CurrentState != MobState.Alive)
            return;
        if (!TryComp<DamageableComponent>(ent, out var damage))
            return;
        if (!Thresholds.TryGetIncapThreshold(ent, out var incap) || incap <= 0)
            return;
        if (!_alerts.TryGet(ev.CurrentAlert, out var alert) || !alert.SupportsSeverity)
            return;

        var shown = damage.TotalDamage + FixedPoint2.New(ent.Comp.PhantomAmount);
        var ceiling = incap.Value - FixedPoint2.New(1);
        if (shown > ceiling)
            shown = ceiling;
        if (shown < damage.TotalDamage)
            shown = damage.TotalDamage;

        if (!Thresholds.TryGetPercentageForState(ent, MobState.Critical, shown, out var percentage))
            return;

        percentage = FixedPoint2.Clamp(percentage.Value, 0, 1);
        var min = _alerts.GetMinSeverity(ev.CurrentAlert);
        var max = _alerts.GetMaxSeverity(ev.CurrentAlert);
        var severity = (short) MathF.Round(MathHelper.Lerp(min, max, percentage.Value.Float()));
        var realPct = FixedPoint2.Clamp(damage.TotalDamage / incap.Value, 0, 1);
        var realSeverity = (short) MathF.Round(MathHelper.Lerp(min, max, realPct.Float()));
        if (severity <= realSeverity && realSeverity < max)
            severity = (short) (realSeverity + 1);
        ev.Severity = severity;
        ev.CancelUpdate = true;
    }
}
