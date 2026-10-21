using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Gatherable;
using Content.Server.Gatherable.Components;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Mining.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Timing;
using Content.Shared.Verbs;
using Content.Shared.Wall;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandResonatorSystem : EntitySystem
{
    private static readonly TimeSpan TileRearmDelay = TimeSpan.FromSeconds(0.35);
    private static readonly Vector2[] CardinalDirections =
    [
        Vector2.UnitX,
        -Vector2.UnitX,
        Vector2.UnitY,
        -Vector2.UnitY,
    ];

    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GatherableSystem _gatherable = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;

    private readonly HashSet<EntityUid> _entities = new();
    private readonly Dictionary<(EntityUid Grid, Vector2i Indices), TimeSpan> _recentlyBurstTiles = new();
    private readonly List<(EntityUid Grid, Vector2i Indices)> _expiredBurstTiles = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandResonatorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<LavalandResonatorComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<LavalandResonatorComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<GatherableComponent, GetVerbsEvent<AlternativeVerb>>(OnMineableGetAlternativeVerbs);
        SubscribeLocalEvent<LavalandResonanceFieldComponent, ComponentShutdown>(OnFieldShutdown);
        SubscribeLocalEvent<LavalandResonatorBoltComponent, BeforeProjectileHitEvent>(OnBoltBeforeHit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        PruneRearmedTiles(curTime);

        var query = EntityQueryEnumerator<LavalandResonanceFieldComponent>();
        while (query.MoveNext(out var uid, out var field))
        {
            if (!field.Bursting && field.Resonator != null && field.BurstAt <= curTime)
                BurstField((uid, field), 1f);
        }
    }

    private void OnAfterInteract(Entity<LavalandResonatorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        var targetCoords = args.Target is { } target
            ? Transform(target).Coordinates
            : args.ClickLocation;

        args.Handled = TryUseResonator(ent, args.User, targetCoords, args.Target != null);
    }

    private void OnUseInHand(Entity<LavalandResonatorComponent> ent, ref UseInHandEvent args)
    {
        ToggleDelay(ent, args.User);
        args.Handled = true;
    }

    private void OnGetAlternativeVerbs(Entity<LavalandResonatorComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
            return;

        var seconds = ent.Comp.UseLongBurstDelay
            ? ent.Comp.ShortBurstDelay
            : ent.Comp.LongBurstDelay;
        var user = args.User;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("lavaland-resonator-toggle-delay-verb", ("seconds", seconds)),
            Act = () => ToggleDelay(ent, user),
        });
    }

    private void OnMineableGetAlternativeVerbs(Entity<GatherableComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess ||
            !args.CanInteract ||
            !args.CanComplexInteract ||
            args.Using is not { } resonator ||
            !TryComp<LavalandResonatorComponent>(resonator, out var resonatorComp) ||
            !HasComp<WallComponent>(ent.Owner))
        {
            return;
        }

        var targetCoords = Transform(ent.Owner).Coordinates;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("lavaland-resonator-place-field-verb"),
            Act = () => TryUseResonator((resonator, resonatorComp), user, targetCoords),
        });
    }

    private void OnFieldShutdown(Entity<LavalandResonanceFieldComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Resonator is not { } resonator ||
            !TryComp<LavalandResonatorComponent>(resonator, out var resonatorComp))
        {
            return;
        }

        resonatorComp.Fields.Remove(ent.Owner);
    }

    private bool TryGetFieldOnTile(
        TileRef tileRef,
        [NotNullWhen(true)] out Entity<LavalandResonanceFieldComponent>? field)
    {
        var query = EntityQueryEnumerator<LavalandResonanceFieldComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var fieldComp, out var xform))
        {
            if (TerminatingOrDeleted(uid) ||
                !_turf.TryGetTileRef(xform.Coordinates, out var fieldTile) ||
                fieldTile.Value.GridUid != tileRef.GridUid ||
                fieldTile.Value.GridIndices != tileRef.GridIndices)
            {
                continue;
            }

            field = (uid, fieldComp);
            return true;
        }

        field = null;
        return false;
    }

    private bool TryUseResonator(
        Entity<LavalandResonatorComponent> ent,
        EntityUid user,
        EntityCoordinates targetCoords,
        bool showInvalidPopup = true)
    {
        if (!_turf.TryGetTileRef(targetCoords, out var tileRef))
            return false;

        if (TryGetFieldOnTile(tileRef.Value, out _))
            return true;

        if (!CanPlaceFieldOnTile(tileRef.Value))
        {
            if (showInvalidPopup)
                _popup.PopupEntity(Loc.GetString("lavaland-resonator-needs-rock"), ent.Owner, user);

            return true;
        }

        if (IsTileRearming(tileRef.Value))
            return true;

        PruneFields(ent.Comp);
        if (ent.Comp.Fields.Count >= ent.Comp.MaxFields)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-resonator-field-limit"), ent.Owner, user);
            return true;
        }

        if (!TryResetUseDelay(ent.Owner))
            return true;

        var fieldUid = Spawn(ent.Comp.FieldPrototype, _turf.GetTileCenter(tileRef.Value));
        var fieldComp = EnsureComp<LavalandResonanceFieldComponent>(fieldUid);
        fieldComp.Resonator = ent.Owner;
        fieldComp.Creator = user;
        fieldComp.BurstAt = _timing.CurTime + TimeSpan.FromSeconds(GetBurstDelay(ent.Comp));
        fieldComp.Damage = new DamageSpecifier(ent.Comp.FieldDamage);
        fieldComp.LavalandDamageMultiplier = ent.Comp.LavalandDamageMultiplier;
        fieldComp.DamageRadius = ent.Comp.DamageRadius;
        fieldComp.IgnoreCreatorDamage = ent.Comp.IgnoreCreatorDamage;
        fieldComp.BurstProjectilePrototype = ent.Comp.BurstProjectilePrototype;
        fieldComp.BurstProjectileCount = ent.Comp.BurstProjectileCount;
        fieldComp.BurstProjectileSpeed = ent.Comp.BurstProjectileSpeed;
        fieldComp.BonusYieldChance = ent.Comp.BonusYieldChance;
        fieldComp.BonusYieldMultiplier = ent.Comp.BonusYieldMultiplier;
        fieldComp.BurstSound = ent.Comp.BurstSound;
        fieldComp.BossDamageFalloff = ent.Comp.BossDamageFalloff;
        fieldComp.BossDamageMinMultiplier = ent.Comp.BossDamageMinMultiplier;
        fieldComp.BossDamageWindow = ent.Comp.BossDamageWindow;

        ent.Comp.Fields.Add(fieldUid);
        _audio.PlayPvs(ent.Comp.PlaceSound, fieldUid);
        return true;
    }

    private bool CanPlaceFieldOnTile(TileRef tileRef)
    {
        if (tileRef.Tile.IsEmpty)
            return false;

        _entities.Clear();
        _lookup.GetEntitiesInTile(tileRef, _entities, LookupFlags.Static);

        foreach (var uid in _entities)
        {
            if (TerminatingOrDeleted(uid) ||
                !HasComp<WallComponent>(uid))
            {
                continue;
            }

            return HasComp<GatherableComponent>(uid);
        }

        return true;
    }

    private bool IsTileRearming(TileRef tileRef)
    {
        var key = GetTileKey(tileRef);
        if (!_recentlyBurstTiles.TryGetValue(key, out var expiresAt))
            return false;

        if (expiresAt > _timing.CurTime)
            return true;

        _recentlyBurstTiles.Remove(key);
        return false;
    }

    private void SetTileRearming(TileRef tileRef)
    {
        _recentlyBurstTiles[GetTileKey(tileRef)] = _timing.CurTime + TileRearmDelay;
    }

    private void PruneRearmedTiles(TimeSpan curTime)
    {
        if (_recentlyBurstTiles.Count == 0)
            return;

        _expiredBurstTiles.Clear();

        foreach (var (tile, expiresAt) in _recentlyBurstTiles)
        {
            if (expiresAt <= curTime)
                _expiredBurstTiles.Add(tile);
        }

        foreach (var tile in _expiredBurstTiles)
        {
            _recentlyBurstTiles.Remove(tile);
        }
    }

    private static (EntityUid Grid, Vector2i Indices) GetTileKey(TileRef tileRef)
    {
        return (tileRef.GridUid, tileRef.GridIndices);
    }

    private void BurstField(Entity<LavalandResonanceFieldComponent> field, float damageMultiplier, EntityUid? activator = null)
    {
        if (field.Comp.Bursting || TerminatingOrDeleted(field.Owner))
            return;

        field.Comp.Bursting = true;
        var xform = Transform(field.Owner);

        if (_turf.TryGetTileRef(xform.Coordinates, out var tileRef))
        {
            SetTileRearming(tileRef.Value);
            MineTile(tileRef.Value, field.Comp.Creator ?? activator, field.Comp);
            DamageMobs(field.Comp, xform, damageMultiplier, activator);
            SpawnBurstProjectiles(field.Comp, xform, activator);
        }

        Spawn("EffectGravityPulse", xform.Coordinates);
        _audio.PlayPvs(field.Comp.BurstSound, field.Owner);
        QueueDel(field.Owner);
    }

    private void MineTile(TileRef tileRef, EntityUid? gatherer, LavalandResonanceFieldComponent field)
    {
        _entities.Clear();
        _lookup.GetEntitiesInTile(tileRef, _entities, LookupFlags.Static);

        foreach (var uid in _entities)
        {
            if (TerminatingOrDeleted(uid) ||
                !HasComp<GatherableComponent>(uid) ||
                !HasComp<WallComponent>(uid))
            {
                continue;
            }

            // Gatherable walls are queue-deleted, so clear their collision before the burst projectiles spawn this tick.
            if (TryComp<PhysicsComponent>(uid, out var physics))
                _physics.SetCanCollide(uid, false, body: physics);

            ApplyBonusYield(uid, field);
            _gatherable.Gather(uid, gatherer);
        }
    }

    private void ApplyBonusYield(EntityUid uid, LavalandResonanceFieldComponent field)
    {
        if (field.BonusYieldMultiplier <= 1 ||
            field.BonusYieldChance <= 0f ||
            !_random.Prob(Math.Clamp(field.BonusYieldChance, 0f, 1f)) ||
            !TryComp<OreVeinComponent>(uid, out var ore) ||
            ore.CurrentOre == null)
        {
            return;
        }

        ore.YieldMultiplier = Math.Max(ore.YieldMultiplier, field.BonusYieldMultiplier);
    }

    private void DamageMobs(
        LavalandResonanceFieldComponent field,
        TransformComponent fieldXform,
        float damageMultiplier,
        EntityUid? activator)
    {
        if (field.Damage.Empty)
            return;

        var damage = new DamageSpecifier(field.Damage);
        if (fieldXform.MapUid is { } mapUid && HasComp<LavalandMapComponent>(mapUid))
            damage *= field.LavalandDamageMultiplier;

        damage *= damageMultiplier;
        if (damage.Empty)
            return;

        _entities.Clear();
        _lookup.GetEntitiesInRange(fieldXform.Coordinates, field.DamageRadius, _entities, LookupFlags.Dynamic);

        foreach (var uid in _entities)
        {
            if (TerminatingOrDeleted(uid) ||
                field.IgnoreCreatorDamage && uid == field.Creator ||
                !TryComp<MobStateComponent>(uid, out var mobState) ||
                _mobState.IsDead(uid, mobState) ||
                !TryComp<DamageableComponent>(uid, out var damageable))
            {
                continue;
            }

            var bossMultiplier = ConsumeBossDamageMultiplier(uid,
                field.BossDamageFalloff,
                field.BossDamageMinMultiplier,
                field.BossDamageWindow);
            _damageable.TryChangeDamage(
                (uid, damageable),
                damage * bossMultiplier,
                interruptsDoAfters: false,
                origin: field.Creator ?? activator ?? field.Resonator);
        }
    }

    private void SpawnBurstProjectiles(
        LavalandResonanceFieldComponent field,
        TransformComponent fieldXform,
        EntityUid? activator)
    {
        if (field.BurstProjectilePrototype is not { } projectilePrototype || field.BurstProjectileCount <= 0)
            return;

        var count = Math.Min(field.BurstProjectileCount, CardinalDirections.Length);
        var shooter = field.Creator ?? activator;

        for (var i = 0; i < count; i++)
        {
            var projectile = Spawn(projectilePrototype, fieldXform.Coordinates);
            if (field.BossDamageFalloff > 0f)
            {
                var bolt = EnsureComp<LavalandResonatorBoltComponent>(projectile);
                bolt.BossDamageFalloff = field.BossDamageFalloff;
                bolt.BossDamageMinMultiplier = field.BossDamageMinMultiplier;
                bolt.BossDamageWindow = field.BossDamageWindow;
            }
            _gun.ShootProjectile(
                projectile,
                CardinalDirections[i],
                Vector2.Zero,
                field.Resonator,
                shooter,
                field.BurstProjectileSpeed);
        }
    }

    private void OnBoltBeforeHit(Entity<LavalandResonatorBoltComponent> ent, ref BeforeProjectileHitEvent args)
    {
        var multiplier = ConsumeBossDamageMultiplier(args.Target,
            ent.Comp.BossDamageFalloff,
            ent.Comp.BossDamageMinMultiplier,
            ent.Comp.BossDamageWindow);
        if (multiplier < 1f)
            args.Damage = args.Damage * multiplier;
    }

    /// <summary>
    /// Returns the damage multiplier for a resonator hit on a boss and records the hit:
    /// every hit within the window makes the next one weaker.
    /// </summary>
    private float ConsumeBossDamageMultiplier(EntityUid target, float falloff, float minMultiplier, TimeSpan window)
    {
        // Not the Legion: its many bodies (and hands) are fought with resonators at full strength.
        if (falloff <= 0f ||
            HasComp<LavalandLegionMegafaunaComponent>(target) ||
            !HasComp<LavalandBossComponent>(target) &&
            !HasComp<LavalandMadMinerComponent>(target) &&
            !HasComp<LavalandEliteComponent>(target))
        {
            return 1f;
        }

        var now = _timing.CurTime;
        var pressure = EnsureComp<LavalandResonatorPressureComponent>(target);
        while (pressure.Hits.Count > 0 && now - pressure.Hits.Peek() >= window)
            pressure.Hits.Dequeue();

        var multiplier = Math.Max(Math.Clamp(minMultiplier, 0f, 1f), 1f - falloff * pressure.Hits.Count);
        pressure.Hits.Enqueue(now);
        return multiplier;
    }

    private bool TryResetUseDelay(EntityUid uid)
    {
        return !TryComp<UseDelayComponent>(uid, out var useDelay) ||
               _useDelay.TryResetDelay((uid, useDelay), checkDelayed: true);
    }

    private void ToggleDelay(Entity<LavalandResonatorComponent> ent, EntityUid user)
    {
        ent.Comp.UseLongBurstDelay = !ent.Comp.UseLongBurstDelay;
        var seconds = GetBurstDelay(ent.Comp);

        _popup.PopupEntity(
            Loc.GetString("lavaland-resonator-delay-set", ("seconds", seconds)),
            ent.Owner,
            user);
    }

    private static float GetBurstDelay(LavalandResonatorComponent component)
    {
        return component.UseLongBurstDelay
            ? component.LongBurstDelay
            : component.ShortBurstDelay;
    }

    private void PruneFields(LavalandResonatorComponent component)
    {
        component.Fields.RemoveWhere(field =>
            TerminatingOrDeleted(field) ||
            !HasComp<LavalandResonanceFieldComponent>(field));
    }
}
