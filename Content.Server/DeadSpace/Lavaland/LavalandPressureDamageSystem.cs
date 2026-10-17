// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Atmos.EntitySystems;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Examine;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>Weakens kinetic weapons outside of lavaland-like pressure. See <see cref="LavalandPressureDamageComponent"/>.</summary>
public sealed class LavalandPressureDamageSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandPressureDamageComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<LavalandPressureDamageComponent, GetMeleeDamageEvent>(OnGetMeleeDamage,
            after: new[] { typeof(SharedWieldableSystem) });
        SubscribeLocalEvent<LavalandPressureDamageComponent, GunShotEvent>(OnGunShot);
    }

    /// <summary>True if the weapon is outside of the pressure range and should be weakened.</summary>
    public bool IsWeakened(Entity<LavalandPressureDamageComponent> ent)
    {
        var pressure = _atmos.GetTileMixture((ent.Owner, Transform(ent)))?.Pressure ?? 0f;
        return pressure < ent.Comp.LowerBound || pressure > ent.Comp.UpperBound;
    }

    private void OnGetMeleeDamage(Entity<LavalandPressureDamageComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (ent.Comp.ApplyToMelee && IsWeakened(ent))
            args.Damage *= ent.Comp.OutOfRangeModifier;
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

    private void OnExamined(Entity<LavalandPressureDamageComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("lavaland-pressure-damage-examine",
            ("min", Math.Round(ent.Comp.LowerBound)),
            ("max", Math.Round(ent.Comp.UpperBound)),
            ("percent", Math.Round(ent.Comp.OutOfRangeModifier * 100))));
    }
}
