// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Organ;
using Content.Shared.DoAfter;
using Robust.Shared.Audio.Systems;
using Content.Shared.Body.Part;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Inventory;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Events;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Stacks;
using Content.Shared.Tools.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
using Content.Shared.Tag;
using Content.Server.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Traits.Assorted;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgerySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly LimbWoundSystem _limbs = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SurgeryLimbPort _port = default!;
    [Dependency] private readonly SurgeryCavitySystem _cavity = default!;
    [Dependency] private readonly SurgeryTeethSystem _teeth = default!;
    [Dependency] private readonly SharedContentEyeSystem _eyes = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly SurgeryInfectionSystem _infection = default!;
    [Dependency] private readonly SurgeryAnesthesiaSystem _anesthesia = default!;
    [Dependency] private readonly SurgeryGraftSystem _graft = default!;
    [Dependency] private readonly Content.Shared.PowerCell.PowerCellSystem _powerCell = default!;

    [Dependency] private readonly SurgeryNearbySystem _nearby = default!;
    [Dependency] private readonly LimbImplantSystem _implants = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;

    [Dependency] private readonly IComponentFactory _factory = default!;

    private const string HealthRules = "Default";
    private const string FloorPlace = "Floor";
    private const string PenTag = "Pen";
    private static readonly SurgeryStep SkinBack = new() { Verb = SurgeryVerbKind.Retract, Layer = SurgeryLayerKind.Skin, Hint = "RetractClose" };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LimbBodyComponent, InteractUsingEvent>(OnUse);
        SubscribeLocalEvent<LimbBodyComponent, TargetBeforeInjectEvent>(OnBeforeInject);
        SubscribeLocalEvent<LimbBodyComponent, InjectorUseAttemptEvent>(OnInjectorUse);
        SubscribeLocalEvent<LimbBodyComponent, SurgeryStepDoAfterEvent>(OnStepDoAfter);
        SubscribeLocalEvent<SurgeryFocusComponent, PlayerDetachedEvent>(OnFocusDetach);
        SubscribeNetworkEvent<SurgeryCutMessage>(OnCut);
        SubscribeNetworkEvent<SurgeryZoneMessage>(OnZone);
        SubscribeLocalEvent<SurgeryToolComponent, MeleeHitEvent>(OnToolHit);
        SubscribeLocalEvent<LimbBodyComponent, InteractHandEvent>(OnEmptyHand, before: [typeof(SharedBuckleSystem)]);
        SubscribeNetworkEvent<SurgeryCancelMessage>(OnCancel);
        SubscribeNetworkEvent<SurgeryPickMessage>(OnPick);
        SubscribeNetworkEvent<SurgeryFocusMessage>(OnFocus);
        SubscribeLocalEvent<SurgerySiteComponent, LimbSeveredEvent>(OnSevered);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<SurgerySiteComponent>();
        while (query.MoveNext(out var uid, out var site))
        {
            if (site.SpringBone > 0f && _timing.CurTime >= site.SpringAt)
            {
                _limbs.ApplyEdit(uid, site.Zone, new LimbEdit { Bone = site.SpringBone });
                site.SpringBone = 0f;
            }

            site.Accumulator += frameTime;
            if (site.Accumulator < 3f || site.Germs <= 0.5f)
                continue;

            site.Accumulator = 0f;
            var poison = new DamageSpecifier();
            poison.DamageDict["Poison"] = MathF.Min(site.Germs * 0.08f, 2f);
            _damage.TryChangeDamage(uid, poison, true, false);
            site.Germs = MathF.Max(0f, site.Germs - 1.5f);
        }

        var busyQuery = EntityQueryEnumerator<SurgeryBusyComponent>();
        while (busyQuery.MoveNext(out var uid, out var busy))
        {
            if (_timing.CurTime < busy.Until)
                continue;

            var patient = busy.Patient;
            RemComp<SurgeryBusyComponent>(uid);
            if (TryComp<SurgerySiteComponent>(patient, out var site))
                Send(patient, uid, site, Held(uid), "surgery-timeout", false);
        }
    }

    private void OnToolHit(Entity<SurgeryToolComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || _combat.IsInCombatMode(args.User))
            return;

        foreach (var hit in args.HitEntities)
        {
            if (HasComp<LimbBodyComponent>(hit) && CanOperate(hit))
            {
                if ((ent.Comp.Passive || Machine(hit)) && !Wanted(hit, ent.Comp) || !ToolFits(hit, ent.Comp))
                    return;

                args.Handled = true;
                if (TryComp<SurgerySiteComponent>(hit, out var site) && site.Layers.Count > 0)
                    Send(hit, args.User, site, ent.Owner, string.Empty, true);
                return;
            }
        }
    }

    private bool Wanted(EntityUid patient, SurgeryToolComponent tool)
    {
        return TryComp<SurgerySiteComponent>(patient, out var site)
               && !site.Done
               && StepNow(patient, site) is { } step
               && tool.VerbsFor(Machine(patient), Plant(patient), Soft(patient)).Contains(step.Verb)
               && ToolFits(patient, tool);
    }

    private void OnEmptyHand(Entity<LimbBodyComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !CanOperate(ent.Owner) || !Near(args.User, ent.Owner))
            return;

        if (!TryComp<SurgerySiteComponent>(ent.Owner, out var site) || site.Done || !WoundOpen(site))
            return;

        args.Handled = true;
        Send(ent.Owner, args.User, site, null, string.Empty, true);
    }

    private void OnBeforeInject(Entity<LimbBodyComponent> ent, ref TargetBeforeInjectEvent args)
    {
        if (!TryComp<SurgerySiteComponent>(ent, out var site) || site.Done || site.Layers.Count == 0)
            return;

        if (StepNow(ent, site) is not { Verb: SurgeryVerbKind.Inject } || !_infection.CanCure(args.UsedInjector))
            return;

        args.OverrideMessage = "";
        args.Cancel();
    }

    private void OnInjectorUse(Entity<LimbBodyComponent> ent, ref InjectorUseAttemptEvent args)
    {
        if (args.Cancelled || !TryComp<SurgerySiteComponent>(ent, out var site) || site.Done || site.Layers.Count == 0)
            return;

        if (StepNow(ent, site) is { Verb: SurgeryVerbKind.Inject })
            args.Cancelled = true;
    }

    private void OnUse(Entity<LimbBodyComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || _combat.IsInCombatMode(args.User) || !CanOperate(ent.Owner))
            return;

        var tool = CompOrNull<SurgeryToolComponent>(args.Used);
        if (tool != null && (tool.Passive || Machine(ent.Owner)) && !Wanted(ent.Owner, tool))
            return;

        if (tool != null && !ToolFits(ent.Owner, tool))
        {
            if (tool.Verbs.Count > 0 && Soft(ent.Owner))
                _popup.PopupEntity(Loc.GetString("surgery-soft-no-saw"), ent.Owner, args.User);
            else if (tool.Verbs.Count > 0 && Plant(ent.Owner))
                _popup.PopupEntity(Loc.GetString("surgery-plant-no-tool"), ent.Owner, args.User);
            return;
        }

        var part = tool == null && HasComp<BodyPartComponent>(args.Used);
        var organ = tool == null && !part && HasComp<OrganComponent>(args.Used);
        if (tool == null && ClickStep(ent.Owner, args.User, args.Used))
        {
            args.Handled = true;
            return;
        }

        if (tool == null && !part && !organ)
        {
            if (HasComp<PillComponent>(args.Used)
                && TryComp<SurgerySiteComponent>(ent.Owner, out var teeth)
                && Tooth(teeth)
                && !teeth.Done)
            {
                args.Handled = true;
                if (TeethInsert(ent.Owner, teeth) && _teeth.TryLodge(ent.Owner, args.Used))
                {
                    teeth.Step++;
                    if (Operation(ent.Owner, teeth, out var tooth) && teeth.Step >= tooth.Steps.Count)
                        Finish(ent.Owner, teeth);
                    Send(ent.Owner, args.User, teeth, null, string.Empty, false);
                }

                return;
            }

            if (!HasComp<LimbImplantComponent>(args.Used)
                && !Unpocketable(args.Used)
                && TryComp<SurgerySiteComponent>(ent.Owner, out var pocket)
                && OpenCavity(pocket)
                && _cavity.TryLodge(ent.Owner, pocket.Zone, args.Used))
            {
                args.Handled = true;
                Send(ent.Owner, args.User, pocket, null, string.Empty, false);
            }

            return;
        }

        if (tool != null)
        {
            args.Handled = true;
            if (TryComp<SurgerySiteComponent>(ent.Owner, out var open) && open.Layers.Count > 0 && !open.Done && WoundOpen(open))
            {
                if (tool.VerbsFor(Machine(ent.Owner), Plant(ent.Owner), Soft(ent.Owner)).Contains(SurgeryVerbKind.Extract)
                    && OpenCavity(open)
                    && _cavity.TryWithdraw(ent.Owner, open.Zone, args.User))
                {
                    Send(ent.Owner, args.User, open, args.Used, string.Empty, false);
                    return;
                }

                Send(ent.Owner, args.User, open, args.Used, string.Empty, true);
            }

            return;
        }

        args.Handled = true;
        var site = EnsureComp<SurgerySiteComponent>(ent.Owner);
        var zoneChanged = false;
        if (part && TryPartZone(args.Used, out var seatZone))
        {
            zoneChanged = site.Zone != seatZone;
            site.Zone = seatZone;
        }
        else if (TryComp<LimbTargetComponent>(args.User, out var target) && target.Selected is { } picked && (LimbWoundRules.IsBody(picked) || picked == LimbZone.Eyes))
        {
            zoneChanged = site.Zone != picked;
            if (zoneChanged)
                Spill(ent.Owner, site);
            site.Zone = picked;
        }

        if (zoneChanged || site.Layers.Count == 0)
        {
            site.Gel = false;
            site.Back = HasComp<SurgeryProneComponent>(ent.Owner);
            Rebuild(ent.Owner, site);
        }

        Send(ent.Owner, args.User, site, args.Used, string.Empty, true);
    }

    private bool Unpocketable(EntityUid used)
    {
        return HasComp<InjectorComponent>(used)
               || HasComp<FitsInDispenserComponent>(used)
               || HasComp<MixableSolutionComponent>(used)
               || HasComp<Content.Shared.Medical.Healing.HealingComponent>(used);
    }

    private void OnPick(SurgeryPickMessage message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user
            || !TryGetEntity(message.Patient, out var patient)
            || !TryComp<SurgerySiteComponent>(patient, out var site)
            || !CanOperate(patient.Value)
            || !Near(user, patient.Value))
            return;

        site.Chosen = message.Organ;
        Send(patient.Value, user, site, Held(user), string.Empty, false);
    }

    private void OnFocus(SurgeryFocusMessage message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        if (message.On != 0)
            Focus(user);
        else
            Release(user);
    }

    private void OnFocusDetach(Entity<SurgeryFocusComponent> ent, ref PlayerDetachedEvent args)
    {
        Release(ent);
    }

    private void Focus(EntityUid user)
    {
        if (!TryComp<ContentEyeComponent>(user, out var eye))
            return;

        var focus = EnsureComp<SurgeryFocusComponent>(user);
        if (!focus.Held)
        {
            focus.Prior = eye.TargetZoom;
            focus.Held = true;
        }

        _eyes.SetZoom(user, SharedContentEyeSystem.MinZoom, eye: eye);
    }

    private void Release(EntityUid user)
    {
        if (!TryComp<SurgeryFocusComponent>(user, out var focus))
            return;

        if (focus.Held && TryComp<ContentEyeComponent>(user, out var eye))
            _eyes.SetZoom(user, focus.Prior, eye: eye);

        RemComp<SurgeryFocusComponent>(user);
    }

    private void OnCancel(SurgeryCancelMessage message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user
            || !TryComp<SurgeryBusyComponent>(user, out var busy))
            return;

        var patient = busy.Patient;
        RemComp<SurgeryBusyComponent>(user);
        if (TryComp<SurgerySiteComponent>(patient, out var site))
            Send(patient, user, site, Held(user), string.Empty, false);
    }

    private bool Embedded(EntityUid patient, LimbZone zone)
    {
        if (!LimbWoundRules.IsBody(zone) || !TryComp<LimbBodyComponent>(patient, out var limbs))
            return false;

        var index = LimbWoundRules.Index(zone);
        return (uint) index < (uint) limbs.Zones.Count && limbs.Zones[index].Embedded;
    }

    private void OnZone(SurgeryZoneMessage message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user
            || !TryGetEntity(message.Patient, out var patient)
            || !CanOperate(patient.Value)
            || !Near(user, patient.Value)
            || !CanMark(user, patient.Value))
            return;

        var zone = (LimbZone) message.Zone;
        if (!LimbWoundRules.IsBody(zone) && zone != LimbZone.Eyes)
            return;

        var askedOperation = message.Part.Length > 0 ? PartOperation(patient.Value, message.Part) : message.Operation.Length > 0 ? message.Operation : null;
        if (!SelfOk(user, patient.Value, zone, askedOperation))
            return;

        var site = EnsureComp<SurgerySiteComponent>(patient.Value);

        if (Covered(patient.Value, zone))
        {
            _popup.PopupEntity(Loc.GetString("surgery-clothes"), patient.Value, user);
            return;
        }

        var back = HasComp<SurgeryProneComponent>(patient.Value);
        var key = KeptKey(zone, message.Part, back);
        var same = zone == site.Zone && message.Part == site.Part && back == site.Back;
        var open = !site.Done && site.Layers.Count > 0 && WoundOpen(site);
        if (open && !same)
            Keep(site);

        if (!same || site.Done || site.Layers.Count == 0)
        {
            site.Done = false;
            if (site.Kept.TryGetValue(key, out var kept) && WoundOpen(kept.Layers))
                RestoreKept(site, kept, zone);
            else
            {
                site.Zone = zone;
                site.Part = message.Part;
                site.Queued = "";
                site.Back = back;
                site.Gel = false;
                site.Cut = 0f;
                site.Germs = 0f;
                Rebuild(patient.Value, site);
                if (message.Part.Length > 0 && PartOperation(patient.Value, message.Part) is { } partOperation)
                    site.Queued = partOperation;
                else if (message.Operation.Length > 0
                    && _prototype.TryIndex<SurgeryOperationPrototype>(message.Operation, out var asked)
                    && FitsAsked(patient.Value, user, zone, asked))
                {
                    site.Queued = asked.ID;
                    site.Part = "";
                }
                else if (message.Part.Length == 0 && CareFor(patient.Value, zone) is { } care)
                    site.Queued = care;
                else if (message.Part.Length == 0
                    && !HasComp<SurgeryProneComponent>(patient.Value)
                    && _prototype.TryIndex<SurgeryAnatomyPrototype>(AnatomyId(patient.Value, zone), out var chosen)
                    && chosen.Operation is { } queuedId)
                    site.Queued = queuedId;
                site.CloseStep = 0;
                site.Sterile = !Operation(patient.Value, site, out var started) || started.Sterile;
            }
        }

        Send(patient.Value, user, site, Held(user), string.Empty, true);
    }

    private void PartGone(EntityUid patient, string part, bool gone)
    {
        if (part.Length == 0)
            return;

        var lost = EnsureComp<SurgeryPartsGoneComponent>(patient);
        if (gone ? lost.Parts.Add(part) : lost.Parts.Remove(part))
            Dirty(patient, lost);
    }

    private string? PartOperation(EntityUid patient, string partId)
    {
        if (!_prototype.TryIndex<SurgeryPartPrototype>(partId, out var part) || part.Machine != Machine(patient))
            return null;

        if (part.Put.Length > 0 && part.Organ.Length > 0 && Enum.TryParse<LimbOrgan>(part.Organ, out var organ) && OrganMissing(patient, organ))
            return part.Put;

        if (part.Put.Length > 0 && part.Implant.Length > 0 && !_implants.Occupied(patient, part.Implant))
            return part.Put;

        if (part.Put.Length > 0 && TryComp<SurgeryPartsGoneComponent>(patient, out var lost) && lost.Parts.Contains(partId))
            return part.Put;

        return part.Operation.Length > 0 ? part.Operation : null;
    }

    private void OnCut(SurgeryCutMessage message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is { } user)
            Cut(user, message);
    }

    private void OnStepDoAfter(Entity<LimbBodyComponent> ent, ref SurgeryStepDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        Cut(args.User, new SurgeryCutMessage
        {
            Patient = GetNetEntity(ent.Owner),
            Verb = args.Verb,
            Layer = 0,
            Organ = args.Organ,
            Progress = 1f,
        });
    }

    private bool ClickStep(EntityUid patient, EntityUid user, EntityUid used)
    {
        if (!TryComp<SurgerySiteComponent>(patient, out var site) || site.Done || site.Layers.Count == 0)
            return false;

        byte organ = 255;
        SurgeryVerbKind verb;
        if (StepNow(patient, site) is { Verb: SurgeryVerbKind.Bandage or SurgeryVerbKind.Append or SurgeryVerbKind.Inject or SurgeryVerbKind.Insert or SurgeryVerbKind.Seat or SurgeryVerbKind.Graft } now
            && VerbAllowed(now.Verb, used, site))
        {
            verb = now.Verb;
            if (verb == SurgeryVerbKind.Insert && SpareOrgan(patient, site, used) is { } wanted)
                organ = (byte) wanted;
        }
        else if (HasComp<OrganComponent>(used) && WoundOpen(site) && SpareOrgan(patient, site, used) is { } spare)
        {
            verb = SurgeryVerbKind.Insert;
            organ = (byte) spare;
        }
        else if (HasComp<LimbImplantComponent>(used) && UndoVerb(patient, site) == (byte) SurgeryVerbKind.Insert)
        {
            if (TryUndo(patient, site, SurgeryVerbKind.Insert, used))
                Send(patient, user, site, null, string.Empty, true);
            return true;
        }
        else
        {
            return false;
        }

        if (verb is SurgeryVerbKind.Bandage or SurgeryVerbKind.Graft or SurgeryVerbKind.Inject)
        {
            Send(patient, user, site, used, string.Empty, true);
            return true;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(1.5), new SurgeryStepDoAfterEvent { Verb = (byte) verb, Organ = organ }, patient, target: patient, used: used)
        {
            BreakOnMove = true,
            BreakOnHandChange = true,
            NeedHand = true,
        });
        return true;
    }

    private LimbOrgan? SpareOrgan(EntityUid patient, SurgerySiteComponent site, EntityUid held)
    {
        string? only = null;
        if (site.Part.Length > 0 && _prototype.TryIndex<SurgeryPartPrototype>(site.Part, out var part) && part.Organ.Length > 0)
            only = part.Organ;

        foreach (var organ in _prototype.EnumeratePrototypes<SurgeryOrganPrototype>())
        {
            if (!Enum.TryParse<LimbOrgan>(organ.ID, out var kind))
                continue;

            if (only != null ? organ.ID != only : organ.Zone != site.Zone.ToString())
                continue;

            if (OrganMissing(patient, kind) && HeldIs(patient, held, kind))
                return kind;
        }

        return null;
    }

    private void Cut(EntityUid user, SurgeryCutMessage message)
    {
        RemComp<SurgeryBusyComponent>(user);
        if (!TryGetEntity(message.Patient, out var patient)
            || !TryComp<SurgerySiteComponent>(patient, out var site)
            || !CanOperate(patient.Value)
            || !Near(user, patient.Value))
            return;

        if (message.Layer >= site.Layers.Count && !Operation(patient.Value, site, out _))
            return;

        var verb = (SurgeryVerbKind) message.Verb;
        var held = Held(user);
        if (!SelfOk(user, patient.Value, site.Zone, Operation(patient.Value, site, out var selfOperation) ? selfOperation.ID : null))
        {
            Send(patient.Value, user, site, held, "surgery-self", false);
            return;
        }

        if (site.Layers.Count > 0 && site.Back != HasComp<SurgeryProneComponent>(patient.Value))
        {
            Send(patient.Value, user, site, held, "surgery-wrong-side", false);
            return;
        }

        if (Covered(patient.Value, site.Zone))
        {
            Send(patient.Value, user, site, held, "surgery-clothes", false);
            return;
        }

        if (verb == SurgeryVerbKind.Insert
            && held is { } spareItem
            && HasComp<OrganComponent>(spareItem)
            && StepNow(patient.Value, site) is not { Verb: SurgeryVerbKind.Insert }
            && (site.Part.Length > 0 || !OpenCavity(site))
            && WoundOpen(site)
            && SpareOrgan(patient.Value, site, spareItem) is { } spare)
        {
            var placed = new SurgeryResult { PlaceOrgan = true, Key = "surgery-inserted" };
            _port.Apply(patient.Value, site.Zone, spare, placed, spareItem);
            Send(patient.Value, user, site, Held(user), placed.Key, false);
            return;
        }

        if (TryUndo(patient.Value, site, verb, held))
        {
            Send(patient.Value, user, site, held, string.Empty, false);
            return;
        }
        if (verb == SurgeryVerbKind.Clamp && message.Organ != 255 && message.Organ != 254)
            site.Chosen = message.Organ;
        if (verb == SurgeryVerbKind.Extract && message.Organ == 254)
        {
            if (OpenCavity(site) && VerbAllowed(verb, held, site) && _cavity.TryWithdraw(patient.Value, site.Zone, user))
                _anesthesia.Tear(patient.Value, site.Zone, Asleep(patient.Value));
            Send(patient.Value, user, site, held, string.Empty, false);
            return;
        }
        if (!VerbAllowed(verb, held, site))
        {
            Send(patient.Value, user, site, held, "surgery-wrong-hand", false);
            return;
        }

        if (verb == SurgeryVerbKind.Cauterize && !WelderReady(held, user, out var welderLine))
        {
            Send(patient.Value, user, site, held, welderLine, false);
            return;
        }

        if (verb == SurgeryVerbKind.Cauterize
            && Operation(patient.Value, site, out var open)
            && (site.Step > 0 || WoundOpen(site))
            && site.Step < open.Steps.Count
            && open.Steps[site.Step].Verb != SurgeryVerbKind.Cauterize)
        {
            if (message.Progress >= 0.95f)
                Abort(patient.Value, site);
            Send(patient.Value, user, site, held, string.Empty, false);
            return;
        }

        if (Tooth(site) && verb == SurgeryVerbKind.Insert)
        {
            if (TeethInsert(patient.Value, site) && _teeth.TryLodge(patient.Value, held))
            {
                site.Step++;
                if (Operation(patient.Value, site, out var tooth) && site.Step >= tooth.Steps.Count && NextClose(tooth, site) == null)
                    Finish(patient.Value, site);
                Send(patient.Value, user, site, held, string.Empty, false);
            }
            else
                Send(patient.Value, user, site, held, "surgery-wrong-hand", false);

            return;
        }

        if (message.Progress <= 0f && message.Slips > 0 && verb is SurgeryVerbKind.SawBone or SurgeryVerbKind.Drill or SurgeryVerbKind.Amputate or SurgeryVerbKind.Chop)
        {
            if (_prototype.TryIndex<SurgeryMissPrototype>(verb.ToString(), out var slip))
                _damage.TryChangeDamage(patient.Value, slip.Damage, origin: user);
            if (LimbWoundRules.IsBody(site.Zone))
                _limbs.ApplyEdit(patient.Value, site.Zone, new LimbEdit { Health = -LimbWoundRules.HealthFor(site.Zone, CompOrNull<LimbBodyComponent>(patient.Value)) * 0.04f, Bleed = 0.5f });
            Send(patient.Value, user, site, held, "surgery-saw-slip", false);
            return;
        }

        var index = StepIndex(patient.Value, site, verb);
        if (index < 0)
        {
            Send(patient.Value, user, site, held, string.Empty, false);
            return;
        }

        Operation(patient.Value, site, out var before);
        var closeIndex = before == null || site.Step < before.Steps.Count ? -1 : CloseIndex(before, site);
        var closedStep = false;
        var reported = message.Progress;
        var asleep = Asleep(patient.Value);
        var flinched = message.Progress > 0f && _anesthesia.Flinch(patient.Value, asleep, StepNow(patient.Value, site), site.Zone);
        if (flinched)
        {
            message.Progress = _anesthesia.Shaken(message.Progress);
            message.Slips = (byte) Math.Min(12, message.Slips + 1);
        }

        var layer = site.Layers[index];
        var place = Place(patient.Value);
        var tool = held == null ? null : CompOrNull<SurgeryToolComponent>(held.Value);
        var implantSlot = ImplantOf(patient.Value, site, before);
        var request = new SurgeryRequest
        {
            Pool = LimbWoundRules.HealthFor(site.Zone, CompOrNull<LimbBodyComponent>(patient.Value)),
            Verb = verb,
            Zone = site.Zone,
            Kind = layer.Kind,
            Integrity = layer.Integrity,
            Retracted = layer.Retracted,
            Intact = StepIntact(patient.Value, site, verb),
            DeeperCut = Deeper(site, index),
            CavityReady = CavityReady(site),
            LayersOpen = LayersOpen(site),
            Progress = Math.Clamp(message.Progress, 0f, 1f),
            Slips = Math.Clamp((int) message.Slips, 0, 12),
            Yank = message.Yank != 0,
            ToolPrecision = tool?.Precision ?? 0.35f,
            ToolSlip = tool?.Slip ?? 0.4f,
            SurfaceSlip = place.Slip,
            BoneFloor = place.BoneFloor,
            Implant = implantSlot.Length > 0,
        };

        if (LimbWoundRules.IsBody(site.Zone) && TryComp<LimbBodyComponent>(patient, out var limbs))
        {
            var state = limbs.Zones[LimbWoundRules.Index(site.Zone)];
            request.CurrentBone = state.Bone;
            request.CurrentFlesh = state.Flesh;
            request.CurrentBleed = state.Bleed;
            request.CurrentSkin = state.Skin;
            request.SkinBare = state.SkinBare;
            request.Atrophied = state.Atrophied;
            request.Embedded = state.Embedded;
            request.Scar = state.Scar;
        }

        var result = SurgeryCut.Resolve(request);
        var rejoin = verb == SurgeryVerbKind.Retract && closeIndex >= 0 && before != null
                     && CloseSteps(before, site)[closeIndex].Verb == SurgeryVerbKind.Retract;
        if (rejoin)
        {
            result.Rejected = false;
            result.Retracted = layer.Retracted && message.Progress <= 0.8f;
            result.Key = result.Retracted ? "surgery-retract-short" : string.Empty;
        }

        var tail = verb == SurgeryVerbKind.Amputate && before is { Appendage.Length: > 0, Attach: false };
        if (tail)
        {
            result = new SurgeryResult
            {
                Integrity = layer.Integrity,
                Retracted = layer.Retracted,
                Finished = message.Progress > 0.8f,
                Key = message.Progress > 0.8f ? "surgery-tail-off" : "surgery-amp-short",
            };
        }

        if (message.Slips > 0 && _prototype.TryIndex<SurgeryMissPrototype>(verb.ToString(), out var miss))
            _damage.TryChangeDamage(patient.Value, miss.Damage, origin: user);
        layer.Integrity = result.Integrity;
        layer.Retracted = result.Retracted;
        site.Layers[index] = layer;
        if (result.NextLoss > 0f && index + 1 < site.Layers.Count
            && site.Layers[index + 1].Kind is not (SurgeryLayerKind.Bone or SurgeryLayerKind.Cavity or SurgeryLayerKind.Globe))
        {
            var next = site.Layers[index + 1];
            next.Integrity = MathF.Max(0f, next.Integrity - result.NextLoss);
            site.Layers[index + 1] = next;
        }

        if (verb == SurgeryVerbKind.Retract && !rejoin)
            Reveal(site);
        if (rejoin && !layer.Retracted)
            SurgeryProcedure.Conceal(site.Layers, index);
        if (verb == SurgeryVerbKind.MendBone && layer.Kind == SurgeryLayerKind.Bone && !layer.Retracted && layer.Integrity >= 0.85f)
            SurgeryProcedure.Conceal(site.Layers, index);
        if (verb == SurgeryVerbKind.Cauterize && reported >= 0.95f)
        {
            layer.Integrity = 1f;
            layer.Retracted = false;
            site.Layers[index] = layer;
        }
        var stepVerb = SurgeryVerbKind.Inspect;
        string stepOrgan = "";
        if (Operation(patient.Value, site, out var operation) && site.Step < operation.Steps.Count)
        {
            stepVerb = operation.Steps[site.Step].Verb;
            stepOrgan = operation.Steps[site.Step].Organ;
        }

        var neededInsert = stepVerb == SurgeryVerbKind.Insert && operation != null && operation.Steps[site.Step].Need.Count > 0;
        if (stepVerb == SurgeryVerbKind.Insert && verb == SurgeryVerbKind.Insert && operation != null
            && !InsertFits(patient.Value, operation.Steps[site.Step], held))
        {
            result.PlaceOrgan = false;
            result.PlaceImplant = false;
        }

        var advance = stepVerb == SurgeryVerbKind.Clamp
            ? message.Progress >= 0.8f
            : tail ? result.Finished : SurgeryProcedure.Advances(stepVerb, site.Layers[index], result);
        if (tail && advance)
            RemoveAppendage(patient.Value, before!.Appendage);

        if (advance && operation != null && site.Step < operation.Steps.Count
            && operation.Steps[site.Step] is { Need.Count: > 0 } needing
            && (held == null || MetaData(held.Value).EntityPrototype is not { } heldProto || !needing.Need.Contains(heldProto.ID)))
        {
            advance = false;
            result.Finished = false;
            result.PlaceOrgan = false;
            result.PlaceImplant = false;
            result.Rejected = true;
            result.Key = "surgery-wrong-hand";
        }

        if (verb == SurgeryVerbKind.Seat && (advance || result.Attach)
            && ZoneSevered(patient.Value, site.Zone)
            && (held == null || !_limbs.PartFits(site.Zone, held.Value)))
        {
            advance = false;
            result.Attach = false;
            result.Finished = false;
            result.Rejected = true;
            result.Key = "surgery-wrong-hand";
        }

        if (verb == SurgeryVerbKind.Append && result.Finished && !AttachAppendage(patient.Value, held))
        {
            result.Finished = false;
            result.Rejected = true;
            result.Key = "surgery-wrong-hand";
            advance = false;
        }
        if (result.RemoveOrgan && !site.Clamped && !CavityReady(site))
        {
            result.RemoveOrgan = false;
            result.Rejected = true;
            advance = false;
        }

        if (result.RemoveOrgan && stepOrgan.Length > 0 && Enum.TryParse<LimbOrgan>(stepOrgan, out var wanted) && OrganMissing(patient.Value, wanted))
        {
            result.RemoveOrgan = false;
            result.Rejected = true;
            result.Key = "surgery-organ-gone";
            advance = false;
        }

        if (result.RemoveImplant && (implantSlot.Length == 0 || !_implants.Occupied(patient.Value, implantSlot)))
        {
            result.RemoveImplant = false;
            result.Rejected = true;
            result.Key = "surgery-implant-gone";
            advance = false;
        }

        if (verb == SurgeryVerbKind.Inject && result.Finished && !_infection.Cure(patient.Value, site.Zone, held))
        {
            result.Finished = false;
            result.Rejected = true;
            result.Key = "surgery-inject-empty";
            advance = false;
        }

        if (verb == SurgeryVerbKind.Shock && result.Finished && held != null && !_powerCell.TryUseActivatableCharge(held.Value, user: user))
        {
            result.Finished = false;
            result.Rejected = true;
            result.Key = "surgery-shock-no-charge";
            advance = false;
        }

        if (verb == SurgeryVerbKind.Bandage && result.Finished)
        {
            _limbs.Bandage(patient.Value, site.Zone);
            SpendHeld(held);
        }

        if (verb == SurgeryVerbKind.Stanch && result.Finished)
            SpendHeld(held);

        if (verb == SurgeryVerbKind.MendBone && result.Finished)
            SpendGel(held);

        if (verb == SurgeryVerbKind.SealSap && result.Finished)
            SpendSap(held);

        if (message.Slips > 0 && operation is { ID: "MendNerve" })
            _status.TryUpdateStatusEffectDuration(patient.Value, SleepingSystem.StatusEffectForcedSleeping, TimeSpan.FromSeconds(12 + message.Slips * 4));

        if (verb == SurgeryVerbKind.Cauterize && !result.Rejected && message.Progress >= 0.85f)
            SpendWelder(held);

        if (result.RemoveOrgan || result.ClearEmbedded)
            _anesthesia.Tear(patient.Value, site.Zone, asleep);

        if (flinched)
            result.Key = "surgery-flinch";

        _infection.Contaminate(user, patient.Value, site, verb, advance || result.Finished);

        var implantStep = result.PlaceImplant || result.RemoveImplant;
        if (implantStep && advance)
        {
            if (result.PlaceImplant)
            {
                if (held == null || !_implants.TryInsert(patient.Value, implantSlot, held.Value))
                {
                    advance = false;
                    result.PlaceImplant = false;
                    result.Rejected = true;
                    result.Key = "surgery-implant-fit";
                }
            }
            else if (implantSlot.Length == 0 || !_implants.TryExtract(patient.Value, implantSlot, out _))
            {
                advance = false;
                result.RemoveImplant = false;
                result.Rejected = true;
                result.Key = "surgery-implant-gone";
            }
        }

        if (advance && stepVerb == verb)
        {
            if (stepVerb == SurgeryVerbKind.Clamp)
                site.Clamped = true;
            if (result.RemoveOrgan)
                site.Clamped = false;
            if (Operation(patient.Value, site, out var voiced) && site.Step < voiced.Steps.Count)
            {
                var acting = voiced.Steps[site.Step];
                Voice(patient.Value, acting.Speech);
                if (acting.Drop is { } drop)
                {
                    if (TryComp<HumanoidAppearanceComponent>(patient.Value, out var dropLook)
                        && acting.DropBySpecies.TryGetValue(dropLook.Species.Id, out var own))
                        drop = own;

                    var dropped = Spawn(drop, Transform(patient.Value).Coordinates.Offset(new Vector2(0.4f, 0.2f)));
                    _graft.Mark(dropped, patient.Value);
                    PartGone(patient.Value, site.Part, true);
                }

                if (acting.Need.Count > 0 && held != null)
                {
                    _graft.Grafted(patient.Value, held.Value, site.Zone, site.Part, null);
                    QueueDel(held.Value);
                    PartGone(patient.Value, site.Part, false);
                }

                if (acting.Effect.Length > 0)
                {
                    var effect = new SurgeryEffectEvent(user, held, acting.Effect);
                    RaiseLocalEvent(patient.Value, ref effect);
                }
            }
            if (verb == SurgeryVerbKind.Graft)
            {
                CoolHeat(patient.Value, site.Zone);
                if (held != null && GraftStack(patient.Value).Length > 0)
                    SpendHeld(held);
                else if (held != null)
                    SpendSkin(held.Value);
            }
            site.Step++;
            site.Rolled = 255;
            if (Operation(patient.Value, site, out var finished) && site.Step >= finished.Steps.Count)
            {
                site.CloseStep = 0;
                if (!FollowCare(patient.Value, site, finished) && NextClose(finished, site) == null)
                    Finish(patient.Value, site);
            }
        }
        else if (before != null && closeIndex >= 0 && CloseSteps(before, site)[closeIndex] is { } closing && closing.Verb == verb
                 && CloseReady(closing, site.Layers[index], result))
        {
            site.CloseStep = closeIndex + 1;
            closedStep = true;
        }

        if (!site.Done && Operation(patient.Value, site, out var running))
        {
            if (site.Step >= running.Steps.Count && NextClose(running, site) == null
                || verb == SurgeryVerbKind.Cauterize && Sealed(site) && NextClose(running, site) == null)
                Finish(patient.Value, site);
        }

        if (result.RemoveOrgan)
            site.Chosen = 255;
        site.Germs = Math.Clamp(site.Germs + result.Germs, 0f, 100f);
        if (result.Gel)
            site.Gel = true;

        if (result.Bone < 0f && place.BoneFloor > 0f && !site.Gel)
        {
            site.SpringBone = -result.Bone;
            site.SpringAt = _timing.CurTime + TimeSpan.FromSeconds(30);
        }
        else if (result.Bone < 0f)
        {
            site.SpringBone = 0f;
        }

        if (!result.SetHealth && _prototype.TryIndex<SurgeryHealthPrototype>(HealthRules, out var costs))
        {
            var loss = 0f;
            if ((advance || result.Finished) && costs.Cost.TryGetValue(verb, out var cost))
            {
                loss += cost;
                site.Cut += cost;
            }

            loss += message.Slips * costs.Slip + (flinched ? costs.Flinch : 0f);
            var gain = 0f;
            var keep = Machine(patient.Value) ? 0f : costs.Keep;
            if ((advance || closedStep) && costs.Restore.Contains(verb))
            {
                gain = MathF.Max(0f, site.Cut - keep) / RestoresLeft(before == null ? null : CloseSteps(before, site), closeIndex, costs);
                site.Cut -= gain;
            }

            result.Health = (gain - loss) * LimbWoundRules.HealthFor(site.Zone, CompOrNull<LimbBodyComponent>(patient.Value));
        }

        LimbOrgan? organ = null;
        if (!string.IsNullOrEmpty(stepOrgan) && Enum.TryParse<LimbOrgan>(stepOrgan, out var named))
            organ = named;
        else if (site.Part.Length == 0)
            organ = site.Chosen != 255 ? (LimbOrgan) site.Chosen : message.Organ == 255 ? null : (LimbOrgan) message.Organ;
        else if (verb == SurgeryVerbKind.Extract)
            result.RemoveOrgan = false;

        if (!implantStep)
        {
            var lodging = verb == SurgeryVerbKind.Insert && held != null && !HasComp<OrganComponent>(held.Value) && !HasComp<LimbImplantComponent>(held.Value);
            if (lodging && _cavity.Blocked(held!.Value))
            {
                result.PlaceOrgan = false;
                result.Rejected = true;
                result.Key = string.Empty;
            }
            else if (lodging)
            {
                if (!OpenCavity(site) || !_cavity.Fits(site.Zone, held!.Value))
                {
                    result.PlaceOrgan = false;
                    result.Rejected = true;
                    result.Key = "surgery-cavity-shut";
                }
                else if (result.PlaceOrgan)
                {
                    var piece = _stack.GetOne(held.Value);
                    if (!_cavity.TryLodge(patient.Value, site.Zone, piece))
                    {
                        result.PlaceOrgan = false;
                        result.Rejected = true;
                        result.Key = "surgery-cavity-full";
                    }
                    else
                    {
                        result.PlaceOrgan = false;
                    }
                }
            }
            else if (verb == SurgeryVerbKind.Insert
                && (organ == null || !OrganMissing(patient.Value, organ.Value) || !HeldIs(patient.Value, held, organ.Value))
                && !neededInsert)
            {
                result.PlaceOrgan = false;
                result.Rejected = true;
                result.Key = "surgery-insert-short";
            }
        }

        if (result.Examined && organ is { } seen)
            site.Examined.Add(seen);

        if (result.RemoveOrgan && organ == null && OpenCavity(site))
            _cavity.TryWithdraw(patient.Value, site.Zone, user);

        if (!result.Rejected && (advance || closedStep || result.Finished || message.Progress >= 0.3f))
            PlayStep(patient.Value, verb.ToString());
        if (message.Slips > 0)
            PlayStep(patient.Value, "Miss");
        _port.Apply(patient.Value, site.Zone, organ, result, held);
        Wither(patient.Value, site.Zone);
        if (verb == SurgeryVerbKind.Seat && result.Attach && ZoneSevered(patient.Value, site.Zone))
            result.Key = "surgery-wrong-hand";
        RemComp<SurgeryBusyComponent>(user);
        Send(patient.Value, user, site, held, result.Key, false);
    }

    private void PlayStep(EntityUid patient, string id)
    {
        if (_prototype.TryIndex<SurgerySoundPrototype>(id, out var sound))
            _audio.PlayPvs(sound.Sound, patient);
    }

    private void Wither(EntityUid patient, LimbZone zone)
    {
        if (!LimbWoundRules.IsBody(zone) || !TryComp<LimbBodyComponent>(patient, out var limbs))
            return;

        var index = LimbWoundRules.Index(zone);
        if ((uint) index >= (uint) limbs.Zones.Count)
            return;

        var state = limbs.Zones[index];
        if (state.Severed || state.Atrophied || state.Health > 0f)
            return;

        _limbs.ApplyEdit(patient, zone, new LimbEdit { SetAtrophy = true });
    }

    private static float RestoresLeft(List<SurgeryStep>? steps, int closeIndex, SurgeryHealthPrototype costs)
    {
        if (steps == null || closeIndex < 0)
            return 1f;

        var left = 0;
        for (var i = closeIndex; i < steps.Count; i++)
        {
            if (costs.Restore.Contains(steps[i].Verb))
                left++;
        }

        return Math.Max(1, left);
    }

    private bool HasAppendage(EntityUid patient, string id)
    {
        return _prototype.TryIndex<SurgeryAppendagePrototype>(id, out var appendage)
               && TryComp<HumanoidAppearanceComponent>(patient, out var look)
               && look.MarkingSet.Markings.TryGetValue(appendage.Category, out var list)
               && list.Count > 0;
    }

    private void RemoveAppendage(EntityUid patient, string id)
    {
        if (!_prototype.TryIndex<SurgeryAppendagePrototype>(id, out var appendage)
            || !TryComp<HumanoidAppearanceComponent>(patient, out var look)
            || !look.MarkingSet.Markings.TryGetValue(appendage.Category, out var list)
            || list.Count == 0)
            return;

        var itemId = appendage.Items.TryGetValue(look.Species.Id, out var own) ? own : appendage.Item;
        var item = Spawn(itemId, Transform(patient).Coordinates.Offset(new Vector2(0.5f, 0.25f)));
        _graft.Mark(item, patient);
        var part = EnsureComp<SurgeryAppendageComponent>(item);
        if (part.Revoke.Count > 0)
            EntityManager.AddComponents(patient, part.Revoke);
        part.Appendage = id;
        part.Markings.Clear();
        foreach (var marking in list)
            part.Markings.Add(new Marking(marking));

        part.Carry.Clear();
        foreach (var name in appendage.Carry)
        {
            if (!_factory.TryGetRegistration(name, out var registration) || !HasComp(patient, registration.Type))
                continue;

            part.Carry.Add(name);
            RemComp(patient, registration.Type);
        }

        Dirty(item, part);
        for (var i = list.Count - 1; i >= 0; i--)
            _humanoid.RemoveMarking(patient, appendage.Category, i, look);

        if (appendage.Slots.Count == 0)
            return;

        var locked = EnsureComp<SurgerySlotLockComponent>(patient);
        foreach (var slot in appendage.Slots)
        {
            _inventory.TryUnequip(patient, slot, silent: true, force: true);
            locked.Slots.Add(slot);
        }

        Dirty(patient, locked);
    }

    private bool AttachAppendage(EntityUid patient, EntityUid? held)
    {
        if (held is not { } item
            || !TryComp<SurgeryAppendageComponent>(item, out var part)
            || HasAppendage(patient, part.Appendage)
            || !TryComp<HumanoidAppearanceComponent>(patient, out var look)
            || !_graft.Accepts(patient, item))
            return false;

        _graft.Grafted(patient, item, LimbZone.Groin, null, part.Appendage);
        foreach (var marking in part.Markings)
            _humanoid.AddMarking(patient, marking.MarkingId, marking.MarkingColors, true, true, look);

        foreach (var name in part.Carry)
        {
            if (_factory.TryGetRegistration(name, out var registration) && !HasComp(patient, registration.Type))
                AddComp(patient, (Component) _factory.GetComponent(registration.Type));
        }

        if (part.Grant.Count > 0)
            EntityManager.AddComponents(patient, part.Grant);

        if (TryComp<SurgerySlotLockComponent>(patient, out var locked)
            && _prototype.TryIndex<SurgeryAppendagePrototype>(part.Appendage, out var appendage))
        {
            foreach (var slot in appendage.Slots)
                locked.Slots.Remove(slot);

            if (locked.Slots.Count == 0)
                RemComp(patient, locked);
            else
                Dirty(patient, locked);
        }

        Del(item);
        return true;
    }

    private void OnSevered(Entity<SurgerySiteComponent> ent, ref LimbSeveredEvent args)
    {
        var site = ent.Comp;
        var gone = new HashSet<LimbZone> { args.Zone };
        if (LimbWoundRules.TryCarried(args.Zone, out var child))
            gone.Add(child);
        if (args.Zone == LimbZone.Head)
        {
            gone.Add(LimbZone.Eyes);
            gone.Add(LimbZone.Mouth);
        }

        var current = KeptKey(site.Zone, site.Part, site.Back);
        var changed = false;
        foreach (var key in site.Kept.Keys.ToList())
        {
            if (key == current || !gone.Contains(SurgeryWounds.ZoneOf(key)))
                continue;

            site.Kept.Remove(key);
            changed = true;
        }

        if (site.Zone != args.Zone && gone.Contains(site.Zone) && !site.Done)
        {
            CloseSite(site);
            changed = true;
        }

        if (changed)
            Dirty(ent);
    }

    private bool ZoneSevered(EntityUid patient, LimbZone zone)
    {
        if (!LimbWoundRules.IsBody(zone) || !TryComp<LimbBodyComponent>(patient, out var limbs))
            return false;

        var index = LimbWoundRules.Index(zone);
        return (uint) index < (uint) limbs.Zones.Count && limbs.Zones[index].Severed;
    }

    private void Send(EntityUid patient, EntityUid user, SurgerySiteComponent site, EntityUid? held, string line, bool play)
    {
        Dirty(patient, site);
        if (!_players.TryGetSessionByEntity(user, out var session))
            return;

        var tool = held == null ? null : CompOrNull<SurgeryToolComponent>(held.Value);
        var place = Place(patient);
        var asleep = Asleep(patient);
        var verbs = new List<byte> { (byte) SurgeryVerbKind.Inspect };
        if (tool != null)
        {
            foreach (var verb in tool.VerbsFor(Machine(patient), Plant(patient), Soft(patient)))
                verbs.Add((byte) verb);
        }

        if (held != null && HasComp<SurgerySkinRollComponent>(held.Value))
            verbs.Add((byte) SurgeryVerbKind.Graft);
        if (held != null && Wraps(held.Value))
            verbs.Add((byte) SurgeryVerbKind.Bandage);
        if (held != null && _infection.CanCure(held.Value))
            verbs.Add((byte) SurgeryVerbKind.Inject);
        if (held != null && HasComp<SurgeryAppendageComponent>(held.Value))
            verbs.Add((byte) SurgeryVerbKind.Append);
        if (held != null && HasComp<BodyPartComponent>(held.Value))
            verbs.Add((byte) SurgeryVerbKind.Seat);
        if (held != null && HasComp<OrganComponent>(held.Value))
            verbs.Add((byte) SurgeryVerbKind.Insert);
        else if (held != null && OpenCavity(site) && _cavity.Fits(site.Zone, held.Value))
            verbs.Add((byte) SurgeryVerbKind.Insert);
        else if (held != null && Tooth(site) && TeethInsert(patient, site) && _teeth.CanLodge(patient, held.Value))
            verbs.Add((byte) SurgeryVerbKind.Insert);

        var organs = new List<byte>();
        var empty = new List<byte>();
        var examined = new List<byte>();
        if (CavityReady(site) && TryComp<LimbBodyComponent>(patient, out var limbs))
        {
            var open = OpenCavity(site);
            var machine = Machine(patient);
            foreach (var organ in _prototype.EnumeratePrototypes<SurgeryOrganPrototype>())
            {
                if (!Enum.TryParse<LimbOrgan>(organ.ID, out var kind) || !organ.InZone(site.Zone, machine))
                    continue;

                var bit = (LimbOrganMask) (1 << (int) kind);
                if ((limbs.Present & bit) == 0)
                    continue;

                var index = (int) kind;
                var missing = (uint) index < (uint) limbs.Organs.Count && limbs.Organs[index].Missing;
                if (missing)
                {
                    if (open)
                        empty.Add((byte) kind);
                    continue;
                }

                organs.Add((byte) kind);
                if (site.Examined.Contains(kind))
                    examined.Add((byte) kind);
            }
        }

        var kinds = new byte[site.Layers.Count];
        var integrity = new float[site.Layers.Count];
        var retracted = new byte[site.Layers.Count];
        for (var i = 0; i < site.Layers.Count; i++)
        {
            kinds[i] = (byte) site.Layers[i].Kind;
            integrity[i] = site.Layers[i].Integrity;
            retracted[i] = site.Layers[i].Retracted ? (byte) 1 : (byte) 0;
        }

        var shake = (tool?.Wobble ?? 22f) * (0.55f + place.Slip) * (asleep ? 0.45f : 1.15f);
        CurrentStep(patient, site, out var stepVerb, out var stepLayer);
        var stepGame = SurgeryGameKind.Cut;
        var playedAcross = false;
        var stepHint = "";
        if (Operation(patient, site, out var played))
        {
            playedAcross = played.Across;
            if (StepNow(patient, site) is { } now)
            {
                stepGame = RollGame(site, now);
                stepHint = now.Hint;
            }
        }

        byte bleeds = 0;
        if (stepGame == SurgeryGameKind.Stanch && LimbWoundRules.IsBody(site.Zone) && TryComp<LimbBodyComponent>(patient, out var bleeding))
            bleeds = (byte) Math.Clamp((int) MathF.Ceiling(bleeding.Zones[LimbWoundRules.Index(site.Zone)].Bleed / 2f), 1, 5);
        RaiseNetworkEvent(new SurgeryBoardMessage
        {
            Patient = GetNetEntity(patient),
            Zone = (byte) site.Zone,
            Wobble = shake,
            Lag = 0.35f + place.Slip * 0.45f + (asleep ? 0f : 0.12f),
            SurfaceSlip = place.Slip,
            BoneFloor = place.BoneFloor,
            Asleep = asleep ? (byte) 1 : (byte) 0,
            Line = line.Length == 0 ? string.Empty : Loc.GetString(line),
            Verbs = verbs.ToArray(),
            LayerKinds = kinds,
            Integrity = integrity,
            Retracted = retracted,
            Organs = organs.ToArray(),
            Empty = empty.ToArray(),
            Examined = examined.ToArray(),
            Play = play ? (byte) 1 : (byte) 0,
            Embedded = Embedded(patient, site.Zone) ? (byte) 1 : (byte) 0,
            Lodged = LodgedProto(patient, site.Zone),
            LodgedItem = LodgedEntity(patient, site.Zone),
            StepVerb = stepLayer < 0 ? (byte) 255 : (byte) stepVerb,
            StepLayer = stepLayer < 0 ? (byte) 255 : (byte) stepLayer,
            Chosen = site.Chosen,
            StepGame = (byte) stepGame,
            CloseVerb = CloseVerb(patient, site),
            UndoVerb = UndoVerb(patient, site),
            SawHits = (byte) Math.Clamp(tool?.SawHits ?? 3, 1, 6),
            Drill = stepGame == SurgeryGameKind.Drill ? (byte) 1 : (byte) 0,
            Across = playedAcross ? (byte) 1 : (byte) 0,
            Finished = site.Done ? (byte) 1 : (byte) 0,
            Part = site.Part,
            StepHint = stepHint,
            Bleeds = bleeds,
        }, session);
    }

    private bool SpeechStep(EntityUid patient, SurgerySiteComponent site, string speech)
    {
        return Operation(patient, site, out var operation)
               && site.Step < operation.Steps.Count
               && operation.Steps[site.Step].Speech == speech
               && operation.Steps[site.Step].Organ.Length == 0;
    }

    private bool InsertFits(EntityUid patient, SurgeryStep step, EntityUid? held)
    {
        if (held == null)
            return false;

        if (step.Implant.Length > 0)
            return _implants.Fits(patient, step.Implant, held.Value);

        if (step.Need.Count > 0)
            return MetaData(held.Value).EntityPrototype is { } proto && step.Need.Contains(proto.ID) && _graft.Accepts(patient, held.Value);

        if (step.Organ.Length > 0)
            return Enum.TryParse<LimbOrgan>(step.Organ, out var organ) && OrganMissing(patient, organ) && HeldIs(patient, held, organ);

        return false;
    }

    private void Voice(EntityUid patient, string speech)
    {
        if (speech.Length == 0)
            return;

        if (speech == "Clear")
        {
            RemComp<Content.Server.Speech.Components.MumbleAccentComponent>(patient);
            return;
        }

        if (_prototype.TryIndex<SurgerySpeechPrototype>(speech, out _) )
            EnsureComp<Content.Server.Speech.Components.MumbleAccentComponent>(patient);
    }

    private static bool WoundOpen(SurgerySiteComponent site)
    {
        return SurgeryWounds.Open(site.Layers);
    }

    private static bool WoundOpen(IReadOnlyList<SurgeryLayerState> layers)
    {
        return SurgeryWounds.Open(layers);
    }

    private SurgeryStep? NextClose(SurgeryOperationPrototype operation, SurgerySiteComponent site)
    {
        var steps = CloseSteps(operation, site);
        var index = CloseIndex(steps, site);
        return index < 0 ? null : steps[index];
    }

    private List<SurgeryStep> CloseSteps(SurgeryOperationPrototype operation, SurgerySiteComponent site)
    {
        var steps = new List<SurgeryStep>(operation.Close);
        SurgeryOperationPrototype? opened = null;
        if (site.Part.Length == 0
            && _prototype.TryIndex<SurgeryAnatomyPrototype>(AnatomyId(site.Owner, site.Zone), out var anatomy)
            && anatomy.Operation is { } open
            && open.Id != operation.ID
            && _prototype.TryIndex<SurgeryOperationPrototype>(open, out var opening))
        {
            opened = opening;
            foreach (var step in opening.Close)
            {
                if (SurgeryProcedure.Index(site.Layers, step.Layer) >= 0 && !steps.Exists(own => own.Layer == step.Layer))
                    steps.Add(step);
            }
        }

        steps = steps.Exists(step => step.Order >= 0)
            ? steps.OrderBy(step => step.Order < 0 ? int.MaxValue : step.Order).ToList()
            : steps.OrderByDescending(step => LayerIndex(site, step.Layer)).ToList();

        if (!operation.CloseRetract
            || !SpreadsSkin(operation) && (opened == null || !SpreadsSkin(opened))
            || steps.Exists(step => step.Verb == SurgeryVerbKind.Retract))
            return steps;

        var seal = steps.FindIndex(step => step.Layer == SurgeryLayerKind.Skin
                                           && step.Verb is SurgeryVerbKind.Cauterize or SurgeryVerbKind.SealSap);
        if (seal >= 0)
            steps.Insert(seal, SkinBack);

        return steps;
    }

    private static bool SpreadsSkin(SurgeryOperationPrototype operation)
    {
        return operation.Steps.Exists(step => step is { Verb: SurgeryVerbKind.Retract, Layer: SurgeryLayerKind.Skin });
    }

    private int CloseIndex(SurgeryOperationPrototype operation, SurgerySiteComponent site)
    {
        return CloseIndex(CloseSteps(operation, site), site);
    }

    private static int CloseIndex(List<SurgeryStep> steps, SurgerySiteComponent site)
    {
        for (var i = Math.Max(0, site.CloseStep); i < steps.Count; i++)
        {
            var step = steps[i];
            var index = LayerIndex(site, step.Layer);
            if (index < 0)
                continue;

            if (!Closed(step, site.Layers[index]))
                return i;
        }

        return -1;
    }

    private static bool CloseReady(SurgeryStep step, SurgeryLayerState layer, SurgeryResult result)
    {
        if (step.Optional)
        {
            return step.Verb switch
            {
                SurgeryVerbKind.Seat => result.Attach,
                _ => result.Finished,
            };
        }

        return Explicit(step.Verb) ? result.Finished : Closed(step, layer);
    }

    private static bool Explicit(SurgeryVerbKind verb)
    {
        return verb is SurgeryVerbKind.Bandage or SurgeryVerbKind.Stanch or SurgeryVerbKind.Inject;
    }

    private static bool Closed(SurgeryStep step, SurgeryLayerState layer)
    {
        if (step.Optional)
            return false;

        if (step.Verb == SurgeryVerbKind.Retract)
            return !layer.Retracted;

        return !Explicit(step.Verb) && !layer.Retracted && layer.Integrity >= 0.85f;
    }

    private static bool StepClosed(SurgerySiteComponent site, SurgeryStep step)
    {
        var index = LayerIndex(site, step.Layer);
        return index < 0 || Closed(step, site.Layers[index]);
    }

    private static int LayerIndex(SurgerySiteComponent site, SurgeryLayerKind kind)
    {
        var index = SurgeryProcedure.Index(site.Layers, kind);
        return index >= 0 || site.Layers.Count == 0 ? index : site.Layers.Count - 1;
    }

    private void Finish(EntityUid patient, SurgerySiteComponent site)
    {
        if (Machine(patient) && site.Cut > 0f)
        {
            _limbs.ApplyEdit(patient, site.Zone, new LimbEdit { Health = site.Cut * LimbWoundRules.HealthFor(site.Zone, CompOrNull<LimbBodyComponent>(patient)) });
            site.Cut = 0f;
        }

        _infection.Settle(patient, site);
        CloseSite(site);
    }

    private byte CloseVerb(EntityUid patient, SurgerySiteComponent site)
    {
        if (!WoundOpen(site) || !Operation(patient, site, out var operation))
            return 255;

        if (site.Step > 0 && site.Step < operation.Steps.Count)
            return (byte) SurgeryVerbKind.Cauterize;

        var steps = CloseSteps(operation, site);
        var index = CloseIndex(steps, site);
        if (index < 0)
            return 255;

        if (steps[index].Optional && site.CloseStep == 0)
        {
            for (var i = index + 1; i < steps.Count; i++)
            {
                if (!steps[i].Optional && !StepClosed(site, steps[i]))
                    return (byte) steps[i].Verb;
            }
        }

        return (byte) steps[index].Verb;
    }

    private void Abort(EntityUid patient, SurgerySiteComponent site)
    {
        for (var i = 0; i < site.Layers.Count; i++)
        {
            var layer = site.Layers[i];
            layer.Integrity = 1f;
            layer.Retracted = false;
            site.Layers[i] = layer;
        }

        site.Step = 0;
        site.CloseStep = 0;
        site.Clamped = false;
        site.Chosen = 255;
        Finish(patient, site);
    }

    private byte UndoVerb(EntityUid patient, SurgerySiteComponent site)
    {
        if (!Operation(patient, site, out var operation) || operation.Steps.Count == 0 || site.Step <= 0)
            return 255;

        var target = site.Step - 1;
        if (target >= operation.Steps.Count)
            return 255;

        return UndoOf(operation.Steps[target], site.Step >= operation.Steps.Count);
    }

    private static byte UndoOf(SurgeryStep step, bool closing)
    {
        if (step.Verb == SurgeryVerbKind.Retract)
            return closing ? (byte) 255 : (byte) SurgeryVerbKind.Retract;

        return ParseUndo(step.Undo);
    }

    private bool TryUndo(EntityUid patient, SurgerySiteComponent site, SurgeryVerbKind verb, EntityUid? held)
    {
        if (!VerbAllowed(verb, held, site) || !Operation(patient, site, out var operation) || site.Step <= 0)
            return false;

        var target = site.Step - 1;
        if (target >= operation.Steps.Count)
            return false;

        if (site.Step < operation.Steps.Count && operation.Steps[site.Step].Verb == verb)
            return false;

        if (UndoOf(operation.Steps[target], site.Step >= operation.Steps.Count) != (byte) verb)
            return false;

        var undone = operation.Steps[target];
        if (verb == SurgeryVerbKind.Insert && undone.Implant.Length > 0
            && (held == null || !_implants.TryInsert(patient, undone.Implant, held.Value)))
            return false;

        Revert(site, undone);
        site.Step = target;
        if (site.Step == 0 && !StepApplied(operation.Steps[0], site))
            Finish(patient, site);
        return true;
    }

    private static void Revert(SurgerySiteComponent site, SurgeryStep step)
    {
        var index = SurgeryProcedure.Index(site.Layers, step.Layer);
        if (index < 0)
            return;

        var layer = site.Layers[index];
        switch (step.Verb)
        {
            case SurgeryVerbKind.Incise:
            case SurgeryVerbKind.Chop:
            case SurgeryVerbKind.SawBone:
                layer.Integrity = 1f;
                layer.Retracted = false;
                break;
            case SurgeryVerbKind.Retract:
                layer.Retracted = false;
                site.Layers[index] = layer;
                SurgeryProcedure.Conceal(site.Layers, index);
                return;
            case SurgeryVerbKind.Cauterize:
            case SurgeryVerbKind.SealSap:
                layer.Integrity = 0.4f;
                layer.Retracted = false;
                break;
            case SurgeryVerbKind.Clamp:
                site.Clamped = false;
                break;
        }

        site.Layers[index] = layer;
    }

    private static bool StepApplied(SurgeryStep step, SurgerySiteComponent site)
    {
        var index = SurgeryProcedure.Index(site.Layers, step.Layer);
        if (index < 0)
            return false;

        var layer = site.Layers[index];
        return step.Verb switch
        {
            SurgeryVerbKind.Incise or SurgeryVerbKind.Chop or SurgeryVerbKind.SawBone => layer.Integrity < 0.9f,
            SurgeryVerbKind.Retract => layer.Retracted,
            SurgeryVerbKind.Cauterize or SurgeryVerbKind.SealSap => layer.Integrity >= 0.85f && !layer.Retracted,
            SurgeryVerbKind.Clamp => site.Clamped,
            _ => false,
        };
    }

    private static byte ParseUndo(string undo)
    {
        return Enum.TryParse<SurgeryVerbKind>(undo, true, out var verb) ? (byte) verb : (byte) 255;
    }

    private static string KeptKey(LimbZone zone, string part, bool back)
    {
        return SurgeryWounds.Key(zone, part, back);
    }

    private static void RestoreKept(SurgerySiteComponent site, SurgeryKept kept, LimbZone zone)
    {
        site.Done = false;
        site.Zone = zone;
        site.Part = kept.Part;
        site.Queued = kept.Queued;
        site.Back = kept.Back;
        site.Layers = new List<SurgeryLayerState>(kept.Layers);
        site.Step = kept.Step;
        site.CloseStep = kept.CloseStep;
        site.Chosen = kept.Chosen;
        site.Clamped = kept.Clamped;
        site.Germs = kept.Germs;
        site.Gel = kept.Gel;
        site.Cut = kept.Cut;
        site.Examined = new HashSet<LimbOrgan>(kept.Examined);
    }

    private static void Keep(SurgerySiteComponent site)
    {
        site.Kept[KeptKey(site.Zone, site.Part, site.Back)] = new SurgeryKept
        {
            Zone = site.Zone,
            Part = site.Part,
            Queued = site.Queued,
            Back = site.Back,
            Layers = new List<SurgeryLayerState>(site.Layers),
            Step = site.Step,
            Chosen = site.Chosen,
            Clamped = site.Clamped,
            Germs = site.Germs,
            Gel = site.Gel,
            Cut = site.Cut,
            CloseStep = site.CloseStep,
            Examined = new HashSet<LimbOrgan>(site.Examined),
        };
    }

    private static void CloseSite(SurgerySiteComponent site)
    {
        for (var i = 0; i < site.Layers.Count; i++)
        {
            var layer = site.Layers[i];
            layer.Integrity = 1f;
            layer.Retracted = false;
            site.Layers[i] = layer;
        }

        site.Step = 0;
        site.CloseStep = 0;
        site.Clamped = false;
        site.Chosen = 255;
        site.Queued = "";
        site.Kept.Remove(KeptKey(site.Zone, site.Part, site.Back));
        site.Part = "";
        site.Done = true;
    }

    private void Rebuild(EntityUid patient, SurgerySiteComponent site)
    {
        var anatomyId = AnatomyId(patient, site.Zone);
        if (site.Part.Length > 0
            && _prototype.TryIndex<SurgeryPartPrototype>(site.Part, out var part)
            && part.Anatomy.Length > 0)
            anatomyId = part.Anatomy;

        if (!_prototype.TryIndex<SurgeryAnatomyPrototype>(anatomyId, out var anatomy))
            return;

        site.Layers.Clear();
        site.Step = 0;
        site.Chosen = 255;
        site.Clamped = false;
        foreach (var kind in anatomy.Layers)
            site.Layers.Add(new SurgeryLayerState { Kind = kind, Integrity = 1f });
    }

    private string AnatomyId(EntityUid patient, LimbZone zone)
    {
        var fallback = zone switch
        {
            LimbZone.Torso => "Torso",
            LimbZone.Head => "Head",
            LimbZone.Groin => "Groin",
            LimbZone.Eyes => "Eyes",
            _ => "FleshLimb",
        };

        if (!TryComp<HumanoidAppearanceComponent>(patient, out var look))
            return fallback;

        if (!_prototype.TryIndex<SurgerySpeciesPrototype>(look.Species.Id, out var species))
            return fallback;

        ProtoId<SurgeryAnatomyPrototype>? pick = zone switch
        {
            LimbZone.Torso => species.Torso,
            LimbZone.Head => species.Head,
            LimbZone.Groin => species.Groin,
            LimbZone.Eyes => species.Eyes,
            _ => species.Limb,
        };

        return pick is { } chosen && chosen.Id.Length > 0 ? chosen.Id : fallback;
    }

    private (float Slip, float BoneFloor) Place(EntityUid patient)
    {
        if (TryComp<BuckleComponent>(patient, out var buckle)
            && buckle.BuckledTo is { } strap
            && TryComp<SurgerySurfaceComponent>(strap, out var surface)
            && _prototype.TryIndex(surface.Place, out SurgeryPlacePrototype? place))
            return (place.Slip, place.BoneFloor);

        if (_prototype.TryIndex<SurgeryPlacePrototype>(FloorPlace, out var floor))
            return (floor.Slip, floor.BoneFloor);

        return (0.7f, 25f);
    }

    private string LodgedProto(EntityUid patient, LimbZone zone)
    {
        if (!TryComp<SurgeryCavityComponent>(patient, out var hold))
            return "";

        return zone switch
        {
            LimbZone.Head => hold.HeadProto,
            LimbZone.Torso => hold.ChestProto,
            LimbZone.Groin => hold.AbdomenProto,
            _ => "",
        };
    }

    private NetEntity LodgedEntity(EntityUid patient, LimbZone zone)
    {
        if (!TryComp<SurgeryCavityComponent>(patient, out var hold))
            return NetEntity.Invalid;

        return zone switch
        {
            LimbZone.Head => hold.HeadItem,
            LimbZone.Torso => hold.ChestItem,
            LimbZone.Groin => hold.AbdomenItem,
            _ => NetEntity.Invalid,
        };
    }

    private void Spill(EntityUid patient, SurgerySiteComponent site)
    {
        var open = false;
        foreach (var layer in site.Layers)
        {
            if (layer.Integrity < 0.9f || layer.Retracted)
                open = true;
        }

        if (open && LimbWoundRules.IsBody(site.Zone))
            _limbs.ApplyEdit(patient, site.Zone, new LimbEdit { Bleed = 2f });
    }

    private void Reveal(SurgerySiteComponent site)
    {
        SurgeryProcedure.Reveal(site.Layers);
    }

    private void SkipOptional(SurgerySiteComponent site, SurgeryOperationPrototype operation, SurgeryVerbKind verb)
    {
        if (site.CloseStep != 0 || site.Step < operation.Steps.Count)
            return;

        var steps = CloseSteps(operation, site);
        var index = CloseIndex(steps, site);
        if (index < 0 || !steps[index].Optional || steps[index].Verb == verb)
            return;

        var target = -1;
        for (var i = index; i < steps.Count; i++)
        {
            if (steps[i].Optional || StepClosed(site, steps[i]))
                continue;

            if (steps[i].Verb == verb)
                target = i;
            break;
        }

        if (target < 0)
            return;

        site.CloseStep = target;
    }

    private int StepIndex(EntityUid patient, SurgerySiteComponent site, SurgeryVerbKind verb)
    {
        if (!Operation(patient, site, out var operation))
            return Gate(site, verb, 0);

        Rewind(site, operation);
        site.Step = SurgeryProcedure.CatchUp(operation.Steps, site.Layers, site.Step);
        SkipOptional(site, operation, verb);
        var step = site.Step;
        if (step >= 0 && step < operation.Steps.Count && operation.Steps[step].Verb == verb)
            return LayerIndex(site, operation.Steps[step].Layer);

        var closing = step >= operation.Steps.Count ? NextClose(operation, site) : null;
        if (closing != null && closing.Verb == verb)
            return LayerIndex(site, closing.Layer);

        if (verb == SurgeryVerbKind.Extract && OrganLeft(patient, site))
            return SurgeryProcedure.Index(site.Layers, SurgeryLayerKind.Cavity);

        if (verb == SurgeryVerbKind.Insert && OpenCavity(site))
            return SurgeryProcedure.Index(site.Layers, SurgeryLayerKind.Cavity);

        return -1;
    }

    private void Rewind(SurgerySiteComponent site, SurgeryOperationPrototype operation)
    {
        if (site.Step < operation.Steps.Count || !Sealed(site) || ZoneSevered(site.Owner, site.Zone) || NextClose(operation, site) != null)
            return;

        site.Step = 0;
        site.Clamped = false;
        site.Chosen = 255;
    }

    private static bool Sealed(SurgerySiteComponent site)
    {
        foreach (var layer in site.Layers)
        {
            if (layer.Kind != SurgeryLayerKind.Skin)
                continue;

            return !layer.Retracted && layer.Integrity >= 0.85f;
        }

        return true;
    }

    private static bool OpenCavity(SurgerySiteComponent site)
    {
        if (!CavityReady(site))
            return false;

        foreach (var layer in site.Layers)
        {
            if (layer.Kind == SurgeryLayerKind.Skin && !layer.Retracted && layer.Integrity > 0.85f)
                return false;
        }

        return true;
    }

    private bool OrganMissing(EntityUid patient, LimbOrgan organ)
    {
        if (!TryComp<LimbBodyComponent>(patient, out var limbs))
            return false;

        if (organ == LimbOrgan.Eyes && limbs.EyesGone > 0)
            return true;

        var index = (int) organ;
        return (uint) index < (uint) limbs.Organs.Count && limbs.Organs[index].Missing;
    }

    private bool HeldIs(EntityUid patient, EntityUid? held, LimbOrgan organ)
    {
        return held != null && SurgeryOrganKindComponent.Matches(EntityManager, held.Value, organ.ToString(), Machine(patient));
    }

    private bool OrganLeft(EntityUid patient, SurgerySiteComponent site)
    {
        if (!OpenCavity(site) || !TryComp<LimbBodyComponent>(patient, out var limbs))
            return false;

        var machine = Machine(patient);
        foreach (var organ in _prototype.EnumeratePrototypes<SurgeryOrganPrototype>())
        {
            if (!Enum.TryParse<LimbOrgan>(organ.ID, out var kind) || !organ.InZone(site.Zone, machine))
                continue;

            var bit = (LimbOrganMask) (1 << (int) kind);
            if ((limbs.Present & bit) == 0)
                continue;

            var index = (int) kind;
            if ((uint) index < (uint) limbs.Organs.Count && limbs.Organs[index].Missing)
                continue;

            return true;
        }

        return false;
    }

    private void CurrentStep(EntityUid patient, SurgerySiteComponent site, out SurgeryVerbKind verb, out int layer)
    {
        verb = SurgeryVerbKind.Inspect;
        layer = -1;
        if (!Operation(patient, site, out var operation))
            return;

        Rewind(site, operation);
        site.Step = SurgeryProcedure.CatchUp(operation.Steps, site.Layers, site.Step);
        var step = site.Step;
        if (step < 0)
            return;

        var current = step < operation.Steps.Count ? operation.Steps[step] : NextClose(operation, site);
        if (current == null)
            return;

        var index = LayerIndex(site, current.Layer);
        if (index < 0)
            return;

        verb = current.Verb;
        layer = index;
    }

    private SurgeryStep? StepNow(EntityUid patient, SurgerySiteComponent site)
    {
        if (!Operation(patient, site, out var operation))
            return null;

        return site.Step < operation.Steps.Count ? operation.Steps[site.Step] : NextClose(operation, site);
    }

    private bool StepIntact(EntityUid patient, SurgerySiteComponent site, SurgeryVerbKind verb)
    {
        return StepNow(patient, site) is { } step && step.Verb == verb && step.Intact;
    }

    private bool TeethInsert(EntityUid patient, SurgerySiteComponent site)
    {
        return Operation(patient, site, out var operation)
               && site.Step < operation.Steps.Count
               && operation.Steps[site.Step].Verb == SurgeryVerbKind.Insert;
    }

    private bool IsOpening(EntityUid patient, LimbZone zone, SurgeryOperationPrototype operation)
    {
        return _prototype.TryIndex<SurgeryAnatomyPrototype>(AnatomyId(patient, zone), out var anatomy)
               && anatomy.Operation is { } open
               && operation.ID == open.Id;
    }

    private bool FollowCare(EntityUid patient, SurgerySiteComponent site, SurgeryOperationPrototype finished)
    {
        if (site.Part.Length > 0
            || !IsOpening(patient, site.Zone, finished)
            || OpeningCare(patient, site.Zone) is not { } next
            || next.ID == finished.ID)
            return false;

        site.Queued = next.ID;
        site.Step = SurgeryProcedure.CatchUp(next.Steps, site.Layers, 0);
        return site.Step < next.Steps.Count;
    }

    private SurgeryOperationPrototype? OpeningCare(EntityUid patient, LimbZone zone)
    {
        if (CareFor(patient, zone) is not { } id
            || !_prototype.TryIndex<SurgeryOperationPrototype>(id, out var care)
            || care.Steps.Count == 0
            || care.Steps[0].Verb != SurgeryVerbKind.Incise)
            return null;

        return care;
    }

    private bool Operation(EntityUid patient, SurgerySiteComponent site, out SurgeryOperationPrototype operation)
    {
        operation = null!;
        if (site.Done)
            return false;

        if (site.Queued.Length > 0 && TryUseOperation(patient, site.Queued, out operation))
            return true;

        if (site.Part.Length > 0
            && _prototype.TryIndex<SurgeryPartPrototype>(site.Part, out var part)
            && part.Operation.Length > 0
            && TryUseOperation(patient, part.Operation, out operation))
            return true;

        if (HasComp<SurgeryProneComponent>(patient))
            return false;

        if (!_prototype.TryIndex<SurgeryAnatomyPrototype>(AnatomyId(patient, site.Zone), out var anatomy)
            || anatomy.Operation is not { } id)
            return false;

        if (TryUseOperation(patient, id, out operation))
            return true;

        var open = site.Zone switch
        {
            LimbZone.Torso => "OpenTorso",
            LimbZone.Head => "OpenHead",
            LimbZone.Groin => "OpenGroin",
            _ => "OpenLimb",
        };
        return TryUseOperation(patient, open, out operation);
    }

    private bool TryUseOperation(EntityUid patient, string id, out SurgeryOperationPrototype operation)
    {
        operation = null!;
        if (!_prototype.TryIndex<SurgeryOperationPrototype>(id, out var found) || Blocked(patient, found))
            return false;

        operation = found;
        return true;
    }

    public bool OnTable(EntityUid patient)
    {
        return TryComp<BuckleComponent>(patient, out var buckle)
               && buckle.BuckledTo is { } strap
               && HasComp<SurgerySurfaceComponent>(strap);
    }

    private bool Infected(EntityUid patient, LimbZone zone)
    {
        return TryComp<SurgeryInfectionComponent>(patient, out var infection) && infection.Rots(zone);
    }

    private bool Blocked(EntityUid patient, SurgeryOperationPrototype operation)
    {
        return !operation.Suits(Machine(patient), Soft(patient), Plant(patient));
    }

    private bool Machine(EntityUid patient)
    {
        return TryComp<HumanoidAppearanceComponent>(patient, out var look) && SurgerySpeciesPrototype.IsMachine(_prototype, look.Species.Id);
    }

    private bool Soft(EntityUid patient)
    {
        return TryComp<LimbBodyComponent>(patient, out var limbs) && limbs.Soft;
    }

    private bool Plant(EntityUid patient)
    {
        return TryComp<LimbBodyComponent>(patient, out var limbs) && limbs.Plant;
    }

    private bool ToolFits(EntityUid patient, SurgeryToolComponent tool)
    {
        return tool.VerbsFor(Machine(patient), Plant(patient), Soft(patient)).Count > 0;
    }

    private string? CareFor(EntityUid patient, LimbZone zone)
    {
        if (!LimbWoundRules.IsBody(zone) || !TryComp<LimbBodyComponent>(patient, out var limbs))
            return null;

        var index = LimbWoundRules.Index(zone);
        if ((uint) index >= (uint) limbs.Zones.Count)
            return null;

        var state = limbs.Zones[index];
        var bone = HasLayer(patient, zone, SurgeryLayerKind.Bone);
        var infected = Infected(patient, zone);
        var field = !OnTable(patient);
        var prone = HasComp<SurgeryProneComponent>(patient);
        var cracked = SurgeryConditions.OrganCracked(limbs.Organs, zone);
        foreach (var when in SurgeryConditions.Order)
        {
            if (!SurgeryConditions.Has(when, state, bone, infected, cracked, LimbWoundRules.HealthFor(zone, limbs)))
                continue;

            string? fallback = null;
            foreach (var operation in _prototype.EnumeratePrototypes<SurgeryOperationPrototype>())
            {
                if (operation.When != when || Blocked(patient, operation) || !operation.Faces(prone))
                    continue;

                if (!operation.Fits(zone))
                    continue;

                if (operation.Field == field)
                    return operation.ID;

                if (!operation.Field)
                    fallback ??= operation.ID;
            }

            if (fallback != null)
                return fallback;
        }

        return null;
    }

    private bool HasLayer(EntityUid patient, LimbZone zone, SurgeryLayerKind kind)
    {
        return _prototype.TryIndex<SurgeryAnatomyPrototype>(AnatomyId(patient, zone), out var anatomy)
            && anatomy.Layers.Contains(kind);
    }

    private bool FitsAsked(EntityUid patient, EntityUid user, LimbZone zone, SurgeryOperationPrototype operation)
    {
        if (Blocked(patient, operation) || operation.Field && OnTable(patient) || !operation.Faces(HasComp<SurgeryProneComponent>(patient)))
            return false;

        if (!_nearby.Has(patient, operation, user))
            return false;

        if (operation.Appendage.Length > 0
            && (!SurgeryAppendagePrototype.Allowed(_prototype, EntityManager, patient, operation.Appendage)
                || operation.Attach == HasAppendage(patient, operation.Appendage)))
            return false;

        if (!operation.Fits(zone))
            return false;

        if (!LimbWoundRules.IsBody(zone) || !TryComp<LimbBodyComponent>(patient, out var limbs))
            return !operation.Whole && !operation.Stump;

        var index = LimbWoundRules.Index(zone);
        if ((uint) index >= (uint) limbs.Zones.Count)
            return false;

        var severed = limbs.Zones[index].Severed;
        if (operation.Whole && (severed || !LimbWoundRules.CanSever(zone)))
            return false;

        if (operation.Stump && !severed)
            return false;

        if (operation.When.Length > 0 && operation.When != "Severed"
            && !SurgeryConditions.Has(operation.When, limbs.Zones[index], HasLayer(patient, zone, SurgeryLayerKind.Bone), Infected(patient, zone),
                SurgeryConditions.OrganCracked(limbs.Organs, zone), LimbWoundRules.HealthFor(zone, limbs)))
            return false;

        if (operation.Implant.Length > 0 && !ImplantMenuFits(patient, operation))
            return false;

        return true;
    }

    private bool ImplantMenuFits(EntityUid patient, SurgeryOperationPrototype operation)
    {
        if (!_implants.HasSlot(patient, operation.Implant))
            return false;

        return operation.ImplantPut
            ? _implants.Free(patient, operation.Implant)
            : _implants.Occupied(patient, operation.Implant);
    }

    private string ImplantOf(EntityUid patient, SurgerySiteComponent site, SurgeryOperationPrototype? operation)
    {
        if (operation != null && site.Step < operation.Steps.Count && operation.Steps[site.Step].Implant.Length > 0)
            return operation.Steps[site.Step].Implant;

        if (operation != null && operation.Implant.Length > 0)
            return operation.Implant;

        if (site.Part.Length > 0
            && _prototype.TryIndex<SurgeryPartPrototype>(site.Part, out var part)
            && part.Implant.Length > 0)
            return part.Implant;

        return string.Empty;
    }

    public void Queue(EntityUid patient, EntityUid user, string operationId)
    {
        if (!_prototype.TryIndex<SurgeryOperationPrototype>(operationId, out var operation)
            || !Enum.TryParse<LimbZone>(operation.Zone, out var zone)
            || !SelfOk(user, patient, zone, operation.ID))
            return;

        if (TryComp<SurgerySiteComponent>(patient, out var existing)
            && existing.Queued == operation.ID
            && existing.Layers.Count > 0
            && existing.Step < operation.Steps.Count)
        {
            Send(patient, user, existing, Held(user), string.Empty, true);
            return;
        }

        var site = EnsureComp<SurgerySiteComponent>(patient);
        var key = KeptKey(zone, "", HasComp<SurgeryProneComponent>(patient));
        if (!site.Done && site.Layers.Count > 0 && WoundOpen(site)
            && (site.Zone != zone || site.Part.Length > 0 || site.Back != HasComp<SurgeryProneComponent>(patient)))
            Keep(site);

        if (site.Kept.TryGetValue(key, out var kept) && WoundOpen(kept.Layers))
        {
            RestoreKept(site, kept, zone);
            Dirty(patient, site);
            Send(patient, user, site, Held(user), string.Empty, true);
            return;
        }

        Spill(patient, site);
        site.Zone = zone;
        site.Part = "";
        site.Queued = operation.ID;
        site.Step = 0;
        site.Rolled = 255;
        site.Gel = false;
        Rebuild(patient, site);
        Dirty(patient, site);
        Send(patient, user, site, Held(user), string.Empty, true);
    }

    private SurgeryGameKind RollGame(SurgerySiteComponent site, SurgeryStep step)
    {
        if (step.Game.Length > 0 && Enum.TryParse<SurgeryGameKind>(step.Game, true, out var pinned))
            return pinned;

        if (!_prototype.TryIndex<SurgeryGameRollPrototype>(step.Verb.ToString(), out var roll) || roll.Games.Count == 0)
            return SurgeryProcedure.Game(step.Verb);

        var key = site.Step * 100 + site.CloseStep;
        if (site.RolledStep == key && site.Rolled != 255 && roll.Games.Contains((SurgeryGameKind) site.Rolled))
            return (SurgeryGameKind) site.Rolled;

        var game = _random.Pick(roll.Games);
        site.Rolled = (byte) game;
        site.RolledStep = key;
        return game;
    }

    private static int Gate(SurgerySiteComponent site, SurgeryVerbKind verb, byte requested)
    {
        var gate = 0;
        for (var i = 0; i < site.Layers.Count; i++)
        {
            if (site.Layers[i].Retracted)
                continue;

            gate = i;
            break;
        }

        var index = Math.Clamp((int) requested, 0, site.Layers.Count - 1);
        if (verb is SurgeryVerbKind.Incise or SurgeryVerbKind.Chop or SurgeryVerbKind.SawBone
            && index > gate
            && site.Layers[gate].Integrity > 0.2f)
            return gate;

        return index;
    }

    private static bool Deeper(SurgerySiteComponent site, int index)
    {
        for (var i = index + 1; i < site.Layers.Count; i++)
        {
            if (site.Layers[i].Kind == SurgeryLayerKind.Cavity)
                continue;

            if (site.Layers[i].Retracted || site.Layers[i].Integrity < 0.85f)
                return true;
        }

        return false;
    }

    private static bool CavityReady(SurgerySiteComponent site)
    {
        for (var i = 0; i < site.Layers.Count; i++)
        {
            var layer = site.Layers[i];
            if (layer.Kind is not (SurgeryLayerKind.Cavity or SurgeryLayerKind.Globe))
                continue;

            if (!(layer.Retracted || layer.Integrity < 0.2f))
                return false;

            if (i > 0 && !site.Layers[i - 1].Retracted && site.Layers[i - 1].Integrity > 0.85f)
                return false;

            return true;
        }

        return false;
    }

    private static bool LayersOpen(SurgerySiteComponent site)
    {
        foreach (var layer in site.Layers)
        {
            if (layer.Kind is SurgeryLayerKind.Cavity or SurgeryLayerKind.Globe)
                continue;

            if (layer.Kind is SurgeryLayerKind.Bone)
                continue;

            if (!layer.Retracted && layer.Integrity > 0.25f)
                return false;
        }

        return true;
    }

    private bool Covered(EntityUid body, LimbZone zone)
    {
        return LimbCoverRules.Worn(body, zone, EntityManager, _inventory);
    }

    private bool Asleep(EntityUid uid)
    {
        return _mobs.IsDead(uid)
               || HasComp<SleepingComponent>(uid)
               || _status.HasEffectComp<PainNumbnessStatusEffectComponent>(uid)
               || _status.HasEffectComp<IgnoreSlowOnDamageComponent>(uid);
    }

    private bool CanOperate(EntityUid uid)
    {
        if (TryComp<HumanoidAppearanceComponent>(uid, out var look) && !SurgerySpeciesPrototype.CanOperate(_prototype, look.Species.Id))
            return false;

        if (_standing.IsDown(uid))
            return true;

        return TryComp<BuckleComponent>(uid, out var buckle) && buckle.Buckled;
    }

    private bool SelfOk(EntityUid user, EntityUid patient, LimbZone zone, string? operation)
    {
        return user != patient || SurgeryMarkPrototype.SelfAllowed(_prototype, zone, operation);
    }

    private bool Near(EntityUid user, EntityUid patient)
    {
        var delta = _transform.GetWorldPosition(user) - _transform.GetWorldPosition(patient);
        return delta.LengthSquared() <= 6.25f;
    }

    private EntityUid? Held(EntityUid user)
    {
        return _hands.TryGetActiveItem(user, out var item) ? item : null;
    }

    private bool CanMark(EntityUid user, EntityUid patient)
    {
        var held = Held(user);
        if (held == null)
            return false;

        var machine = Machine(patient);
        if (TryComp<SurgeryToolComponent>(held.Value, out var tool) && tool.Probe)
            return machine;

        return !machine && (HasComp<SurgeryMarkerComponent>(held.Value) || _tags.HasTag(held.Value, PenTag));
    }

    private bool VerbAllowed(SurgeryVerbKind verb, EntityUid? held, SurgerySiteComponent site)
    {
        if (verb is SurgeryVerbKind.Inspect or SurgeryVerbKind.SetBone)
            return held == null || verb != SurgeryVerbKind.SetBone;

        if (held == null)
            return false;

        if (verb is SurgeryVerbKind.Seat)
            return held != null && HasComp<BodyPartComponent>(held.Value);

        if (verb is SurgeryVerbKind.Graft)
            return GraftMaterial(site.Owner, held.Value);

        if (verb is SurgeryVerbKind.Bandage)
            return held != null && Wraps(held.Value);

        if (verb is SurgeryVerbKind.Inject)
            return held != null && _infection.CanCure(held.Value);

        if (verb is SurgeryVerbKind.Append)
            return held != null && HasComp<SurgeryAppendageComponent>(held.Value);

        if (verb is SurgeryVerbKind.Insert)
            return held != null && (HasComp<OrganComponent>(held.Value) || HasComp<ItemComponent>(held.Value));

        if (verb is SurgeryVerbKind.MendBone && Soft(site.Owner))
            return held != null && HasComp<SurgerySlimeGelComponent>(held.Value);

        if (verb is SurgeryVerbKind.SealSap)
            return held != null && HasComp<SurgeryDionaSapComponent>(held.Value);

        return TryComp<SurgeryToolComponent>(held.Value, out var tool)
               && tool.VerbsFor(Machine(site.Owner), Plant(site.Owner), Soft(site.Owner)).Contains(verb);
    }

    private bool Wraps(EntityUid held)
    {
        foreach (var wrap in _prototype.EnumeratePrototypes<SurgeryWrapPrototype>())
        {
            foreach (var tag in wrap.Tags)
            {
                if (_tags.HasTag(held, tag))
                    return true;
            }
        }

        return false;
    }

    private void CoolHeat(EntityUid patient, LimbZone zone)
    {
        var amount = zone switch
        {
            LimbZone.Torso or LimbZone.Head => 75f,
            LimbZone.ArmLeft or LimbZone.ArmRight or LimbZone.LegLeft or LimbZone.LegRight => 45f,
            LimbZone.HandLeft or LimbZone.HandRight or LimbZone.FootLeft or LimbZone.FootRight => 15f,
            LimbZone.Groin => 45f,
            _ => 0f,
        };
        if (amount <= 0f)
            return;

        var heal = new DamageSpecifier();
        heal.DamageDict["Heat"] = -amount;
        _damage.TryChangeDamage(patient, heal, true, false);
    }

    private bool Tooth(SurgerySiteComponent site)
    {
        return site.Part.Length > 0 && _prototype.TryIndex<SurgeryPartPrototype>(site.Part, out var part) && part.Pill;
    }

    private bool GraftMaterial(EntityUid patient, EntityUid held)
    {
        var stack = GraftStack(patient);
        if (stack.Length > 0)
            return TryComp<StackComponent>(held, out var pile) && pile.StackTypeId == stack;

        return HasComp<SurgerySkinRollComponent>(held);
    }

    private string GraftStack(EntityUid patient)
    {
        return TryComp<HumanoidAppearanceComponent>(patient, out var look)
               && _prototype.TryIndex<SurgerySpeciesPrototype>(look.Species, out var species)
            ? species.GraftStack
            : "";
    }

    private void SpendSkin(EntityUid roll)
    {
        if (!TryComp<SurgerySkinRollComponent>(roll, out var skin))
            return;

        skin.Uses--;
        if (skin.Uses <= 0)
            QueueDel(roll);
    }

    private void SpendGel(EntityUid? held)
    {
        if (held == null)
            return;

        if (TryComp<SurgeryBoneGelComponent>(held.Value, out var bone))
        {
            bone.Uses--;
            if (bone.Uses <= 0)
                QueueDel(held.Value);
            return;
        }

        if (!TryComp<SurgerySlimeGelComponent>(held.Value, out var gel))
            return;

        gel.Uses--;
        if (gel.Uses <= 0)
            QueueDel(held.Value);
    }

    private void SpendSap(EntityUid? held)
    {
        if (held == null || !TryComp<SurgeryDionaSapComponent>(held.Value, out var sap))
            return;

        sap.Uses--;
        if (sap.Uses <= 0)
            QueueDel(held.Value);
    }

    private void SpendHeld(EntityUid? held)
    {
        if (held == null || !HasComp<StackComponent>(held.Value))
            return;

        _stack.TryUse(held.Value, 1);
    }

    private bool WelderReady(EntityUid? held, EntityUid user, out string line)
    {
        line = string.Empty;
        if (held == null || !TryComp<WelderComponent>(held.Value, out var welder))
            return true;

        if (!_toggle.IsActivated(held.Value))
        {
            line = "welder-component-welder-not-lit-message";
            _popup.PopupEntity(Loc.GetString(line), held.Value, user);
            return false;
        }

        if (!_solutions.TryGetSolution(held.Value, welder.FuelSolutionName, out _, out var solution)
            || solution.GetTotalPrototypeQuantity(welder.FuelReagent) < welder.FuelLitCost)
        {
            line = "welder-component-cannot-weld-message";
            _popup.PopupEntity(Loc.GetString(line), held.Value, user);
            return false;
        }

        return true;
    }

    private void SpendWelder(EntityUid? held)
    {
        if (held == null || !TryComp<WelderComponent>(held.Value, out var welder))
            return;

        if (!_solutions.TryGetSolution(held.Value, welder.FuelSolutionName, out var sol, out _))
            return;

        _solutions.RemoveReagent(sol.Value, welder.FuelReagent, FixedPoint2.New(1));
    }

    private bool TryPartZone(EntityUid part, out LimbZone zone)
    {
        zone = LimbZone.Torso;
        if (!TryComp<BodyPartComponent>(part, out var body))
            return false;

        zone = body.PartType switch
        {
            BodyPartType.Head => LimbZone.Head,
            BodyPartType.Arm => body.Symmetry == BodyPartSymmetry.Left ? LimbZone.ArmLeft : LimbZone.ArmRight,
            BodyPartType.Hand => body.Symmetry == BodyPartSymmetry.Left ? LimbZone.HandLeft : LimbZone.HandRight,
            BodyPartType.Leg => body.Symmetry == BodyPartSymmetry.Left ? LimbZone.LegLeft : LimbZone.LegRight,
            BodyPartType.Foot => body.Symmetry == BodyPartSymmetry.Left ? LimbZone.FootLeft : LimbZone.FootRight,
            _ => LimbZone.Torso,
        };
        return zone != LimbZone.Torso || body.PartType == BodyPartType.Torso;
    }
}

[RegisterComponent]
public sealed partial class SurgeryFocusComponent : Component
{
    public Vector2 Prior = Vector2.One;
    public bool Held;
}
