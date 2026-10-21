// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Server effects of lavaland loot weapons: cleaving saw bleed and open-mode bonus, Cain and Abel combo and wisps.
/// </summary>
public sealed class LavalandLootWeaponsSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly List<EntityUid> _expired = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandCleavingSawEffectsComponent, MeleeHitEvent>(OnSawHit);

        SubscribeLocalEvent<LavalandCainAndAbelComponent, MeleeHitEvent>(OnCainAbelHit);
        SubscribeLocalEvent<LavalandCainAndAbelComponent, GotEquippedHandEvent>(OnCainAbelEquipped);
        SubscribeLocalEvent<LavalandCainAndAbelComponent, GotUnequippedHandEvent>(OnCainAbelUnequipped);
        SubscribeLocalEvent<LavalandCainAndAbelComponent, AfterInteractEvent>(OnCainAbelAfterInteract);
        SubscribeLocalEvent<LavalandCainAndAbelComponent, ComponentShutdown>(OnCainAbelShutdown);
        SubscribeLocalEvent<LavalandCainAndAbelWielderComponent, DamageChangedEvent>(OnWielderDamaged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        _expired.Clear();
        var query = EntityQueryEnumerator<LavalandSawBleedComponent>();
        while (query.MoveNext(out var uid, out var bleed))
        {
            if (now - bleed.LastStack < bleed.DecayDelay || now < bleed.NextDecay)
                continue;

            bleed.NextDecay = now + bleed.DecayInterval;
            if (--bleed.Stacks <= 0)
                _expired.Add(uid);
        }

        foreach (var uid in _expired)
            RemComp<LavalandSawBleedComponent>(uid);
    }

    public bool IsLavalandCreature(EntityUid uid)
    {
        return HasComp<LavalandFaunaComponent>(uid) ||
               HasComp<LavalandBossComponent>(uid) ||
               HasComp<LavalandMadMinerComponent>(uid) ||
               HasComp<LavalandLegionMegafaunaComponent>(uid);
    }

    #region Cleaving saw

    private void OnSawHit(Entity<LavalandCleavingSawEffectsComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        var open = _toggle.IsActivated(ent.Owner);
        foreach (var target in args.HitEntities)
        {
            if (!IsLavalandCreature(target) || !TryComp<MobStateComponent>(target, out var mob) || _mobState.IsDead(target, mob))
                continue;

            // Open: a slow heavy cleave, and the bleed piled up by the closed saw bursts. Closed: quick cuts that
            // only pile up the bleed - never past the point where an open blow would burst it.
            if (open)
            {
                _damageable.TryChangeDamage(target, ent.Comp.OpenBonusDamage, origin: args.User);
                TryBurstBleed(target, ent.Comp.BleedBurstDamage, args.User);
            }
            else
            {
                AddBleed(target, ent.Comp.BleedStacksPerHit, ent.Comp.BleedBurstDamage, args.User);
            }
        }
    }

    private void AddBleed(EntityUid target, int stacks, DamageSpecifier burstDamage, EntityUid user)
    {
        var bleed = EnsureComp<LavalandSawBleedComponent>(target);
        bleed.BurstDamage = burstDamage;
        var wasPrimed = bleed.Stacks >= bleed.Threshold;
        bleed.Stacks = Math.Min(bleed.Threshold, bleed.Stacks + stacks);
        bleed.LastStack = _timing.CurTime;

        // Ready for the finishing blow: unfold the saw and strike.
        if (!wasPrimed && bleed.Stacks >= bleed.Threshold)
            _popup.PopupEntity(Loc.GetString("lavaland-saw-bleed-primed", ("target", target)), target, user, PopupType.Medium);
    }

    /// <summary>The open saw's finishing blow: a full pile of bleed bursts at once.</summary>
    private void TryBurstBleed(EntityUid target, DamageSpecifier burstDamage, EntityUid user)
    {
        if (!TryComp<LavalandSawBleedComponent>(target, out var bleed) || bleed.Stacks < bleed.Threshold)
            return;

        bleed.BurstDamage = burstDamage;

        // The built-up bleed bursts at once.
        _damageable.TryChangeDamage(target, bleed.BurstDamage, origin: user);
        _popup.PopupEntity(Loc.GetString("lavaland-saw-bleed-burst", ("target", target)), target, PopupType.LargeCaution);
        RemComp<LavalandSawBleedComponent>(target);
    }

    #endregion

    #region Cain and Abel

    private void OnCainAbelEquipped(Entity<LavalandCainAndAbelComponent> ent, ref GotEquippedHandEvent args)
    {
        ent.Comp.Wielder = args.User;
        EnsureComp<LavalandCainAndAbelWielderComponent>(args.User).Weapon = ent.Owner;
    }

    private void OnCainAbelUnequipped(Entity<LavalandCainAndAbelComponent> ent, ref GotUnequippedHandEvent args)
    {
        SetCombo(ent, 0);
        ent.Comp.Wielder = null;
        if (TryComp<LavalandCainAndAbelWielderComponent>(args.User, out var wielder) && wielder.Weapon == ent.Owner)
            RemComp<LavalandCainAndAbelWielderComponent>(args.User);
    }

    private void OnCainAbelShutdown(Entity<LavalandCainAndAbelComponent> ent, ref ComponentShutdown args)
    {
        foreach (var wisp in ent.Comp.Wisps)
            QueueDel(wisp);
        ent.Comp.Wisps.Clear();
    }

    private void OnWielderDamaged(Entity<LavalandCainAndAbelWielderComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin is not { } origin || origin == ent.Owner ||
            !TryComp<LavalandCainAndAbelComponent>(ent.Comp.Weapon, out var weapon))
        {
            return;
        }

        SetCombo((ent.Comp.Weapon, weapon), 0);
    }

    private void OnCainAbelHit(Entity<LavalandCainAndAbelComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        var hitLarge = false;
        var multiplier = Math.Max(1, ent.Comp.Combo) * ent.Comp.DamageBoost - 1f;
        foreach (var target in args.HitEntities)
        {
            if (!IsLavalandCreature(target) || !TryComp<MobStateComponent>(target, out var mob) || _mobState.IsDead(target, mob))
                continue;

            hitLarge = true;
            if (multiplier > 0f)
                _damageable.TryChangeDamage(target, args.BaseDamage * multiplier, origin: args.User);
        }

        if (hitLarge)
            SetCombo(ent, ent.Comp.Combo + 1);
    }

    private void OnCainAbelAfterInteract(Entity<LavalandCainAndAbelComponent> ent, ref AfterInteractEvent args)
    {
        // A click beyond melee reach releases the wisps at the clicked spot.
        if (args.Handled || args.CanReach || ent.Comp.Wisps.Count == 0)
            return;

        var origin = _transform.GetMapCoordinates(args.User);
        var target = _transform.ToMapCoordinates(args.ClickLocation);
        var offset = target.Position - origin.Position;
        if (target.MapId != origin.MapId || offset.LengthSquared() < 0.01f ||
            offset.Length() > ent.Comp.WispFireRange)
        {
            return;
        }

        args.Handled = true;
        var count = ent.Comp.Wisps.Count;
        var baseAngle = offset.ToWorldAngle();
        for (var i = 0; i < count; i++)
        {
            var spread = Angle.FromDegrees((i - (count - 1) / 2f) * 6f);
            var projectile = Spawn(ent.Comp.WispProjectile, Transform(args.User).Coordinates);
            _gun.ShootProjectile(projectile, (baseAngle + spread).ToWorldVec(), Vector2.Zero, ent.Owner, args.User,
                ent.Comp.WispProjectileSpeed);
        }

        if (ent.Comp.WispFireSound != null)
            _audio.PlayPvs(ent.Comp.WispFireSound, args.User);
        SetCombo(ent, 0);
    }

    private void SetCombo(Entity<LavalandCainAndAbelComponent> ent, int combo)
    {
        combo = Math.Clamp(combo, 0, ent.Comp.MaxCombo);
        ent.Comp.Combo = combo;

        while (ent.Comp.Wisps.Count > combo)
        {
            var last = ent.Comp.Wisps[^1];
            ent.Comp.Wisps.RemoveAt(ent.Comp.Wisps.Count - 1);
            QueueDel(last);
        }

        if (ent.Comp.Wielder is not { } wielder || TerminatingOrDeleted(wielder))
            return;

        while (ent.Comp.Wisps.Count < combo)
        {
            var index = ent.Comp.Wisps.Count;
            var angle = Angle.FromDegrees(360.0 / ent.Comp.MaxCombo * index);
            var wisp = Spawn(ent.Comp.WispPrototype, new EntityCoordinates(wielder, angle.ToVec() * ent.Comp.WispOrbitRadius));
            ent.Comp.Wisps.Add(wisp);
        }
    }

    #endregion
}
