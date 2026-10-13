// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Chemistry.TileReactions;
using Content.Server.DoAfter;
using Content.Server.Electrocution;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Popups;
using Content.Shared.Atmos;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Changeling.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.Psychiatry;
using Content.Shared.DoAfter;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Robust.Shared.Player;
using NewStatusEffectsSystem = Content.Shared.StatusEffectNew.StatusEffectsSystem;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Jittering;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Components;
using Content.Shared.Fluids.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Materials;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Psychiatry;

public sealed class PsychiatryTreatmentSystem : EntitySystem
{
    [Dependency] private readonly BloodstreamSystem _blood = default!;
    [Dependency] private readonly BlindableSystem _blinding = default!;
    [Dependency] private readonly NewStatusEffectsSystem _newStatus = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly ElectrocutionSystem _electro = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly PsychiatrySystem _psychiatry = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly PuddleSystem _puddles = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly Vector2i[] Cardinal = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];

    private static readonly ProtoId<TagPrototype> MetalTag = "Metal";
    private static readonly ProtoId<TagPrototype> WoodenTag = "Wooden";
    private static readonly ProtoId<DamageModifierSetPrototype> MetallicSet = "Metallic";

    private static readonly HashSet<string> MetalMaterials = new()
    {
        "Steel", "Plasteel", "Gold", "Silver", "Plasma", "Uranium", "Bananium",
        "Copper", "Brass", "Aluminium", "Aluminum", "Iron", "Titanium",
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShockTherapyComponent, AfterInteractEvent>(OnShockInteract);
        SubscribeLocalEvent<ShockTherapyComponent, ShockTherapyDoAfterEvent>(OnShockDoAfter);
        SubscribeLocalEvent<FirmwarePatchComponent, AfterInteractEvent>(OnFirmwareInteract);
        SubscribeLocalEvent<FirmwarePatchComponent, FirmwarePatchDoAfterEvent>(OnFirmwareDoAfter);
        SubscribeLocalEvent<HardResetProbeComponent, AfterInteractEvent>(OnHardResetInteract);
        SubscribeLocalEvent<HardResetProbeComponent, HardResetDoAfterEvent>(OnHardResetDoAfter);
        SubscribeLocalEvent<CascadeSpikeComponent, AfterInteractEvent>(OnCascadeInteract);
        SubscribeLocalEvent<CascadeSpikeComponent, CascadeSpikeDoAfterEvent>(OnCascadeDoAfter);
        SubscribeLocalEvent<AdminCurePatchComponent, AfterInteractEvent>(OnAdminPatchInteract);
        SubscribeLocalEvent<AdminCurePatchComponent, AdminCurePatchDoAfterEvent>(OnAdminPatchDoAfter);
        SubscribeLocalEvent<ExpandICChatRecipientsEvent>(OnChatRecipients);
        SubscribeLocalEvent<RegenerativeStasisExitedEvent>(OnStasisExited);
    }

    private static readonly EntProtoId DeafEffect = "StatusEffectDeaf";
    private static readonly EntProtoId MutedEffect = "StatusEffectMuted";

    private void ApplySensoryFault(EntityUid target)
    {
        switch (_random.Next(3))
        {
            case 0:
                ApplyBlind(target);
                break;
            case 1:
                ApplyDeaf(target);
                break;
            default:
                ApplyMute(target);
                break;
        }

        _popup.PopupEntity(Loc.GetString("psychiatry-sensory-fault"), target, PopupType.LargeCaution);
    }

    private void ApplyBlind(EntityUid target)
    {
        if (!TryComp<BlindableComponent>(target, out var blindable))
            return;

        var missing = blindable.MaxDamage - blindable.EyeDamage;
        if (missing > 0)
            _blinding.AdjustEyeDamage((target, blindable), missing);
    }

    private void ApplyDeaf(EntityUid target)
    {
        _newStatus.TrySetStatusEffectDuration(target, DeafEffect);
    }

    private void ApplyMute(EntityUid target)
    {
        _newStatus.TrySetStatusEffectDuration(target, MutedEffect);
    }

    private void OnStasisExited(ref RegenerativeStasisExitedEvent args)
    {
        _psychiatry.ClearIllness(args.Target, "stasis");
        _newStatus.TryRemoveStatusEffect(args.Target, DeafEffect);
        _newStatus.TryRemoveStatusEffect(args.Target, MutedEffect);

        if (!TryComp<BlindableComponent>(args.Target, out var blindable) || blindable.EyeDamage <= 0)
            return;

        _blinding.AdjustEyeDamage((args.Target, blindable), -blindable.EyeDamage);
    }

    private void OnChatRecipients(ExpandICChatRecipientsEvent ev)
    {
        List<ICommonSession>? drop = null;
        foreach (var (session, _) in ev.Recipients)
        {
            if (session.AttachedEntity is not { } listener || listener == ev.Source)
                continue;
            if (!_newStatus.HasEffectComp<DeafStatusEffectComponent>(listener))
                continue;
            drop ??= new List<ICommonSession>();
            drop.Add(session);
        }

        if (drop == null)
            return;

        foreach (var session in drop)
            ev.Recipients.Remove(session);
    }

    private void OnShockInteract(Entity<ShockTherapyComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!_toggle.IsActivated(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("psychiatry-shock-not-on"), ent.Owner, args.User);
            return;
        }

        if (target == args.User)
        {
            _popup.PopupEntity(Loc.GetString("psychiatry-no-self"), args.User, args.User);
            return;
        }

        if (!_powerCell.HasActivatableCharge(ent.Owner, user: args.User))
            return;

        var living = HasComp<MobStateComponent>(target) || HasComp<BodyComponent>(target);
        var steps = living ? 4 : 1;
        var totalSeconds = living
            ? _random.NextFloat(ent.Comp.DoAfterMinSeconds, ent.Comp.DoAfterMaxSeconds)
            : 2.5f;
        var hit = living ? ent.Comp.ShockDamage / steps : ent.Comp.ShockDamage;
        var name = Identity.Name(target, EntityManager);
        _popup.PopupEntity(Loc.GetString("psychiatry-shock-target", ("target", name)), ent.Owner, args.User);
        ent.Comp.Progress = 0;
        ent.Comp.ProgressSteps = steps;
        ent.Comp.ProgressHit = hit;
        ent.Comp.ProgressLiving = living;
        if (StartShockStep(ent, args.User, target, 0, steps, totalSeconds / steps, hit, living))
            args.Handled = true;
    }

    private bool StartShockStep(
        Entity<ShockTherapyComponent> tool,
        EntityUid user,
        EntityUid target,
        int step,
        int steps,
        float stepSeconds,
        float hitDamage,
        bool living)
    {
        var ev = new ShockTherapyDoAfterEvent
        {
            Step = step,
            Steps = steps,
            StepSeconds = stepSeconds,
            HitDamage = hitDamage,
            Living = living,
        };
        var doAfter = new DoAfterArgs(EntityManager, user, stepSeconds, ev, tool, target: target, used: tool)
        {
            BreakOnMove = false,
            BreakOnDamage = false,
            NeedHand = true,
            DistanceThreshold = 1.5f,
        };
        return _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnShockDoAfter(Entity<ShockTherapyComponent> ent, ref ShockTherapyDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;
        args.Handled = true;

        if (!_toggle.IsActivated(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("psychiatry-shock-not-on"), ent.Owner, args.User);
            return;
        }

        if (ent.Comp.Progress == 0 && !_powerCell.TryUseActivatableCharge(ent.Owner, user: args.User))
            return;

        ent.Comp.Progress++;
        var finished = false;
        if (ent.Comp.ProgressLiving)
        {
            StrikeLiving(ent, target, args.User, ent.Comp.ProgressHit, ent.Comp.Progress == 1);
            if (ent.Comp.Progress >= ent.Comp.ProgressSteps)
            {
                ent.Comp.Progress = 0;
                finished = true;
                FinishLivingShock(ent, target, args.User);
            }
            else
            {
                StartShockStep(ent, args.User, target, ent.Comp.Progress, ent.Comp.ProgressSteps, args.StepSeconds, ent.Comp.ProgressHit, true);
            }
        }
        else
        {
            ent.Comp.Progress = 0;
            finished = true;
            var damage = (int) ent.Comp.ProgressHit;
            switch (Classify(target))
            {
                case ShockSurface.Body:
                    ShockBody(ent, target, args.User, damage);
                    break;
                case ShockSurface.Moisture:
                    var ignited = ShockPuddleChain(ent.Owner, target, damage, args.User, evaporateOrigin: true);
                    _popup.PopupEntity(
                        Loc.GetString(ignited ? "psychiatry-shock-ignited" : "psychiatry-shock-evaporated"),
                        target,
                        args.User,
                        PopupType.Medium);
                    break;
                case ShockSurface.Metal:
                    ShockContacts(ent.Owner, target, damage, ignoreInsulation: false, except: args.User);
                    EvaporateContacts(target);
                    _popup.PopupEntity(Loc.GetString("psychiatry-shock-metal"), target, args.User, PopupType.Medium);
                    break;
                default:
                    _popup.PopupEntity(Loc.GetString("psychiatry-shock-insulator"), target, args.User);
                    break;
            }

            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Items/Defib/defib_zap.ogg"), target);
            PulseShockVisual(ent);
        }

        if (finished && !_powerCell.HasActivatableCharge(ent.Owner))
            _toggle.TryDeactivate(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ShockTherapyComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ShockUntil == default || now < comp.ShockUntil)
                continue;
            comp.ShockUntil = default;
            _appearance.SetData(uid, ShockTherapyVisuals.Shocking, false);
        }
    }

    private void PulseShockVisual(Entity<ShockTherapyComponent> ent)
    {
        ent.Comp.ShockUntil = _timing.CurTime + TimeSpan.FromSeconds(0.8);
        _appearance.SetData(ent.Owner, ShockTherapyVisuals.Shocking, true);
    }

    private void StrikeLiving(Entity<ShockTherapyComponent> ent, EntityUid target, EntityUid user, float hitDamage, bool first)
    {
        if (first)
        {
            var hasGag = _inventory.TryGetSlotEntity(target, "mask", out var mask) &&
                         HasComp<MedicalGagComponent>(mask.Value);
            if (!hasGag)
            {
                var crush = new DamageSpecifier();
                crush.DamageDict["Blunt"] = FixedPoint2.New(35);
                _damageable.TryChangeDamage(target, crush, origin: user);
                if (TryComp<BloodstreamComponent>(target, out var blood))
                    _blood.TryModifyBleedAmount((target, blood), 2f);
                _popup.PopupEntity(Loc.GetString("psychiatry-shock-no-gag"), target, PopupType.LargeCaution);
            }
        }

        var shock = new DamageSpecifier();
        shock.DamageDict["Shock"] = FixedPoint2.New(hitDamage);
        _damageable.TryChangeDamage(target, shock, origin: user);
        _jitter.DoJitter(target, TimeSpan.FromSeconds(0.8), refresh: true, amplitude: 16f, frequency: 8f);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Items/Defib/defib_zap.ogg"), target);
        PulseShockVisual(ent);
        ShockPuddleNeighbors(ent.Owner, target, Math.Max(1, (int) hitDamage), user);
        EvaporateContacts(target);
    }

    private void FinishLivingShock(Entity<ShockTherapyComponent> ent, EntityUid target, EntityUid user)
    {
        if (_psychiatry.IsPositronic(target))
        {
            var scrambled = _psychiatry.TryApplyCyber(target, SchizophreniaStage.Latent, "shock-therapy", ignoreCooldown: true);
            _popup.PopupEntity(Loc.GetString(scrambled ? "psychiatry-shock-positronic" : "psychiatry-shock-zapped"), user, user, PopupType.Medium);
            return;
        }

        if (!TryComp<SchizophreniaComponent>(target, out var illness)
            || illness.Kind != PsychiatryIllnessKind.Schizophrenia
            || illness.Stage <= SchizophreniaStage.None)
        {
            _popup.PopupEntity(Loc.GetString("psychiatry-shock-zapped"), user, user, PopupType.Medium);
            return;
        }

        _psychiatry.AdjustStage(target, -(int) SchizophreniaStage.Acute, "shock-therapy");
        _psychiatry.HoldOnset(target);
        if (_random.Prob(_cfg.GetCVar(CCCCVars.PsychiatryShockFaultChance)))
            ApplySensoryFault(target);
        _popup.PopupEntity(Loc.GetString("psychiatry-shock-done", ("stages", (int) SchizophreniaStage.Acute)), user, user, PopupType.Medium);
    }

    private void ShockBody(Entity<ShockTherapyComponent> ent, EntityUid target, EntityUid user, int damage)
    {
        var hasGag = _inventory.TryGetSlotEntity(target, "mask", out var mask) &&
                     HasComp<MedicalGagComponent>(mask.Value);

        if (!hasGag)
        {
            var crush = new DamageSpecifier();
            crush.DamageDict["Blunt"] = FixedPoint2.New(35);
            _damageable.TryChangeDamage(target, crush, origin: user);
            if (TryComp<BloodstreamComponent>(target, out var blood))
                _blood.TryModifyBleedAmount((target, blood), 2f);
            _popup.PopupEntity(Loc.GetString("psychiatry-shock-no-gag"), target, PopupType.LargeCaution);
        }

        _electro.TryDoElectrocution(target, ent.Owner, damage, TimeSpan.FromSeconds(3), refresh: true, ignoreInsulation: true);
        ShockPuddleNeighbors(ent.Owner, target, damage, user);
        EvaporateContacts(target);

        if (!TryComp<SchizophreniaComponent>(target, out var illness)
            || illness.Kind != PsychiatryIllnessKind.Schizophrenia
            || illness.Stage <= SchizophreniaStage.None)
        {
            _popup.PopupEntity(Loc.GetString("psychiatry-shock-zapped"), user, user, PopupType.Medium);
            return;
        }

        _psychiatry.AdjustStage(target, -(int) SchizophreniaStage.Acute, "shock-therapy");
        _psychiatry.HoldOnset(target);
        _popup.PopupEntity(Loc.GetString("psychiatry-shock-done", ("stages", (int) SchizophreniaStage.Acute)), user, user, PopupType.Medium);
    }

    private void ShockPuddleNeighbors(EntityUid source, EntityUid body, int damage, EntityUid except)
    {
        foreach (var uid in Contacts(body))
        {
            if (!HasComp<PuddleComponent>(uid))
                continue;
            ShockPuddleChain(source, uid, damage, except, evaporateOrigin: false);
        }
    }

    private bool ShockPuddleChain(EntityUid source, EntityUid origin, int damage, EntityUid except, bool evaporateOrigin)
    {
        var puddles = ConnectedPuddles(origin);
        var shocked = new HashSet<EntityUid>();
        var ignited = false;

        foreach (var puddle in puddles)
        {
            var flammable = IsFlammablePuddle(puddle);
            ShockContacts(source, puddle, damage, ignoreInsulation: false, except, shocked);
            if (flammable)
            {
                if (IgnitePuddle(puddle, source))
                    ignited = true;
                continue;
            }

            if (evaporateOrigin && puddle == origin)
                Evaporate(puddle);
        }

        return ignited;
    }

    private List<EntityUid> ConnectedPuddles(EntityUid origin)
    {
        var result = new List<EntityUid>();
        if (!TryComp<PuddleComponent>(origin, out _))
            return result;

        var xform = Transform(origin);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var mapGrid))
        {
            result.Add(origin);
            return result;
        }

        var originTile = _map.TileIndicesFor(grid, mapGrid, xform.Coordinates);
        var queued = new Queue<Vector2i>();
        var seen = new HashSet<Vector2i>();
        queued.Enqueue(originTile);
        seen.Add(originTile);

        while (queued.Count > 0 && result.Count < _cfg.GetCVar(CCCCVars.PsychiatryPuddleChainCap))
        {
            var tile = queued.Dequeue();
            var tileRef = _map.GetTileRef(grid, mapGrid, tile);
            if (!_puddles.TryGetPuddle(tileRef, out var puddle))
                continue;

            result.Add(puddle);
            foreach (var offset in Cardinal)
            {
                var next = tile + offset;
                if (seen.Add(next))
                    queued.Enqueue(next);
            }
        }

        return result;
    }

    private bool IsFlammablePuddle(EntityUid puddle)
    {
        if (!TryComp<PuddleComponent>(puddle, out var comp))
            return false;
        if (!_solutions.TryGetSolution(puddle, comp.SolutionName, out _, out var solution))
            return false;

        foreach (var reagent in solution.Contents)
        {
            if (reagent.Quantity <= FixedPoint2.Zero)
                continue;
            if (!_proto.TryIndex<ReagentPrototype>(reagent.Reagent.Prototype, out var proto))
                continue;
            if (proto.ReactiveEffects != null && proto.ReactiveEffects.ContainsKey("Flammable"))
                return true;
            foreach (var reaction in proto.TileReactions)
            {
                if (reaction is FlammableTileReaction)
                    return true;
            }
        }

        return false;
    }

    private bool IgnitePuddle(EntityUid puddle, EntityUid source)
    {
        var xform = Transform(puddle);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var mapGrid))
            return false;

        var tile = _map.TileIndicesFor(grid, mapGrid, xform.Coordinates);
        var mix = _atmos.GetTileMixture(grid, null, tile, excite: true);
        if (mix == null || mix.GetMoles(Gas.Oxygen) < 0.5f)
            return false;

        if (!_atmos.IsHotspotActive(grid, tile)
            && mix.GetMoles(Gas.Plasma) < 0.5f
            && mix.GetMoles(Gas.Tritium) < 0.5f
            && mix.GetMoles(Gas.Hydrogen) < 0.5f)
        {
            mix.AdjustMoles(Gas.Plasma, 0.6f);
        }

        _atmos.HotspotExpose(grid, tile, Atmospherics.PlasmaMinimumBurnTemperature + 80f, 25f, source, true);
        if (!TryComp<PuddleComponent>(puddle, out var comp)
            || !_solutions.TryGetSolution(puddle, comp.SolutionName, out _, out var solution))
            return true;

        _puddles.DoTileReactions(_map.GetTileRef(grid, mapGrid, tile), solution);
        return true;
    }

    private void ShockContacts(EntityUid source, EntityUid conductor, int damage, bool ignoreInsulation, EntityUid except, HashSet<EntityUid>? shocked = null)
    {
        foreach (var uid in Contacts(conductor))
        {
            if (uid == conductor || uid == source || uid == except)
                continue;
            if (!HasComp<MobStateComponent>(uid) && !HasComp<BodyComponent>(uid))
                continue;
            if (shocked != null && !shocked.Add(uid))
                continue;
            _electro.TryDoElectrocution(uid, source, damage, TimeSpan.FromSeconds(2), refresh: true, ignoreInsulation: ignoreInsulation);
        }
    }

    private void EvaporateContacts(EntityUid origin)
    {
        foreach (var uid in Contacts(origin))
        {
            if (!HasComp<PuddleComponent>(uid))
                continue;
            if (IsFlammablePuddle(uid))
                IgnitePuddle(uid, origin);
            else
                Evaporate(uid);
        }
    }

    private void Evaporate(EntityUid puddle)
    {
        if (!TryComp<PuddleComponent>(puddle, out var comp))
            return;
        if (!_solutions.TryGetSolution(puddle, comp.SolutionName, out var soln))
            return;
        _solutions.RemoveAllSolution(soln.Value);
    }

    private List<EntityUid> Contacts(EntityUid uid)
    {
        var list = new List<EntityUid>();
        var xform = Transform(uid);
        if (xform.MapID == MapId.Nullspace)
            return list;

        var box = _lookup.GetWorldAABB(uid).Enlarged(0.25f);
        foreach (var other in _lookup.GetEntitiesIntersecting(xform.MapID, box))
            list.Add(other);
        return list;
    }

    private ShockSurface Classify(EntityUid uid)
    {
        if (HasComp<MobStateComponent>(uid) || HasComp<BodyComponent>(uid))
            return ShockSurface.Body;
        if (HasComp<PuddleComponent>(uid))
            return ShockSurface.Moisture;
        if (IsWood(uid))
            return ShockSurface.Insulator;
        if (IsMetal(uid))
            return ShockSurface.Metal;
        return ShockSurface.Insulator;
    }

    private bool IsWood(EntityUid uid)
    {
        if (_tag.HasTag(uid, WoodenTag))
            return true;
        return HasMaterial(uid, "Wood") && !HasMetalMaterial(uid);
    }

    private bool IsMetal(EntityUid uid)
    {
        if (_tag.HasTag(uid, MetalTag))
            return true;
        if (TryComp<DamageableComponent>(uid, out var damage) && damage.DamageModifierSetId == MetallicSet)
            return true;
        return HasMetalMaterial(uid);
    }

    private bool HasMetalMaterial(EntityUid uid)
    {
        if (!TryComp<PhysicalCompositionComponent>(uid, out var comp))
            return false;
        foreach (var (material, amount) in comp.MaterialComposition)
        {
            if (amount > 0 && MetalMaterials.Contains(material))
                return true;
        }

        return false;
    }

    private bool HasMaterial(EntityUid uid, string material)
    {
        return TryComp<PhysicalCompositionComponent>(uid, out var comp)
               && comp.MaterialComposition.TryGetValue(material, out var amount)
               && amount > 0;
    }

    private bool RefuseUnlessPositronic(EntityUid user, EntityUid target, bool requireIllness, out SchizophreniaComponent? illness)
    {
        illness = null;
        if (target == user)
        {
            _popup.PopupEntity(Loc.GetString("psychiatry-no-self"), user, user);
            return true;
        }

        if (!_psychiatry.IsPositronic(target))
        {
            if (HasComp<MobStateComponent>(target) || HasComp<BodyComponent>(target))
                _popup.PopupEntity(Loc.GetString("psychiatry-cyber-organic"), user, user);
            return true;
        }

        if (!requireIllness)
            return false;

        if (!TryComp(target, out illness) || illness.Kind != PsychiatryIllnessKind.Cyberpsychosis)
        {
            _popup.PopupEntity(Loc.GetString("psychiatry-cyber-none"), user, user);
            return true;
        }

        return false;
    }

    private bool StartToolDoAfter(EntityUid tool, EntityUid user, EntityUid target, float seconds, DoAfterEvent ev)
    {
        var args = new DoAfterArgs(EntityManager, user, seconds, ev, tool, target: target, used: tool)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            DistanceThreshold = 1.5f,
        };
        return _doAfter.TryStartDoAfter(args);
    }

    private void OnFirmwareInteract(Entity<FirmwarePatchComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;
        if (RefuseUnlessPositronic(args.User, target, true, out var illness))
            return;
        if (illness!.Stage != SchizophreniaStage.Latent)
        {
            _popup.PopupEntity(Loc.GetString("psychiatry-patch-latent"), args.User, args.User);
            return;
        }

        if (StartToolDoAfter(ent, args.User, target, ent.Comp.Delay, new FirmwarePatchDoAfterEvent()))
            args.Handled = true;
    }

    private void OnFirmwareDoAfter(Entity<FirmwarePatchComponent> ent, ref FirmwarePatchDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;
        args.Handled = true;
        if (RefuseUnlessPositronic(args.User, target, true, out var illness) || illness!.Stage != SchizophreniaStage.Latent)
            return;

        _psychiatry.AdjustStage(target, -1, "firmware-patch");
        QueueDel(ent.Owner);
    }

    private void OnHardResetInteract(Entity<HardResetProbeComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!_toggle.IsActivated(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("psychiatry-probe-not-on"), ent.Owner, args.User);
            return;
        }

        if (target == args.User)
        {
            _popup.PopupEntity(Loc.GetString("psychiatry-no-self"), args.User, args.User);
            return;
        }

        if (RefuseUnlessPositronic(args.User, target, false, out _))
            return;

        if (!_powerCell.HasActivatableCharge(ent.Owner, user: args.User))
            return;

        const int steps = 4;
        var totalSeconds = _random.NextFloat(ent.Comp.DoAfterMinSeconds, ent.Comp.DoAfterMaxSeconds);
        var hit = ent.Comp.ShockDamage / steps;
        var name = Identity.Name(target, EntityManager);
        _popup.PopupEntity(Loc.GetString("psychiatry-probe-target", ("target", name)), ent.Owner, args.User);
        ent.Comp.Progress = 0;
        ent.Comp.ProgressSteps = steps;
        ent.Comp.ProgressHit = hit;
        if (StartProbeStep(ent, args.User, target, 0, steps, totalSeconds / steps, hit))
            args.Handled = true;
    }

    private bool StartProbeStep(
        Entity<HardResetProbeComponent> tool,
        EntityUid user,
        EntityUid target,
        int step,
        int steps,
        float stepSeconds,
        float hitDamage)
    {
        var ev = new HardResetDoAfterEvent
        {
            Step = step,
            Steps = steps,
            StepSeconds = stepSeconds,
            HitDamage = hitDamage,
        };
        var doAfter = new DoAfterArgs(EntityManager, user, stepSeconds, ev, tool, target: target, used: tool)
        {
            BreakOnMove = false,
            BreakOnDamage = false,
            NeedHand = true,
            DistanceThreshold = 1.5f,
        };
        return _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnHardResetDoAfter(Entity<HardResetProbeComponent> ent, ref HardResetDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;
        args.Handled = true;

        if (!_toggle.IsActivated(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("psychiatry-probe-not-on"), ent.Owner, args.User);
            return;
        }

        if (ent.Comp.Progress == 0 && !_powerCell.TryUseActivatableCharge(ent.Owner, user: args.User))
            return;

        ent.Comp.Progress++;
        var shock = new DamageSpecifier();
        shock.DamageDict["Shock"] = FixedPoint2.New(ent.Comp.ProgressHit);
        _damageable.TryChangeDamage(target, shock, origin: args.User);
        _jitter.DoJitter(target, TimeSpan.FromSeconds(0.8), refresh: true, amplitude: 16f, frequency: 8f);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Items/Defib/defib_zap.ogg"), target);

        var finished = ent.Comp.Progress >= ent.Comp.ProgressSteps;
        if (finished)
        {
            ent.Comp.Progress = 0;
            if (TryComp(target, out SchizophreniaComponent? illness)
                && illness.Kind == PsychiatryIllnessKind.Cyberpsychosis)
            {
                _psychiatry.AdjustStage(target, -(int) SchizophreniaStage.Acute, "hard-reset");
                _psychiatry.HoldOnset(target);
                if (_random.Prob(_cfg.GetCVar(CCCCVars.PsychiatryShockFaultChance)))
                    ApplySensoryFault(target);
                _popup.PopupEntity(Loc.GetString("psychiatry-probe-done"), args.User, args.User, PopupType.Medium);
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("psychiatry-shock-zapped"), args.User, args.User, PopupType.Medium);
            }
        }
        else
        {
            StartProbeStep(ent, args.User, target, ent.Comp.Progress, ent.Comp.ProgressSteps, args.StepSeconds, ent.Comp.ProgressHit);
        }

        if (finished && !_powerCell.HasActivatableCharge(ent.Owner))
            _toggle.TryDeactivate(ent.Owner);
    }

    private void OnCascadeInteract(Entity<CascadeSpikeComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;
        if (RefuseUnlessPositronic(args.User, target, false, out _))
            return;
        if (StartToolDoAfter(ent, args.User, target, ent.Comp.Delay, new CascadeSpikeDoAfterEvent()))
            args.Handled = true;
    }

    private void OnCascadeDoAfter(Entity<CascadeSpikeComponent> ent, ref CascadeSpikeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;
        args.Handled = true;
        if (RefuseUnlessPositronic(args.User, target, false, out _))
            return;

        _psychiatry.TryApplyCyber(target, SchizophreniaStage.Latent, "cascade-spike", ignoreCooldown: true, forced: true);
        _popup.PopupEntity(Loc.GetString("psychiatry-cyber-onset"), target, PopupType.MediumCaution);
        QueueDel(ent.Owner);
    }

    private void OnAdminPatchInteract(Entity<AdminCurePatchComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;
        if (RefuseUnlessPositronic(args.User, target, true, out _))
            return;
        if (StartToolDoAfter(ent, args.User, target, ent.Comp.Delay, new AdminCurePatchDoAfterEvent()))
            args.Handled = true;
    }

    private void OnAdminPatchDoAfter(Entity<AdminCurePatchComponent> ent, ref AdminCurePatchDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;
        args.Handled = true;
        if (RefuseUnlessPositronic(args.User, target, true, out _))
            return;

        _psychiatry.ClearIllness(target, "admin-patch");
        _popup.PopupEntity(Loc.GetString("psychiatry-admin-cured"), target, args.User);
        QueueDel(ent.Owner);
    }

    private enum ShockSurface
    {
        Body,
        Metal,
        Moisture,
        Insulator,
    }
}
