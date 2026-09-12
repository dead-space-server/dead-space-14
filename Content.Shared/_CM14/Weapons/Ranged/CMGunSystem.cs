using Content.Shared._CM14.Weapons.Ranged;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Timing;
namespace Content.Shared._CM14.Weapons.Ranged;
public sealed class CMGunSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    private EntityQuery<ProjectileComponent> _projectileQuery;
    public override void Initialize()
    {
        base.Initialize();
        _projectileQuery = GetEntityQuery<ProjectileComponent>();
        // DS14-start
        // DS14: The base multiplier seeds the relayed modifier event and must not modify itself again.
        SubscribeLocalEvent<GunDamageModifierComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<GunDamageModifierComponent, AmmoShotEvent>(OnAmmoShot);
        // DS14-end
    }
    private void OnMapInit(Entity<GunDamageModifierComponent> ent, ref MapInitEvent args)
    {
        RefreshGunDamageMultiplier(ent.AsNullable());
    }
    // DS14-start
    private void OnAmmoShot(Entity<GunDamageModifierComponent> ent, ref AmmoShotEvent args)
    {
        foreach (var projectile in args.FiredProjectiles)
        {
            if (!_projectileQuery.TryGetComponent(projectile, out var component))
                continue;

            component.Damage *= ent.Comp.ModifiedMultiplier;
            Dirty(projectile, component);
        }
    }
    // DS14-end
    public void RefreshGunDamageMultiplier(Entity<GunDamageModifierComponent?> gun)
    {
        gun.Comp = EnsureComp<GunDamageModifierComponent>(gun);
        var ev = new GetGunDamageModifierEvent(gun.Comp.Multiplier);
        RaiseLocalEvent(gun, ref ev);
        gun.Comp.ModifiedMultiplier = ev.Multiplier;
    }
    public void RefreshGunDamageMultiplier(EntityUid gun)
    {
        RefreshGunDamageMultiplier((gun, null));
    }
    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
    }
}
