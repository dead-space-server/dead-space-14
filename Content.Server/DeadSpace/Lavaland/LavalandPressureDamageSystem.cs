// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Atmos.EntitySystems;
using Content.Server.DeadSpace.Attachments;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.Damage;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Attachments;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Examine;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>Weakens kinetic weapons outside of lavaland-like pressure. See <see cref="LavalandPressureDamageComponent"/>.</summary>
public sealed class LavalandPressureDamageSystem : SharedLavalandPressureDamageSystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly MobThresholdSystem _threshold = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandPressureDamageComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<LavalandPressureDamageComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<LavalandPressureDamageComponent, DamageMarkerDetonatedEvent>(OnMarkDetonated,
            after: new[] { typeof(AttachmentSystem) });
    }

    /// <summary>How often a weapon's air is looked at, so its holder's client knows how hard it hits.</summary>
    private static readonly TimeSpan PressureCheckInterval = TimeSpan.FromSeconds(0.5);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandPressureDamageComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextPressureCheck)
                continue;

            comp.NextPressureCheck = now + PressureCheckInterval;
            RefreshWeakened((uid, comp));
        }
    }

    protected override void RefreshWeakened(Entity<LavalandPressureDamageComponent> ent)
    {
        var weakened = IsWeakened(ent);
        if (ent.Comp.Weakened == weakened)
            return;

        ent.Comp.Weakened = weakened;
        Dirty(ent);
    }

    /// <summary>True if the weapon is outside of the pressure range and should be weakened.</summary>
    public bool IsWeakened(Entity<LavalandPressureDamageComponent> ent)
    {
        var xform = Transform(ent);

        // Places fought with lavaland gear under breathable air (the Celestial zone).
        if (xform.MapUid is { } map && HasComp<LavalandKineticZoneComponent>(map))
            return false;

        var pressure = _atmos.GetTileMixture((ent.Owner, xform))?.Pressure ?? 0f;
        return pressure < ent.Comp.LowerBound || pressure > ent.Comp.UpperBound;
    }

    private void OnGunShot(Entity<LavalandPressureDamageComponent> ent, ref GunShotEvent args)
    {
        if (!ent.Comp.ApplyToProjectiles || !IsWeakened(ent))
            return;

        foreach (var (ammo, _) in args.Ammo)
        {
            if (TryComp<ProjectileComponent>(ammo, out var projectile))
                projectile.Damage *= ent.Comp.OutOfRangeModifier;
        }
    }

    /// <summary>A boss loses a share of its health to the mark; and like any hit, the mark is weaker off lavaland.</summary>
    private void OnMarkDetonated(Entity<LavalandPressureDamageComponent> ent, ref DamageMarkerDetonatedEvent args)
    {
        if (TryGetBossHealth(args.Target, out var health))
        {
            var share = new DamageSpecifier(_prototype.Index(ent.Comp.BossMarkDamageType),
                health * ent.Comp.BossMarkHealthFraction);
            args.BonusDamage += share;
        }

        if (ent.Comp.ApplyToMelee && IsWeakened(ent))
            args.BonusDamage *= ent.Comp.OutOfRangeModifier;
    }

    /// <summary>The full health of a lavaland boss (the Ash Wolf keeps its own: its thresholds are never reached).</summary>
    private bool TryGetBossHealth(EntityUid target, out float health)
    {
        health = 0f;
        if (TryComp<LavalandAshWolfComponent>(target, out var wolf))
        {
            if (wolf.Tamed)
                return false;

            health = wolf.MaxHealth;
            return true;
        }

        if (!HasComp<LavalandBossComponent>(target) ||
            !_threshold.TryGetThresholdForState(target, MobState.Dead, out var dead))
        {
            return false;
        }

        health = dead.Value.Float();
        return true;
    }

    private void OnExamined(Entity<LavalandPressureDamageComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("lavaland-pressure-damage-examine",
            ("min", Math.Round(ent.Comp.LowerBound)),
            ("max", Math.Round(ent.Comp.UpperBound)),
            ("percent", Math.Round(ent.Comp.OutOfRangeModifier * 100))));
    }
}
