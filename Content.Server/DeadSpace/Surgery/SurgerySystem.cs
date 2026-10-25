// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Organ;
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
using Content.Shared.Item;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
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
    [Dependency] private readonly SharedContentEyeSystem _eyes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LimbBodyComponent, InteractUsingEvent>(OnUse);
        SubscribeLocalEvent<SurgeryFocusComponent, PlayerDetachedEvent>(OnFocusDetach);
        SubscribeNetworkEvent<SurgeryCutMessage>(OnCut);
        SubscribeNetworkEvent<SurgeryZoneMessage>(OnZone);
        SubscribeLocalEvent<SurgeryToolComponent, MeleeHitEvent>(OnToolHit);
        SubscribeLocalEvent<LimbBodyComponent, InteractHandEvent>(OnEmptyHand, before: [typeof(SharedBuckleSystem)]);
        SubscribeNetworkEvent<SurgeryCancelMessage>(OnCancel);
        SubscribeNetworkEvent<SurgeryPickMessage>(OnPick);
        SubscribeNetworkEvent<SurgeryFocusMessage>(OnFocus);
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
                args.Handled = true;
                if (TryComp<SurgerySiteComponent>(hit, out var site) && site.Layers.Count > 0)
                    Send(hit, args.User, site, ent.Owner, string.Empty, true);
                return;
            }
        }
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

    private void OnUse(Entity<LimbBodyComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || _combat.IsInCombatMode(args.User) || !CanOperate(ent.Owner))
            return;

        var tool = CompOrNull<SurgeryToolComponent>(args.Used);
        var part = tool == null && HasComp<BodyPartComponent>(args.Used);
        var organ = tool == null && !part && HasComp<OrganComponent>(args.Used);
        if (tool == null && !part && !organ)
        {
            if (TryComp<SurgerySiteComponent>(ent.Owner, out var pocket)
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
                if (tool.Verbs.Contains(SurgeryVerbKind.Extract)
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
            Rebuild(ent.Owner, site);
        }

        Send(ent.Owner, args.User, site, args.Used, string.Empty, true);
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
            || !Near(user, patient.Value))
            return;

        var site = EnsureComp<SurgerySiteComponent>(patient.Value);
        var zone = (LimbZone) message.Zone;
        if (!LimbWoundRules.IsBody(zone) && zone != LimbZone.Eyes)
            return;

        var key = zone + ":" + message.Part;
        var fresh = site.Done || site.Layers.Count == 0 || !WoundOpen(site);
        if (fresh || zone != site.Zone || message.Part != site.Part)
        {
            if (!fresh && site.Layers.Count > 0)
                Keep(site);

            site.Done = false;
            if (!fresh && site.Kept.TryGetValue(key, out var kept) && WoundOpen(kept.Layers))
            {
                site.Zone = zone;
                site.Part = kept.Part;
                site.Queued = kept.Queued;
                site.Layers = new List<SurgeryLayerState>(kept.Layers);
                site.Step = kept.Step;
                site.Chosen = kept.Chosen;
                site.Clamped = kept.Clamped;
                site.Germs = kept.Germs;
                site.Gel = kept.Gel;
                site.Cut = kept.Cut;
                site.Examined = new HashSet<LimbOrgan>(kept.Examined);
            }
            else
            {
                site.Zone = zone;
                site.Part = message.Part;
                site.Queued = "";
                site.Gel = false;
                site.Cut = 0f;
                Rebuild(patient.Value, site);
                if (message.Part.Length == 0
                    && _prototype.TryIndex<SurgeryAnatomyPrototype>(AnatomyId(patient.Value, zone), out var chosen)
                    && chosen.Operation is { } queuedId)
                    site.Queued = queuedId;
            }
        }

        Send(patient.Value, user, site, Held(user), string.Empty, true);
    }

    private void OnCut(SurgeryCutMessage message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

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
        if (TryUndo(patient.Value, site, verb, held))
        {
            Send(patient.Value, user, site, held, string.Empty, false);
            return;
        }
        if (verb == SurgeryVerbKind.Clamp && message.Organ != 255 && message.Organ != 254)
            site.Chosen = message.Organ;
        if (verb == SurgeryVerbKind.Extract && message.Organ == 254)
        {
            if (OpenCavity(site) && VerbAllowed(verb, held))
                _cavity.TryWithdraw(patient.Value, site.Zone, user);
            Send(patient.Value, user, site, held, string.Empty, false);
            return;
        }
        if (!VerbAllowed(verb, held))
        {
            Send(patient.Value, user, site, held, "surgery-wrong-hand", false);
            return;
        }

        if (Covered(patient.Value, site.Zone) && verb is SurgeryVerbKind.Incise or SurgeryVerbKind.SawBone or SurgeryVerbKind.Amputate)
        {
            Send(patient.Value, user, site, held, "surgery-clothes", false);
            return;
        }

        var index = StepIndex(patient.Value, site, verb);
        if (index < 0)
        {
            Send(patient.Value, user, site, held, string.Empty, false);
            return;
        }
        var layer = site.Layers[index];
        var place = Place(patient.Value);
        var tool = held == null ? null : CompOrNull<SurgeryToolComponent>(held.Value);
        var request = new SurgeryRequest
        {
            Verb = verb,
            Zone = site.Zone,
            Kind = layer.Kind,
            Integrity = layer.Integrity,
            Retracted = layer.Retracted,
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
        if (message.Slips > 0 && _prototype.TryIndex<SurgeryMissPrototype>(verb.ToString(), out var miss))
            _damage.TryChangeDamage(patient.Value, miss.Damage, origin: user);
        layer.Integrity = result.Integrity;
        layer.Retracted = result.Retracted;
        site.Layers[index] = layer;
        if (result.NextLoss > 0f && index + 1 < site.Layers.Count)
        {
            var next = site.Layers[index + 1];
            next.Integrity = MathF.Max(0f, next.Integrity - result.NextLoss);
            site.Layers[index + 1] = next;
        }

        Reveal(site);
        var stepVerb = SurgeryVerbKind.Inspect;
        string stepOrgan = "";
        if (Operation(patient.Value, site, out var operation) && site.Step < operation.Steps.Count)
        {
            stepVerb = operation.Steps[site.Step].Verb;
            stepOrgan = operation.Steps[site.Step].Organ;
        }

        var advance = stepVerb == SurgeryVerbKind.Clamp
            ? message.Progress >= 0.8f
            : SurgeryProcedure.Advances(stepVerb, site.Layers[index], result);
        if (result.RemoveOrgan && !site.Clamped && !CavityReady(site))
        {
            result.RemoveOrgan = false;
            result.Rejected = true;
            advance = false;
        }

        if (advance && stepVerb == verb)
        {
            if (stepVerb == SurgeryVerbKind.Clamp)
                site.Clamped = true;
            if (result.RemoveOrgan)
                site.Clamped = false;
            if (Operation(patient.Value, site, out var voiced) && site.Step < voiced.Steps.Count)
                Voice(patient.Value, voiced.Steps[site.Step].Speech);
            site.Step++;
            site.Rolled = 255;
            if (Operation(patient.Value, site, out var finished) && site.Step >= finished.Steps.Count)
                CloseSite(site);
        }

        if (Operation(patient.Value, site, out var running)
            && verb == SurgeryVerbKind.Cauterize
            && Sealed(site)
            && NextClose(running, site) == null)
            CloseSite(site);

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

        if (result.Health < 0f)
            site.Cut += -result.Health;

        if (advance && verb is SurgeryVerbKind.Incise or SurgeryVerbKind.SawBone)
        {
            var tax = LimbWoundRules.HealthFor(site.Zone) * 0.18f;
            site.Cut += tax;
            result.Health -= tax;
        }

        if (advance && verb == SurgeryVerbKind.Cauterize && layer.Kind == SurgeryLayerKind.Skin)
        {
            var keep = LimbWoundRules.HealthFor(site.Zone) * 0.15f;
            var back = MathF.Max(0f, site.Cut - keep);
            result.Health += back;
            site.Cut = 0f;
        }

        LimbOrgan? organ = site.Chosen != 255 ? (LimbOrgan) site.Chosen : message.Organ == 255 ? null : (LimbOrgan) message.Organ;
        var lodging = verb == SurgeryVerbKind.Insert && held != null && !HasComp<OrganComponent>(held.Value);
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
            else if (result.PlaceOrgan && !_cavity.TryLodge(patient.Value, site.Zone, held.Value))
            {
                result.PlaceOrgan = false;
                result.Rejected = true;
                result.Key = "surgery-cavity-full";
            }
            else if (result.PlaceOrgan)
            {
                result.PlaceOrgan = false;
            }
        }
        else if (verb == SurgeryVerbKind.Insert
            && (organ == null || !OrganMissing(patient.Value, organ.Value) || !HeldIs(held, organ.Value))
            && !SpeechStep(patient.Value, site, "Clear"))
        {
            result.PlaceOrgan = false;
            result.Rejected = true;
            result.Key = "surgery-insert-short";
        }
        if (organ == null
            && !string.IsNullOrEmpty(stepOrgan)
            && Enum.TryParse<LimbOrgan>(stepOrgan, out var named))
            organ = named;
        if (result.Examined && organ is { } seen)
            site.Examined.Add(seen);

        if (result.RemoveOrgan && organ == null && OpenCavity(site))
            _cavity.TryWithdraw(patient.Value, site.Zone, user);

        _port.Apply(patient.Value, site.Zone, organ, result, held);
        RemComp<SurgeryBusyComponent>(user);
        Send(patient.Value, user, site, held, result.Key, false);
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
            foreach (var verb in tool.Verbs)
                verbs.Add((byte) verb);
        }

        if (held != null && HasComp<BodyPartComponent>(held.Value))
            verbs.Add((byte) SurgeryVerbKind.Seat);
        if (held != null && HasComp<OrganComponent>(held.Value))
            verbs.Add((byte) SurgeryVerbKind.Insert);
        else if (held != null && OpenCavity(site) && _cavity.Fits(site.Zone, held.Value))
            verbs.Add((byte) SurgeryVerbKind.Insert);

        var organs = new List<byte>();
        var empty = new List<byte>();
        var examined = new List<byte>();
        if (CavityReady(site) && TryComp<LimbBodyComponent>(patient, out var limbs))
        {
            var open = OpenCavity(site);
            foreach (var organ in _prototype.EnumeratePrototypes<SurgeryOrganPrototype>())
            {
                if (!Enum.TryParse<LimbOrgan>(organ.ID, out var kind) || !Enum.TryParse<LimbZone>(organ.Zone, out var zone))
                    continue;

                if (zone != site.Zone)
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
        if (Operation(patient, site, out var played) && site.Step < played.Steps.Count)
            stepGame = RollGame(played.Steps[site.Step]);
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
            Part = site.Part,
        }, session);
    }

    private bool SpeechStep(EntityUid patient, SurgerySiteComponent site, string speech)
    {
        return Operation(patient, site, out var operation)
               && site.Step < operation.Steps.Count
               && operation.Steps[site.Step].Speech == speech
               && operation.Steps[site.Step].Organ.Length == 0;
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
        return WoundOpen(site.Layers);
    }

    private static bool WoundOpen(IReadOnlyList<SurgeryLayerState> layers)
    {
        foreach (var layer in layers)
        {
            if (layer.Integrity < 0.9f || layer.Retracted)
                return true;
        }

        return false;
    }

    private static SurgeryStep? NextClose(SurgeryOperationPrototype operation, SurgerySiteComponent site)
    {
        foreach (var step in operation.Close)
        {
            var index = SurgeryProcedure.Index(site.Layers, step.Layer);
            if (index < 0)
                continue;

            var layer = site.Layers[index];
            if (layer.Retracted || layer.Integrity < 0.85f)
                return step;
        }

        return null;
    }

    private byte CloseVerb(EntityUid patient, SurgerySiteComponent site)
    {
        if (!WoundOpen(site) || !Operation(patient, site, out var operation))
            return 255;

        var step = NextClose(operation, site);
        return step == null ? (byte) 255 : (byte) step.Verb;
    }

    private byte UndoVerb(EntityUid patient, SurgerySiteComponent site)
    {
        if (!Operation(patient, site, out var operation) || operation.Steps.Count == 0 || site.Step <= 0)
            return 255;

        var target = site.Step - 1;
        if (target >= operation.Steps.Count)
            return 255;

        return ParseUndo(operation.Steps[target].Undo);
    }

    private bool TryUndo(EntityUid patient, SurgerySiteComponent site, SurgeryVerbKind verb, EntityUid? held)
    {
        if (!VerbAllowed(verb, held) || !Operation(patient, site, out var operation) || site.Step <= 0)
            return false;

        var target = site.Step - 1;
        if (target >= operation.Steps.Count)
            return false;

        if (ParseUndo(operation.Steps[target].Undo) != (byte) verb)
            return false;

        Revert(site, operation.Steps[target]);
        site.Step = target;
        if (site.Step == 0 && !StepApplied(operation.Steps[0], site))
            CloseSite(site);
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
            case SurgeryVerbKind.SawBone:
                layer.Integrity = 1f;
                layer.Retracted = false;
                break;
            case SurgeryVerbKind.Retract:
                layer.Retracted = false;
                break;
            case SurgeryVerbKind.Cauterize:
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
            SurgeryVerbKind.Incise or SurgeryVerbKind.SawBone => layer.Integrity < 0.9f,
            SurgeryVerbKind.Retract => layer.Retracted,
            SurgeryVerbKind.Cauterize => layer.Integrity >= 0.85f && !layer.Retracted,
            SurgeryVerbKind.Clamp => site.Clamped,
            _ => false,
        };
    }

    private static byte ParseUndo(string undo)
    {
        return Enum.TryParse<SurgeryVerbKind>(undo, true, out var verb) ? (byte) verb : (byte) 255;
    }

    private static void Keep(SurgerySiteComponent site)
    {
        site.Kept[site.Zone + ":" + site.Part] = new SurgeryKept
        {
            Part = site.Part,
            Queued = site.Queued,
            Layers = new List<SurgeryLayerState>(site.Layers),
            Step = site.Step,
            Chosen = site.Chosen,
            Clamped = site.Clamped,
            Germs = site.Germs,
            Gel = site.Gel,
            Cut = site.Cut,
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
        site.Clamped = false;
        site.Chosen = 255;
        site.Queued = "";
        site.Kept.Remove(site.Zone + ":" + site.Part);
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

        if (_prototype.TryIndex<SurgeryPlacePrototype>("Floor", out var floor))
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

    private int StepIndex(EntityUid patient, SurgerySiteComponent site, SurgeryVerbKind verb)
    {
        if (!Operation(patient, site, out var operation))
            return Gate(site, verb, 0);

        Rewind(site, operation);
        site.Step = SurgeryProcedure.CatchUp(operation.Steps, site.Layers, site.Step);
        var step = site.Step;
        if (step >= 0 && step < operation.Steps.Count && operation.Steps[step].Verb == verb)
            return SurgeryProcedure.Index(site.Layers, operation.Steps[step].Layer);

        var closing = NextClose(operation, site);
        if (closing != null && closing.Verb == verb)
            return SurgeryProcedure.Index(site.Layers, closing.Layer);

        if (verb == SurgeryVerbKind.Extract && OrganLeft(patient, site))
            return SurgeryProcedure.Index(site.Layers, SurgeryLayerKind.Cavity);

        if (verb == SurgeryVerbKind.Insert && OpenCavity(site))
            return SurgeryProcedure.Index(site.Layers, SurgeryLayerKind.Cavity);

        return -1;
    }

    private void Rewind(SurgerySiteComponent site, SurgeryOperationPrototype operation)
    {
        if (site.Step < operation.Steps.Count || !Sealed(site))
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

    private bool HeldIs(EntityUid? held, LimbOrgan organ)
    {
        return held != null
               && TryComp<MetaDataComponent>(held.Value, out var meta)
               && meta.EntityPrototype != null
               && meta.EntityPrototype.ID.Contains(organ.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private bool OrganLeft(EntityUid patient, SurgerySiteComponent site)
    {
        if (!OpenCavity(site) || !TryComp<LimbBodyComponent>(patient, out var limbs))
            return false;

        foreach (var organ in _prototype.EnumeratePrototypes<SurgeryOrganPrototype>())
        {
            if (!Enum.TryParse<LimbOrgan>(organ.ID, out var kind) || !Enum.TryParse<LimbZone>(organ.Zone, out var zone))
                continue;

            if (zone != site.Zone)
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
        if (step < 0 || step >= operation.Steps.Count)
            return;

        var index = SurgeryProcedure.Index(site.Layers, operation.Steps[step].Layer);
        if (index < 0)
            return;

        verb = operation.Steps[step].Verb;
        layer = index;
    }

    private bool Operation(EntityUid patient, SurgerySiteComponent site, out SurgeryOperationPrototype operation)
    {
        operation = null!;
        if (site.Part.Length > 0
            && _prototype.TryIndex<SurgeryPartPrototype>(site.Part, out var part)
            && part.Operation.Length > 0
            && _prototype.TryIndex<SurgeryOperationPrototype>(part.Operation, out var picked))
        {
            operation = picked;
            return true;
        }

        if (site.Queued.Length > 0 && _prototype.TryIndex<SurgeryOperationPrototype>(site.Queued, out var queued))
        {
            operation = queued;
            return true;
        }

        if (!_prototype.TryIndex<SurgeryAnatomyPrototype>(AnatomyId(patient, site.Zone), out var anatomy)
            || anatomy.Operation is not { } id)
            return false;

        return _prototype.TryIndex(id, out operation!);
    }

    public void Queue(EntityUid patient, EntityUid user, string operationId)
    {
        if (!_prototype.TryIndex<SurgeryOperationPrototype>(operationId, out var operation)
            || !Enum.TryParse<LimbZone>(operation.Zone, out var zone))
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

    private SurgeryGameKind RollGame(SurgeryStep step)
    {
        if (step.Game.Length > 0 && Enum.TryParse<SurgeryGameKind>(step.Game, true, out var pinned))
            return pinned;

        return GameOf(step);
    }

    private static SurgeryGameKind GameOf(SurgeryStep step)
    {
        if (!string.IsNullOrEmpty(step.Game) && Enum.TryParse<SurgeryGameKind>(step.Game, true, out var parsed))
            return parsed;

        return step.Verb switch
        {
            SurgeryVerbKind.Retract => SurgeryGameKind.Spread,
            SurgeryVerbKind.SawBone => SurgeryGameKind.Saw,
            SurgeryVerbKind.Clamp => SurgeryGameKind.Clamp,
            SurgeryVerbKind.Extract => SurgeryGameKind.Sever,
            _ => SurgeryGameKind.Cut,
        };
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
        if (verb is SurgeryVerbKind.Incise or SurgeryVerbKind.SawBone
            && index > gate
            && site.Layers[gate].Integrity > 0.2f)
            return gate;

        return index;
    }

    private static bool Deeper(SurgerySiteComponent site, int index)
    {
        for (var i = index + 1; i < site.Layers.Count; i++)
        {
            if (site.Layers[i].Retracted || site.Layers[i].Integrity < 0.85f)
                return true;
        }

        return false;
    }

    private static bool CavityReady(SurgerySiteComponent site)
    {
        foreach (var layer in site.Layers)
        {
            if (layer.Kind is SurgeryLayerKind.Cavity or SurgeryLayerKind.Globe)
                return layer.Retracted || layer.Integrity < 0.2f;
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
        var slots = _inventory.GetSlotEnumerator(body);
        while (slots.NextItem(out var item))
        {
            if (TryComp<LimbCoverComponent>(item, out var cover) && !cover.OnlyWhenBlocking && cover.Covers(zone))
                return true;
        }

        return false;
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
        if (_mobs.IsDead(uid) || _standing.IsDown(uid))
            return true;

        return TryComp<BuckleComponent>(uid, out var buckle) && buckle.Buckled;
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

    private bool VerbAllowed(SurgeryVerbKind verb, EntityUid? held)
    {
        if (verb is SurgeryVerbKind.Inspect)
            return true;

        if (held == null)
            return false;

        if (verb is SurgeryVerbKind.Seat)
            return held != null && HasComp<BodyPartComponent>(held.Value);

        if (verb is SurgeryVerbKind.Insert)
            return held != null && (HasComp<OrganComponent>(held.Value) || HasComp<ItemComponent>(held.Value));

        return TryComp<SurgeryToolComponent>(held.Value, out var tool) && tool.Verbs.Contains(verb);
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
