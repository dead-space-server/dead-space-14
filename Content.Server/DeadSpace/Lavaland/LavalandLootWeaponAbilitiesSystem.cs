// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Blink;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Server.Gatherable;
using Content.Server.Gatherable.Components;
using Content.Server.Mind;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Weapons.Melee;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Components;
using Content.Shared.Throwing;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Wall;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Active abilities of lavaland loot weapons: Cain and Abel dagger throws, the possessed soulscythe
/// and concussive gauntlets mining rock on contact.
/// </summary>
public sealed class LavalandLootWeaponAbilitiesSystem : EntitySystem
{
    [Dependency] private readonly Content.Shared.Actions.SharedActionsSystem _actions = default!;
    [Dependency] private readonly BlinkSystem _blink = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GatherableSystem _gatherable = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly LavalandLootWeaponsSystem _weapons = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly Robust.Shared.Containers.SharedContainerSystem _containers = default!;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandCainAndAbelComponent, LavalandCainAndAbelSwapActionEvent>(OnCainAbelSwap);
        SubscribeLocalEvent<LavalandCainAndAbelComponent, ItemWieldedEvent>(OnCainAbelWielded, after: [typeof(SharedWieldableSystem)]);
        SubscribeLocalEvent<LavalandCainAndAbelComponent, ItemUnwieldedEvent>(OnCainAbelUnwielded, after: [typeof(SharedWieldableSystem)]);
        SubscribeLocalEvent<LavalandCainAndAbelComponent, GetItemActionsEvent>(OnCainAbelGetActions);
        SubscribeLocalEvent<LavalandCainAndAbelComponent, LavalandCainAndAbelThrowActionEvent>(OnCainAbelThrow);
        SubscribeLocalEvent<LavalandCainAndAbelDaggerComponent, ProjectileHitEvent>(OnDaggerHit);

        SubscribeLocalEvent<LavalandSoulscytheComponent, UseInHandEvent>(OnScytheUse);
        SubscribeLocalEvent<LavalandSoulscytheComponent, MindAddedMessage>(OnScytheMindAdded);
        SubscribeLocalEvent<LavalandSoulscytheComponent, MeleeHitEvent>(OnScytheHit);
        SubscribeLocalEvent<LavalandSoulscytheComponent, RefreshMovementSpeedModifiersEvent>(OnScytheSpeed);
        SubscribeLocalEvent<LavalandSoulscytheComponent, InteractHandEvent>(OnScytheGrabbed);
        SubscribeLocalEvent<LavalandSoulscytheComponent, ExaminedEvent>(OnScytheExamined);
        SubscribeLocalEvent<LavalandSoulscytheComponent, AttackAttemptEvent>(OnScytheAttackAttempt);
        SubscribeLocalEvent<LavalandBossComponent, DamageModifyEvent>(OnBossDamageModify);
        SubscribeLocalEvent<LavalandEliteComponent, DamageModifyEvent>(OnEliteDamageModify);

        SubscribeLocalEvent<LavalandConcussiveGauntletsComponent, GotEquippedEvent>(OnGauntletsEquipped);
        SubscribeLocalEvent<LavalandConcussiveGauntletsComponent, GotUnequippedEvent>(OnGauntletsUnequipped);
        SubscribeLocalEvent<LavalandBumpMinerComponent, StartCollideEvent>(OnBumpMinerCollide);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var scythes = EntityQueryEnumerator<LavalandSoulscytheComponent, TransformComponent>();
        while (scythes.MoveNext(out var uid, out var scythe, out var scytheXform))
            UpdateScytheMovement((uid, scythe), scytheXform);

        var crystals = EntityQueryEnumerator<LavalandDaggerCrystalComponent, TransformComponent>();
        while (crystals.MoveNext(out _, out var crystal, out var xform))
        {
            _mobs.Clear();
            _lookup.GetEntitiesInRange(xform.Coordinates, crystal.Radius, _mobs);
            foreach (var mob in _mobs)
            {
                if (mob.Owner == crystal.Thrower || _mobState.IsDead(mob, mob.Comp) || !crystal.Hit.Add(mob))
                    continue;

                var large = _weapons.IsLavalandCreature(mob);
                _damageable.TryChangeDamage(mob.Owner, large ? crystal.LargeDamage : crystal.SmallDamage,
                    origin: crystal.Thrower);
                _stun.TryUpdateParalyzeDuration(mob, crystal.Stun);
            }
        }
    }

    #region Cain and Abel throws

    private const string CainAbelRsi = "/Textures/_DeadSpace/Lavaland/Objects/Weapons/cain_and_abel.rsi";

    /// <summary>The other dagger to the fore: Cain for the dash to the dagger, Abel for the ring of ice.</summary>
    private void OnCainAbelSwap(Entity<LavalandCainAndAbelComponent> ent, ref LavalandCainAndAbelSwapActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        ent.Comp.LaunchMode = !ent.Comp.LaunchMode;
        _popup.PopupEntity(Loc.GetString(ent.Comp.LaunchMode
            ? "lavaland-cain-abel-mode-launch"
            : "lavaland-cain-abel-mode-crystals"), args.Performer, args.Performer);
        UpdateCainAbelLook(ent);
    }

    /// <summary>
    /// Cain strikes fast, as twin blades on chains do, each blow lighter; Abel as the daggers were made. The swing
    /// shown is the blade in front.
    /// </summary>
    private void UpdateCainAbelBlows(Entity<LavalandCainAndAbelComponent> ent)
    {
        if (!TryComp<MeleeWeaponComponent>(ent, out var melee))
            return;

        // The first time: Abel's own, as the prototype gave them.
        if (ent.Comp.AbelDamage == null)
        {
            ent.Comp.AbelAttackRate = melee.AttackRate;
            ent.Comp.AbelDamage = new DamageSpecifier(melee.Damage);
        }

        var cain = ent.Comp.LaunchMode;
        melee.AttackRate = cain ? ent.Comp.CainAttackRate : ent.Comp.AbelAttackRate;
        melee.Damage = cain ? ent.Comp.AbelDamage * ent.Comp.CainDamageMultiplier : new DamageSpecifier(ent.Comp.AbelDamage);
        melee.Animation = cain ? ent.Comp.CainArc : ent.Comp.AbelArc;
        melee.WideAnimation = cain ? ent.Comp.CainWideArc : ent.Comp.AbelWideArc;
        Dirty(ent, melee);
        ent.Comp.CainHits = 0;
    }

    // Wielding sets its own prefix: the right one for the dagger in front goes over it.
    private void OnCainAbelWielded(Entity<LavalandCainAndAbelComponent> ent, ref ItemWieldedEvent args)
    {
        UpdateCainAbelLook(ent);
    }

    private void OnCainAbelUnwielded(Entity<LavalandCainAndAbelComponent> ent, ref ItemUnwieldedEvent args)
    {
        UpdateCainAbelLook(ent);
    }

    /// <summary>
    /// Cain (red) for the dash to the dagger, Abel (blue) for the ring of ice: in one hand or both, on the floor, on
    /// the actions.
    /// </summary>
    private void UpdateCainAbelLook(Entity<LavalandCainAndAbelComponent> ent)
    {
        var cain = ent.Comp.LaunchMode;
        UpdateCainAbelBlows(ent);
        var wielded = TryComp<WieldableComponent>(ent, out var wieldable) && wieldable.Wielded;
        _appearance.SetData(ent, LavalandCainAndAbelVisuals.Cain, cain);
        _item.SetHeldPrefix(ent, (cain, wielded) switch
        {
            (true, true) => "cain-wielded",
            (true, false) => "cain",
            (false, true) => "wielded",
            _ => null,
        });

        var icon = new SpriteSpecifier.Rsi(new ResPath(CainAbelRsi), cain ? "icon-cain" : "icon");
        if (ent.Comp.ThrowActionEntity is { } action)
            _actions.SetIcon(action, icon);
        if (ent.Comp.SwapActionEntity is { } swap)
            _actions.SetIcon(swap, icon);
    }

    private void OnCainAbelGetActions(Entity<LavalandCainAndAbelComponent> ent, ref GetItemActionsEvent args)
    {
        if (!args.InHands)
            return;

        args.AddAction(ref ent.Comp.ThrowActionEntity, ent.Comp.ThrowAction);
        args.AddAction(ref ent.Comp.SwapActionEntity, ent.Comp.SwapAction);
        UpdateCainAbelLook(ent);
    }

    private void OnCainAbelThrow(Entity<LavalandCainAndAbelComponent> ent, ref LavalandCainAndAbelThrowActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        var now = _timing.CurTime;
        if (now < ent.Comp.NextThrow)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-cain-abel-not-ready"), args.Performer, args.Performer);
            return;
        }

        var user = args.Performer;
        var origin = _transform.GetMapCoordinates(user);
        var target = _transform.ToMapCoordinates(args.Target);
        var direction = target.Position - origin.Position;
        if (target.MapId != origin.MapId || direction.LengthSquared() < 0.01f)
            return;

        var cooldown = ent.Comp.LaunchMode ? ent.Comp.LaunchCooldown : ent.Comp.CrystalsCooldown;
        ent.Comp.NextThrow = now + cooldown;
        // The action button shows the same recharge.
        _actions.SetCooldown(ent.Comp.ThrowActionEntity, now, now + cooldown);
        var dagger = Spawn(ent.Comp.DaggerProjectile, Transform(user).Coordinates);
        var daggerComp = EnsureComp<LavalandCainAndAbelDaggerComponent>(dagger);
        daggerComp.Weapon = ent.Owner;
        daggerComp.Thrower = user;
        daggerComp.LaunchMode = ent.Comp.LaunchMode;
        _gun.ShootProjectile(dagger, Vector2.Normalize(direction), Vector2.Zero, ent.Owner, user, ent.Comp.DaggerSpeed);
    }

    private void OnDaggerHit(Entity<LavalandCainAndAbelDaggerComponent> ent, ref ProjectileHitEvent args)
    {
        if (ent.Comp.Triggered || !TryComp<LavalandCainAndAbelComponent>(ent.Comp.Weapon, out var weapon))
            return;

        ent.Comp.Triggered = true;
        var coordinates = Transform(args.Target).Coordinates;
        Spawn(weapon.EngravedPrototype, coordinates);

        if (ent.Comp.LaunchMode)
        {
            var target = _transform.ToMapCoordinates(coordinates);
            var thrower = ent.Comp.Thrower;
            var distance = Vector2.Distance(_transform.GetMapCoordinates(thrower).Position, target.Position);
            // A plain rush to the dagger, without the blink's trail of blood.
            _blink.TryStartDash(thrower, target, Math.Max(20f, distance / 0.4f), TimeSpan.FromSeconds(0.6),
                TimeSpan.FromSeconds(0.2), visual: false);
            return;
        }

        // A diamond-shaped ring of crystals around the engraved spot.
        var radius = Math.Max(1, weapon.CrystalRingRadius);
        for (var x = -radius; x <= radius; x++)
        {
            var y = radius - Math.Abs(x);
            SpawnCrystal(weapon, coordinates.Offset(new Vector2(x, y)), ent.Comp.Thrower);
            if (y != 0)
                SpawnCrystal(weapon, coordinates.Offset(new Vector2(x, -y)), ent.Comp.Thrower);
        }
    }

    private void SpawnCrystal(LavalandCainAndAbelComponent weapon, EntityCoordinates coordinates, EntityUid owner)
    {
        var crystal = Spawn(weapon.CrystalPrototype, coordinates);
        if (TryComp<LavalandDaggerCrystalComponent>(crystal, out var comp))
            comp.Thrower = owner;
    }

    #endregion

    #region Soulscythe

    private void OnScytheExamined(Entity<LavalandSoulscytheComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Possessed ? "lavaland-soulscythe-possessed" : "lavaland-soulscythe-dormant"));
        if (ent.Comp.Possessed)
            args.PushMarkup(Loc.GetString("lavaland-soulscythe-blood", ("blood", (int) ent.Comp.Blood)));
    }

    private void OnScytheUse(Entity<LavalandSoulscytheComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Possessed || ent.Comp.Awakening)
            return;
        args.Handled = true;

        ent.Comp.Awakening = true;
        ent.Comp.Master = args.User;
        EntityManager.AddComponents(ent, ent.Comp.AwakenComponents, false);
        _popup.PopupEntity(Loc.GetString("lavaland-soulscythe-awakening"), args.User, args.User);
    }

    private void OnScytheMindAdded(Entity<LavalandSoulscytheComponent> ent, ref MindAddedMessage args)
    {
        if (ent.Comp.Possessed)
            return;

        ent.Comp.Possessed = true;
        ent.Comp.Awakening = false;
        var bound = EnsureComp<LavalandSoulscytheBoundComponent>(ent);
        bound.Master = ent.Comp.Master;
        Dirty(ent, bound);
        _mind.MakeSentient(ent);
        _appearance.SetData(ent, LavalandArtifactVisuals.Active, true);
        _movement.RefreshMovementSpeedModifiers(ent);
        if (ent.Comp.Master is { } master)
            _popup.PopupEntity(Loc.GetString("lavaland-soulscythe-glows"), master, master);
    }

    private void OnScytheHit(Entity<LavalandSoulscytheComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || !ent.Comp.Possessed)
            return;

        foreach (var target in args.HitEntities)
        {
            if (TryComp<MobStateComponent>(target, out var mob) && !_mobState.IsDead(target, mob))
            {
                ent.Comp.Blood = Math.Min(ent.Comp.MaxBlood, ent.Comp.Blood + ent.Comp.BloodPerHit);
                _movement.RefreshMovementSpeedModifiers(ent);
                break;
            }
        }
    }

    private void UpdateScytheMovement(Entity<LavalandSoulscytheComponent> ent, TransformComponent xform)
    {
        if (!ent.Comp.Possessed && !ent.Comp.Awakening)
            return;

        // Kill whoever awakened it and it falls asleep again.
        if (ent.Comp.Master is not { } owner || TerminatingOrDeleted(owner) || _mobState.IsDead(owner))
        {
            PutScytheToSleep(ent);
            return;
        }

        if (!ent.Comp.Possessed)
            return;

        UpdateScytheRange(ent, xform, owner);
        TryBreakFree(ent);

        var position = _transform.GetMapCoordinates(xform);
        var last = ent.Comp.LastPosition;
        ent.Comp.LastPosition = position;

        // Only moving on its own costs blood, not being carried around.
        if (last.MapId != position.MapId || ent.Comp.Master is { } master && xform.ParentUid == master ||
            !TryComp<MindContainerComponent>(ent, out var mind) || !mind.HasMind ||
            _containers.IsEntityInContainer(ent.Owner))
        {
            return;
        }

        ent.Comp.Moved += Vector2.Distance(last.Position, position.Position);
        while (ent.Comp.Moved >= 1f)
        {
            ent.Comp.Moved -= 1f;
            ent.Comp.Blood = Math.Max(0f, ent.Comp.Blood - ent.Comp.MoveCostPerTile);
        }

        if (ent.Comp.Blood <= 0f)
            _movement.RefreshMovementSpeedModifiers(ent);
    }

    private void OnScytheSpeed(Entity<LavalandSoulscytheComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Possessed && (ent.Comp.Blood <= 0f || ent.Comp.OutOfRange))
            args.ModifySpeed(0f, 0f);
    }

    /// <summary>Too far from its master: no blows.</summary>
    /// <summary>
    /// The awakened scythe fighting on its own barely scratches bosses and elites: its master kept away while it
    /// alone cut down any boss.
    /// </summary>
    private const float ScytheBossDamageFactor = 0.25f;

    private void OnBossDamageModify(EntityUid uid, LavalandBossComponent component, DamageModifyEvent args)
    {
        ReduceScytheDamage(args);
    }

    private void OnEliteDamageModify(EntityUid uid, LavalandEliteComponent component, DamageModifyEvent args)
    {
        ReduceScytheDamage(args);
    }

    private void ReduceScytheDamage(DamageModifyEvent args)
    {
        if (args.Origin is { } origin &&
            TryComp<LavalandSoulscytheComponent>(origin, out var scythe) && scythe.Possessed)
        {
            args.Damage *= ScytheBossDamageFactor;
        }
    }

    private void OnScytheAttackAttempt(Entity<LavalandSoulscytheComponent> ent, ref AttackAttemptEvent args)
    {
        if (ent.Comp.Possessed && ent.Comp.OutOfRange)
            args.Cancel();
    }

    /// <summary>Its power reaches only so far from whoever awakened it.</summary>
    private void UpdateScytheRange(Entity<LavalandSoulscytheComponent> ent, TransformComponent xform, EntityUid owner)
    {
        var ownerXform = Transform(owner);
        var outOfRange = ownerXform.MapID != xform.MapID ||
                         Vector2.Distance(_transform.GetWorldPosition(ownerXform), _transform.GetWorldPosition(xform)) >
                         ent.Comp.ActiveRange;
        if (outOfRange == ent.Comp.OutOfRange)
            return;

        ent.Comp.OutOfRange = outOfRange;
        _movement.RefreshMovementSpeedModifiers(ent);
        _popup.PopupEntity(Loc.GetString(outOfRange ? "lavaland-soulscythe-out-of-range" : "lavaland-soulscythe-in-range"),
            ent, ent, PopupType.Medium);
    }

    /// <summary>Held by someone while it wants to move: it tears itself out of their hands.</summary>
    private void TryBreakFree(Entity<LavalandSoulscytheComponent> ent)
    {
        var now = _timing.CurTime;
        if (now < ent.Comp.NextBreakFree || ent.Comp.OutOfRange ||
            !TryComp<InputMoverComponent>(ent, out var mover) ||
            !mover.HasDirectionalMovement ||
            !_containers.TryGetContainingContainer(ent.Owner, out var container) ||
            !_hands.IsHolding(container.Owner, ent))
        {
            return;
        }

        var holder = container.Owner;
        ent.Comp.NextBreakFree = now + ent.Comp.BreakFreeCooldown;
        if (!_hands.TryDrop(holder, ent.Owner, checkActionBlocker: false))
            return;

        var direction = _transform.GetWorldRotation(holder).ToWorldVec();
        _throwing.TryThrow(ent.Owner, direction * ent.Comp.BreakFreeThrow, recoil: false);
        _popup.PopupEntity(Loc.GetString("lavaland-soulscythe-breaks-free", ("holder", holder)), holder, PopupType.MediumCaution);
    }

    /// <summary>Its master is dead: the soul leaves it and it is a plain dormant scythe again.</summary>
    private void PutScytheToSleep(Entity<LavalandSoulscytheComponent> ent)
    {
        if (TryComp<MindContainerComponent>(ent, out var mindContainer) && mindContainer.Mind is { } mindId)
            _mind.TransferTo(mindId, null, createGhost: true);

        EntityManager.RemoveComponents(ent, ent.Comp.AwakenComponents);
        RemComp<LavalandSoulscytheBoundComponent>(ent);
        ent.Comp.Possessed = false;
        ent.Comp.Awakening = false;
        ent.Comp.OutOfRange = false;
        ent.Comp.Master = null;
        ent.Comp.Blood = 0f;
        _appearance.SetData(ent, LavalandArtifactVisuals.Active, false);
        _movement.RefreshMovementSpeedModifiers(ent);
        _popup.PopupEntity(Loc.GetString("lavaland-soulscythe-sleeps"), ent, PopupType.Medium);
    }

    /// <summary>
    /// Somebody else grabs the awakened scythe: it will not be picked up (see LavalandSoulscytheBoundSystem) and
    /// slashes the hand that tried.
    /// </summary>
    private void OnScytheGrabbed(Entity<LavalandSoulscytheComponent> ent, ref InteractHandEvent args)
    {
        if (!ent.Comp.Possessed || ent.Comp.Master is not { } master || args.User == master)
            return;

        args.Handled = true;
        var now = _timing.CurTime;
        if (now < ent.Comp.NextGrabPunish)
            return;

        ent.Comp.NextGrabPunish = now + ent.Comp.GrabPunishCooldown;
        _damageable.TryChangeDamage(args.User, ent.Comp.GrabPunishDamage, origin: ent);
        _popup.PopupEntity(Loc.GetString("lavaland-soulscythe-grab-punish"), args.User, args.User, PopupType.MediumCaution);
    }

    #endregion

    #region Concussive gauntlets

    private void OnGauntletsEquipped(Entity<LavalandConcussiveGauntletsComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & SlotFlags.GLOVES) != 0)
            EnsureComp<LavalandBumpMinerComponent>(args.Equipee).Gloves = ent.Owner;
    }

    private void OnGauntletsUnequipped(Entity<LavalandConcussiveGauntletsComponent> ent, ref GotUnequippedEvent args)
    {
        if (TryComp<LavalandBumpMinerComponent>(args.Equipee, out var miner) && miner.Gloves == ent.Owner)
            RemComp<LavalandBumpMinerComponent>(args.Equipee);
    }

    private void OnBumpMinerCollide(Entity<LavalandBumpMinerComponent> ent, ref StartCollideEvent args)
    {
        var now = _timing.CurTime;
        if (now < ent.Comp.NextMine ||
            !HasComp<GatherableComponent>(args.OtherEntity) ||
            !HasComp<WallComponent>(args.OtherEntity) ||
            !TryComp<LavalandConcussiveGauntletsComponent>(ent.Comp.Gloves, out var gloves))
        {
            return;
        }

        ent.Comp.NextMine = now + gloves.BumpCooldown;
        _gatherable.Gather(args.OtherEntity, ent);
    }

    #endregion
}
