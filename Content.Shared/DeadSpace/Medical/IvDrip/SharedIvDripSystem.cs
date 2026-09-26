// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Foldable;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Standing;
using Content.Shared.Tag;
using Content.Shared.Timing;
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Medical.IvDrip;

public abstract class SharedIvDripSystem : EntitySystem
{
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ReactiveSystem _reactive = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPuddleSystem _puddle = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;

    private static readonly ProtoId<ReagentPrototype> Blood = "Blood";
    private static readonly ProtoId<TagPrototype> BloodpackTag = "Bloodpack";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IvDripComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<IvDripComponent, ComponentShutdown>(OnDripShutdown);
        SubscribeLocalEvent<IvDripComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<IvDripComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<IvDripComponent, ActivateInWorldEvent>(OnActivateDrip);
        SubscribeLocalEvent<IvDripComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<IvDripComponent, FoldAttemptEvent>(OnFoldAttempt);
        SubscribeLocalEvent<IvDripComponent, FoldedEvent>(OnFolded);
        SubscribeLocalEvent<IvDripComponent, GettingPickedUpAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<IvDripComponent, IvDripAttachDoAfterEvent>(OnAttachDoAfter);
        SubscribeLocalEvent<IvDripComponent, SolutionContainerChangedEvent>(OnSolutionChanged);

        SubscribeLocalEvent<IvDripNeedleComponent, AfterInteractEvent>(OnNeedleAfterInteract);
        SubscribeLocalEvent<IvDripNeedleComponent, ComponentShutdown>(OnNeedleShutdown);
        SubscribeLocalEvent<IvDripNeedleComponent, DroppedEvent>(OnNeedleDropped);
        SubscribeLocalEvent<IvDripNeedleComponent, ExaminedEvent>(OnNeedleExamined);

        SubscribeLocalEvent<IvDripConnectedComponent, MobStateChangedEvent>(OnPatientMobState);
        SubscribeLocalEvent<IvDripConnectedComponent, StoodEvent>(OnPatientStood);
        SubscribeLocalEvent<IvDripConnectedComponent, ComponentShutdown>(OnConnectedShutdown);
    }

    private void OnMapInit(Entity<IvDripComponent> ent, ref MapInitEvent args)
    {
        UpdateVisuals(ent);
    }

    private void OnSolutionChanged(Entity<IvDripComponent> ent, ref SolutionContainerChangedEvent args)
    {
        if (args.SolutionId != IvDripComponent.TankSolutionId)
            return;

        UpdateVisuals(ent);
    }

    private void OnFoldAttempt(Entity<IvDripComponent> ent, ref FoldAttemptEvent args)
    {
        if (!args.Comp.IsFolded && !ent.Comp.CanRefold)
            args.Cancelled = true;
    }

    private void OnFolded(Entity<IvDripComponent> ent, ref FoldedEvent args)
    {
        if (!args.IsFolded)
            DetachPatient(ent, silent: true);
        else
            ClearNeedle(ent);

        UpdateVisuals(ent);
        Dirty(ent);
    }

    private void OnPickupAttempt(Entity<IvDripComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (TryComp<FoldableComponent>(ent, out var fold) && !fold.IsFolded)
            args.Cancel();
    }

    private void OnDripShutdown(Entity<IvDripComponent> ent, ref ComponentShutdown args)
    {
        ClearNeedle(ent);
        DetachPatient(ent, silent: true);
    }

    private void OnExamined(Entity<IvDripComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (TryComp<FoldableComponent>(ent, out var fold) && fold.IsFolded)
        {
            args.PushMarkup(Loc.GetString("iv-drip-examine-folded"));
            return;
        }

        args.PushMarkup(Loc.GetString("iv-drip-examine-speed", ("speed", Loc.GetString($"iv-drip-speed-{ent.Comp.Speed.ToString().ToLowerInvariant()}"))));

        if (TryGetTankSolution(ent, out _, out var solution) && solution.Volume > FixedPoint2.Zero)
            args.PushMarkup(Loc.GetString("iv-drip-examine-volume", ("current", solution.Volume), ("max", solution.MaxVolume)));
        else
            args.PushMarkup(Loc.GetString("iv-drip-examine-empty"));

        if (ent.Comp.AttachedPatient is { } patient && Exists(patient))
            args.PushMarkup(Loc.GetString("iv-drip-examine-attached", ("patient", Identity.Entity(patient, EntityManager))));
        else
            args.PushMarkup(Loc.GetString("iv-drip-examine-detached"));
    }

    private void OnGetVerbs(Entity<IvDripComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (TryComp<FoldableComponent>(ent, out var fold) && fold.IsFolded)
            return;

        var user = args.User;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("iv-drip-verb-cycle-speed"),
            Act = () => CycleSpeed(ent, user),
            Priority = 3,
        });

        if (TryGetTankSolution(ent, out _, out var tank) && tank.Volume > FixedPoint2.Zero)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("iv-drip-verb-clear-tank"),
                Act = () => ClearTank(ent, user),
                Priority = 2,
            });
        }

        if (ent.Comp.AttachedPatient != null)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("iv-drip-verb-detach"),
                Act = () =>
                {
                    if (ent.Comp.Speed != IvDripSpeed.Off && ent.Comp.AttachedPatient is { } patient)
                        SpillFromDrip(ent, patient);
                    DetachPatient(ent, silent: false, user: user);
                },
                Priority = 1,
            });
        }
    }

    private void OnActivateDrip(Entity<IvDripComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (TryComp<FoldableComponent>(ent, out var fold) && fold.IsFolded)
            return;

        args.Handled = true;
        TryToggleNeedle(ent, args.User);
    }

    private void OnInteractUsing(Entity<IvDripComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<FoldableComponent>(ent, out var fold) && fold.IsFolded)
            return;

        if (_tag.HasTag(args.Used, BloodpackTag))
        {
            if (!TryGetTankSolution(ent, out var soln, out var solution))
                return;

            var amount = FixedPoint2.New(ent.Comp.BloodpackFillAmount);
            amount = FixedPoint2.Min(amount, solution.AvailableVolume);
            if (amount <= FixedPoint2.Zero)
            {
                _popup.PopupClient(Loc.GetString("iv-drip-tank-full"), ent, args.User);
                args.Handled = true;
                return;
            }

            if (!_stack.TryUse(args.Used, 1))
                return;

            args.Handled = true;
            _solutions.TryAddReagent(soln, Blood, amount);
            UpdateVisuals(ent);
            _popup.PopupClient(Loc.GetString("iv-drip-bloodpack-filled", ("amount", amount)), ent, args.User);
            return;
        }

        if (TryComp(args.Used, out InjectorComponent? injector) &&
            TryRefillTankFromInjector(ent, (args.Used, injector), args.User))
        {
            args.Handled = true;
        }
    }

    private bool TryRefillTankFromInjector(Entity<IvDripComponent> drip, Entity<InjectorComponent> injector, EntityUid user)
    {
        if (_useDelay.IsDelayed(injector.Owner))
            return false;

        if (!_proto.Resolve(injector.Comp.ActiveModeProtoId, out var activeMode))
            return false;

        if (activeMode.Behavior == InjectorBehavior.Draw)
            return false;

        if (!_solutions.ResolveSolution(injector.Owner, injector.Comp.SolutionName, ref injector.Comp.Solution, out var injectorSolution)
            || injectorSolution.Volume <= FixedPoint2.Zero)
        {
            if (activeMode.Behavior.HasFlag(InjectorBehavior.Dynamic))
                return false;

            _popup.PopupClient(Loc.GetString("injector-component-empty-message", ("injector", injector.Owner)), user, user);
            return true;
        }

        if (!activeMode.Behavior.HasFlag(InjectorBehavior.Inject) &&
            !activeMode.Behavior.HasFlag(InjectorBehavior.Dynamic))
            return false;

        if (!TryGetTankSolution(drip, out var tankSoln, out var tank))
            return false;

        var planned = FixedPoint2.Min(injector.Comp.CurrentTransferAmount ?? injectorSolution.Volume, injectorSolution.Volume);
        var real = FixedPoint2.Min(planned, tank.AvailableVolume);
        if (real <= FixedPoint2.Zero)
        {
            _popup.PopupClient(Loc.GetString("iv-drip-tank-full"), drip, user);
            return true;
        }

        var removed = _solutions.SplitSolution(injector.Comp.Solution.Value, real);
        _solutions.Refill(drip.Owner, tankSoln, removed);
        _useDelay.TryResetDelay(injector.Owner);
        UpdateVisuals(drip);

        _popup.PopupClient(Loc.GetString("injector-component-inject-success-message",
            ("amount", removed.Volume),
            ("target", Identity.Entity(drip.Owner, EntityManager))), drip, user);

        Dirty(injector);
        return true;
    }

    private void OnNeedleExamined(Entity<IvDripNeedleComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("iv-drip-needle-examine"));
    }

    private void OnNeedleAfterInteract(Entity<IvDripNeedleComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target || !args.CanReach)
            return;

        if (!TryComp(ent.Comp.Drip, out IvDripComponent? drip))
        {
            DeleteNeedle(ent.Owner);
            return;
        }

        if (!HasComp<BloodstreamComponent>(target))
            return;

        args.Handled = true;
        TryStartAttach((ent.Comp.Drip, drip), target, args.User, ent.Owner);
    }

    private void OnNeedleDropped(Entity<IvDripNeedleComponent> ent, ref DroppedEvent args)
    {
        DeleteNeedle(ent.Owner);
    }

    private void OnNeedleShutdown(Entity<IvDripNeedleComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp(ent.Comp.Drip, out IvDripComponent? drip) && drip.ActiveNeedle == ent.Owner)
        {
            drip.ActiveNeedle = null;
            Dirty(ent.Comp.Drip, drip);
        }
    }

    public bool TryToggleNeedle(Entity<IvDripComponent> drip, EntityUid user)
    {
        if (TryComp<FoldableComponent>(drip, out var fold) && fold.IsFolded)
            return false;

        if (drip.Comp.AttachedPatient is { } patient && Exists(patient))
        {
            if (drip.Comp.Speed != IvDripSpeed.Off)
                SpillFromDrip(drip, patient);

            DetachPatient(drip, silent: false, user: user);
            return true;
        }

        if (drip.Comp.ActiveNeedle is { } existing && Exists(existing) && !TerminatingOrDeleted(existing))
        {
            if (_hands.IsHolding(user, existing))
            {
                ClearNeedle(drip);
                _popup.PopupClient(Loc.GetString("iv-drip-needle-returned"), drip, user);
                return true;
            }

            _popup.PopupClient(Loc.GetString("iv-drip-needle-in-use"), drip, user);
            return false;
        }

        return TryGiveNeedle(drip, user);
    }

    public bool TryGiveNeedle(Entity<IvDripComponent> drip, EntityUid user)
    {
        if (TryComp<FoldableComponent>(drip, out var fold) && fold.IsFolded)
            return false;

        if (drip.Comp.AttachedPatient != null)
            return false;

        if (drip.Comp.ActiveNeedle is { } existing && Exists(existing) && !TerminatingOrDeleted(existing))
        {
            if (_hands.IsHolding(user, existing))
            {
                _popup.PopupClient(Loc.GetString("iv-drip-needle-already"), drip, user);
                return true;
            }

            _popup.PopupClient(Loc.GetString("iv-drip-needle-in-use"), drip, user);
            return false;
        }

        if (!_hands.TryGetEmptyHand(user, out var hand))
        {
            _popup.PopupClient(Loc.GetString("iv-drip-needle-no-hand"), drip, user);
            return false;
        }

        if (!PredictedTrySpawnInContainer(drip.Comp.NeedlePrototype, user, hand, out var spawned)
            || spawned is not { } needle)
        {
            _popup.PopupClient(Loc.GetString("iv-drip-needle-no-hand"), drip, user);
            return false;
        }

        var needleComp = EnsureComp<IvDripNeedleComponent>(needle);
        needleComp.Drip = drip.Owner;
        Dirty(needle, needleComp);
        EnsureComp<UnremoveableComponent>(needle);

        drip.Comp.ActiveNeedle = needle;
        Dirty(drip);
        _popup.PopupClient(Loc.GetString("iv-drip-needle-taken"), drip, user);
        return true;
    }

    public void ClearNeedle(Entity<IvDripComponent> drip)
    {
        if (drip.Comp.ActiveNeedle is not { } needle)
            return;

        drip.Comp.ActiveNeedle = null;
        Dirty(drip);
        DeleteNeedle(needle);
    }

    private void DeleteNeedle(EntityUid needle)
    {
        if (!Exists(needle) || TerminatingOrDeleted(needle))
            return;

        RemComp<IvDripNeedleComponent>(needle);
        RemComp<UnremoveableComponent>(needle);
        PredictedQueueDel(needle);
    }

    private void TryStartAttach(Entity<IvDripComponent> drip, EntityUid patient, EntityUid user, EntityUid? needle = null)
    {
        if (!IsInAttachRange(drip.Owner, patient, drip.Comp))
        {
            _popup.PopupClient(Loc.GetString("iv-drip-too-far"), drip, user);
            return;
        }

        if (!IsNeedleHolderNear(drip.Owner, user, drip.Comp))
        {
            _popup.PopupClient(Loc.GetString("iv-drip-too-far"), drip, user);
            return;
        }

        if (HasComp<IvDripConnectedComponent>(patient))
        {
            _popup.PopupClient(Loc.GetString("iv-drip-already-attached"), patient, user);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(1.5),
            new IvDripAttachDoAfterEvent(), drip, target: patient, used: needle ?? drip.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnAttachDoAfter(Entity<IvDripComponent> ent, ref IvDripAttachDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } patient)
            return;

        args.Handled = true;

        if (!IsInAttachRange(ent.Owner, patient, ent.Comp) || !IsNeedleHolderNear(ent.Owner, args.User, ent.Comp))
        {
            _popup.PopupClient(Loc.GetString("iv-drip-too-far"), ent, args.User);
            return;
        }

        if (HasComp<IvDripConnectedComponent>(patient))
            return;

        if (ent.Comp.AttachedPatient is { } old && old != patient)
            DetachPatient(ent, silent: true);

        ClearNeedle(ent);

        ent.Comp.AttachedPatient = patient;
        ent.Comp.PatientWasDowned = _standing.IsDown(patient) || _mobState.IsCritical(patient) || _mobState.IsIncapacitated(patient);
        ent.Comp.NextTransfer = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.TransferIntervalSeconds);
        Dirty(ent);

        var connected = EnsureComp<IvDripConnectedComponent>(patient);
        connected.Drip = ent.Owner;
        Dirty(patient, connected);

        _popup.PopupClient(Loc.GetString("iv-drip-attached", ("patient", Identity.Entity(patient, EntityManager))), ent, args.User);
        UpdateVisuals(ent);
    }

    public void CycleSpeed(Entity<IvDripComponent> ent, EntityUid? user = null)
    {
        ent.Comp.Speed = (IvDripSpeed) (((int) ent.Comp.Speed + 1) % 4);
        Dirty(ent);
        UpdateVisuals(ent);

        if (user != null)
        {
            _popup.PopupClient(Loc.GetString("iv-drip-speed-set",
                ("speed", Loc.GetString($"iv-drip-speed-{ent.Comp.Speed.ToString().ToLowerInvariant()}"))), ent, user.Value);
        }
    }

    public void ClearTank(Entity<IvDripComponent> ent, EntityUid? user = null)
    {
        if (!TryGetTankSolution(ent, out var soln, out var solution) || solution.Volume <= FixedPoint2.Zero)
            return;

        _solutions.RemoveAllSolution(soln);
        UpdateVisuals(ent);

        if (user != null)
            _popup.PopupClient(Loc.GetString("iv-drip-tank-cleared"), ent, user.Value);
    }

    public void DetachPatient(Entity<IvDripComponent> ent, bool silent, EntityUid? user = null)
    {
        if (ent.Comp.AttachedPatient is not { } patient)
            return;

        ent.Comp.AttachedPatient = null;
        ent.Comp.PatientWasDowned = false;
        Dirty(ent);

        if (TryComp(patient, out IvDripConnectedComponent? connected) && connected.Drip == ent.Owner)
            RemComp<IvDripConnectedComponent>(patient);

        if (!silent && user != null)
            _popup.PopupClient(Loc.GetString("iv-drip-detached"), ent, user.Value);

        UpdateVisuals(ent);
    }

    private void OnConnectedShutdown(Entity<IvDripConnectedComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp(ent.Comp.Drip, out IvDripComponent? drip) && drip.AttachedPatient == ent.Owner)
        {
            drip.AttachedPatient = null;
            Dirty(ent.Comp.Drip, drip);
        }
    }

    private void OnPatientMobState(Entity<IvDripConnectedComponent> ent, ref MobStateChangedEvent args)
    {
        if (!TryComp(ent.Comp.Drip, out IvDripComponent? drip))
            return;

        if (args.OldMobState is MobState.Critical or MobState.PreCritical &&
            args.NewMobState == MobState.Alive)
        {
            TryYank((ent.Comp.Drip, drip), ent.Owner);
        }
    }

    private void OnPatientStood(Entity<IvDripConnectedComponent> ent, ref StoodEvent args)
    {
        if (!TryComp(ent.Comp.Drip, out IvDripComponent? drip))
            return;

        if (!drip.PatientWasDowned)
            return;

        if (_mobState.IsCritical(ent) || _mobState.IsIncapacitated(ent))
            return;

        TryYank((ent.Comp.Drip, drip), ent.Owner);
    }

    private void TryYank(Entity<IvDripComponent> drip, EntityUid patient)
    {
        if (drip.Comp.AttachedPatient != patient)
            return;

        var damage = new DamageSpecifier();
        damage.DamageDict["Piercing"] = drip.Comp.YankPiercing;
        damage.DamageDict["Slash"] = drip.Comp.YankSlash;
        _damageable.TryChangeDamage(patient, damage, origin: drip);

        SpillFromDrip(drip, patient);
        SpillPatientBlood(patient);

        _popup.PopupEntity(Loc.GetString("iv-drip-yank"), patient);
        DetachPatient(drip, silent: true);
    }

    private void SpillFromDrip(Entity<IvDripComponent> drip, EntityUid patient)
    {
        var coords = _transform.GetMoverCoordinates(patient);

        if (!TryGetTankSolution(drip, out var soln, out var solution) || solution.Volume <= FixedPoint2.Zero)
            return;

        var spillAmt = FixedPoint2.Max(solution.Volume * drip.Comp.YankSpillFraction, FixedPoint2.New(1));
        spillAmt = FixedPoint2.Min(spillAmt, solution.Volume);
        var split = _solutions.SplitSolution(soln, spillAmt);
        _puddle.TrySpillAt(coords, split, out _);
        UpdateVisuals(drip);
    }

    private void SpillPatientBlood(EntityUid patient)
    {
        var amount = FixedPoint2.New(5);
        if (!_bloodstream.TryModifyBloodLevel(patient, -amount))
            return;

        var blood = new Solution();
        blood.AddReagent(Blood, amount);
        _puddle.TrySpillAt(_transform.GetMoverCoordinates(patient), blood, out _);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsServer)
        {
            var needles = EntityQueryEnumerator<IvDripNeedleComponent>();
            while (needles.MoveNext(out var needleUid, out var needle))
            {
                if (TerminatingOrDeleted(needleUid))
                    continue;

                if (!Exists(needle.Drip) || !TryComp(needle.Drip, out IvDripComponent? dripComp))
                {
                    DeleteNeedle(needleUid);
                    continue;
                }

                if (dripComp.AttachedPatient != null)
                {
                    if (dripComp.ActiveNeedle == needleUid)
                    {
                        dripComp.ActiveNeedle = null;
                        Dirty(needle.Drip, dripComp);
                    }

                    DeleteNeedle(needleUid);
                    continue;
                }

                var holder = Transform(needleUid).ParentUid;
                if (Exists(holder) &&
                    HasComp<HandsComponent>(holder) &&
                    IsNeedleHolderNear(needle.Drip, holder, dripComp))
                    continue;

                if (dripComp.ActiveNeedle != needleUid)
                {
                    DeleteNeedle(needleUid);
                    continue;
                }

                dripComp.ActiveNeedle = null;
                Dirty(needle.Drip, dripComp);

                var notifyUser = Exists(holder) && HasComp<HandsComponent>(holder) ? holder : (EntityUid?) null;
                if (notifyUser != null)
                    _popup.PopupEntity(Loc.GetString("iv-drip-needle-retracted"), needle.Drip, notifyUser.Value);

                DeleteNeedle(needleUid);
            }
        }

        var query = EntityQueryEnumerator<IvDripComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.AttachedPatient is { } patient)
            {
                if (!Exists(patient) || !HasComp<BloodstreamComponent>(patient))
                {
                    DetachPatient((uid, comp), silent: true);
                    continue;
                }

                if (!IsInAttachRange(uid, patient, comp))
                {
                    TryYank((uid, comp), patient);
                    continue;
                }

                if (_standing.IsDown(patient) || _mobState.IsCritical(patient) || _mobState.IsIncapacitated(patient))
                    comp.PatientWasDowned = true;
            }

            if (comp.Speed == IvDripSpeed.Off)
                continue;

            if (_timing.CurTime < comp.NextTransfer)
                continue;

            comp.NextTransfer = _timing.CurTime + TimeSpan.FromSeconds(comp.TransferIntervalSeconds);
            Dirty(uid, comp);

            if (comp.AttachedPatient is { } attached)
            {
                DoTransfer((uid, comp), attached);
                continue;
            }

            DoIdleDrip((uid, comp));
        }
    }

    private void DoTransfer(Entity<IvDripComponent> drip, EntityUid patient)
    {
        var amount = TransferAmount(drip.Comp);
        if (amount <= FixedPoint2.Zero)
            return;

        if (!TryGetTankSolution(drip, out var soln, out var solution) || solution.Volume <= FixedPoint2.Zero)
            return;

        var take = FixedPoint2.Min(amount, solution.Volume);
        var injected = _solutions.SplitSolution(soln, take);
        if (injected.Volume <= FixedPoint2.Zero)
            return;

        _bloodstream.TryAddToBloodstream(patient, injected);
        _reactive.DoEntityReaction(patient, injected, ReactionMethod.Injection);
        UpdateVisuals(drip);
    }

    private void DoIdleDrip(Entity<IvDripComponent> drip)
    {
        if (!TryGetTankSolution(drip, out var soln, out var solution) || solution.Volume <= FixedPoint2.Zero)
            return;

        var amount = FixedPoint2.Min(FixedPoint2.New(drip.Comp.IdleDripTransfer), solution.Volume);
        if (amount <= FixedPoint2.Zero)
            return;

        var split = _solutions.SplitSolution(soln, amount);
        _puddle.TrySpillAt(drip.Owner, split, out _);
        UpdateVisuals(drip);
    }

    private FixedPoint2 TransferAmount(IvDripComponent comp) => comp.Speed switch
    {
        IvDripSpeed.Slow => FixedPoint2.New(comp.SlowTransfer),
        IvDripSpeed.Medium => FixedPoint2.New(comp.MediumTransfer),
        IvDripSpeed.Fast => FixedPoint2.New(comp.FastTransfer),
        _ => FixedPoint2.Zero,
    };

    private bool TryGetTankSolution(Entity<IvDripComponent> drip, out Entity<SolutionComponent> soln, out Solution solution)
    {
        soln = default!;
        solution = new Solution();

        if (!_solutions.TryGetSolution(drip.Owner, IvDripComponent.TankSolutionId, out var found, out var sol))
            return false;

        soln = found!.Value;
        solution = sol!;
        return true;
    }

    private bool IsInAttachRange(EntityUid drip, EntityUid patient, IvDripComponent comp)
    {
        var delta = _transform.GetWorldPosition(patient) - _transform.GetWorldPosition(drip);
        var dist = delta.Length();
        return dist >= comp.MinAttachRange && dist <= comp.MaxAttachRange;
    }

    private bool IsNeedleHolderNear(EntityUid drip, EntityUid holder, IvDripComponent comp)
    {
        var delta = _transform.GetWorldPosition(holder) - _transform.GetWorldPosition(drip);
        return delta.Length() <= comp.MaxNeedleRange;
    }

    protected void UpdateVisuals(Entity<IvDripComponent> ent)
    {
        var folded = TryComp<FoldableComponent>(ent, out var fold) && fold.IsFolded;
        _appearance.SetData(ent, IvDripVisuals.Folded, folded);
        _appearance.SetData(ent, IvDripVisuals.Speed, (int) ent.Comp.Speed);

        Color? color = null;
        var hasBag = false;
        if (TryGetTankSolution(ent, out _, out var solution) && solution.Volume > FixedPoint2.Zero)
        {
            hasBag = true;
            color = solution.GetColor(_proto);
        }

        _appearance.SetData(ent, IvDripVisuals.HasBag, hasBag);
        if (color != null)
            _appearance.SetData(ent, IvDripVisuals.BagColor, color.Value);
    }
}
