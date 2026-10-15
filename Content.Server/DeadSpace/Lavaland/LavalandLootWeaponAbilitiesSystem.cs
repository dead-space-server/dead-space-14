// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Blink;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Gatherable;
using Content.Server.Gatherable.Components;
using Content.Server.Mind;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Actions;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.Examine;
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
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Active abilities of lavaland loot weapons: Cain and Abel dagger throws, the possessed soulscythe
/// and concussive gauntlets mining rock on contact.
/// </summary>
public sealed class LavalandLootWeaponAbilitiesSystem : EntitySystem
{
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
    [Dependency] private readonly Robust.Shared.Containers.SharedContainerSystem _containers = default!;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandCainAndAbelComponent, UseInHandEvent>(OnCainAbelUseInHand);
        SubscribeLocalEvent<LavalandCainAndAbelComponent, GetItemActionsEvent>(OnCainAbelGetActions);
        SubscribeLocalEvent<LavalandCainAndAbelComponent, LavalandCainAndAbelThrowActionEvent>(OnCainAbelThrow);
        SubscribeLocalEvent<LavalandCainAndAbelDaggerComponent, ProjectileHitEvent>(OnDaggerHit);

        SubscribeLocalEvent<LavalandSoulscytheComponent, UseInHandEvent>(OnScytheUse);
        SubscribeLocalEvent<LavalandSoulscytheComponent, MindAddedMessage>(OnScytheMindAdded);
        SubscribeLocalEvent<LavalandSoulscytheComponent, MeleeHitEvent>(OnScytheHit);
        SubscribeLocalEvent<LavalandSoulscytheComponent, RefreshMovementSpeedModifiersEvent>(OnScytheSpeed);
        SubscribeLocalEvent<LavalandSoulscytheComponent, GettingPickedUpAttemptEvent>(OnScythePickupAttempt);
        SubscribeLocalEvent<LavalandSoulscytheComponent, ExaminedEvent>(OnScytheExamined);

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
                if (mob.Owner == crystal.Owner || _mobState.IsDead(mob, mob.Comp) || !crystal.Hit.Add(mob))
                    continue;

                var large = _weapons.IsLavalandCreature(mob);
                _damageable.TryChangeDamage(mob.Owner, large ? crystal.LargeDamage : crystal.SmallDamage,
                    origin: crystal.Owner);
                _stun.TryUpdateParalyzeDuration(mob, crystal.Stun);
            }
        }
    }

    #region Cain and Abel throws

    private void OnCainAbelUseInHand(Entity<LavalandCainAndAbelComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        ent.Comp.LaunchMode = !ent.Comp.LaunchMode;
        _popup.PopupEntity(Loc.GetString(ent.Comp.LaunchMode
            ? "lavaland-cain-abel-mode-launch"
            : "lavaland-cain-abel-mode-crystals"), args.User, args.User);
    }

    private void OnCainAbelGetActions(Entity<LavalandCainAndAbelComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            args.AddAction(ref ent.Comp.ThrowActionEntity, ent.Comp.ThrowAction);
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

        ent.Comp.NextThrow = now + (ent.Comp.LaunchMode ? ent.Comp.LaunchCooldown : ent.Comp.CrystalsCooldown);
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
            _blink.TryStartDash(thrower, target, Math.Max(20f, distance / 0.4f), TimeSpan.FromSeconds(0.6),
                TimeSpan.FromSeconds(0.2));
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
            comp.Owner = owner;
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
        if (!ent.Comp.Possessed)
            return;

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
        if (ent.Comp.Possessed && ent.Comp.Blood <= 0f)
            args.ModifySpeed(0f, 0f);
    }

    private void OnScythePickupAttempt(Entity<LavalandSoulscytheComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (ent.Comp.Possessed && ent.Comp.Master is { } master && args.User != master)
            args.Cancel();
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
