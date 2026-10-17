using Content.Shared.Buckle.Components;
using Content.Shared.DeadSpace.Surgery.Conditions;
using Content.Shared.DeadSpace.Surgery.Effects;
using Content.Shared.DeadSpace.Surgery.Prototypes;
using Content.Shared.Random.Helpers;
using Content.Shared.Tools.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Surgery;

public partial class SharedSurgerySystem : EntitySystem, ISurgeryConditionRaiser, ISurgeryEffectRaiser
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedToolSystem _tools = default!;

    private readonly List<SurgeryPrototype> _surgeries = [];
    private bool _surgeriesDirty = true;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<SurgeryPrototype>() || args.WasModified<SurgeryStepPrototype>())
            _surgeriesDirty = true;
    }

    public IReadOnlyList<SurgeryPrototype> Surgeries
    {
        get
        {
            if (_surgeriesDirty)
                RefreshSurgeries();

            return _surgeries;
        }
    }

    private void RefreshSurgeries()
    {
        _surgeries.Clear();

        foreach (var surgery in _prototype.EnumeratePrototypes<SurgeryPrototype>())
        {
            _surgeries.Add(surgery);
        }

        _surgeries.Sort((a, b) =>
        {
            var byPriority = b.Priority.CompareTo(a.Priority);
            return byPriority != 0 ? byPriority : string.CompareOrdinal(a.ID, b.ID);
        });

        _surgeriesDirty = false;
    }

    public SurgeryContext CreateContext(
        EntityUid patient,
        EntityUid? surgeon = null,
        EntityUid? tool = null,
        SurgeryPrototype? surgery = null,
        SurgeryStepPrototype? step = null,
        int stepIndex = 0)
    {
        EntityUid? table = null;

        if (TryComp<BuckleComponent>(patient, out var buckle) &&
            buckle.BuckledTo is { } strap &&
            HasComp<OperatingTableComponent>(strap))
        {
            table = strap;
        }

        return new SurgeryContext(patient, surgeon, tool, table, surgery, step, stepIndex);
    }

    public bool IsOperable(EntityUid patient)
    {
        return !HasComp<SurgeryImmuneComponent>(patient);
    }

    public bool ConditionsMet(SurgeryCondition[]? conditions, SurgeryContext context)
    {
        if (conditions == null)
            return true;

        foreach (var condition in conditions)
        {
            if (!condition.Check(context, this))
                return false;
        }

        return true;
    }

    public bool RaiseConditionEvent<T>(T condition, SurgeryContext context) where T : SurgeryConditionBase<T>
    {
        var ev = new SurgeryConditionEvent<T>(condition, context);
        RaiseLocalEvent(ref ev);
        return ev.Result;
    }

    public bool ToolMatches(SurgeryStepVariant variant, EntityUid? tool)
    {
        var needsTool = variant.Tool != null || variant.Qualities.Count > 0;

        if (!needsTool)
            return tool == null;

        if (tool is not { } used)
            return false;

        if (variant.Tool is { } whitelist && !_whitelist.IsValid(whitelist, used))
            return false;

        if (variant.Qualities.Count == 0)
            return true;

        foreach (var quality in variant.Qualities)
        {
            if (!_tools.HasQuality(used, quality.Id))
                return false;
        }

        return true;
    }

    public bool TryGetVariant(
        SurgeryStepPrototype step,
        EntityUid? tool,
        SurgeryContext context,
        out SurgeryStepVariant variant)
    {
        foreach (var candidate in step.Variants)
        {
            if (!ToolMatches(candidate, tool))
                continue;

            if (!ConditionsMet(candidate.Conditions, context))
                continue;

            variant = candidate;
            return true;
        }

        variant = default!;
        return false;
    }

    public bool TryGetStepIndex(SurgeryPrototype surgery, SurgeryProgressComponent progress, out int stepIndex)
    {
        stepIndex = progress.Active == surgery.ID ? progress.StepIndex : 0;
        return stepIndex >= 0 && stepIndex < surgery.Steps.Length;
    }

    public bool IsAvailable(
        SurgeryPrototype surgery,
        SurgeryProgressComponent progress,
        EntityUid patient,
        EntityUid? surgeon,
        EntityUid? tool)
    {
        if (progress.Active is { } active && active != surgery.ID)
            return false;

        if (!surgery.Repeatable && progress.IsCompleted(surgery.ID))
            return false;

        foreach (var requirement in surgery.Requires)
        {
            if (!progress.IsCompleted(requirement))
                return false;
        }

        var context = CreateContext(patient, surgeon, tool, surgery);
        return ConditionsMet(surgery.Conditions, context);
    }

    public bool TryFindStep(
        EntityUid patient,
        SurgeryProgressComponent? progress,
        EntityUid? surgeon,
        EntityUid? tool,
        out SurgeryAttempt attempt)
    {
        attempt = default;

        if (!IsOperable(patient))
            return false;

        progress ??= new SurgeryProgressComponent();

        if (progress.Active is { } activeId && _prototype.TryIndex(activeId, out var activeSurgery))
            return TryFindStepIn(activeSurgery, progress, patient, surgeon, tool, out attempt);

        foreach (var surgery in Surgeries)
        {
            if (!IsAvailable(surgery, progress, patient, surgeon, tool))
                continue;

            if (TryFindStepIn(surgery, progress, patient, surgeon, tool, out attempt))
                return true;
        }

        return false;
    }

    private bool TryFindStepIn(
        SurgeryPrototype surgery,
        SurgeryProgressComponent progress,
        EntityUid patient,
        EntityUid? surgeon,
        EntityUid? tool,
        out SurgeryAttempt attempt)
    {
        attempt = default;

        if (!TryGetStepIndex(surgery, progress, out var stepIndex))
            return false;

        var step = _prototype.Index(surgery.Steps[stepIndex]);
        var context = CreateContext(patient, surgeon, tool, surgery, step, stepIndex);

        if (!TryGetVariant(step, tool, context, out var variant))
            return false;

        attempt = new SurgeryAttempt(surgery, step, variant, stepIndex);
        return true;
    }

    public void ApplyEffects(SurgeryEffect[]? effects, SurgeryContext context, EntityUid? user = null, float scale = 1f)
    {
        if (effects == null)
            return;

        foreach (var effect in effects)
        {
            if (effect.Probability <= 0f)
                continue;

            if (effect.Probability < 1f &&
                !SharedRandomExtensions.PredictedProb(
                    _timing,
                    effect.Probability,
                    GetNetEntity(context.Patient),
                    GetNetEntity(user)))
            {
                continue;
            }

            effect.RaiseEvent(context, this, scale, user);
        }
    }

    public void RaiseEffectEvent<T>(T effect, SurgeryContext context, float scale, EntityUid? user)
        where T : SurgeryEffectBase<T>
    {
        var ev = new SurgeryEffectEvent<T>(effect, context, scale, user);
        RaiseLocalEvent(ref ev);
    }

    public float GetComplicationChance(SurgeryComplicationPrototype complication, SurgeryContext context)
    {
        var chance = complication.Chance;

        foreach (var modifier in complication.Modifiers)
        {
            if (!ConditionsMet(modifier.Conditions, context))
                continue;

            chance = (chance + modifier.Add) * modifier.Multiply;
        }

        var ev = new SurgeryComplicationRollEvent(context, complication) { Chance = chance };
        RaiseLocalEvent(ref ev);

        return Math.Clamp(ev.Chance, 0f, 1f);
    }

    public float GetDirt(EntityUid uid, SurgeryDirtinessComponent? dirt = null)
    {
        return Resolve(uid, ref dirt, false) ? dirt.Amount : 0f;
    }

    public void AddDirt(EntityUid uid, float amount)
    {
        if (MathHelper.CloseTo(amount, 0f))
            return;

        var dirt = EnsureComp<SurgeryDirtinessComponent>(uid);
        dirt.Amount = Math.Clamp(dirt.Amount + amount, 0f, dirt.Max);
        Dirty(uid, dirt);
    }
}
