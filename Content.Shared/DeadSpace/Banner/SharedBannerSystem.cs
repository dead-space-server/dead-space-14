using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Banner;

public abstract class SharedBannerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BannerDefenseBuffComponent, DamageModifyEvent>(OnDefenseModify);
        SubscribeLocalEvent<BannerGunBoostComponent, GunRefreshModifiersEvent>(OnGunRefresh);
        SubscribeLocalEvent<BannerGunBoostComponent, GunGetAmmoSpreadEvent>(OnGunSpread);
    }

    private void OnDefenseModify(Entity<BannerDefenseBuffComponent> ent, ref DamageModifyEvent args)
    {
        if (_timing.CurTime >= ent.Comp.EndTime)
            return;

        args.Damage *= ent.Comp.Coefficient;
    }

    private void OnGunRefresh(Entity<BannerGunBoostComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (_timing.CurTime >= ent.Comp.EndTime)
            return;

        args.FireRate += ent.Comp.FireRateBonus;
        args.MinAngle = Angle.Zero;
        args.MaxAngle = Angle.Zero;
        args.AngleIncrease = Angle.Zero;
    }

    private void OnGunSpread(Entity<BannerGunBoostComponent> ent, ref GunGetAmmoSpreadEvent args)
    {
        if (_timing.CurTime >= ent.Comp.EndTime)
            return;

        args.Spread = Angle.Zero;
    }
}
