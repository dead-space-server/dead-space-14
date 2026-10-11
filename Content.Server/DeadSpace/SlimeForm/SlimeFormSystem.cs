// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Polymorph.Systems;
using Content.Shared.Actions;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Corvax.TTS;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.DeadSpace.SlimeForm;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.Polymorph;
using Content.Shared.Standing;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.SlimeForm;

public sealed class SlimeFormSystem : EntitySystem
{
    private static readonly ProtoId<PolymorphPrototype> SlimePolymorph = "SlimePersonForm";

    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedCuffableSystem _cuffs = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlimeFormComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SlimeFormComponent, SlimeFormActionEvent>(OnToggle);
        SubscribeLocalEvent<SlimeFormComponent, SlimeFormEnterDoAfterEvent>(OnEnter);
        SubscribeLocalEvent<SlimeFormComponent, SlimeFormExitDoAfterEvent>(OnExit);
        SubscribeLocalEvent<SlimeFormComponent, PolymorphedEvent>(OnPolymorphed);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<SlimeFormComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Busy || comp.Limbs.Count == 0 || _timing.CurTime < comp.NextLimbDrop)
                continue;

            DropNextLimb((uid, comp));
        }
    }

    private void OnMapInit(Entity<SlimeFormComponent> ent, ref MapInitEvent args)
    {
        EntityUid? action = null;
        if (_actions.AddAction(ent, ref action, ent.Comp.FormAction))
            ent.Comp.Action = action;
    }

    private void OnToggle(Entity<SlimeFormComponent> ent, ref SlimeFormActionEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Busy)
        {
            args.Handled = true;
            return;
        }

        args.Handled = ent.Comp.IsSlime
            ? StartChange(ent, new SlimeFormExitDoAfterEvent(), dropLimbs: false)
            : StartChange(ent, new SlimeFormEnterDoAfterEvent(), dropLimbs: true);
    }

    private void OnEnter(Entity<SlimeFormComponent> ent, ref SlimeFormEnterDoAfterEvent args)
    {
        var apply = !args.Cancelled && !_mobState.IsDead(ent);
        FinishChange(ent);
        if (!apply)
            return;

        DropRestraints(ent);
        _polymorph.PolymorphEntity(ent, SlimePolymorph);
    }

    private void OnExit(Entity<SlimeFormComponent> ent, ref SlimeFormExitDoAfterEvent args)
    {
        var apply = !args.Cancelled && !_mobState.IsDead(ent);
        FinishChange(ent);
        if (!apply)
            return;

        _polymorph.Revert(ent.Owner);
    }

    private void OnPolymorphed(Entity<SlimeFormComponent> ent, ref PolymorphedEvent args)
    {
        if (args.IsRevert)
        {
            if (TryComp<SlimeFormComponent>(args.NewEntity, out var form) && form.Action is { } action)
                _actions.SetCooldown(action, TimeSpan.FromSeconds(ent.Comp.FormCooldown));

            return;
        }

        if (!TryComp<HumanoidAppearanceComponent>(args.OldEntity, out var appearance))
            return;

        var body = appearance.SkinColor.WithAlpha(1f);
        var face = appearance.EyeColor.WithAlpha(1f);
        _appearance.SetData(args.NewEntity, SlimeFormVisuals.BodyColor, body);
        _appearance.SetData(args.NewEntity, SlimeFormVisuals.FaceColor, face);

        if (!TryComp<TTSComponent>(args.NewEntity, out var newTts))
            return;

        var voice = TryComp<TTSComponent>(args.OldEntity, out var oldTts) && oldTts.VoicePrototypeId != null
            ? oldTts.VoicePrototypeId
            : appearance.Voice.Id;
        newTts.VoicePrototypeId = voice;
    }

    private bool StartChange(Entity<SlimeFormComponent> ent, DoAfterEvent doAfterEvent, bool dropLimbs)
    {
        if (ent.Comp.Busy || _mobState.IsDead(ent))
            return false;

        var time = TimeSpan.FromSeconds(Math.Max(ent.Comp.FormDuration, 0f));
        var doAfter = new DoAfterArgs(EntityManager, ent, time, doAfterEvent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            RequireCanInteract = false,
            BreakOnWeightlessMove = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        ent.Comp.Busy = true;
        _jitter.DoJitter(ent, time, true, 4f, 8f);
        if (dropLimbs)
            QueueLimbs(ent, time);

        return true;
    }

    private void FinishChange(Entity<SlimeFormComponent> ent)
    {
        ent.Comp.Busy = false;
        RemComp<JitteringComponent>(ent);
        RestoreLimbs(ent);
    }

    private void QueueLimbs(Entity<SlimeFormComponent> ent, TimeSpan time)
    {
        ent.Comp.Limbs.Clear();
        var ordered = new List<(EntityUid Id, int Order)>();
        foreach (var (id, part) in _body.GetBodyChildren(ent.Owner))
        {
            var order = part.PartType switch
            {
                BodyPartType.Hand => 0,
                BodyPartType.Foot => 0,
                BodyPartType.Arm => 1,
                BodyPartType.Leg => 1,
                _ => -1,
            };

            if (order < 0 || part.IsVital)
                continue;

            if (_body.GetParentPartAndSlotOrNull(id) == null)
                continue;

            ordered.Add((id, order));
        }

        ordered.Sort((a, b) => a.Order.CompareTo(b.Order));
        foreach (var (id, _) in ordered)
        {
            if (_body.GetParentPartAndSlotOrNull(id) is not { } slot)
                continue;

            ent.Comp.Limbs.Add(new SlimeFormLimb(id, slot.Parent, slot.Slot));
        }

        if (ent.Comp.Limbs.Count == 0)
            return;

        ent.Comp.LimbInterval = time.Ticks > 0
            ? time / (double) (ent.Comp.Limbs.Count + 1)
            : TimeSpan.Zero;
        ent.Comp.NextLimbDrop = _timing.CurTime + ent.Comp.LimbInterval;
    }

    private void DropNextLimb(Entity<SlimeFormComponent> ent)
    {
        var limb = ent.Comp.Limbs[0];
        ent.Comp.Limbs.RemoveAt(0);
        ent.Comp.NextLimbDrop = _timing.CurTime + ent.Comp.LimbInterval;

        if (!TryComp(limb.Part, out TransformComponent? xform) || !Exists(limb.Parent))
            return;

        ent.Comp.Fallen.Add(limb);
        _transform.AttachToGridOrMap(limb.Part, xform);
        ColorLimb(ent.Owner, limb.Part);
    }

    private void ColorLimb(EntityUid body, EntityUid part)
    {
        if (!TryComp<HumanoidAppearanceComponent>(body, out var appearance))
            return;

        EnsureComp<AppearanceComponent>(part);
        _appearance.SetData(part, SlimeFormVisuals.BodyColor, appearance.SkinColor.WithAlpha(1f));
    }

    private void RestoreLimbs(Entity<SlimeFormComponent> ent)
    {
        ent.Comp.Limbs.Clear();
        for (var i = ent.Comp.Fallen.Count - 1; i >= 0; i--)
        {
            var limb = ent.Comp.Fallen[i];
            if (!Exists(limb.Part) || !Exists(limb.Parent))
                continue;

            _body.AttachPart(limb.Parent, limb.Slot, limb.Part);
        }

        ent.Comp.Fallen.Clear();
        if (TryComp<StandingStateComponent>(ent, out var standing) && !standing.Standing)
            _standing.Stand(ent, standing, force: true);
    }

    private void DropRestraints(EntityUid uid)
    {
        if (!_cuffs.TryGetAllCuffs(uid, out var cuffs))
            return;

        foreach (var cuff in cuffs)
            _cuffs.Uncuff(uid, null, cuff);
    }
}
