using Content.Shared.DeadSpace.Surgery;
using Content.Shared.DeadSpace.Surgery.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Tools.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgerySystem : SharedSurgerySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryTargetComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SurgeryTargetComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<SurgeryTargetComponent, SurgeryDoAfterEvent>(OnDoAfter);

        SubscribeLocalEvent<SurgeryDirtCleanerComponent, AfterInteractEvent>(OnCleanerAfterInteract);
        SubscribeLocalEvent<SurgeryDirtCleanerComponent, UseInHandEvent>(OnCleanerUseInHand);
    }

    private void OnInteractUsing(Entity<SurgeryTargetComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryStartSurgery(ent.Owner, args.User, args.Used))
            args.Handled = true;
    }

    private void OnInteractHand(Entity<SurgeryTargetComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (TryStartSurgery(ent.Owner, args.User, null))
            args.Handled = true;
    }

    private bool TryStartSurgery(EntityUid patient, EntityUid user, EntityUid? tool)
    {
        TryComp<SurgeryProgressComponent>(patient, out var progress);

        if (!TryFindStep(patient, progress, user, tool, out var attempt))
        {
            if (progress is { Active: not null })
                _popup.PopupEntity(Loc.GetString("surgery-error-wrong-tool"), patient, user);

            return false;
        }

        if (user == patient)
        {
            _popup.PopupEntity(Loc.GetString("surgery-error-self"), patient, user);
            return true;
        }

        EnsureComp<SurgeryProgressComponent>(patient);

        if (attempt.Variant.StartMessage is { } startMessage)
            _popup.PopupEntity(Loc.GetString(startMessage), patient, user);

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            GetDuration(attempt.Variant, tool),
            new SurgeryDoAfterEvent
            {
                Surgery = attempt.Surgery.ID,
                StepIndex = attempt.StepIndex,
                Tool = GetNetEntity(tool),
            },
            patient,
            target: patient,
            used: tool)
        {
            NeedHand = attempt.Variant.NeedHand,
            BreakOnMove = attempt.Variant.BreakOnMove,
            BreakOnWeightlessMove = false,
            BreakOnDamage = attempt.Variant.BreakOnDamage,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        _doAfter.TryStartDoAfter(doAfter);
        return true;
    }

    private TimeSpan GetDuration(SurgeryStepVariant variant, EntityUid? tool)
    {
        var speed = 1f;

        if (tool is { } used && TryComp<ToolComponent>(used, out var toolComponent))
            speed = Math.Max(toolComponent.SpeedModifier, 0.05f);

        return TimeSpan.FromSeconds(Math.Max(variant.Duration / speed, 0.1f));
    }

    private void OnDoAfter(Entity<SurgeryTargetComponent> ent, ref SurgeryDoAfterEvent args)
    {
        if (args.Handled || args.Target is not { } patient)
            return;

        args.Handled = true;

        var tool = args.Used;

        if (tool == null && args.Tool is { } netTool && TryGetEntity(netTool, out var resolved))
            tool = resolved;

        if (!_prototype.TryIndex(args.Surgery, out var surgery) ||
            !TryComp<SurgeryProgressComponent>(patient, out var progress))
        {
            return;
        }

        if (!TryGetStepIndex(surgery, progress, out var stepIndex) || stepIndex != args.StepIndex)
            return;

        var step = _prototype.Index(surgery.Steps[stepIndex]);
        var context = CreateContext(patient, args.User, tool, surgery, step, stepIndex);

        if (!TryGetVariant(step, tool, context, out var variant))
            return;

        if (args.Cancelled)
        {
            ApplyEffects(variant.FailEffects, context, args.User);
            return;
        }

        ApplyEffects(variant.Effects, context, args.User);

        if (variant.Sound is { } sound)
            _audio.PlayPvs(sound, patient);

        if (variant.SuccessMessage is { } successMessage)
            _popup.PopupEntity(Loc.GetString(successMessage), patient, args.User);

        var stepCompleted = new SurgeryStepCompletedEvent(context);
        RaiseLocalEvent(patient, ref stepCompleted);

        RollComplications(surgery, step, variant, context);
        Advance(patient, progress, surgery, context);
    }

    private void Advance(EntityUid patient, SurgeryProgressComponent progress, SurgeryPrototype surgery, SurgeryContext context)
    {
        var wasStarted = progress.Active == surgery.ID;

        if (!wasStarted)
        {
            progress.Active = surgery.ID;
            var started = new SurgeryStartedEvent(context);
            RaiseLocalEvent(patient, ref started);
        }

        var next = context.StepIndex + 1;

        if (next < surgery.Steps.Length)
        {
            progress.StepIndex = next;
            Dirty(patient, progress);
            return;
        }

        progress.Active = null;
        progress.StepIndex = 0;

        if (!progress.Completed.Contains(surgery.ID))
            progress.Completed.Add(surgery.ID);

        Dirty(patient, progress);

        ApplyEffects(surgery.Effects, context, context.Surgeon);

        var completed = new SurgeryCompletedEvent(context);
        RaiseLocalEvent(patient, ref completed);
    }

    private void RollComplications(
        SurgeryPrototype surgery,
        SurgeryStepPrototype step,
        SurgeryStepVariant variant,
        SurgeryContext context)
    {
        RollComplicationList(surgery.Complications, context);
        RollComplicationList(step.Complications, context);
        RollComplicationList(variant.Complications, context);
    }

    private void RollComplicationList(ProtoId<SurgeryComplicationPrototype>[] complications, SurgeryContext context)
    {
        foreach (var id in complications)
        {
            if (!_prototype.TryIndex(id, out var complication))
                continue;

            var chance = GetComplicationChance(complication, context);

            if (chance <= 0f || !_random.Prob(chance))
                continue;

            ApplyEffects(complication.Effects, context, context.Surgeon);

            if (complication.Message is { } message && context.Surgeon is { } surgeon)
                _popup.PopupEntity(Loc.GetString(message), context.Patient, surgeon, PopupType.MediumCaution);

            if (complication.PatientMessage is { } patientMessage)
                _popup.PopupEntity(Loc.GetString(patientMessage), context.Patient, context.Patient, PopupType.MediumCaution);

            var triggered = new SurgeryComplicationTriggeredEvent(context, complication, chance);
            RaiseLocalEvent(context.Patient, ref triggered);
        }
    }

    private void OnCleanerAfterInteract(Entity<SurgeryDirtCleanerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target || target == args.User)
            return;

        if (TryClean(ent, target, args.User))
            args.Handled = true;
    }

    private void OnCleanerUseInHand(Entity<SurgeryDirtCleanerComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (TryClean(ent, args.User, args.User))
            args.Handled = true;
    }

    private bool TryClean(Entity<SurgeryDirtCleanerComponent> cleaner, EntityUid target, EntityUid user)
    {
        if (!TryComp<SurgeryDirtinessComponent>(target, out var dirt) || dirt.Amount <= 0f)
            return false;

        AddDirt(target, -cleaner.Comp.Amount);

        var message = user == target ? "surgery-clean-self" : "surgery-clean-other";
        _popup.PopupEntity(Loc.GetString(message), target, user, PopupType.Medium);
        return true;
    }
}
