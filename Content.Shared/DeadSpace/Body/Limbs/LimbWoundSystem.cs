// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Prototypes;
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
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
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Input;
using Content.Shared.Inventory;
using System.Runtime.InteropServices;
using Content.Shared.Medical;
using Content.Shared.Medical.Cryogenics;
using Content.Shared.Medical.Healing;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Speech;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
using Content.Shared.Temperature.Components;
using Content.Shared.Traits.Assorted;
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

public sealed class LimbWoundSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly BlindableSystem _blind = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
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

    private readonly Dictionary<string, int> _fields = new();
    private readonly bool[] _fireBlocked = new bool[LimbWoundRules.BodyZoneCount];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LimbBodyComponent, MapInitEvent>(OnSpecies);
        SubscribeLocalEvent<LimbBodyComponent, MobStateChangedEvent>(OnMobState);
        SubscribeLocalEvent<LimbBodyComponent, UpdateMobStateEvent>(OnLimbLife, after: [typeof(MobThresholdSystem)]);
        SubscribeLocalEvent<LimbBodyComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<LimbBodyComponent, LimbHarvestDoAfterEvent>(OnHarvest);
        SubscribeLocalEvent<LimbBodyComponent, BeforeExplodeEvent>(OnBlast);
        SubscribeLocalEvent<LimbBodyComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<LimbBodyComponent, RefreshMovementSpeedModifiersEvent>(OnMove);
        SubscribeLocalEvent<LimbBodyComponent, StandAttemptEvent>(OnStand);
        SubscribeLocalEvent<LimbBodyComponent, StoodEvent>(OnStood);
        SubscribeLocalEvent<LimbBodyComponent, HealingDoAfterEvent>(OnHeal, before: [typeof(HealingSystem)]);
        SubscribeLocalEvent<LimbBodyComponent, BeforeStaminaDamageEvent>(OnStamina);
        SubscribeLocalEvent<LimbBodyComponent, IgnitedEvent>(OnIgnited);
        SubscribeLocalEvent<LimbBodyComponent, ExtinguishedEvent>(OnExtinguished);
        SubscribeLocalEvent<LimbBodyComponent, LimbSeveredEvent>(OnSevered);
        SubscribeLocalEvent<LimbBodyComponent, AttackedEvent>(OnMelee);
        SubscribeLocalEvent<LimbBodyComponent, PickupAttemptEvent>(OnPickup);
        SubscribeLocalEvent<LimbBodyComponent, LimbArmorZoneEvent>(OnArmorZone);
        SubscribeLocalEvent<LimbBodyComponent, BeforeMeleeDamageEvent>(OnBeforeMelee, after: [typeof(ParrySystem)]);
        SubscribeLocalEvent<MeleeWeaponComponent, GetMeleeAttackRateEvent>(OnMeleeRate);
        SubscribeLocalEvent<GunComponent, GunRefreshModifiersEvent>(OnGunModifiers);
        SubscribeLocalEvent<ProjectileComponent, ProjectileHitEvent>(OnProjectile);
        SubscribeLocalEvent<HitscanBasicDamageComponent, HitscanDamageDealtEvent>(OnHitscan);
        SubscribeAllEvent<LimbAimSetEvent>(OnAimSet);
        SubscribeAllEvent<LimbZoneSetEvent>(OnZoneSet);

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.AimShot, InputCmdHandler.FromDelegate(OnAimDown, OnAimUp))
            .Register<LimbWoundSystem>();

        Subs.CVar(_cfg, CCVars.LimbEnabled, value => LimbTuning.Enabled = value, true);
        Subs.CVar(_cfg, CCVars.LimbHealthExtremity, value => LimbTuning.ExtremityHealth = value, true);
        Subs.CVar(_cfg, CCVars.LimbHealthLimb, value => LimbTuning.LimbHealth = value, true);
        Subs.CVar(_cfg, CCVars.LimbHealthHead, value => LimbTuning.HeadHealth = value, true);
        Subs.CVar(_cfg, CCVars.LimbHealthTorso, value => LimbTuning.TorsoHealth = value, true);
        Subs.CVar(_cfg, CCVars.LimbHealthGroin, value => LimbTuning.GroinHealth = value, true);
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

            LimbWoundRules.SpreadFire(zones, blocked);
            BurnClothing(uid, zones);
            Dirty(uid, limbs);
        }

        var bleed = BleedTotal(uid, limbs);
        if (LimbWoundRules.DrainBleed(zones))
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
                var loss = bleed * LimbWoundRules.BleedPerFlesh + failure.Bloodloss;
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
            _damage.ChangeDamage(uid, spec);
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
            _damage.ChangeDamage(uid, heat);
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
        if (!Resolve(uid, ref target))
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
        if (!Resolve(uid, ref aim))
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
        if (!LimbTuning.Enabled || !Resolve(uid, ref limbs))
            return LimbStrikeResult.Miss();

        EnsureZones(limbs);
        var span = CollectionsMarshal.AsSpan(limbs.Zones);
        if (LimbWoundRules.IsSmall(strike.Zone))
        {
            var dummy = span[LimbWoundRules.Index(LimbZone.Head)];
            var small = LimbWoundRules.Apply(ref dummy, strike);
            if (small.Applied)
                ApplySmallToHead(span, strike);
            ApplyOrgans(uid, limbs, strike, small, ready);
            ApplyResult(uid, strike.Zone, small);
            FinishHit(uid, limbs, strike, small);
            return small;
        }

        ref var state = ref span[LimbWoundRules.Index(strike.Zone)];
        var result = LimbWoundRules.Apply(ref state, strike);
        ApplyOrgans(uid, limbs, strike, result, ready);
        var pelletSevered = ApplyPelletSever(uid, limbs, strike);
        Dirty(uid, limbs);
        ApplyResult(uid, strike.Zone, result);
        FinishHit(uid, limbs, strike, result);
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

    public float BleedTotal(EntityUid uid, LimbBodyComponent? limbs = null)
    {
        if (!Resolve(uid, ref limbs, false))
            return 0f;

        EnsureZones(limbs);
        var total = 0f;
        foreach (var zone in CollectionsMarshal.AsSpan(limbs.Zones))
            total += zone.Bleed;

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

    private void OnStood(EntityUid uid, LimbBodyComponent limbs, StoodEvent args)
    {
        if (!LimbTuning.Enabled || _net.IsClient || MissingLegs(limbs) < 2)
            return;

        _standing.Down(uid, playSound: false, dropHeldItems: false, force: true);
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
            LimbWoundRules.Ointment(ref state, LimbWoundRules.OintmentDose);

        LimbWoundRules.Mend(ref state, HealAmount(healing.Damage), LimbWoundRules.HealthFor(zone));

        Dirty(ent);
        _movement.RefreshMovementSpeedModifiers(ent);
        RefreshActive(ent, ent.Comp);
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
        if (!LimbTuning.Enabled || !TryComp<MeleeWeaponComponent>(args.Used, out var melee))
            return;

        var zone = LimbZone.Torso;
        if (TryComp<LimbTargetComponent>(args.User, out var picked) && picked.Selected is { } selected)
            zone = selected;

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
        Strike(uid, strike, limbs, ready);
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
        if (limbs.LedgerZone is { } picked && LimbWoundRules.IsBody(picked))
        {
            aimedHeal = true;
            zone = picked;
        }
        limbs.LedgerZone = null;
        var fromStrike = limbs.StrikeOpen;
        limbs.StrikeOpen = false;
        var changed = false;
        if (!args.DamageIncreased && !aimedHeal)
        {
            changed = SpreadHeal(zones, args.DamageDelta);
        }
        else if (!fromStrike && args.DamageIncreased && HeatOf(args.DamageDelta) > 0f && AnyFire(limbs))
        {
            LimbWoundRules.Scorch(zones, HeatOf(args.DamageDelta));
            changed = true;
            ref var state = ref zones[LimbWoundRules.Index(zone)];
            foreach (var (type, value) in args.DamageDelta.DamageDict)
            {
                if (type == "Heat" || value <= 0)
                    continue;

                Note(ref state, type, value.Float());
            }
        }
        else
        {
            ref var state = ref zones[LimbWoundRules.Index(zone)];
            foreach (var (type, value) in args.DamageDelta.DamageDict)
            {
                if (value == 0)
                    continue;

                if (args.DamageIncreased && value < 0)
                    continue;

                if (!args.DamageIncreased && value > 0)
                    continue;

                Note(ref state, type, value.Float());
                changed = true;
            }

            if (!args.DamageIncreased)
                ClampHits(ref state);
        }

        if (changed)
            Dirty(uid, limbs);
    }

    private static float HeatOf(DamageSpecifier delta)
    {
        return delta.DamageDict.TryGetValue("Heat", out var value) && value > 0 ? value.Float() : 0f;
    }

    private bool SpreadHeal(Span<LimbZoneState> zones, DamageSpecifier delta)
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
        }

        return changed;
    }

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
            field = slot == 0 ? 6 : 5;
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

        if (_mobs.IsDead(ent.Owner) && _combat.IsInCombatMode(args.Attacker) && TryHarvest(args.Attacker, ent, args.Weapon))
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
        if (!LimbTuning.Enabled || !TryComp<LimbBodyComponent>(args.Target, out var limbs))
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
        var zone = Place(aimed, distance, ready, slip);
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
        var result = Strike(args.Target, strike, limbs, ready);
        if (result.Ricochet)
        {
            limbs.LedgerZone = null;
            args = args with { Damage = new DamageSpecifier() };
        }
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
        ent.Comp.ArmorZone = Place(aimed, distance, ready, Cover(ent, shooter, aimed).Slip);
        ent.Comp.FireOrigin = ent.Comp.ArmorZone;
        ent.Comp.FireOriginTime = _timing.CurTime;
        ent.Comp.StrikeOpen = true;
    }

    private void OnHitscan(Entity<HitscanBasicDamageComponent> ent, ref HitscanDamageDealtEvent args)
    {
        if (!LimbTuning.Enabled || args.Data.Shooter is not { } shooter || !TryComp<LimbBodyComponent>(args.Target, out var limbs))
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
        var zone = Place(aimed, distance, ready, Cover(args.Target, shooter, aimed).Slip);
        limbs.ArmorZone = zone;
        var incoming = ent.Comp.IgnoreResistances ? new DamageSpecifier(ent.Comp.Damage) : Resist(args.Target, ent.Comp.Damage, shooter);
        limbs.ArmorZone = null;
        var worn = incoming;
        var born = Amount(ent.Comp.Damage, "Piercing");
        var armored = worn.GetTotal() + 0.05f < ent.Comp.Damage.GetTotal();
        var strike = BuildStrike(args.Target, shooter, zone, worn, projectile: true, distance, tool: ent.Owner, catalogPierce: born, throughArmor: armored);
        Strike(args.Target, strike, limbs, ready);
        if (!LimbWoundRules.IsBody(zone) || zone == LimbZone.Torso)
            return;

        EnsureZones(limbs);
        var zones = CollectionsMarshal.AsSpan(limbs.Zones);
        ref var fromZone = ref zones[LimbWoundRules.Index(LimbZone.Torso)];
        ref var toZone = ref zones[LimbWoundRules.Index(zone)];
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

        if (!TryComp<LimbAimComponent>(user, out var aim))
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
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var look))
            return;

        if (!_proto.TryIndex<LimbSpeciesPrototype>(look.Species, out var profile))
            return;

        ent.Comp.Present = LimbSpeciesRules.Mask(profile.Organs);
        ent.Comp.Wound = profile.Wound;
        ent.Comp.LiquidSever = profile.LiquidSever;
        Dirty(ent);
    }

    private void OnMobState(Entity<LimbBodyComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            if (ent.Comp.Died == null)
                ent.Comp.Died = _timing.CurTime;
            return;
        }

        ent.Comp.Died = null;
    }

    private void OnLimbLife(Entity<LimbBodyComponent> ent, ref UpdateMobStateEvent args)
    {
        if (!LimbTuning.Enabled)
            return;

        EnsureZones(ent.Comp);
        var life = LimbWoundRules.Life(
            ent.Comp.Zones[LimbWoundRules.Index(LimbZone.Head)].Health,
            ent.Comp.Zones[LimbWoundRules.Index(LimbZone.Torso)].Health);
        if (life == LimbLife.Dead)
            args.State = MobState.Dead;
        else if (life == LimbLife.Crit && args.State == MobState.Alive)
            args.State = MobState.Critical;
    }

    private void OnRejuvenate(Entity<LimbBodyComponent> ent, ref RejuvenateEvent args)
    {
        ent.Comp.Zones = LimbZoneState.CreateAll();
        ent.Comp.Organs = LimbOrganRules.CreateAll();
        ent.Comp.Mouth = 0f;
        ent.Comp.EyesGone = 0;
        ent.Comp.EyeBurn = 0f;
        ent.Comp.LungWhistle = false;
        ent.Comp.Died = null;
        ent.Comp.PendingRefund = null;
        CopySpecies(ent);
        if (_net.IsServer)
        {
            RestoreParts(ent.Owner);
            ShowBody(ent.Owner);
        }
        RemComp<LimbWhistleComponent>(ent);
        RemComp<LimbActiveComponent>(ent);
        Dirty(ent);
    }

    private void CopySpecies(Entity<LimbBodyComponent> ent)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var look))
            return;

        if (!_proto.TryIndex<LimbSpeciesPrototype>(look.Species, out var profile))
            return;

        ent.Comp.Present = LimbSpeciesRules.Mask(profile.Organs);
        ent.Comp.Wound = profile.Wound;
        ent.Comp.LiquidSever = profile.LiquidSever;
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

    private bool TryHarvest(EntityUid user, Entity<LimbBodyComponent> target, EntityUid weapon)
    {
        if (_net.IsClient)
            return false;

        if (!TryComp<LimbTargetComponent>(user, out var selected) || selected.Selected is not { } zone)
            return false;

        var damage = _melee.GetDamage(weapon, user);
        var mark = LimbWoundRules.MarkFromAmounts(
            Amount(damage, "Slash"),
            Amount(damage, "Piercing"),
            Amount(damage, "Blunt"),
            Amount(damage, "Heat"),
            Amount(damage, "Caustic"));
        var extract = mark == LimbMark.Point || zone is LimbZone.Eyes or LimbZone.Mouth;
        if (!extract && !LimbWoundRules.CanSever(zone))
        {
            var key = zone == LimbZone.Groin ? "limb-harvest-groin" : "limb-harvest-torso";
            _popup.PopupEntity(Loc.GetString(key), target, user);
            return true;
        }

        if (!extract && !LimbWoundRules.IsBody(zone))
            return false;

        EnsureZones(target.Comp);
        if (!extract && target.Comp.Zones[LimbWoundRules.Index(zone)].Severed)
        {
            _popup.PopupEntity(Loc.GetString("limb-harvest-gone"), target, user);
            return true;
        }

        var cut = 1f;
        if (!extract && TryComp<LimbCutComponent>(weapon, out var blade))
            cut = blade.Factor;

        var slash = Amount(damage, "Slash");
        var pace = extract ? 1f : cut * MathF.Max(1f, slash / 6f);
        var delay = TimeSpan.FromSeconds(LimbWoundRules.HarvestSeconds(zone, pace));
        var doAfter = new DoAfterArgs(EntityManager, user, delay, new LimbHarvestDoAfterEvent(zone, extract), target, target, weapon)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BlockDuplicate = true,
        };
        return _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnHarvest(Entity<LimbBodyComponent> ent, ref LimbHarvestDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || _net.IsClient)
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

            Dirty(ent);
            return;
        }

        if (!LimbWoundRules.IsBody(args.Zone) || !LimbWoundRules.CanSever(args.Zone))
            return;

        ref var state = ref CollectionsMarshal.AsSpan(ent.Comp.Zones)[LimbWoundRules.Index(args.Zone)];
        if (state.Severed)
            return;

        DropZone(ent.Owner, ent.Comp, args.Zone);
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
        var slots = _inventory.GetSlotEnumerator(uid);
        while (slots.NextItem(out var item))
        {
            if (!TryComp<LimbCoverComponent>(item, out var cover) || !cover.Blast || !cover.Covers(zone))
                continue;

            return true;
        }

        return false;
    }

    private void OnBlast(Entity<LimbBodyComponent> ent, ref BeforeExplodeEvent args)
    {
        if (!LimbTuning.Enabled || _net.IsClient)
            return;

        if (args.Damage.GetTotal() <= 0)
            return;

        EnsureZones(ent.Comp);
        var zones = CollectionsMarshal.AsSpan(ent.Comp.Zones);
        var count = Math.Min(zones.Length, LimbWoundRules.BodyZoneCount);
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
        if (open > 0)
        {
            var share = 1f / open;
            for (var i = 0; i < zones.Length && i < LimbWoundRules.BodyZoneCount; i++)
            {
                if (zones[i].Severed)
                    continue;

                if (LimbWoundRules.ApplyBlast(ref zones[i], heat[i] * wound * share, pierce[i] * wound * share, blunt[i] * wound * share, slash[i] * wound * share))
                    changed = true;

                if (caustic[i] > 0f || other[i] > 0f)
                {
                    zones[i].Health = MathF.Max(0f, zones[i].Health - (caustic[i] + other[i]) * wound * share);
                    changed = true;
                }

                if (!LimbWoundRules.TryBlastSever(args.Distance, BlastBlocked(ent, (LimbZone) i), _random.NextFloat(), (LimbZone) i))
                    continue;

                DropZone(ent.Owner, ent.Comp, (LimbZone) i);
                zones = CollectionsMarshal.AsSpan(ent.Comp.Zones);
                changed = true;
            }
        }

        if (!changed)
            return;

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

            var flesh = EnsureComp<LimbFleshComponent>(part.Id);
            flesh.Bare = bare;
            flesh.Skin = skin;
            flesh.Eyes = eyes;
            flesh.Marks.Clear();
            flesh.EyeSprite = "";
            flesh.EyeState = "";
            if (!bare && look != null)
                CopyLook(flesh, look, zone, eyesRemain);

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
            if (limbs != null && limbs.Died is { } died)
            {
                var gone = (float) (_timing.CurTime - died).TotalSeconds;
                fresh.Left = MathF.Max(0f, fresh.Total - gone);
                fresh.Spent = fresh.Left <= 0f;
            }

            Dirty(part.Id, fresh);
            EnsureComp<TemperatureComponent>(part.Id);
            HidePart(body, zone);

            _containers.Remove(part.Id, container, force: true);
            return;
        }
    }

    private void CopyLook(LimbFleshComponent flesh, HumanoidAppearanceComponent look, LimbZone zone, bool eyesRemain)
    {
        foreach (var list in look.MarkingSet.Markings.Values)
        {
            foreach (var marking in list)
            {
                if (!marking.Visible || !_proto.TryIndex<MarkingPrototype>(marking.MarkingId, out var proto))
                    continue;

                if (!LimbLookRules.Fits(zone, proto.BodyPart, eyesRemain))
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

        var mark = strike.BehindArmor && strike.Mark == LimbMark.Point ? LimbMark.Blunt : strike.Mark;
        ApplyRibs(uid, strike, mark);
        if (strike.Zone is not (LimbZone.Eyes or LimbZone.Mouth)
            && LimbOrganRules.TryTarget(strike.Zone, mark, out var organ)
            && (limbs.Present & (LimbOrganMask) (1 << (int) organ)) != 0
            && _random.NextFloat() < LimbWoundRules.SlipChance(LimbOrganRules.OrganSize(organ), strike.Distance, ready))
            return;

        var organs = CollectionsMarshal.AsSpan(limbs.Organs);
        var brain = (int) LimbOrgan.Brain;
        var brainWas = (uint) brain < (uint) organs.Length && organs[brain].Ruptured;
        var hit = LimbOrganRules.Apply(organs, strike.Zone, mark, strike.Amount * strike.RoundOrgan, strike.WoundMultiplier, limbs.Present);
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

            CrackRibs(uid, scaled * row.Factor, mark);
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
                case LimbHooks.Crit when _mobs.IsAlive(uid) && strike.WoundMultiplier > 0.99f && !strike.ThroughArmor:
                    _mobs.ChangeMobState(uid, MobState.Critical);
                    break;
                case LimbHooks.Air:
                    var air = new DamageSpecifier();
                    air.DamageDict["Asphyxiation"] = FixedPoint2.New(row.Factor * LimbOrganRules.LungHitAir);
                    if (TryComp<LimbBodyComponent>(uid, out var lungs))
                    {
                        lungs.LedgerSilence++;
                        EnsureZones(lungs);
                        CollectionsMarshal.AsSpan(lungs.Zones)[LimbWoundRules.Index(LimbZone.Torso)].HitAir += row.Factor * LimbOrganRules.LungHitAir;
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
            if (LimbWoundRules.Spend(ref state, loss))
                RefreshPosture(uid, limbs);
            Dirty(uid, limbs);
        }

        if (!_net.IsServer)
            return;

        ApplyLife(uid, limbs);
        RefreshActive(uid, limbs);
    }

    private void ApplyLife(EntityUid uid, LimbBodyComponent limbs)
    {
        if (!_net.IsServer)
            return;

        var head = limbs.Zones[LimbWoundRules.Index(LimbZone.Head)].Health;
        var torso = limbs.Zones[LimbWoundRules.Index(LimbZone.Torso)].Health;
        switch (LimbWoundRules.Life(head, torso))
        {
            case LimbLife.Dead:
                _mobs.ChangeMobState(uid, MobState.Dead);
                break;
            case LimbLife.Crit:
                _mobs.ChangeMobState(uid, MobState.Critical);
                break;
        }
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

    private void CrackRibs(EntityUid uid, float amount, LimbMark mark)
    {
        if (amount <= 0f || !TryComp<LimbBodyComponent>(uid, out var limbs))
            return;

        EnsureZones(limbs);
        ref var torso = ref CollectionsMarshal.AsSpan(limbs.Zones)[LimbWoundRules.Index(LimbZone.Torso)];
        torso.Bone += amount;
        if (torso.Bone >= LimbWoundRules.BreakBone && torso.Flesh >= LimbWoundRules.StrainFlesh && mark is LimbMark.Point or LimbMark.Edge)
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
        if (!args.ShowPopup)
            return;

        _popup.PopupEntity(Loc.GetString("limb-grasp-burn"), uid, args.User);
    }

    private void OnAccent(EntityUid uid, LimbBodyComponent limbs, AccentGetEvent args)
    {
        if (!LimbTuning.Enabled)
            return;

        args.Message = LimbWoundRules.Garble(args.Message, limbs.Mouth);
    }

    private LimbZone Place(LimbZone zone, float distance, bool ready, float extra = 0f)
    {
        if (_random.NextFloat() < LimbWoundRules.SlipChance(LimbWoundRules.TargetSize(zone), distance, ready, extra))
            return LimbWoundRules.SlipTo(zone);

        return zone;
    }

    private static DamageSpecifier Invert(DamageSpecifier damage)
    {
        var heal = new DamageSpecifier();
        foreach (var (type, value) in damage.DamageDict)
            heal.DamageDict[type] = -value;

        return heal;
    }

    private void ApplyResult(EntityUid uid, LimbZone zone, LimbStrikeResult result)
    {
        if (result.EyeDamage > 0 && TryComp<BlindableComponent>(uid, out var blind))
            _blind.AdjustEyeDamage((uid, blind), result.EyeDamage);

        if (!result.DropItem)
            return;

        var location = zone == LimbZone.HandLeft ? HandLocation.Left : HandLocation.Right;
        if (!TryComp<HandsComponent>(uid, out var hands))
            return;

        foreach (var (id, hand) in hands.Hands)
        {
            if (hand.Location != location || !_hands.TryGetHeldItem((uid, hands), id, out var held))
                continue;

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
        if (state.Severed)
            return false;

        if (LimbWoundRules.StripsSkin(true, strike.Distance, state.Skin))
            state.SkinBare = true;

        DropZone(uid, limbs, zone);
        return true;
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
        ReleaseGrip(uid, zone);
        var ev = new LimbSeveredEvent(zone);
        RaiseLocalEvent(uid, ref ev);
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    private LimbStrike BuildStrike(EntityUid target, EntityUid user, LimbZone zone, DamageSpecifier damage, bool projectile, float distance = 0f, EntityUid? tool = null, float? catalogPierce = null, bool throughArmor = false, LimbZone? called = null)
    {
        var slash = Amount(damage, "Slash");
        var pierce = catalogPierce ?? Amount(damage, "Piercing");
        var blunt = Amount(damage, "Blunt");
        var heat = Amount(damage, "Heat");
        var caustic = Amount(damage, "Caustic");
        var mark = LimbWoundRules.MarkFromAmounts(slash, pierce, blunt, heat, caustic);
        var amount = mark switch
        {
            LimbMark.Edge => slash,
            LimbMark.Point => pierce,
            LimbMark.Heat => heat,
            LimbMark.Liquid => caustic,
            _ => blunt,
        };
        var harm = slash + pierce + blunt + heat + caustic;
        var liquid = 0f;
        if (TryComp<LimbBodyComponent>(target, out var body))
        {
            amount *= body.Wound;
            harm *= body.Wound;
            liquid = body.LiquidSever;
        }

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
                sharp = cutComp.Sever;
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

        var slots = _inventory.GetSlotEnumerator(target);
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

    private void RefreshPosture(EntityUid uid, LimbBodyComponent limbs)
    {
        if (!LimbTuning.Enabled || _net.IsClient)
            return;

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

    private void BurnClothing(EntityUid uid, Span<LimbZoneState> zones)
    {
        if (_net.IsClient)
            return;

        var count = Math.Min(zones.Length, LimbWoundRules.BodyZoneCount);
        var doomed = new List<EntityUid>();
        for (var i = 0; i < count; i++)
        {
            ref var state = ref zones[i];
            if (state.Fire <= 0f || state.Severed)
            {
                state.Cloth = 0f;
                continue;
            }

            state.Cloth += LimbActiveComponent.Period;
            if (state.Cloth < LimbWoundRules.ClothingBurn)
                continue;

            state.Cloth = 0f;
            var zone = (LimbZone) i;
            var slots = _inventory.GetSlotEnumerator(uid);
            while (slots.NextItem(out var item))
            {
                if (!TryComp<LimbCoverComponent>(item, out var cover) || cover.StopsFire || !cover.Covers(zone))
                    continue;

                if (!doomed.Contains(item))
                    doomed.Add(item);
            }
        }

        foreach (var item in doomed)
            QueueDel(item);
    }

    private bool[] FireBlocked(EntityUid uid)
    {
        Array.Clear(_fireBlocked);
        var blocked = _fireBlocked;
        var slots = _inventory.GetSlotEnumerator(uid);
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

    private static void ApplySmallToHead(Span<LimbZoneState> zones, LimbStrike strike)
    {
        ref var head = ref zones[LimbWoundRules.Index(LimbZone.Head)];
        var amount = strike.Amount * strike.WoundMultiplier;
        if (strike.Mark is LimbMark.Heat or LimbMark.Liquid)
            head.Skin += amount;
        else if (strike.Zone == LimbZone.Mouth && strike.Mark == LimbMark.Blunt)
            head.Bone += amount * 0.5f;
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

    private static void EnsureZones(LimbBodyComponent limbs)
    {
        while (limbs.Zones.Count < LimbWoundRules.BodyZoneCount)
        {
            var zone = (LimbZone) limbs.Zones.Count;
            limbs.Zones.Add(new LimbZoneState { Health = LimbWoundRules.HealthFor(zone) });
        }
    }

    private static void EnsureOrgans(LimbBodyComponent limbs)
    {
        while (limbs.Organs.Count < LimbOrganRules.Count)
            limbs.Organs.Add(new LimbOrganState());
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
