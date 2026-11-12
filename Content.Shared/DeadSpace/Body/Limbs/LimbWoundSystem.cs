// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.DeadSpace.Cloning;
using Content.Shared.DeadSpace.Items;
using Content.Shared.Forensics.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Prototypes;
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
using Content.Shared.Actions.Components;
using Content.Shared.Changeling.Components;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Events;
using Content.Shared.DoAfter;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Explosion;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Hands;
using Content.Shared.HealthExaminable;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.IdentityManagement;
using Content.Shared.Input;
using Content.Shared.Inventory;
using System.Runtime.InteropServices;
using Content.Shared.Medical;
using Content.Shared.Medical.Cryogenics;
using Content.Shared.Medical.Healing;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Speech;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Temperature.Components;
using Content.Shared.Throwing;
using Content.Shared.Traits.Assorted;
using Content.Shared.DeadSpace.TheCircle.Geist;
using Content.Shared.DeadSpace.Weapons.Parry;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable.Components;
using Content.Shared.Blocking.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
namespace Content.Shared.DeadSpace.Body.Limbs;

public sealed partial class LimbWoundSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly BlindableSystem _blind = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedGunSystem _guns = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly LimbImplantSystem _implants = default!;
    [Dependency] private readonly Robust.Shared.Audio.Systems.SharedAudioSystem _audio = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;

    private EntityUid? _cause;

    private static readonly Robust.Shared.Audio.SoundSpecifier HarvestSound = new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");
    private static readonly Robust.Shared.Audio.SoundSpecifier SeverSound = new Robust.Shared.Audio.SoundCollectionSpecifier("gib");
    private static readonly Robust.Shared.Audio.SoundSpecifier ButcherSound = new Robust.Shared.Audio.SoundCollectionSpecifier("SpikeButcher");

    private readonly Dictionary<string, int> _fields = new();
    private readonly bool[] _fireBlocked = new bool[LimbWoundRules.BodyZoneCount];
    private readonly HashSet<EntityUid> _legless = new();

    private const SlotFlags Worn = SlotFlags.WITHOUT_POCKET;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LimbBodyComponent, HealthBeingExaminedEvent>(OnHealthExamined);
        SubscribeLocalEvent<LimbBodyComponent, MapInitEvent>(OnSpecies);
        SubscribeLocalEvent<LimbBodyComponent, MobStateChangedEvent>(OnMobState);
        SubscribeLocalEvent<LimbBodyComponent, UpdateMobStateEvent>(OnLimbLife, after: [typeof(MobThresholdSystem)]);
        SubscribeLocalEvent<LimbBodyComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<LimbBodyComponent, LimbHarvestDoAfterEvent>(OnHarvest);
        SubscribeLocalEvent<LimbHarvestingComponent, MeleeAttackEvent>(OnHarvestSwing);
        SubscribeLocalEvent<LimbBodyComponent, BeforeExplodeEvent>(OnBlast);
        SubscribeLocalEvent<LimbBodyComponent, UpdateCanMoveEvent>(OnCanMove);
        SubscribeLocalEvent<LimbBodyComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<LimbBodyComponent, RefreshMovementSpeedModifiersEvent>(OnMove);
        SubscribeLocalEvent<LimbBodyComponent, StandAttemptEvent>(OnStand);
        SubscribeLocalEvent<LimbBodyComponent, StoodEvent>(OnStood);
        SubscribeLocalEvent<LimbBodyComponent, StandUpAttemptEvent>(OnStandUp);
        SubscribeLocalEvent<LimbBodyComponent, HealingDoAfterEvent>(OnHeal, before: [typeof(HealingSystem)]);
        SubscribeLocalEvent<LimbBodyComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<LimbBodyComponent, BeforeStaminaDamageEvent>(OnStamina);
        SubscribeLocalEvent<LimbBodyComponent, IgnitedEvent>(OnIgnited);
        SubscribeLocalEvent<LimbBodyComponent, ExtinguishedEvent>(OnExtinguished);
        SubscribeLocalEvent<LimbBodyComponent, LimbSeveredEvent>(OnSevered);
        SubscribeLocalEvent<LimbBodyComponent, AttackedEvent>(OnMelee);
        SubscribeLocalEvent<LimbBodyComponent, PickupAttemptEvent>(OnPickup);
        SubscribeLocalEvent<LimbBodyComponent, LimbArmorZoneEvent>(OnArmorZone);
        SubscribeLocalEvent<LimbBodyComponent, DamageModifyEvent>(OnDeadLimbHit);
        SubscribeLocalEvent<LimbBodyComponent, BeforeMeleeDamageEvent>(OnBeforeMelee, after: [typeof(ParrySystem)]);
        SubscribeLocalEvent<MeleeWeaponComponent, GetMeleeAttackRateEvent>(OnMeleeRate);
        SubscribeLocalEvent<LimbBodyComponent, GetMeleeDamageEvent>(OnFistDamage);
        SubscribeLocalEvent<GunComponent, GunRefreshModifiersEvent>(OnGunModifiers);
        SubscribeLocalEvent<GunComponent, GotEquippedHandEvent>((uid, _, _) => _guns.RefreshModifiers(uid));
        SubscribeLocalEvent<GunComponent, GotUnequippedHandEvent>((uid, _, _) => _guns.RefreshModifiers(uid));
        SubscribeLocalEvent<ProjectileComponent, ProjectileHitEvent>(OnProjectile);
        SubscribeLocalEvent<HitscanBasicDamageComponent, HitscanDamageDealtEvent>(OnHitscan);
        SubscribeAllEvent<LimbAimSetEvent>(OnAimSet);
        SubscribeAllEvent<LimbZoneSetEvent>(OnZoneSet);

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.AimShot, InputCmdHandler.FromDelegate(OnAimDown, OnAimUp))
            .Register<LimbWoundSystem>();

        Subs.CVar(_cfg, CCVars.LimbEnabled, value => LimbTuning.Enabled = value, true);
        Subs.CVar(_cfg, CCVars.LimbBurnFirst, value => LimbTuning.BurnFirst = value, true);
        Subs.CVar(_cfg, CCVars.LimbBurnSecond, value => LimbTuning.BurnSecond = value, true);
        Subs.CVar(_cfg, CCVars.LimbBurnThird, value => LimbTuning.BurnThird = value, true);
        Subs.CVar(_cfg, CCVars.LimbBurnFourth, value => LimbTuning.BurnFourth = value, true);
        Subs.CVar(_cfg, CCVars.LimbSeverHand, value => LimbTuning.SeverHand = value, true);
        Subs.CVar(_cfg, CCVars.LimbSeverLimb, value => LimbTuning.SeverLimb = value, true);
        Subs.CVar(_cfg, CCVars.LimbSeverHead, value => LimbTuning.SeverHead = value, true);
        Subs.CVar(_cfg, CCVars.LimbCrawlSpeed, value => LimbTuning.CrawlSpeed = value, true);
        Subs.CVar(_cfg, CCVars.LimbCrawlAcceleration, value => LimbTuning.CrawlAcceleration = value, true);
        Subs.CVar(_cfg, CCVars.LimbFireSpread, value => LimbTuning.FireSpread = value, true);
        Subs.CVar(_cfg, CCVars.LimbLimpSpeed, value => LimbTuning.LimpSpeed = value, true);
        Subs.CVar(_cfg, CCVars.LimbMissingLegSpeed, value => LimbTuning.MissingLegSpeed = value, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<LimbWoundSystem>();
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient || !LimbTuning.Enabled)
            return;

        DropLegless();
        var query = EntityQueryEnumerator<LimbActiveComponent, LimbBodyComponent>();
        while (query.MoveNext(out var uid, out var active, out var limbs))
        {
            active.Accumulator += frameTime;
            if (active.Accumulator < LimbActiveComponent.Period)
                continue;

            active.Accumulator -= LimbActiveComponent.Period;
            if (!Step(uid, limbs, active))
                RemComp<LimbActiveComponent>(uid);
        }
    }

    private bool Step(EntityUid uid, LimbBodyComponent limbs, LimbActiveComponent active)
    {
        if (_mobs.IsDead(uid))
            return false;

        EnsureZones(limbs);
        EnsureOrgans(limbs);
        var zones = CollectionsMarshal.AsSpan(limbs.Zones);
        if (AnyFire(limbs))
        {
            var blocked = FireBlocked(uid);
            var count = Math.Min(zones.Length, LimbWoundRules.BodyZoneCount);
            for (var i = 0; i < count; i++)
            {
                if (!blocked[i])
                    continue;

                zones[i].Fire = 0f;
                zones[i].Cloth = 0f;
            }

            if (TryComp<FlammableComponent>(uid, out var flame) && flame.OnFire
                && LimbWoundRules.Hottest(zones) is var hot && hot >= 0 && zones[hot].Fire < flame.FireStacks)
                zones[hot].Fire = flame.FireStacks;

            LimbWoundRules.SpreadFire(zones, blocked);
            Dirty(uid, limbs);
        }

        var bleed = BleedTotal(uid, limbs);
        if (!Machine(uid) && LimbWoundRules.DrainBleed(zones, limbs.TorsoPool))
            Dirty(uid, limbs);
        ApplyLife(uid, limbs);
        var failure = LimbOrganRules.Tick(CollectionsMarshal.AsSpan(limbs.Organs));
        if (_mobs.IsCritical(uid))
            failure = failure with { Asphyxiation = failure.Asphyxiation + LimbWoundRules.CritAir };
        var torso = zones[LimbWoundRules.Index(LimbZone.Torso)];
        var air = failure.Asphyxiation + LimbWoundRules.RibBreath(torso.Bone, torso.Flesh);
        if (bleed > LimbWoundRules.TickBleed || failure.Bloodloss > 0f || air > 0f || failure.Poison > 0f)
        {
            var spec = new DamageSpecifier();
            if (bleed > LimbWoundRules.TickBleed || failure.Bloodloss > 0f)
            {
                var loss = bleed * LimbWoundRules.BleedLocal + failure.Bloodloss;
                spec.DamageDict["Bloodloss"] = FixedPoint2.New(loss);
                var open = 0f;
                for (var i = 0; i < zones.Length; i++)
                {
                    if (zones[i].Bleed > LimbWoundRules.TickBleed)
                        open += zones[i].Bleed;
                }

                if (open > 0f)
                {
                    for (var i = 0; i < zones.Length; i++)
                    {
                        if (zones[i].Bleed <= LimbWoundRules.TickBleed)
                            continue;

                        zones[i].HitBlood += loss * zones[i].Bleed / open;
                    }

                    _bloodstream.TryBleedOut(uid, FixedPoint2.New(loss));
                }
                else
                    zones[LimbWoundRules.Index(LimbZone.Torso)].HitBlood += loss;
            }

            if (air > 0f)
            {
                spec.DamageDict["Asphyxiation"] = FixedPoint2.New(air);
                zones[LimbWoundRules.Index(LimbZone.Torso)].HitAir += air;
            }

            if (failure.Poison > 0f)
            {
                spec.DamageDict["Poison"] = FixedPoint2.New(failure.Poison);
                zones[LimbWoundRules.Index(LimbZone.Torso)].HitCaustic += failure.Poison;
            }
            limbs.LedgerSilence++;
            _damage.ChangeDamage(uid, spec, interruptsDoAfters: false);
            limbs.LedgerSilence--;
            Dirty(uid, limbs);
        }

        var pain = 0f;
        for (var i = 0; i < zones.Length; i++)
            pain += LimbWoundRules.BurnPain(zones[i].Skin);

        if (pain > 0f)
        {
            var heat = new DamageSpecifier();
            heat.DamageDict["Heat"] = FixedPoint2.New(pain);
            limbs.LedgerSilence++;
            _damage.ChangeDamage(uid, heat, interruptsDoAfters: false);
            limbs.LedgerSilence--;
        }

        if (HasComp<InsideCryoPodComponent>(uid))
        {
            active.CryoAccumulator += LimbActiveComponent.Period;
            if (active.CryoAccumulator >= LimbActiveComponent.CryoPeriod)
            {
                active.CryoAccumulator = 0f;
                for (var i = 0; i < zones.Length; i++)
                    LimbWoundRules.Cryo(ref zones[i]);
                LimbOrganRules.Cryo(CollectionsMarshal.AsSpan(limbs.Organs));
                Dirty(uid, limbs);
                _movement.RefreshMovementSpeedModifiers(uid);
            }
        }

        return LimbWoundRules.Awake(zones, CollectionsMarshal.AsSpan(limbs.Organs), HasComp<InsideCryoPodComponent>(uid));
    }

    private void RefreshActive(EntityUid uid, LimbBodyComponent limbs)
    {
        if (_net.IsClient)
            return;

        EnsureZones(limbs);
        EnsureOrgans(limbs);
        var cryo = HasComp<InsideCryoPodComponent>(uid);
        if (LimbWoundRules.Awake(CollectionsMarshal.AsSpan(limbs.Zones), CollectionsMarshal.AsSpan(limbs.Organs), cryo))
            EnsureComp<LimbActiveComponent>(uid);
        else
            RemComp<LimbActiveComponent>(uid);
    }

    public void SetTarget(EntityUid uid, LimbZone? zone, LimbTargetComponent? target = null)
    {
        if (HasComp<GhostComponent>(uid) || !Resolve(uid, ref target, logMissing: false))
            return;

        target.Selected = zone;
        Dirty(uid, target);
        if (TryComp<LimbAimComponent>(uid, out var aim))
        {
            aim.CalledZone = zone ?? LimbZone.Torso;
            Dirty(uid, aim);
        }
    }

    public void SetAiming(EntityUid uid, bool aiming, LimbAimComponent? aim = null)
    {
        if (HasComp<GhostComponent>(uid) || !Resolve(uid, ref aim, logMissing: false))
            return;

        var was = aim.Aiming;
        aim.Aiming = aiming;
        Dirty(uid, aim);
        RefreshHeldGuns(uid);
        if (_net.IsServer && was != aiming)
            _popup.PopupEntity(Loc.GetString(aiming ? "limb-aim-on" : "limb-aim-off"), uid, uid);
    }

    public LimbStrikeResult Strike(EntityUid uid, LimbStrike strike, LimbBodyComponent? limbs = null, bool ready = false)
    {
        if (!LimbTuning.Enabled || HasComp<GodmodeComponent>(uid) || !Resolve(uid, ref limbs))
            return LimbStrikeResult.Miss();

        EnsureZones(limbs);
        var span = CollectionsMarshal.AsSpan(limbs.Zones);
        if (LimbWoundRules.IsSmall(strike.Zone))
        {
            var dummy = span[LimbWoundRules.Index(LimbZone.Head)];
            var small = LimbWoundRules.Apply(ref dummy, strike, limbs.Soft);
            if (small.Applied)
                ApplySmallToHead(span, strike);
            ApplyOrgans(uid, limbs, strike, small, ready);
            ApplyResult(uid, strike.Zone, small);
            FinishHit(uid, limbs, strike, small);
            return small;
        }

        var index = LimbWoundRules.Index(strike.Zone);
        ref var state = ref span[index];
        var boneBefore = state.Bone;
        var result = LimbWoundRules.Apply(ref state, strike, limbs.Soft);
        ApplyOrgans(uid, limbs, strike, result, ready);
        var pelletSevered = ApplyPelletSever(uid, limbs, strike);
        Dirty(uid, limbs);
        ApplyResult(uid, strike.Zone, result);
        FinishHit(uid, limbs, strike, result);
        LogBone(uid, strike.Zone, boneBefore, limbs.Zones[index].Bone);
        if (pelletSevered)
            _movement.RefreshMovementSpeedModifiers(uid);

        if (result.Severed)
            DropZone(uid, limbs, strike.Zone);
        else if (result.Applied && strike.Mark == LimbMark.Blunt)
        {
            _movement.RefreshMovementSpeedModifiers(uid);
        }

        return result;
    }

    public bool HasLimp(EntityUid uid, LimbBodyComponent? limbs = null)
    {
        if (!Resolve(uid, ref limbs, false))
            return false;

        EnsureZones(limbs);
        var zones = CollectionsMarshal.AsSpan(limbs.Zones);
        var adrenaline = Adrenaline(uid);
        return LimbWoundRules.Limp(zones[LimbWoundRules.Index(LimbZone.LegLeft)], adrenaline)
               || LimbWoundRules.Limp(zones[LimbWoundRules.Index(LimbZone.LegRight)], adrenaline)
               || LimbWoundRules.Limp(zones[LimbWoundRules.Index(LimbZone.FootLeft)], adrenaline)
               || LimbWoundRules.Limp(zones[LimbWoundRules.Index(LimbZone.FootRight)], adrenaline);
    }

    public void Bandage(EntityUid uid, LimbZone zone, LimbBodyComponent? limbs = null)
    {
        if (!Resolve(uid, ref limbs) || !LimbWoundRules.IsBody(zone))
            return;

        EnsureZones(limbs);
        ref var state = ref CollectionsMarshal.AsSpan(limbs.Zones)[LimbWoundRules.Index(zone)];
        LimbWoundRules.Bandage(ref state);
        Dirty(uid, limbs);
        RefreshActive(uid, limbs);
    }

    public bool CanStopOneBleed(EntityUid uid, LimbBodyComponent? limbs = null)
    {
        if (!Resolve(uid, ref limbs, false) || Machine(uid))
            return false;

        EnsureZones(limbs);
        EnsureOrgans(limbs);
        var zones = CollectionsMarshal.AsSpan(limbs.Zones);
        var count = Math.Min(zones.Length, LimbWoundRules.BodyZoneCount);
        for (var i = 0; i < count; i++)
        {
            if (!zones[i].Severed && zones[i].Bleed > LimbWoundRules.TickBleed)
                return true;
        }

        return OrganBleeds(limbs, LimbOrgan.Heart);
    }

    public bool StopOneBleed(EntityUid uid, LimbBodyComponent? limbs = null)
    {
        if (!Resolve(uid, ref limbs, false) || Machine(uid))
            return false;

        EnsureZones(limbs);
        EnsureOrgans(limbs);
        var zones = CollectionsMarshal.AsSpan(limbs.Zones);
        var best = -1;
        var amount = LimbWoundRules.TickBleed;
        var count = Math.Min(zones.Length, LimbWoundRules.BodyZoneCount);
        for (var i = 0; i < count; i++)
        {
            if (zones[i].Severed || zones[i].Bleed <= amount)
                continue;

            amount = zones[i].Bleed;
            best = i;
        }

        if (best >= 0)
        {
            zones[best].Bleed = 0f;
            Dirty(uid, limbs);
            RefreshActive(uid, limbs);
            return true;
        }

        if (StopOrganBleed(limbs, LimbOrgan.Heart))
        {
            Dirty(uid, limbs);
            return true;
        }

        return false;
    }

    private static bool OrganBleeds(LimbBodyComponent limbs, LimbOrgan organ)
    {
        var index = (int) organ;
        return (uint) index < (uint) limbs.Organs.Count && limbs.Organs[index].Ruptured && !limbs.Organs[index].BleedStopped;
    }

    private static bool StopOrganBleed(LimbBodyComponent limbs, LimbOrgan organ)
    {
        var index = (int) organ;
        if ((uint) index >= (uint) limbs.Organs.Count)
            return false;

        var state = limbs.Organs[index];
        if (!state.Ruptured || state.BleedStopped)
            return false;

        state.BleedStopped = true;
        limbs.Organs[index] = state;
        return true;
    }

    public float BleedTotal(EntityUid uid, LimbBodyComponent? limbs = null)
    {
        if (!Resolve(uid, ref limbs, false))
            return 0f;

        EnsureZones(limbs);
        if (Machine(uid))
            return 0f;

        var total = 0f;
        var zones = CollectionsMarshal.AsSpan(limbs.Zones);
        for (var i = 0; i < zones.Length; i++)
        {
            if (zones[i].Severed || LimbWoundRules.DeadLimb((LimbZone) i, zones[i]))
                continue;

            total += zones[i].Bleed;
        }

        return total;
    }

    private void OnMove(Entity<LimbBodyComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!LimbTuning.Enabled)
            return;

        var missing = MissingLegs(ent.Comp);
        if (missing >= 2)
            return;

        var limp = HasLimp(ent, ent.Comp);
        if (!limp && missing == 0)
            return;

        var speed = LimbWoundRules.MoveSpeed(limp, missing);
        args.ModifySpeed(speed, speed);
    }

    private void OnStand(EntityUid uid, LimbBodyComponent limbs, StandAttemptEvent args)
    {
        if (!LimbTuning.Enabled || MissingLegs(limbs) < 2)
            return;

        args.Cancel();
    }

    private void OnStandUp(Entity<LimbBodyComponent> ent, ref StandUpAttemptEvent args)
    {
        if (args.Cancelled || !LimbTuning.Enabled || MissingLegs(ent.Comp) < 2)
            return;

        args.Cancelled = true;
        args.Autostand = false;
        args.Message = (Loc.GetString("limb-stand-no-legs"), PopupType.SmallCaution);
    }

    private void OnStood(EntityUid uid, LimbBodyComponent limbs, StoodEvent args)
    {
        if (!LimbTuning.Enabled || _net.IsClient || MissingLegs(limbs) < 2)
            return;

        _legless.Add(uid);
    }

    private void DropLegless()
    {
        if (_legless.Count == 0)
            return;

        foreach (var uid in _legless)
        {
            if (!TryComp<LimbBodyComponent>(uid, out var limbs) || MissingLegs(limbs) < 2)
                continue;

            _standing.Down(uid, playSound: false, dropHeldItems: false, force: true);
        }

        _legless.Clear();
    }

    private void OnHeal(Entity<LimbBodyComponent> ent, ref HealingDoAfterEvent args)
    {
        if (!LimbTuning.Enabled || args.Cancelled || args.Handled || !TryComp(args.Used, out HealingComponent? healing))
            return;

        EnsureZones(ent.Comp);
        var zones = CollectionsMarshal.AsSpan(ent.Comp.Zones);
        var index = -1;
        var selectedIndex = -1;
        if (TryComp<LimbTargetComponent>(args.User, out var picked) && picked.Selected is { } selected)
            selectedIndex = LimbWoundRules.Index(selected);
        index = LimbWoundRules.TreatIndex(zones, selectedIndex);
        var burnOnly = (Heals(healing.Damage, "Heat") || Heals(healing.Damage, "Caustic"))
                       && healing.BloodlossModifier >= 0 && !Heals(healing.Damage, "Blunt");
        if (selectedIndex < 0 && burnOnly && MostBurned(zones) is var hot && hot >= 0)
            index = hot;
        if (index >= 0)
            ent.Comp.LedgerZone = (LimbZone) index;
        if (index < 0)
            return;

        ref var state = ref zones[index];
        var zone = (LimbZone) index;
        if (healing.BloodlossModifier < 0)
            LimbWoundRules.Bandage(ref state);

        if (Heals(healing.Damage, "Blunt"))
            LimbWoundRules.Splint(ref state);

        if (Heals(healing.Damage, "Heat") || Heals(healing.Damage, "Caustic"))
        {
            var burned = state.Skin;
            LimbWoundRules.Ointment(ref state, LimbWoundRules.OintmentDose);
            if (state.Skin < burned)
                ent.Comp.MendedAt = _timing.CurTime;
        }

        var before = state.Health;
        LimbWoundRules.Mend(ref state, HealAmount(healing.Damage), LimbWoundRules.HealthFor(zone, ent.Comp), LimbWoundRules.Revives(zone));
        if (state.Health > before)
            ent.Comp.MendedAt = _timing.CurTime;

        Dirty(ent);
        _movement.RefreshMovementSpeedModifiers(ent);
        RefreshActive(ent, ent.Comp);
    }

    private void OnBeforeDamage(Entity<LimbBodyComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (!LimbTuning.Enabled || _net.IsClient || args.Cancelled || ent.Comp.LedgerSilence > 0)
            return;

        var cool = 0f;
        foreach (var (type, value) in args.Damage.DamageDict)
        {
            if (type is "Heat" or "Caustic" && value < 0)
                cool -= value.Float();
        }

        if (cool <= 0f)
            return;

        EnsureZones(ent.Comp);
        if (LimbWoundRules.CoolSkin(CollectionsMarshal.AsSpan(ent.Comp.Zones), cool))
            Dirty(ent);
    }

    private static int MostBurned(Span<LimbZoneState> zones)
    {
        var best = -1;
        var worst = 0f;
        for (var i = 0; i < zones.Length && i < LimbWoundRules.BodyZoneCount; i++)
        {
            if (zones[i].Severed || zones[i].Skin <= worst)
                continue;

            worst = zones[i].Skin;
            best = i;
        }

        return best;
    }

    private void OnIgnited(Entity<LimbBodyComponent> ent, ref IgnitedEvent args)
    {
        if (!LimbTuning.Enabled)
            return;

        EnsureZones(ent.Comp);
        var amount = 2f;
        DamageSpecifier? burn = null;
        if (TryComp<FlammableComponent>(ent, out var fire))
        {
            amount = MathF.Max(amount, fire.FireStacks);
            burn = fire.Damage;
        }

        LimbZone? hit = null;
        if (ent.Comp.FireOrigin is { } origin
            && ent.Comp.FireOriginTime == _timing.CurTime
            && LimbWoundRules.IsBody(origin))
            hit = origin;

        ent.Comp.ArmorZone = hit ?? LimbZone.Torso;
        amount *= FireScale(ent.Owner, burn);
        ent.Comp.ArmorZone = null;
        var zones = CollectionsMarshal.AsSpan(ent.Comp.Zones);
        if (hit is { } struck)
            LimbWoundRules.Ignite(zones, struck, amount);
        else
            LimbWoundRules.IgniteFloor(zones, amount);
        Dirty(ent);
        RefreshActive(ent, ent.Comp);
    }

    private void OnExtinguished(Entity<LimbBodyComponent> ent, ref ExtinguishedEvent args)
    {
        EnsureZones(ent.Comp);
        var zones = CollectionsMarshal.AsSpan(ent.Comp.Zones);
        for (var i = 0; i < zones.Length; i++)
            zones[i].Fire = 0f;

        Dirty(ent);
        RefreshActive(ent, ent.Comp);
    }

    private void OnMelee(EntityUid uid, LimbBodyComponent limbs, AttackedEvent args)
    {
        if (!LimbTuning.Enabled || HasComp<GodmodeComponent>(uid) || !TryComp<MeleeWeaponComponent>(args.Used, out var melee))
            return;

        var zone = LimbZone.Torso;
        if (TryComp<LimbTargetComponent>(args.User, out var picked) && picked.Selected is { } selected)
            zone = Present(limbs, selected);

        limbs.LedgerZone = zone;
        limbs.ArmorZone = zone;
        limbs.FireOrigin = zone;
        limbs.FireOriginTime = _timing.CurTime;
        limbs.StrikeOpen = true;
        var armed = args.Used != args.User;
        var ready = _combat.IsInCombatMode(uid);
        limbs.AllowDrop = zone == LimbZone.Head && armed;
        limbs.DropHeavyOnly = ready;
        limbs.StaminaArmed = Amount(melee.Damage, "Blunt") > 0f;
        limbs.StaminaScale = !limbs.AllowDrop ? 0f : ready ? 0.5f : 1f;
        var incoming = new DamageSpecifier(melee.Damage);
        if (ready)
        {
            incoming *= 0.9f;
            foreach (var (type, value) in melee.Damage.DamageDict)
            {
                if (value > 0)
                    args.BonusDamage.DamageDict[type] = -value * 0.1f;
            }
        }

        var worn = Resist(uid, incoming, args.User);
        var armored = worn.GetTotal() + 0.05f < melee.Damage.GetTotal();
        var strike = BuildStrike(uid, args.User, zone, worn, projectile: false, tool: args.Used, throughArmor: armored);
        _cause = args.User;
        Strike(uid, strike, limbs, ready);
        _cause = null;
    }

    private void OnStamina(Entity<LimbBodyComponent> ent, ref BeforeStaminaDamageEvent args)
    {
        if (!ent.Comp.StaminaArmed)
            return;

        ent.Comp.StaminaArmed = false;
        if (ent.Comp.StaminaScale <= 0f)
        {
            args.Cancelled = true;
            return;
        }

        args.Value *= ent.Comp.StaminaScale;
    }

    public void NoteDamage(EntityUid uid, LimbBodyComponent limbs, DamageChangedEvent args)
    {
        limbs.ArmorZone = null;
        if (!LimbTuning.Enabled || args.DamageDelta == null || limbs.LedgerSilence > 0)
            return;

        EnsureZones(limbs);
        var zones = CollectionsMarshal.AsSpan(limbs.Zones);
        var aimedHeal = false;
        var zone = LimbZone.Torso;
        var picked = limbs.LedgerZone is { } aimed && LimbWoundRules.IsSmall(aimed) ? LimbZone.Head : limbs.LedgerZone;
        if (picked is { } body && LimbWoundRules.IsBody(body))
        {
            aimedHeal = true;
            zone = body;
        }
        limbs.LedgerZone = null;
        var fromStrike = limbs.StrikeOpen;
        limbs.StrikeOpen = false;
        var changed = false;
        var healed = false;
        foreach (var value in args.DamageDelta.DamageDict.Values)
        {
            if (value < 0)
                healed = true;
        }

        if (healed && !aimedHeal)
            changed = SpreadHeal(zones, args.DamageDelta, limbs);

        if (args.DamageIncreased && !fromStrike && HeatOf(args.DamageDelta) > 0f && AnyFire(limbs) && !healed)
        {
            LimbWoundRules.Scorch(zones, HeatOf(args.DamageDelta) * limbs.Burn);
            changed = true;
            ref var state = ref zones[LimbWoundRules.Index(zone)];
            foreach (var (type, value) in args.DamageDelta.DamageDict)
            {
                if (type == "Heat" || value <= 0)
                    continue;

                Note(ref state, type, value.Float());
            }
        }
        else if (args.DamageIncreased || aimedHeal)
        {
            ref var state = ref zones[LimbWoundRules.Index(zone)];
            foreach (var (type, value) in args.DamageDelta.DamageDict)
            {
                if (value <= 0)
                    continue;

                Note(ref state, type, value.Float());
                changed = true;
            }
        }

        if (healed && args.Damageable.TotalDamage <= FixedPoint2.New(0.5))
            changed |= RestoreZones(limbs);

        if (changed)
            Dirty(uid, limbs);
    }

    private static float HeatOf(DamageSpecifier delta)
    {
        return delta.DamageDict.TryGetValue("Heat", out var value) && value > 0 ? value.Float() : 0f;
    }

    private bool SpreadHeal(Span<LimbZoneState> zones, DamageSpecifier delta, LimbBodyComponent limbs)
    {
        var count = Math.Min(zones.Length, LimbWoundRules.BodyZoneCount);
        var changed = false;
        foreach (var (type, value) in delta.DamageDict)
        {
            if (value >= 0)
                continue;

            var field = Field(type);
            var values = new float[count];
            for (var i = 0; i < count; i++)
            {
                if (!zones[i].Severed)
                    values[i] = FieldOf(zones[i], field);
            }

            LimbWoundRules.SpreadOne(values, value.Float());
            for (var i = 0; i < count; i++)
            {
                if (zones[i].Severed)
                    continue;

                var have = FieldOf(zones[i], field);
                if (Math.Abs(have - values[i]) <= 0.001f)
                    continue;

                SetField(ref zones[i], field, values[i]);
                changed = true;
            }

            if (field != AirField)
            {
                MendDeficits(zones, -value.Float(), limbs);
                changed = true;
            }
        }

        return changed;
    }

    private static void MendDeficits(Span<LimbZoneState> zones, float amount, LimbBodyComponent limbs)
    {
        if (amount <= 0f)
            return;

        var count = Math.Min(zones.Length, LimbWoundRules.BodyZoneCount);
        var deficit = new float[count];
        var total = 0f;
        for (var i = 0; i < count; i++)
        {
            var zone = (LimbZone) i;
            if (zones[i].Severed || zones[i].Atrophied && !LimbWoundRules.Revives(zone))
                continue;

            deficit[i] = MathF.Max(0f, LimbWoundRules.HealthFor(zone, limbs) - zones[i].Health);
            total += deficit[i];
        }

        if (total <= 0f)
            return;

        for (var i = 0; i < count; i++)
        {
            if (deficit[i] <= 0f)
                continue;

            var zone = (LimbZone) i;
            LimbWoundRules.Mend(ref zones[i], amount * deficit[i] / total, LimbWoundRules.HealthFor(zone, limbs), LimbWoundRules.Revives(zone));
        }
    }

    private const int AirField = 6;

    private int Field(string type)
    {
        if (_fields.TryGetValue(type, out var cached))
            return cached;

        DamageGroupPrototype? found = null;
        foreach (var group in _proto.EnumeratePrototypes<DamageGroupPrototype>())
        {
            if (!group.DamageTypes.Contains(type))
                continue;

            found = group;
            break;
        }

        var slot = found == null ? 0 : found.DamageTypes.IndexOf(type);
        var field = 0;
        if (found != null && found.ID == "Airloss")
            field = slot == 0 ? AirField : 5;
        else if (found != null && found.ID == "Burn")
            field = type == "Caustic" ? 4 : 3;
        else if (found != null && found.ID == "Toxin")
            field = 4;
        else if (slot == 1)
            field = 1;
        else if (slot == 2)
            field = 2;

        _fields[type] = field;
        return field;
    }

    private static float FieldOf(in LimbZoneState state, int field)
    {
        return field switch
        {
            1 => state.HitSlash,
            2 => state.HitPierce,
            3 => state.HitHeat,
            4 => state.HitCaustic,
            5 => state.HitBlood,
            6 => state.HitAir,
            _ => state.HitBlunt,
        };
    }

    private static void SetField(ref LimbZoneState state, int field, float amount)
    {
        switch (field)
        {
            case 1:
                state.HitSlash = amount;
                break;
            case 2:
                state.HitPierce = amount;
                break;
            case 3:
                state.HitHeat = amount;
                break;
            case 4:
                state.HitCaustic = amount;
                break;
            case 5:
                state.HitBlood = amount;
                break;
            case 6:
                state.HitAir = amount;
                break;
            default:
                state.HitBlunt = amount;
                break;
        }
    }

    public float LedgerFor(in LimbZoneState state, string type)
    {
        return FieldOf(state, Field(type));
    }

    private void Note(ref LimbZoneState state, string type, float amount)
    {
        var field = Field(type);
        SetField(ref state, field, FieldOf(state, field) + amount);
    }

    private static void ClampHits(ref LimbZoneState state)
    {
        state.HitBlunt = MathF.Max(0f, state.HitBlunt);
        state.HitSlash = MathF.Max(0f, state.HitSlash);
        state.HitPierce = MathF.Max(0f, state.HitPierce);
        state.HitHeat = MathF.Max(0f, state.HitHeat);
        state.HitCaustic = MathF.Max(0f, state.HitCaustic);
        state.HitBlood = MathF.Max(0f, state.HitBlood);
        state.HitAir = MathF.Max(0f, state.HitAir);
    }

    private DamageSpecifier Resist(EntityUid target, DamageSpecifier damage, EntityUid? origin = null, bool global = true)
    {
        var next = new DamageSpecifier(damage);
        if (TryComp<DamageableComponent>(target, out var damageable)
            && damageable.DamageModifierSetId is { } setId
            && _proto.TryIndex(setId, out DamageModifierSetPrototype? modifier))
            next = DamageSpecifier.ApplyModifierSet(next, modifier);

        var ev = new DamageModifyEvent(next, origin);
        RaiseLocalEvent(target, ev);
        return global ? _damage.ApplyUniversalAllModifiers(ev.Damage) : ev.Damage;
    }

    private float FireScale(EntityUid uid, DamageSpecifier? sample)
    {
        var ev = new GetFireProtectionEvent();
        RaiseLocalEvent(uid, ref ev);
        if (TryComp<InventoryComponent>(uid, out var inventory))
            _inventory.RelayEvent((uid, inventory), ref ev);

        var scale = ev.Multiplier;
        if (sample == null || sample.GetTotal() <= 0)
            return scale;

        var raw = sample.GetTotal().Float();
        if (raw <= 0f)
            return scale;

        return scale * Resist(uid, sample).GetTotal().Float() / raw;
    }

    private void OnBeforeMelee(Entity<LimbBodyComponent> ent, ref BeforeMeleeDamageEvent args)
    {
        if (!LimbTuning.Enabled || args.Cancelled)
            return;

        if (_mobs.IsDead(ent.Owner) && Harvesting(args.Attacker, ent.Owner))
        {
            if (TryComp<LimbHarvestingComponent>(args.Attacker, out var harvesting))
                _doAfter.Cancel(args.Attacker, harvesting.Index);
            return;
        }

        if (!args.Wide && _mobs.IsDead(ent.Owner) && _combat.IsInCombatMode(args.Attacker) && TryHarvest(args.Attacker, ent, args.Weapon))
            args.Cancelled = true;
    }

    private void OnMeleeRate(Entity<MeleeWeaponComponent> ent, ref GetMeleeAttackRateEvent args)
    {
        if (!LimbTuning.Enabled || !TryComp<LimbBodyComponent>(args.User, out var limbs))
            return;

        EnsureZones(limbs);
        var zones = CollectionsMarshal.AsSpan(limbs.Zones);
        var adrenaline = Adrenaline(args.User);
        var slow = LimbWoundRules.SlowHands(zones[LimbWoundRules.Index(LimbZone.ArmLeft)], zones[LimbWoundRules.Index(LimbZone.HandLeft)], adrenaline)
                   || LimbWoundRules.SlowHands(zones[LimbWoundRules.Index(LimbZone.ArmRight)], zones[LimbWoundRules.Index(LimbZone.HandRight)], adrenaline);
        if (slow)
            args.Multipliers *= LimbWoundRules.HandAttackRate;
    }

    private void OnFistDamage(Entity<LimbBodyComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (args.Weapon != ent.Owner
            || !TryComp<HandsComponent>(ent, out var hands)
            || !_hands.TryGetHand((ent, hands), hands.ActiveHandId, out var found)
            || found.Value.Location is not (HandLocation.Left or HandLocation.Right)
            || !TryComp<MeleeWeaponComponent>(ent, out var own)
            || !TryComp<HumanoidAppearanceComponent>(ent, out var look))
            return;

        var symmetry = found.Value.Location == HandLocation.Left ? BodyPartSymmetry.Left : BodyPartSymmetry.Right;
        foreach (var part in _body.GetBodyChildrenOfType(ent, BodyPartType.Hand))
        {
            if (part.Component.Symmetry != symmetry)
                continue;

            var from = DonorSpecies(part.Id);
            if (from.Length == 0 && _body.GetParentPartOrNull(part.Id) is { } arm)
                from = DonorSpecies(arm);

            if (from.Length == 0
                || from == look.Species.Id
                || !_proto.TryIndex<SpeciesPrototype>(from, out var species)
                || !_proto.TryIndex(species.Prototype, out var mob)
                || !mob.TryGetComponent<MeleeWeaponComponent>(out var donor, EntityManager.ComponentFactory))
                return;

            var baseTotal = own.Damage.GetTotal().Float();
            var scale = baseTotal > 0f ? args.Damage.GetTotal().Float() / baseTotal : 1f;
            args.Damage = new DamageSpecifier(donor.Damage) * scale;
            return;
        }
    }

    private string DonorSpecies(EntityUid part)
    {
        return TryComp<LimbFleshComponent>(part, out var flesh) ? flesh.Species : "";
    }

    private void OnGunModifiers(Entity<GunComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (!LimbTuning.Enabled || Holder(ent.Owner) is not { } user || !TryComp<LimbAimComponent>(user, out var aim) || !aim.Aiming)
            return;

        if (!AimPrecise(user))
            return;

        args.FireRate *= LimbWoundRules.AimFireRate;
        args.AngleIncrease *= LimbWoundRules.AimSpread;
        args.MaxAngle *= LimbWoundRules.AimSpread;
    }

    private void OnProjectile(Entity<ProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (!LimbTuning.Enabled || HasComp<GodmodeComponent>(args.Target) || !TryComp<LimbBodyComponent>(args.Target, out var limbs))
            return;

        var shooter = args.Shooter;
        LimbAimComponent? aim = null;
        var aiming = shooter is { } gunner && TryComp(gunner, out aim) && aim.Aiming && AimPrecise(gunner);
        var ready = _combat.IsInCombatMode(args.Target);
        var hit = _transform.GetWorldPosition(ent);
        var from = ent.Comp.LimbShotOrigin ?? (shooter != null ? _transform.GetWorldPosition(shooter.Value) : hit);
        var distance = (hit - from).Length();
        var keep = LimbWoundRules.BulletDistance(distance);
        if (keep < 0.999f)
            args.Damage *= keep;
        var aimPoint = ent.Comp.LimbAimPoint ?? hit;
        var aimed = ZoneForShot(aiming, aim, args.Target, from, hit, aimPoint);
        var slip = shooter != null ? Cover(args.Target, shooter.Value, aimed).Slip : 0f;
        var zone = Present(limbs, Place(aimed, distance, ready, slip));
        limbs.LedgerZone = zone;
        limbs.ArmorZone = zone;
        limbs.FireOrigin = zone;
        limbs.FireOriginTime = _timing.CurTime;
        limbs.StrikeOpen = true;
        limbs.AllowDrop = zone == LimbZone.Head;
        limbs.DropHeavyOnly = false;
        var worn = Resist(args.Target, args.Damage, shooter);
        var born = Amount(ent.Comp.Damage, "Piercing");
        var armored = worn.GetTotal() + 0.05f < args.Damage.GetTotal();
        var strike = BuildStrike(args.Target, shooter ?? args.Target, zone, worn, projectile: true, distance, tool: ent.Owner, catalogPierce: born, throughArmor: armored, called: aiming ? aimed : zone);
        _cause = shooter;
        var result = Strike(args.Target, strike, limbs, ready);
        _cause = null;
        if (result.Ricochet)
        {
            limbs.LedgerZone = null;
            args = args with { Damage = new DamageSpecifier() };
        }
    }

    private void OnDeadLimbHit(EntityUid uid, LimbBodyComponent limbs, DamageModifyEvent args)
    {
        if (!LimbTuning.Enabled || limbs.LedgerSilence > 0 || limbs.ArmorZone is not { } zone || !LimbWoundRules.IsLimb(zone)
            || limbs.FireOriginTime != _timing.CurTime)
            return;

        EnsureZones(limbs);
        var index = LimbWoundRules.Index(zone);
        if ((uint) index >= (uint) limbs.Zones.Count || !LimbWoundRules.DeadLimb(zone, limbs.Zones[index]))
            return;

        var kept = new DamageSpecifier();
        foreach (var (type, value) in args.Damage.DamageDict)
        {
            if (value < 0)
                kept.DamageDict[type] = value;
        }

        args.Damage = kept;
    }

    private void OnArmorZone(Entity<LimbBodyComponent> ent, ref LimbArmorZoneEvent args)
    {
        if (!LimbTuning.Enabled)
            return;

        if (args.Data.Shooter is not { } shooter)
        {
            ent.Comp.ArmorZone = LimbZone.Torso;
            ent.Comp.FireOrigin = LimbZone.Torso;
            ent.Comp.FireOriginTime = _timing.CurTime;
            ent.Comp.StrikeOpen = true;
            return;
        }

        var aiming = TryComp<LimbAimComponent>(shooter, out var aim) && aim.Aiming && AimPrecise(shooter);
        var hit = args.Data.HitPosition?.Position ?? _transform.GetWorldPosition(ent);
        var from = _transform.GetWorldPosition(args.Data.Gun);
        var ready = _combat.IsInCombatMode(ent);
        var distance = (hit - from).Length();
        var aimPoint = hit;
        if (TryComp<GunComponent>(args.Data.Gun, out var gun) && gun.ShootCoordinates is { } coords)
            aimPoint = _transform.ToMapCoordinates(coords).Position;
        var aimed = ZoneForShot(aiming, aim, ent, from, hit, aimPoint);
        ent.Comp.ArmorZone = Present(ent.Comp, Place(aimed, distance, ready, Cover(ent, shooter, aimed).Slip));
        ent.Comp.FireOrigin = ent.Comp.ArmorZone;
        ent.Comp.FireOriginTime = _timing.CurTime;
        ent.Comp.StrikeOpen = true;
    }

    private void OnHitscan(Entity<HitscanBasicDamageComponent> ent, ref HitscanDamageDealtEvent args)
    {
        if (!LimbTuning.Enabled || HasComp<GodmodeComponent>(args.Target) || args.Data.Shooter is not { } shooter || !TryComp<LimbBodyComponent>(args.Target, out var limbs))
            return;

        var aiming = TryComp<LimbAimComponent>(shooter, out var aim) && aim.Aiming && AimPrecise(shooter);
        var hit = args.Data.HitPosition?.Position ?? _transform.GetWorldPosition(args.Target);
        var from = _transform.GetWorldPosition(args.Data.Gun);
        var ready = _combat.IsInCombatMode(args.Target);
        var distance = (hit - from).Length();
        var aimPoint = hit;
        if (TryComp<GunComponent>(args.Data.Gun, out var gun) && gun.ShootCoordinates is { } coords)
            aimPoint = _transform.ToMapCoordinates(coords).Position;
        var aimed = ZoneForShot(aiming, aim, args.Target, from, hit, aimPoint);
        var zone = limbs.FireOriginTime == _timing.CurTime && limbs.FireOrigin is { } rolled
            ? rolled
            : Present(limbs, Place(aimed, distance, ready, Cover(args.Target, shooter, aimed).Slip));
        limbs.ArmorZone = zone;
        var incoming = ent.Comp.IgnoreResistances ? new DamageSpecifier(ent.Comp.Damage) : Resist(args.Target, ent.Comp.Damage, shooter);
        limbs.ArmorZone = null;
        var worn = incoming;
        var born = Amount(ent.Comp.Damage, "Piercing");
        var armored = worn.GetTotal() + 0.05f < ent.Comp.Damage.GetTotal();
        var strike = BuildStrike(args.Target, shooter, zone, worn, projectile: true, distance, tool: ent.Owner, catalogPierce: born, throughArmor: armored);
        _cause = shooter;
        Strike(args.Target, strike, limbs, ready);
        _cause = null;
        var landed = LimbWoundRules.IsSmall(zone) ? LimbZone.Head : zone;
        if (!LimbWoundRules.IsBody(landed) || landed == LimbZone.Torso)
            return;

        EnsureZones(limbs);
        var zones = CollectionsMarshal.AsSpan(limbs.Zones);
        ref var fromZone = ref zones[LimbWoundRules.Index(LimbZone.Torso)];
        ref var toZone = ref zones[LimbWoundRules.Index(landed)];
        foreach (var (type, value) in args.DamageDealt.DamageDict)
        {
            if (value <= 0)
                continue;

            var amount = value.Float();
            Note(ref fromZone, type, -amount);
            Note(ref toZone, type, amount);
        }

        Dirty(args.Target, limbs);
    }

    private void OnZoneSet(LimbZoneSetEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        SetTarget(user, msg.Zone);
    }

    private void OnAimSet(LimbAimSetEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        SetAiming(user, msg.Aiming);
    }

    private void OnAimDown(ICommonSession? session)
    {
        if (!LimbTuning.Enabled || !_net.IsClient || session?.AttachedEntity is not { } user)
            return;

        if (HasComp<GhostComponent>(user) || !TryComp<LimbAimComponent>(user, out var aim))
            return;

        var next = !aim.Aiming;
        RaisePredictiveEvent(new LimbAimSetEvent(next));
        SetAiming(user, next);
    }

    private void OnAimUp(ICommonSession? session)
    {
    }

    private void OnSpecies(Entity<LimbBodyComponent> ent, ref MapInitEvent args)
    {
        FitPools(ent.Comp);
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var look))
        {
            Dirty(ent);
            return;
        }

        if (!_proto.TryIndex<LimbSpeciesPrototype>(look.Species, out var profile))
            return;

        ent.Comp.Present = LimbSpeciesRules.Mask(profile.Organs);
        ent.Comp.Vital = profile.Vital;
        ent.Comp.Wound = profile.Wound;
        ent.Comp.Burn = profile.Burn;
        ent.Comp.LiquidSever = profile.LiquidSever;
        ent.Comp.Soft = profile.Soft;
        ent.Comp.Plant = profile.Plant;
        ent.Comp.HeadVital = profile.HeadVital;
        ent.Comp.HasEars = profile.Ears;
        Dirty(ent);
        _implants.EnsureSlots(ent.Owner);
    }

    private void OnMobState(Entity<LimbBodyComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            if (ent.Comp.Died == null)
            {
                ent.Comp.Died = _timing.CurTime;
                ent.Comp.Decay = 0f;
            }
            if (!_net.IsClient)
                _standing.Down(ent.Owner, playSound: false, dropHeldItems: true, force: true);
            return;
        }

        ent.Comp.Died = null;
        ent.Comp.Decay = 0f;
        ent.Comp.LimbDeath = false;
        if (args.NewMobState == MobState.Alive)
            ent.Comp.LimbCrit = false;
    }

    private void OnLimbLife(Entity<LimbBodyComponent> ent, ref UpdateMobStateEvent args)
    {
        if (InStasis(ent.Owner))
        {
            args.State = MobState.Dead;
            return;
        }

        if (!LimbTuning.Enabled)
            return;

        EnsureZones(ent.Comp);
        var life = LifeOf(ent.Owner, ent.Comp);
        if (life == LimbLife.Dead)
        {
            if (args.State != MobState.Dead)
                ent.Comp.LimbDeath = true;
            args.State = MobState.Dead;
        }
        else if (life == LimbLife.Crit && args.State == MobState.Alive)
            args.State = MobState.Critical;
    }

    private bool InStasis(EntityUid uid)
    {
        if (!TryComp<ActionsComponent>(uid, out var actions))
            return false;

        foreach (var action in actions.Actions)
        {
            if (TryComp<RegenerativeStasisActionComponent>(action, out var stasis) && stasis.IsInStasis)
                return true;
        }

        return false;
    }

    private void OnRejuvenate(Entity<LimbBodyComponent> ent, ref RejuvenateEvent args)
    {
        ent.Comp.Zones = LimbZoneState.CreateAll();
        ent.Comp.Organs = LimbOrganRules.CreateAll();
        ent.Comp.Mouth = 0f;
        ent.Comp.Ears = 0f;
        ent.Comp.EarsGone = false;
        ent.Comp.EyesGone = 0;
        ent.Comp.EyeBurn = 0f;
        ent.Comp.LungWhistle = false;
        ent.Comp.BoneMend = 0f;
        ent.Comp.Died = null;
        ent.Comp.Decay = 0f;
        ent.Comp.LimbDeath = false;
        ent.Comp.LimbCrit = false;
        ent.Comp.PendingRefund = null;
        CopySpecies(ent);
        if (_net.IsServer)
        {
            RestoreParts(ent.Owner);
            ShowBody(ent.Owner);
            _blocker.UpdateCanMove(ent.Owner);
        }
        RemComp<LimbWhistleComponent>(ent);
        RemComp<LimbActiveComponent>(ent);
        Dirty(ent);
        if (_net.IsServer)
        {
            _mobs.UpdateMobState(ent.Owner);
            RefreshPosture(ent.Owner, ent.Comp);
        }
    }

    private void CopySpecies(Entity<LimbBodyComponent> ent)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var look))
            return;

        if (!_proto.TryIndex<LimbSpeciesPrototype>(look.Species, out var profile))
            return;

        ent.Comp.Present = LimbSpeciesRules.Mask(profile.Organs);
        ent.Comp.Vital = profile.Vital;
        ent.Comp.Wound = profile.Wound;
        ent.Comp.Burn = profile.Burn;
        ent.Comp.LiquidSever = profile.LiquidSever;
        ent.Comp.Soft = profile.Soft;
        ent.Comp.Plant = profile.Plant;
        ent.Comp.HeadVital = profile.HeadVital;
        ent.Comp.HasEars = profile.Ears;
    }

    private void RestoreParts(EntityUid body)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp) || bodyComp.Prototype is not { } protoId)
            return;

        if (!_proto.TryIndex(protoId, out BodyPrototype? prototype))
            return;

        var have = new Dictionary<string, EntityUid>();
        if (bodyComp.RootContainer.ContainedEntity is { } root)
            have[prototype.Root] = root;

        var frontier = new Queue<string>();
        var seen = new HashSet<string> { prototype.Root };
        frontier.Enqueue(prototype.Root);
        while (frontier.TryDequeue(out var slotId))
        {
            if (!prototype.Slots.TryGetValue(slotId, out var slot))
                continue;

            foreach (var next in slot.Connections)
            {
                if (!seen.Add(next) || !have.TryGetValue(slotId, out var parent))
                    continue;

                frontier.Enqueue(next);
                if (!prototype.Slots.TryGetValue(next, out var childSlot) || childSlot.Part is not { } partProto)
                    continue;

                var containerId = SharedBodySystem.GetPartSlotContainerId(next);
                if (_containers.TryGetContainer(parent, containerId, out var container) && container.ContainedEntities.Count > 0)
                {
                    have[next] = container.ContainedEntities[0];
                    continue;
                }

                var child = Spawn(partProto, new EntityCoordinates(parent, Vector2.Zero));
                if (!TryComp<BodyPartComponent>(child, out var childPart))
                {
                    QueueDel(child);
                    continue;
                }

                var attached = _containers.TryGetContainer(parent, containerId, out _)
                    ? _body.AttachPart(parent, next, child)
                    : _body.TryCreatePartSlotAndAttach(parent, next, child, childPart.PartType);
                if (!attached)
                {
                    QueueDel(child);
                    continue;
                }

                have[next] = child;
            }
        }
    }

    private bool Harvesting(EntityUid user, EntityUid target)
    {
        return TryComp<LimbHarvestingComponent>(user, out var harvesting) && harvesting.Target == GetNetEntity(target);
    }

    private bool TryHarvest(EntityUid user, Entity<LimbBodyComponent> target, EntityUid weapon)
    {
        if (_net.IsClient)
            return false;

        if (!TryComp<LimbTargetComponent>(user, out var selected) || selected.Selected is not { } zone)
            return false;

        if (weapon == user)
            return false;

        var damage = _melee.GetDamage(weapon, user);
        var extract = zone == LimbZone.Eyes;
        if (extract ? Amount(damage, "Piercing") <= 0f : Amount(damage, "Slash") <= 0f)
            return false;

        if (!extract && !LimbWoundRules.CanSever(zone))
            return false;

        if (!extract && !LimbWoundRules.IsBody(zone))
            return false;

        EnsureZones(target.Comp);
        if (!extract && target.Comp.Zones[LimbWoundRules.Index(zone)].Severed)
            return false;

        if (!extract && SeverProof(target, zone))
        {
            _popup.PopupEntity(Loc.GetString("limb-harvest-armored"), target, user);
            return true;
        }

        var cut = 1f;
        if (!extract && TryComp<LimbCutComponent>(weapon, out var blade))
            cut = blade.Factor;

        var slash = Amount(damage, "Slash");
        var pace = extract ? 1f : LimbWoundRules.HarvestPace(cut, slash);
        var delay = TimeSpan.FromSeconds(LimbWoundRules.HarvestSeconds(zone, pace));
        var doAfter = new DoAfterArgs(EntityManager, user, delay, new LimbHarvestDoAfterEvent(zone, extract), target, target, weapon)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BlockDuplicate = false,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };
        if (_doAfter.TryStartDoAfter(doAfter, out var id))
        {
            var harvesting = EnsureComp<LimbHarvestingComponent>(user);
            harvesting.Target = GetNetEntity(target.Owner);
            harvesting.Index = id!.Value.Index;
            harvesting.StartedTick = _timing.CurTick.Value;
            Dirty(user, harvesting);
            _audio.PlayPvs(ButcherSound, Transform(target).Coordinates);
            _popup.PopupEntity(Loc.GetString("limb-harvest-start",
                    ("user", Identity.Entity(user, EntityManager)),
                    ("target", Identity.Entity(target, EntityManager)),
                    ("zone", zone.ToString())),
                target,
                PopupType.LargeCaution);
        }

        return true;
    }

    private void OnHarvestSwing(Entity<LimbHarvestingComponent> ent, ref MeleeAttackEvent args)
    {
        if (ent.Comp.StartedTick == _timing.CurTick.Value)
            return;

        _doAfter.Cancel(ent.Owner, ent.Comp.Index);
    }

    private void OnHarvest(Entity<LimbBodyComponent> ent, ref LimbHarvestDoAfterEvent args)
    {
        if (args.Handled || _net.IsClient)
            return;

        if (TryComp<LimbHarvestingComponent>(args.User, out var harvesting) && harvesting.Target == GetNetEntity(ent.Owner))
            RemComp<LimbHarvestingComponent>(args.User);
        if (args.Cancelled)
            return;

        args.Handled = true;
        if (!_mobs.IsDead(ent.Owner))
            return;

        EnsureZones(ent.Comp);
        EnsureOrgans(ent.Comp);
        if (args.Extract)
        {
            if (args.Zone == LimbZone.Eyes)
                ent.Comp.EyesGone = 2;

            if (LimbOrganRules.TryTarget(args.Zone, LimbMark.Point, out var organ)
                && (ent.Comp.Present & (LimbOrganMask) (1 << (int) organ)) != 0)
            {
                var organState = ent.Comp.Organs[(int) organ];
                organState.Ruptured = true;
                organState.Damage = 100f;
                ent.Comp.Organs[(int) organ] = organState;
            }

            _adminLog.Add(LogType.Limb, LogImpact.Medium, $"{ToPrettyString(args.User):user} gouged {args.Zone} of the corpse {ToPrettyString(ent.Owner):target}");

            _audio.PlayPvs(HarvestSound, ent);
            Dirty(ent);
            return;
        }

        if (!LimbWoundRules.IsBody(args.Zone) || !LimbWoundRules.CanSever(args.Zone))
            return;

        ref var state = ref CollectionsMarshal.AsSpan(ent.Comp.Zones)[LimbWoundRules.Index(args.Zone)];
        if (state.Severed)
            return;

        var at = Transform(ent).Coordinates;
        _cause = args.User;
        DropZone(ent.Owner, ent.Comp, args.Zone);
        _cause = null;
        _audio.PlayPvs(ButcherSound, at);
        _audio.PlayPvs(SeverSound, at);
    }

    private void CoveredBlast(Entity<LimbBodyComponent> ent, DamageSpecifier damage, LimbZone zone, out float heat, out float pierce, out float blunt, out float slash, out float caustic, out float other)
    {
        ent.Comp.ArmorZone = zone;
        var worn = Resist(ent.Owner, damage, global: false);
        ent.Comp.ArmorZone = null;
        if (TryComp<DamageableComponent>(ent.Owner, out var damageable))
        {
            var kept = new DamageSpecifier();
            foreach (var (type, value) in worn.DamageDict)
            {
                if (value > 0 && damageable.Damage.DamageDict.ContainsKey(type))
                    kept.DamageDict[type] = value;
            }

            worn = kept;
        }

        heat = Amount(worn, "Heat");
        pierce = Amount(worn, "Piercing");
        blunt = Amount(worn, "Blunt");
        slash = Amount(worn, "Slash");
        caustic = Amount(worn, "Caustic");
        other = 0f;
        foreach (var (_, value) in worn.DamageDict)
        {
            if (value > 0)
                other += value.Float();
        }

        other = MathF.Max(0f, other - heat - pierce - blunt - slash - caustic);
    }

    private bool BlastBlocked(EntityUid uid, LimbZone zone)
    {
        var slots = _inventory.GetSlotEnumerator(uid, Worn);
        while (slots.NextItem(out var item))
        {
            if (!TryComp<LimbCoverComponent>(item, out var cover) || !(cover.Blast || cover.Sever) || !cover.Covers(zone))
                continue;

            return true;
        }

        return false;
    }

    public bool SeverProof(EntityUid uid, LimbZone zone)
    {
        var slots = _inventory.GetSlotEnumerator(uid, Worn);
        while (slots.NextItem(out var item))
        {
            if (TryComp<LimbCoverComponent>(item, out var cover) && cover.Sever && cover.Covers(zone))
                return true;
        }

        return false;
    }

    private void OnBlast(Entity<LimbBodyComponent> ent, ref BeforeExplodeEvent args)
    {
        if (!LimbTuning.Enabled || _net.IsClient || HasComp<GodmodeComponent>(ent))
            return;

        if (args.Damage.GetTotal() <= 0)
            return;

        EnsureZones(ent.Comp);
        var zones = CollectionsMarshal.AsSpan(ent.Comp.Zones);
        var count = Math.Min(zones.Length, LimbWoundRules.BodyZoneCount);
        var worn = new float[LimbWoundRules.BodyZoneCount];
        LostShares(ent.Comp, worn);
        var mech = new bool[LimbWoundRules.BodyZoneCount];
        var heat = new float[count];
        var pierce = new float[count];
        var blunt = new float[count];
        var slash = new float[count];
        var caustic = new float[count];
        var other = new float[count];
        var punch = 0f;
        for (var i = 0; i < count; i++)
        {
            if (zones[i].Severed)
                continue;

            CoveredBlast(ent, args.Damage, (LimbZone) i, out heat[i], out pierce[i], out blunt[i], out slash[i], out caustic[i], out other[i]);
            punch = MathF.Max(punch, heat[i] + pierce[i] + blunt[i] + slash[i] + caustic[i] + other[i]);
        }

        if (punch <= 0f)
            return;

        var wound = ent.Comp.Wound;
        var open = 0;
        for (var i = 0; i < zones.Length && i < LimbWoundRules.BodyZoneCount; i++)
        {
            if (!zones[i].Severed)
                open++;
        }

        var changed = false;
        var sever = new List<LimbZone>();
        if (open > 0)
        {
            var share = 1f / open;
            for (var i = 0; i < zones.Length && i < LimbWoundRules.BodyZoneCount; i++)
            {
                if (zones[i].Severed)
                    continue;

                if (LimbWoundRules.ApplyBlast(ref zones[i], heat[i] * ent.Comp.Burn * share, pierce[i] * wound * share, blunt[i] * wound * share, slash[i] * wound * share, ent.Comp.Soft))
                    changed = true;

                if (caustic[i] > 0f || other[i] > 0f)
                {
                    zones[i].Health = MathF.Max(0f, zones[i].Health - (caustic[i] + other[i]) * wound * share);
                    changed = true;
                }

                var zoneDamage = heat[i] + pierce[i] + blunt[i] + slash[i] + caustic[i] + other[i];
                mech[i] = pierce[i] > 0f || blunt[i] > 0f || slash[i] > 0f;
                if (LimbWoundRules.TryBlastSever(args.Distance, BlastBlocked(ent, (LimbZone) i), _random.NextFloat(), (LimbZone) i, zoneDamage))
                    sever.Add((LimbZone) i);
            }
        }

        _random.Shuffle(sever);
        var torn = 0;
        foreach (var zone in sever)
        {
            if (torn >= LimbWoundRules.BlastSeverMax)
                break;

            var index = (int) zone;
            if (index >= ent.Comp.Zones.Count || ent.Comp.Zones[index].Severed)
                continue;

            DropZone(ent.Owner, ent.Comp, zone);
            torn++;
            changed = true;
        }

        if (!changed)
            return;

        RuptureWorn(ent.Owner, ent.Comp, worn, mech);
        Dirty(ent);
        ApplyLife(ent.Owner, ent.Comp);
        RefreshActive(ent.Owner, ent.Comp);
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnSevered(Entity<LimbBodyComponent> ent, ref LimbSeveredEvent args)
    {
        SeverPart(ent, args.Zone);
        RefreshPosture(ent, ent.Comp);
    }

    private static void ClearOrgan(LimbBodyComponent limbs, LimbOrgan organ)
    {
        var index = (int) organ;
        if ((uint) index >= (uint) limbs.Organs.Count)
            return;

        var state = limbs.Organs[index];
        state.Ruptured = false;
        state.Damage = 0f;
        limbs.Organs[index] = state;
    }

    public void SeverPart(EntityUid body, LimbZone zone)
    {
        if (_net.IsClient || !TryComp<BodyComponent>(body, out var bodyComp))
            return;

        ReleaseBlade(body, zone);
        if (LimbWoundRules.TryCarried(zone, out var carried))
            ReleaseBlade(body, carried);

        var type = PartType(zone);
        if (type == null)
            return;

        var symmetry = Symmetry(zone);
        foreach (var part in _body.GetBodyChildrenOfType(body, type.Value, bodyComp))
        {
            if (symmetry != BodyPartSymmetry.None && part.Component.Symmetry != symmetry)
                continue;

            if (!_containers.TryGetContainingContainer((part.Id, null, null), out var container))
                continue;

            if (HasComp<LimbProsthesisComponent>(part.Id))
            {
                if (TryComp<HumanoidAppearanceComponent>(body, out var worn))
                    ClearGraftLook(body, worn, zone);
                HidePart(body, zone);
                _containers.Remove(part.Id, container, force: true);
                _transform.SetCoordinates(part.Id, Transform(body).Coordinates);
                return;
            }

            var bare = false;
            var eyesRemain = true;
            LimbZoneState? woundState = null;
            if (TryComp<LimbBodyComponent>(body, out var limbs))
            {
                EnsureZones(limbs);
                var zones = CollectionsMarshal.AsSpan(limbs.Zones);
                var index = LimbWoundRules.Index(zone);
                if (index < zones.Length)
                {
                    bare = zones[index].SkinBare;
                    woundState = zones[index];
                }

                eyesRemain = limbs.EyesGone < 2;
                if (zone == LimbZone.Head)
                {
                    EnsureOrgans(limbs);
                    ClearOrgan(limbs, LimbOrgan.Brain);
                    ClearOrgan(limbs, LimbOrgan.Eyes);
                }
            }

            var skin = Color.White;
            var eyes = Color.White;
            HumanoidAppearanceComponent? look = null;
            if (TryComp(body, out look))
            {
                skin = look.SkinColor;
                eyes = look.EyeColor;
            }

            var grafted = TryComp<LimbFleshComponent>(part.Id, out var donor) && donor.Species.Length > 0
                          && look != null && (donor.Species != look.Species.Id || donor.Skin != look.SkinColor);
            var flesh = EnsureComp<LimbFleshComponent>(part.Id);
            flesh.Source = body;
            flesh.Donor = Name(body);
            flesh.Bare = bare;
            flesh.Gelatin = HasComp<SurgeryMannequinComponent>(body);
            flesh.Eyes = eyes;
            flesh.EyeSprite = "";
            flesh.EyeState = "";
            if (!grafted)
            {
                flesh.Skin = skin;
                flesh.Species = look?.Species.Id ?? "";
                flesh.Dna = CompOrNull<DnaComponent>(body)?.DNA ?? "";
                flesh.Unstable = HasComp<UncloningComponent>(body);
                flesh.Wound = limbs?.Wound ?? 1f;
                flesh.Burn = limbs?.Burn ?? 1f;
                flesh.LiquidSever = limbs?.LiquidSever ?? 0f;
                flesh.DamageSet = "";
                if (TryComp<DamageableComponent>(body, out var damageable) && damageable.DamageModifierSetId is { } setId)
                    flesh.DamageSet = setId.Id;
                flesh.Marks.Clear();
                flesh.CarrySprites.Clear();
                flesh.CarryStates.Clear();
                if (look != null)
                    CopyCarried(flesh, look, zone);
                if (!bare && look != null)
                    CopyLook(flesh, look, zone, eyesRemain);
            }

            if (flesh.DamageSet.Length > 0)
                _damage.SetDamageModifierSetId(part.Id, flesh.DamageSet);

            if (look != null)
                ClearGraftLook(body, look, zone);

            Dirty(part.Id, flesh);

            if (woundState is { } ws)
            {
                var wound = EnsureComp<LimbPartWoundComponent>(part.Id);
                wound.Zone = zone;
                wound.State = ws;
                Dirty(part.Id, wound);

                if (TryComp<LimbWoundVisualsComponent>(body, out var bodyVis))
                {
                    var partVis = EnsureComp<LimbWoundVisualsComponent>(part.Id);
                    partVis.Thresholds = bodyVis.Thresholds;
                    partVis.States = bodyVis.States;
                    partVis.Groups = bodyVis.Groups;
                    Dirty(part.Id, partVis);
                }
            }

            var fresh = EnsureComp<LimbFreshComponent>(part.Id);
            if (limbs is { Died: not null })
            {
                fresh.Left = MathF.Max(0f, fresh.Total - limbs.Decay);
                fresh.Spent = fresh.Left <= 0f;
            }

            Dirty(part.Id, fresh);
            EnsureComp<TemperatureComponent>(part.Id);
            _implants.Keep(body, part.Id);
            HidePart(body, zone);

            if (limbs is { Soft: true })
                EnsureComp<SurgerySlimeDissolveComponent>(part.Id);

            _containers.Remove(part.Id, container, force: true);
            if (_dropAt is { } at)
            {
                _transform.SetCoordinates(part.Id, Transform(body).Coordinates);
                _transform.SetWorldPosition(part.Id, _transform.GetWorldPosition(body) + at);
                return;
            }

            _transform.SetCoordinates(part.Id, Transform(body).Coordinates);
            var fling = _random.NextAngle().ToVec() * _random.NextFloat(LimbWoundRules.FlingMin, LimbWoundRules.FlingMax);
            _throwing.TryThrow(part.Id, fling, LimbWoundRules.FlingSpeed, recoil: false, playSound: false);
            return;
        }

        _implants.DropLost(body, zone);
    }

    private void CopyCarried(LimbFleshComponent flesh, HumanoidAppearanceComponent look, LimbZone zone)
    {
        if (!LimbWoundRules.TryCarried(zone, out var child)
            || !_proto.TryIndex<SpeciesPrototype>(look.Species, out var species)
            || !_proto.TryIndex<HumanoidSpeciesBaseSpritesPrototype>(species.SpriteSet, out var sprites))
            return;

        var layers = new List<HumanoidVisualLayers>();
        LimbLookRules.Collect(child, layers);
        foreach (var layer in layers)
        {
            if (!sprites.Sprites.TryGetValue(layer, out var layerId)
                || !_proto.TryIndex<HumanoidSpeciesSpriteLayer>(layerId, out var sprite)
                || sprite.BaseSprite is not SpriteSpecifier.Rsi rsi)
                continue;

            flesh.CarrySprites.Add(rsi.RsiPath.ToString());
            flesh.CarryStates.Add(rsi.RsiState);
        }
    }

    private void CopyLook(LimbFleshComponent flesh, HumanoidAppearanceComponent look, LimbZone zone, bool eyesRemain)
    {
        var carried = LimbWoundRules.TryCarried(zone, out var child);
        foreach (var list in look.MarkingSet.Markings.Values)
        {
            foreach (var marking in list)
            {
                if (!marking.Visible || !_proto.TryIndex<MarkingPrototype>(marking.MarkingId, out var proto))
                    continue;

                if (!LimbLookRules.Fits(zone, proto.BodyPart, eyesRemain)
                    && !(carried && LimbLookRules.Fits(child, proto.BodyPart, eyesRemain)))
                    continue;

                flesh.Marks.Add(new Marking(marking));
            }
        }

        if (!eyesRemain || !LimbLookRules.Fits(zone, HumanoidVisualLayers.Eyes, true))
            return;

        if (!_proto.TryIndex<SpeciesPrototype>(look.Species, out var species)
            || !_proto.TryIndex<HumanoidSpeciesBaseSpritesPrototype>(species.SpriteSet, out var sprites)
            || !sprites.Sprites.TryGetValue(HumanoidVisualLayers.Eyes, out var eyeLayerId)
            || !_proto.TryIndex<HumanoidSpeciesSpriteLayer>(eyeLayerId, out var eyeLayer)
            || eyeLayer.BaseSprite is not SpriteSpecifier.Rsi rsi)
            return;

        flesh.EyeSprite = rsi.RsiPath.ToString();
        flesh.EyeState = rsi.RsiState;
    }

    private void HidePart(EntityUid body, LimbZone zone)
    {
        if (!TryComp<HumanoidAppearanceComponent>(body, out var look))
            return;

        var layers = new List<HumanoidVisualLayers>();
        LimbLookRules.Collect(zone, layers);
        foreach (var layer in layers)
            _humanoid.SetLayerVisibility((body, look), layer, false);
    }

    private void ShowBody(EntityUid body)
    {
        if (!TryComp<HumanoidAppearanceComponent>(body, out var look))
            return;

        foreach (var slot in LimbLookRules.Slots)
            _humanoid.SetLayerVisibility((body, look), slot.Layer, true);
    }

    private void ApplyOrgans(EntityUid uid, LimbBodyComponent limbs, LimbStrike strike, LimbStrikeResult result, bool ready)
    {
        if (!result.Applied && result.EyeDamage <= 0)
            return;

        EnsureOrgans(limbs);
        var taken = strike.Amount * strike.RoundOrgan * strike.WoundMultiplier;
        if (strike.RoundOrgan <= 0f)
            return;

        if (strike.ThroughArmor && taken < LimbWoundRules.HeavyPoint)
            return;

        var mark = LimbWoundRules.BehindArmorTrauma(strike) ? LimbMark.Blunt : strike.Mark;
        ApplyRibs(uid, strike, mark);
        if (strike.Zone is not (LimbZone.Eyes or LimbZone.Mouth)
            && LimbOrganRules.TryTarget(strike.Zone, mark, out var organ, strike.Projectile)
            && (limbs.Present & (LimbOrganMask) (1 << (int) organ)) != 0
            && _random.NextFloat() < LimbWoundRules.SlipChance(LimbOrganRules.OrganSize(organ), strike.Distance, ready))
            return;

        var organs = CollectionsMarshal.AsSpan(limbs.Organs);
        var brain = (int) LimbOrgan.Brain;
        var brainWas = (uint) brain < (uint) organs.Length && organs[brain].Ruptured;
        var hit = LimbOrganRules.Apply(organs, strike.Zone, mark, strike.Amount * strike.RoundOrgan, strike.WoundMultiplier, limbs.Present, strike.Projectile);
        if (!brainWas && (uint) brain < (uint) organs.Length && organs[brain].Ruptured)
        {
            var rupture = new LimbBrainRuptureEvent();
            RaiseLocalEvent(uid, ref rupture);
        }

        if (hit.EyeDamage > 0 && TryComp<BlindableComponent>(uid, out var blind))
            _blind.AdjustEyeDamage((uid, blind), hit.EyeDamage);

        if (!_net.IsServer || !hit.Hit)
            return;

        RunHooks(uid, strike, mark, hit);
    }

    private void ApplyRibs(EntityUid uid, LimbStrike strike, LimbMark mark)
    {
        var scaled = strike.Amount * strike.WoundMultiplier;
        foreach (var row in LimbCatalog.Rows)
        {
            if (row.Hook != LimbHooks.Rib || !LimbCatalog.Fits(row, strike.Zone, mark, strike.Projectile))
                continue;

            CrackRibs(uid, scaled * row.Factor, mark, strike.Projectile);
        }
    }

    private void RunHooks(EntityUid uid, LimbStrike strike, LimbMark mark, OrganHit hit)
    {
        var scaled = strike.Amount * strike.WoundMultiplier;
        foreach (var row in LimbCatalog.Rows)
        {
            if (!LimbCatalog.Fits(row, strike.Zone, mark, strike.Projectile) || row.Hook == LimbHooks.Organ || row.Hook == LimbHooks.Rib)
                continue;

            switch (row.Hook)
            {
                case LimbHooks.Concussion when hit.Concussion && !_net.IsClient && strike.WoundMultiplier > 0.99f && !strike.ThroughArmor && !Adrenaline(uid) && TryComp<LimbBodyComponent>(uid, out var drop) && drop.AllowDrop:
                    var heavy = scaled >= LimbOrganRules.ConcussionHit;
                    if (drop.DropHeavyOnly && mark == LimbMark.Blunt && !heavy)
                        break;

                    var concussion = new LimbConcussionEvent(heavy);
                    RaiseLocalEvent(uid, ref concussion);
                    break;
                case LimbHooks.Air when hit.Organ == LimbOrgan.Lungs:
                    var gasp = MathF.Min(row.Factor * LimbOrganRules.LungHitAir, scaled * LimbOrganRules.LungHitShare);
                    if (gasp <= 0f)
                        break;

                    var air = new DamageSpecifier();
                    air.DamageDict["Asphyxiation"] = FixedPoint2.New(gasp);
                    if (TryComp<LimbBodyComponent>(uid, out var lungs))
                    {
                        lungs.LedgerSilence++;
                        EnsureZones(lungs);
                        CollectionsMarshal.AsSpan(lungs.Zones)[LimbWoundRules.Index(LimbZone.Torso)].HitAir += gasp;
                    }

                    _damage.TryChangeDamage(uid, air, interruptsDoAfters: false);
                    if (lungs != null)
                        lungs.LedgerSilence--;
                    break;
                case LimbHooks.Blind when TryComp<BlindableComponent>(uid, out var shot):
                    _blind.AdjustEyeDamage((uid, shot), LimbOrganRules.EyeRuptureDamage);
                    break;
                case LimbHooks.Whistle:
                    MarkWhistle(uid);
                    break;
            }

            var ev = new LimbHookEvent(row.Hook, strike.Zone, mark, scaled * row.Factor, row.Organ);
            RaiseLocalEvent(uid, ref ev);
        }
    }

    private void LostShares(LimbBodyComponent limbs, float[] lost)
    {
        EnsureZones(limbs);
        var count = Math.Min(Math.Min(limbs.Zones.Count, lost.Length), LimbWoundRules.BodyZoneCount);
        for (var i = 0; i < count; i++)
        {
            var zone = (LimbZone) i;
            lost[i] = LimbWoundRules.LostShare(limbs.Zones[i], LimbWoundRules.HealthFor(zone, limbs));
        }
    }

    private bool RuptureWorn(EntityUid uid, LimbBodyComponent limbs, float[] before, bool[] mechanical)
    {
        EnsureZones(limbs);
        var zones = CollectionsMarshal.AsSpan(limbs.Zones);
        var count = Math.Min(Math.Min(zones.Length, before.Length), LimbWoundRules.BodyZoneCount);
        var any = false;
        for (var i = 0; i < count; i++)
        {
            if (zones[i].Severed || before[i] < LimbOrganRules.RuptureShare)
                continue;

            var zone = (LimbZone) i;
            var lost = LimbWoundRules.LostShare(zones[i], LimbWoundRules.HealthFor(zone, limbs));
            if (lost <= before[i] || i >= mechanical.Length || !mechanical[i])
                continue;

            any |= RuptureIf(uid, limbs, zone);
        }

        return any;
    }

    private bool RuptureIf(EntityUid uid, LimbBodyComponent limbs, LimbZone zone)
    {
        if (!LimbWoundRules.IsBody(zone))
            return false;

        EnsureOrgans(limbs);
        var organs = CollectionsMarshal.AsSpan(limbs.Organs);
        var brain = (int) LimbOrgan.Brain;
        var heart = (int) LimbOrgan.Heart;
        var brainWas = (uint) brain < (uint) organs.Length && organs[brain].Ruptured;
        var heartWas = (uint) heart < (uint) organs.Length && organs[heart].Ruptured;
        var eyes = LimbOrganRules.RuptureHome(organs, limbs.Present, zone, LimbOrganRules.RuptureChance, _random.NextFloat());
        var brainNow = (uint) brain < (uint) organs.Length && organs[brain].Ruptured;
        var heartNow = (uint) heart < (uint) organs.Length && organs[heart].Ruptured;
        if (eyes > 0 && TryComp<BlindableComponent>(uid, out var blind))
            _blind.AdjustEyeDamage((uid, blind), eyes);

        if (!brainWas && brainNow)
        {
            var rupture = new LimbBrainRuptureEvent();
            RaiseLocalEvent(uid, ref rupture);
        }

        return eyes > 0 || !brainWas && brainNow || !heartWas && heartNow;
    }

    private void FinishHit(EntityUid uid, LimbBodyComponent limbs, LimbStrike strike, LimbStrikeResult result)
    {
        if (strike.Zone != LimbZone.Mouth && !result.Applied && result.EyeDamage <= 0)
            return;

        NoteFace(uid, limbs, strike);

        if (!result.Applied && result.EyeDamage <= 0)
            return;

        if (LimbWoundRules.IsBody(strike.Zone))
        {
            ref var state = ref CollectionsMarshal.AsSpan(limbs.Zones)[LimbWoundRules.Index(strike.Zone)];
            var loss = (strike.Harm > 0f ? strike.Harm : strike.Amount) * strike.WoundMultiplier;
            var lost = LimbWoundRules.LostShare(state, LimbWoundRules.HealthFor(strike.Zone, limbs));
            if (LimbWoundRules.Spend(ref state, loss))
                RefreshPosture(uid, limbs);
            if (_net.IsServer && !limbs.Soft && !state.Severed)
                LimbWoundRules.RollCrack(ref state, lost, _random.NextFloat());
            if (_net.IsServer && !state.Severed
                && lost >= LimbOrganRules.RuptureShare
                && strike.Mark is LimbMark.Edge or LimbMark.Point or LimbMark.Blunt)
                RuptureIf(uid, limbs, strike.Zone);
            Dirty(uid, limbs);
        }

        if (!_net.IsServer)
            return;

        ApplyLife(uid, limbs);
        RefreshActive(uid, limbs);
    }

    private static float HeadHealth(LimbBodyComponent limbs)
    {
        return limbs.HeadVital ? limbs.Zones[LimbWoundRules.Index(LimbZone.Head)].Health : LimbWoundRules.HealthFor(LimbZone.Head, limbs);
    }

    public bool IsVital(EntityUid body, LimbOrgan organ)
    {
        if (TryComp<LimbBodyComponent>(body, out var limbs) && limbs.Vital != null)
            return limbs.Vital.Contains(organ);

        return _proto.TryIndex<SurgeryOrganPrototype>(organ.ToString(), out var proto) && proto.Vital;
    }

    private bool VitalGone(LimbBodyComponent limbs, bool headlessLing = false)
    {
        if (limbs.Vital != null)
        {
            foreach (var kind in limbs.Vital)
            {
                if (headlessLing && kind is LimbOrgan.Brain or LimbOrgan.Eyes)
                    continue;

                if (Gone(limbs, kind, (LimbOrganMask) (1 << (int) kind)))
                    return true;
            }

            return false;
        }

        foreach (var organ in _proto.EnumeratePrototypes<SurgeryOrganPrototype>())
        {
            if (!organ.Vital || !Enum.TryParse<LimbOrgan>(organ.ID, out var kind))
                continue;

            if (headlessLing && kind is LimbOrgan.Brain or LimbOrgan.Eyes)
                continue;

            if (Gone(limbs, kind, (LimbOrganMask) (1 << (int) kind)))
                return true;
        }

        return false;
    }

    private static bool Gone(LimbBodyComponent limbs, LimbOrgan organ, LimbOrganMask mask)
    {
        if ((limbs.Present & mask) == 0)
            return false;

        var index = (int) organ;
        return (uint) index < (uint) limbs.Organs.Count && limbs.Organs[index].Missing;
    }

    private void ReleaseOrganDeath(EntityUid uid, LimbBodyComponent limbs)
    {
        if (!_net.IsServer || VitalGone(limbs) || InStasis(uid))
            return;

        var head = HeadHealth(limbs);
        var torso = limbs.Zones[LimbWoundRules.Index(LimbZone.Torso)].Health;
        var life = LimbWoundRules.Life(head, torso);
        var state = life == LimbLife.Dead ? MobState.Dead : life == LimbLife.Crit ? MobState.Critical : MobState.Alive;
        if (state == MobState.Dead && HasComp<SurgeryMannequinComponent>(uid))
            state = MobState.Critical;
        _thresholds.SetCurrentThresholdState(uid, state);
        _mobs.ChangeMobState(uid, state);
    }

    private void ApplyLife(EntityUid uid, LimbBodyComponent limbs)
    {
        if (!_net.IsServer)
            return;

        var life = LifeOf(uid, limbs);
        if (life == LimbLife.Dead)
        {
            KillByLimbs(uid, limbs);
            return;
        }

        if (_mobs.IsDead(uid))
        {
            if (limbs.LimbDeath && !DeadByDamage(uid))
            {
                limbs.LimbDeath = false;
                _mobs.UpdateMobState(uid);
            }

            return;
        }

        if (life == LimbLife.Crit)
        {
            if (_mobs.IsAlive(uid))
                limbs.LimbCrit = true;
            _mobs.ChangeMobState(uid, MobState.Critical);
            return;
        }

        if (limbs.LimbCrit && _mobs.IsCritical(uid))
        {
            limbs.LimbCrit = false;
            _mobs.UpdateMobState(uid);
        }
    }

    private LimbLife LifeOf(EntityUid uid, LimbBodyComponent limbs)
    {
        var headlessLing = HasComp<ChangelingIdentityComponent>(uid)
                           && (limbs.Zones[LimbWoundRules.Index(LimbZone.Head)].Severed || !HasPart(uid, LimbZone.Head));
        var head = headlessLing ? limbs.HeadPool : HeadHealth(limbs);
        var torso = limbs.Zones[LimbWoundRules.Index(LimbZone.Torso)].Health;
        return VitalGone(limbs, headlessLing) ? LimbLife.Dead : LimbWoundRules.Life(head, torso);
    }

    public void KillByLimbs(EntityUid uid, LimbBodyComponent? limbs = null)
    {
        if (!Resolve(uid, ref limbs, false))
            return;

        if (_mobs.IsDead(uid))
            return;

        if (HasComp<SurgeryMannequinComponent>(uid))
        {
            _mobs.ChangeMobState(uid, MobState.Critical);
            return;
        }

        limbs.LimbDeath = true;
        _mobs.ChangeMobState(uid, MobState.Dead);
    }

    private bool DeadByDamage(EntityUid uid)
    {
        return TryComp<MobThresholdsComponent>(uid, out var thresholds)
            && TryComp<DamageableComponent>(uid, out var damage)
            && (thresholds.CurrentThresholdState == MobState.Dead && !thresholds.AllowRevives
                || _thresholds.TryGetThresholdForState(uid, MobState.Dead, out var dead, thresholds) && damage.TotalDamage >= dead.Value);
    }

    private void NoteFace(EntityUid uid, LimbBodyComponent limbs, LimbStrike strike)
    {
        if (strike.Zone == LimbZone.Mouth)
        {
            var gain = strike.Amount * strike.WoundMultiplier;
            if (strike.Mark == LimbMark.Liquid)
                gain *= 0.25f;

            limbs.Mouth += gain;
        }

        if (strike.Zone == LimbZone.Eyes)
        {
            if (strike.Mark is LimbMark.Edge or LimbMark.Point)
                limbs.EyesGone = (byte) Math.Min(2, limbs.EyesGone + 1);

            if (strike.Mark is LimbMark.Heat or LimbMark.Liquid)
                limbs.EyeBurn += strike.Amount * strike.WoundMultiplier;

            if (TryComp<BlindableComponent>(uid, out var blind))
            {
                var target = LimbWoundRules.Vision(limbs.EyesGone, limbs.EyeBurn);
                var delta = target - blind.EyeDamage;
                if (delta != 0)
                    _blind.AdjustEyeDamage((uid, blind), delta);
            }
        }

        Dirty(uid, limbs);
    }

    private void MarkWhistle(EntityUid uid)
    {
        if (!TryComp<LimbBodyComponent>(uid, out var limbs) || limbs.LungWhistle)
            return;

        limbs.LungWhistle = true;
        EnsureComp<LimbWhistleComponent>(uid);
        Dirty(uid, limbs);
    }

    private void CrackRibs(EntityUid uid, float amount, LimbMark mark, bool projectile)
    {
        if (amount <= 0f || !TryComp<LimbBodyComponent>(uid, out var limbs))
            return;

        EnsureZones(limbs);
        ref var torso = ref CollectionsMarshal.AsSpan(limbs.Zones)[LimbWoundRules.Index(LimbZone.Torso)];
        if (!limbs.Soft && !projectile && torso.Bone >= LimbWoundRules.BreakBone && torso.Flesh >= LimbWoundRules.StrainFlesh && mark is LimbMark.Point or LimbMark.Edge)
            MarkWhistle(uid);

        Dirty(uid, limbs);
    }

    private void OnPickup(EntityUid uid, LimbBodyComponent limbs, PickupAttemptEvent args)
    {
        if (!LimbTuning.Enabled)
            return;

        EnsureZones(limbs);
        var zones = CollectionsMarshal.AsSpan(limbs.Zones);
        var grasp = LimbWoundRules.CanGrasp(
            zones[LimbWoundRules.Index(LimbZone.ArmLeft)],
            zones[LimbWoundRules.Index(LimbZone.HandLeft)],
            zones[LimbWoundRules.Index(LimbZone.ArmRight)],
            zones[LimbWoundRules.Index(LimbZone.HandRight)]);
        if (grasp)
            return;

        args.Cancel();
        if (!args.ShowPopup || !_timing.IsFirstTimePredicted || _timing.CurTime < limbs.NextGraspPopup)
            return;

        limbs.NextGraspPopup = _timing.CurTime + TimeSpan.FromSeconds(2);
        _popup.PopupClient(Loc.GetString("limb-grasp-burn"), uid, args.User);
    }

    private void OnAccent(EntityUid uid, LimbBodyComponent limbs, AccentGetEvent args)
    {
        if (!LimbTuning.Enabled)
            return;

        args.Message = LimbWoundRules.Garble(args.Message, limbs.Mouth);
    }

    private static LimbZone Present(LimbBodyComponent limbs, LimbZone zone)
    {
        if (LimbWoundRules.IsSmall(zone))
            return Present(limbs, LimbZone.Head) == LimbZone.Head ? zone : Present(limbs, LimbZone.Torso);

        for (var i = 0; i < 4 && LimbWoundRules.IsBody(zone); i++)
        {
            var index = LimbWoundRules.Index(zone);
            if ((uint) index >= (uint) limbs.Zones.Count
                || !limbs.Zones[index].Severed && !LimbWoundRules.DeadLimb(zone, limbs.Zones[index]))
                return zone;

            zone = LimbWoundRules.SlipTo(zone);
        }

        return LimbWoundRules.IsBody(zone) ? zone : LimbZone.Torso;
    }

    private LimbZone Place(LimbZone zone, float distance, bool ready, float extra = 0f)
    {
        if (_random.NextFloat() < LimbWoundRules.SlipChance(LimbWoundRules.TargetSize(zone), distance, ready, extra))
            return LimbWoundRules.SlipTo(zone);

        return zone;
    }

    private void ApplyResult(EntityUid uid, LimbZone zone, LimbStrikeResult result)
    {
        if (result.EyeDamage > 0 && TryComp<BlindableComponent>(uid, out var blind))
            _blind.AdjustEyeDamage((uid, blind), result.EyeDamage);

        if (!result.DropItem || _net.IsClient || HasComp<WeaponRetentionComponent>(uid))
            return;

        var location = zone == LimbZone.HandLeft ? HandLocation.Left : HandLocation.Right;
        if (!TryComp<HandsComponent>(uid, out var hands))
            return;

        foreach (var (id, hand) in hands.Hands)
        {
            if (hand.Location != location || !_hands.TryGetHeldItem((uid, hands), id, out var held))
                continue;

            if (!HasComp<PreventForcedDropComponent>(held.Value))
                _hands.TryDrop((uid, hands), held.Value);
            return;
        }
    }

    private bool ApplyPelletSever(EntityUid uid, LimbBodyComponent limbs, LimbStrike strike)
    {
        if (_net.IsClient || !strike.Projectile)
            return false;

        var hit = strike.Aimed ? strike.Called : strike.Zone;
        if (!LimbWoundRules.PelletCounts(strike.Distance, strike.BehindArmor, strike.Pellet, strike.PelletSever))
            return false;

        if (_timing.CurTime > limbs.PelletUntil)
            limbs.PelletHits = 0;

        limbs.PelletHits++;
        limbs.PelletUntil = _timing.CurTime + TimeSpan.FromSeconds(LimbWoundRules.PelletWindow);
        if (!LimbWoundRules.TryPelletVolley(limbs.PelletHits, strike.Distance, strike.BehindArmor, strike.Pellet, strike.PelletSever, hit, strike.SecondaryRoll, out var zone))
            return false;

        limbs.PelletHits = 0;
        EnsureZones(limbs);
        ref var state = ref CollectionsMarshal.AsSpan(limbs.Zones)[LimbWoundRules.Index(zone)];
        if (state.Severed || SeverProof(uid, zone))
            return false;

        if (LimbWoundRules.StripsSkin(true, strike.Distance, state.Skin))
            state.SkinBare = true;

        DropZone(uid, limbs, zone);
        return true;
    }

    private void LogBone(EntityUid uid, LimbZone zone, float before, float after)
    {
        string? what = before < LimbWoundRules.BreakBone && after >= LimbWoundRules.BreakBone ? "fractured"
            : before < LimbWoundRules.StrainBone && after >= LimbWoundRules.StrainBone ? "cracked"
            : null;
        if (what == null)
            return;

        if (_cause is { } cause)
            _adminLog.Add(LogType.Limb, LogImpact.Medium, $"{ToPrettyString(cause):user} {what} the {zone} bone of {ToPrettyString(uid):target}");
        else
            _adminLog.Add(LogType.Limb, LogImpact.Medium, $"{ToPrettyString(uid):target} got a {what} {zone} bone");
    }

    private void DropZone(EntityUid uid, LimbBodyComponent limbs, LimbZone zone)
    {
        EnsureZones(limbs);
        var zones = CollectionsMarshal.AsSpan(limbs.Zones);
        var index = LimbWoundRules.Index(zone);
        if ((uint) index >= (uint) zones.Length)
            return;

        if (!zones[index].Severed)
            LimbWoundRules.MarkSevered(ref zones[index]);

        if (LimbWoundRules.TryCarried(zone, out var child))
        {
            var carried = LimbWoundRules.Index(child);
            if ((uint) carried < (uint) zones.Length && !zones[carried].Severed)
                LimbWoundRules.MarkSevered(ref zones[carried]);
        }

        Dirty(uid, limbs);
        if (_cause is { } cause)
            _adminLog.Add(LogType.Limb, LogImpact.High, $"{ToPrettyString(cause):user} severed {zone} of {ToPrettyString(uid):target}");
        else
            _adminLog.Add(LogType.Limb, LogImpact.High, $"{ToPrettyString(uid):target} lost {zone}");
        ReleaseGrip(uid, zone);
        var ev = new LimbSeveredEvent(zone);
        RaiseLocalEvent(uid, ref ev);
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    private LimbStrike BuildStrike(EntityUid target, EntityUid user, LimbZone zone, DamageSpecifier damage, bool projectile, float distance = 0f, EntityUid? tool = null, float? catalogPierce = null, bool throughArmor = false, LimbZone? called = null)
    {
        var slash = Amount(damage, "Slash");
        var landed = Amount(damage, "Piercing");
        var pierce = catalogPierce ?? landed;
        var blunt = Amount(damage, "Blunt");
        var heat = Amount(damage, "Heat");
        var caustic = Amount(damage, "Caustic");
        var mark = LimbWoundRules.MarkFromAmounts(slash, pierce, blunt, heat, caustic);
        var amount = mark switch
        {
            LimbMark.Edge => slash,
            LimbMark.Point => landed,
            LimbMark.Heat => heat,
            LimbMark.Liquid => caustic,
            _ => blunt,
        };
        var harm = slash + landed + blunt + heat + caustic;
        var liquid = 0f;
        var wound = 1f;
        var burn = 1f;
        if (PartRates(target, zone, out var partWound, out var partBurn, out var partLiquid))
        {
            wound = partWound;
            burn = partBurn;
            liquid = partLiquid;
        }
        else if (TryComp<LimbBodyComponent>(target, out var body))
        {
            wound = body.Wound;
            burn = body.Burn;
            liquid = body.LiquidSever;
        }

        amount *= mark == LimbMark.Heat ? burn : wound;
        harm *= wound;

        var cover = Cover(target, user, zone);
        var multiplier = throughArmor ? 1f : cover.WoundMultiplier;
        var aimed = projectile && TryComp<LimbAimComponent>(user, out var aim) && aim.Aiming && AimPrecise(user);
        var cut = 1f;
        var severChance = 1f;
        var sharp = false;
        var pellet = false;
        var pelletSever = false;
        var roundBone = 0f;
        var roundOrgan = 1f;
        var roundBleed = 0f;
        if (tool is { } held)
        {
            if (TryComp<LimbCutComponent>(held, out var cutComp))
            {
                cut = cutComp.Factor;
                sharp = cutComp.Sever && !SeverProof(target, zone);
                severChance = cutComp.SeverChance;
            }

            if (TryComp<LimbRoundComponent>(held, out var round))
            {
                pellet = round.Pellet;
                pelletSever = round.Sever;
                roundBone = round.Bone;
                roundOrgan = round.Organ;
                roundBleed = round.Bleed;
            }
        }

        return new LimbStrike(zone, mark, amount, _random.NextFloat(), pierce, projectile, cover.Face, cover.Grip, cover.Ricochet, cover.ShieldBlocked, multiplier, aimed, cover.BehindArmor, _random.NextFloat(), distance, _random.NextFloat(), cut, liquid, ThroughArmor: throughArmor, Called: called ?? zone, Sharp: sharp, Pellet: pellet, PelletSever: pelletSever, RoundBone: roundBone, RoundOrgan: roundOrgan, RoundBleed: roundBleed, SeverChance: severChance, Harm: harm);
    }

    private LimbCover Cover(EntityUid target, EntityUid attacker, LimbZone zone)
    {
        var face = false;
        var ricochet = false;
        var grip = false;
        var multiplier = 1f;
        var shield = false;
        var behindArmor = false;
        var slip = 0f;
        var fromFront = FromFront(target, attacker);

        void Take(LimbCoverComponent comp, bool blocking)
        {
            if (comp.OnlyWhenBlocking && (!blocking || !fromFront))
                return;

            if (comp.Face)
                face = true;

            if (comp.Grip && zone is LimbZone.HandLeft or LimbZone.HandRight)
                grip = true;

            if (comp.Zones.Contains(zone) || comp.Face && zone is LimbZone.Eyes or LimbZone.Mouth or LimbZone.Head)
            {
                multiplier *= comp.WoundMultiplier;
                if (comp.Ricochet)
                    ricochet = true;

                if (comp.OnlyWhenBlocking)
                    shield = true;

                if (comp.BluntBackface)
                    behindArmor = true;

                slip += comp.Slip;
            }
        }

        var slots = _inventory.GetSlotEnumerator(target, Worn);
        while (slots.NextItem(out var item))
        {
            if (TryComp<LimbCoverComponent>(item, out var comp))
                Take(comp, false);
        }

        if (TryComp<HandsComponent>(target, out var hands))
        {
            foreach (var held in _hands.EnumerateHeld((target, hands)))
            {
                if (!TryComp<LimbCoverComponent>(held, out var comp))
                    continue;

                var blocking = TryComp<BlockingComponent>(held, out var block) && block.IsBlocking;
                Take(comp, blocking);
            }
        }

        return new LimbCover(face, ricochet, grip, shield, multiplier, behindArmor, slip);
    }

    private int MissingLegs(LimbBodyComponent limbs)
    {
        EnsureZones(limbs);
        var zones = CollectionsMarshal.AsSpan(limbs.Zones);
        var missing = 0;
        if (LimbWoundRules.Useless(zones[LimbWoundRules.Index(LimbZone.LegLeft)]) || LimbWoundRules.Useless(zones[LimbWoundRules.Index(LimbZone.FootLeft)]))
            missing++;
        if (LimbWoundRules.Useless(zones[LimbWoundRules.Index(LimbZone.LegRight)]) || LimbWoundRules.Useless(zones[LimbWoundRules.Index(LimbZone.FootRight)]))
            missing++;

        return missing;
    }

    private void OnCanMove(Entity<LimbBodyComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (LimbTuning.Enabled && LimbWoundRules.Limbless(ent.Comp.Zones))
            args.Cancel();
    }

    public bool Limbless(EntityUid uid)
    {
        return LimbTuning.Enabled && TryComp<LimbBodyComponent>(uid, out var limbs) && LimbWoundRules.Limbless(limbs.Zones);
    }

    private void RefreshPosture(EntityUid uid, LimbBodyComponent limbs)
    {
        if (!LimbTuning.Enabled || _net.IsClient)
            return;

        _blocker.UpdateCanMove(uid);

        if (MissingLegs(limbs) < 2)
        {
            _body.UpdateMovementSpeed(uid);
            _movement.RefreshMovementSpeedModifiers(uid);
            return;
        }

        _standing.Down(uid, playSound: false, dropHeldItems: false, force: true);
        if (!TryComp<BodyComponent>(uid, out var body) || body.LegEntities.Count > 0)
            _movement.ChangeBaseSpeed(uid, LimbTuning.CrawlSpeed, LimbTuning.CrawlSpeed, LimbTuning.CrawlAcceleration);

        _movement.RefreshMovementSpeedModifiers(uid);
    }

    private void ReleaseGrip(EntityUid uid, LimbZone zone)
    {
        if (_net.IsClient || !TryComp<HandsComponent>(uid, out var hands))
            return;

        var left = zone is LimbZone.ArmLeft or LimbZone.HandLeft;
        var right = zone is LimbZone.ArmRight or LimbZone.HandRight;
        if (!left && !right)
            return;

        foreach (var (id, hand) in hands.Hands)
        {
            var side = left && hand.Location == HandLocation.Left || right && hand.Location == HandLocation.Right;
            if (!side || !_hands.TryGetHeldItem((uid, hands), id, out var held))
                continue;

            _hands.TryDrop((uid, hands), held.Value);
        }

        foreach (var (id, _) in hands.Hands)
        {
            if (!_hands.TryGetHeldItem((uid, hands), id, out var held))
                continue;

            if (!TryComp<WieldableComponent>(held, out var wield) || !wield.Wielded)
                continue;

            _hands.TryDrop((uid, hands), held.Value);
        }
    }

    private bool[] FireBlocked(EntityUid uid)
    {
        Array.Clear(_fireBlocked);
        var blocked = _fireBlocked;
        var slots = _inventory.GetSlotEnumerator(uid, Worn);
        while (slots.NextItem(out var item))
        {
            if (!TryComp<LimbCoverComponent>(item, out var comp) || !comp.StopsFire)
                continue;

            foreach (var zone in comp.Zones)
            {
                if (LimbWoundRules.IsBody(zone))
                    blocked[LimbWoundRules.Index(zone)] = true;
            }
        }

        return blocked;
    }

    private static float HealAmount(DamageSpecifier damage)
    {
        var total = 0f;
        foreach (var value in damage.DamageDict.Values)
        {
            if (value < 0)
                total -= value.Float();
        }

        return total;
    }

    public bool MendedNow(EntityUid body)
    {
        return TryComp<LimbBodyComponent>(body, out var limbs) && limbs.MendedAt == _timing.CurTime;
    }

    public bool CanMend(EntityUid body, DamageSpecifier heal)
    {
        if (!LimbTuning.Enabled || HealAmount(heal) <= 0f || !TryComp<LimbBodyComponent>(body, out var limbs))
            return false;

        EnsureZones(limbs);
        var burns = Heals(heal, "Heat") || Heals(heal, "Caustic");
        var count = Math.Min(limbs.Zones.Count, LimbWoundRules.BodyZoneCount);
        for (var i = 0; i < count; i++)
        {
            var zone = (LimbZone) i;
            var state = limbs.Zones[i];
            if (state.Severed)
                continue;

            if (burns && state.Skin > 0f)
                return true;

            if (state.Atrophied && !LimbWoundRules.Revives(zone))
                continue;

            if (state.Health < LimbWoundRules.HealthFor(zone, limbs) - 0.5f)
                return true;
        }

        return false;
    }

    private bool RestoreZones(LimbBodyComponent limbs)
    {
        EnsureZones(limbs);
        var zones = CollectionsMarshal.AsSpan(limbs.Zones);
        var count = Math.Min(zones.Length, LimbWoundRules.BodyZoneCount);
        var changed = false;
        for (var i = 0; i < count; i++)
        {
            var zone = (LimbZone) i;
            var pool = LimbWoundRules.HealthFor(zone, limbs);
            if (zones[i].Health >= pool && !zones[i].Atrophied
                || zones[i].Health <= 0f && !LimbWoundRules.Revives(zone))
                continue;

            var before = zones[i].Health;
            LimbWoundRules.Mend(ref zones[i], pool, pool, LimbWoundRules.Revives(zone));
            changed |= zones[i].Health != before;
        }

        return changed;
    }

    private static void ApplySmallToHead(Span<LimbZoneState> zones, LimbStrike strike)
    {
        ref var head = ref zones[LimbWoundRules.Index(LimbZone.Head)];
        var amount = strike.Amount * strike.WoundMultiplier;
        if (strike.Mark is LimbMark.Heat or LimbMark.Liquid)
            head.Skin += amount;
        else if (strike.Zone == LimbZone.Mouth && strike.Mark is LimbMark.Edge or LimbMark.Point)
            head.Flesh += amount * 0.5f;
    }

    private LimbZone ZoneForShot(bool aiming, LimbAimComponent? aim, EntityUid target, Vector2 from, Vector2 hit, Vector2 aimPoint)
    {
        var origin = _transform.GetWorldPosition(target);
        var local = LimbAim.Body(LimbAim.Landing(from, hit, aimPoint, origin), _transform.GetWorldRotation(target).GetCardinalDir());
        var natural = LimbWoundRules.ZoneFromHit(local.X, local.Y);
        if (!aiming || aim == null)
            return natural;

        return LimbWoundRules.Pick(true, aim.CalledZone, natural);
    }

    private bool AimPrecise(EntityUid uid)
    {
        if (!TryComp<InputMoverComponent>(uid, out var mover))
            return true;

        return LimbWoundRules.AimIsPrecise(mover.HasDirectionalMovement, mover.Sprinting);
    }

    private void OnHealthExamined(Entity<LimbBodyComponent> ent, ref HealthBeingExaminedEvent args)
    {
        if (!LimbTuning.Enabled)
            return;

        LimbExamine.Write(args.Message, ent.Comp, Machine(ent.Owner), Adrenaline(ent.Owner));
    }

    private bool Adrenaline(EntityUid uid)
    {
        return _status.HasEffectComp<IgnoreSlowOnDamageComponent>(uid)
               || _status.HasEffectComp<PainNumbnessStatusEffectComponent>(uid);
    }

    private bool FromFront(EntityUid target, EntityUid user)
    {
        var delta = _transform.GetWorldPosition(user) - _transform.GetWorldPosition(target);
        if (delta.LengthSquared() < 0.0001f)
            return true;

        var facing = _transform.GetWorldRotation(target).ToWorldVec();
        return Vector2.Dot(facing, Vector2.Normalize(delta)) > 0f;
    }

    private EntityUid? Holder(EntityUid item)
    {
        var parent = Transform(item).ParentUid;
        while (parent.IsValid())
        {
            if (HasComp<LimbAimComponent>(parent))
                return parent;

            parent = Transform(parent).ParentUid;
        }

        return null;
    }

    private void RefreshHeldGuns(EntityUid user)
    {
        if (!TryComp<HandsComponent>(user, out var hands))
            return;

        foreach (var held in _hands.EnumerateHeld((user, hands)))
        {
            if (HasComp<GunComponent>(held))
                _guns.RefreshModifiers(held);
        }
    }

    private static bool AnyFire(LimbBodyComponent limbs)
    {
        foreach (var zone in limbs.Zones)
        {
            if (zone.Fire > 0f)
                return true;
        }

        return false;
    }

    private static bool Heals(DamageSpecifier damage, string type)
    {
        return damage.DamageDict.TryGetValue(type, out var value) && value < 0;
    }

    private static float Amount(DamageSpecifier damage, string type)
    {
        return damage.DamageDict.TryGetValue(type, out var value) ? value.Float() : 0f;
    }

    private static void FitPools(LimbBodyComponent limbs)
    {
        EnsureZones(limbs);
        for (var i = 0; i < limbs.Zones.Count && i < LimbWoundRules.BodyZoneCount; i++)
        {
            var zone = (LimbZone) i;
            var state = limbs.Zones[i];
            var plain = LimbWoundRules.HealthFor(zone);
            var pool = LimbWoundRules.HealthFor(zone, limbs);
            if (MathF.Abs(state.Health - plain) > 0.01f || MathF.Abs(plain - pool) <= 0.01f)
                continue;

            state.Health = pool;
            limbs.Zones[i] = state;
        }
    }

    private static void EnsureZones(LimbBodyComponent limbs)
    {
        while (limbs.Zones.Count < LimbWoundRules.BodyZoneCount)
        {
            var zone = (LimbZone) limbs.Zones.Count;
            limbs.Zones.Add(new LimbZoneState { Health = LimbWoundRules.HealthFor(zone, limbs) });
        }
    }

    private static void EnsureOrgans(LimbBodyComponent limbs)
    {
        while (limbs.Organs.Count < LimbOrganRules.Count)
            limbs.Organs.Add(new LimbOrganState());
    }

    private bool PartRates(EntityUid body, LimbZone zone, out float wound, out float burn, out float liquid)
    {
        wound = 1f;
        burn = 1f;
        liquid = 0f;
        var type = PartType(zone);
        if (type == null || !TryComp<BodyComponent>(body, out var bodyComp))
            return false;

        var symmetry = Symmetry(zone);
        foreach (var part in _body.GetBodyChildrenOfType(body, type.Value, bodyComp))
        {
            if (symmetry != BodyPartSymmetry.None && part.Component.Symmetry != symmetry)
                continue;

            if (!TryComp<LimbFleshComponent>(part.Id, out var flesh) || flesh.Species.Length == 0)
                return false;

            wound = flesh.Wound;
            burn = flesh.Burn;
            liquid = flesh.LiquidSever;
            return true;
        }

        return false;
    }

    private static BodyPartType? PartType(LimbZone zone)
    {
        return zone switch
        {
            LimbZone.Head => BodyPartType.Head,
            LimbZone.ArmLeft or LimbZone.ArmRight => BodyPartType.Arm,
            LimbZone.HandLeft or LimbZone.HandRight => BodyPartType.Hand,
            LimbZone.LegLeft or LimbZone.LegRight => BodyPartType.Leg,
            LimbZone.FootLeft or LimbZone.FootRight => BodyPartType.Foot,
            _ => null,
        };
    }

    private static BodyPartSymmetry Symmetry(LimbZone zone)
    {
        return zone switch
        {
            LimbZone.ArmLeft or LimbZone.HandLeft or LimbZone.LegLeft or LimbZone.FootLeft => BodyPartSymmetry.Left,
            LimbZone.ArmRight or LimbZone.HandRight or LimbZone.LegRight or LimbZone.FootRight => BodyPartSymmetry.Right,
            _ => BodyPartSymmetry.None,
        };
    }
}

public readonly record struct LimbCover(bool Face, bool Ricochet, bool Grip, bool ShieldBlocked, float WoundMultiplier, bool BehindArmor, float Slip = 0f);

[ByRefEvent]
public record struct LimbArmorZoneEvent(HitscanRaycastFiredData Data);

[Serializable, NetSerializable]
public sealed class LimbAimSetEvent : EntityEventArgs
{
    public bool Aiming;

    public LimbAimSetEvent(bool aiming)
    {
        Aiming = aiming;
    }
}

[Serializable, NetSerializable]
public sealed class LimbZoneSetEvent : EntityEventArgs
{
    public LimbZone? Zone;

    public LimbZoneSetEvent(LimbZone? zone)
    {
        Zone = zone;
    }
}
