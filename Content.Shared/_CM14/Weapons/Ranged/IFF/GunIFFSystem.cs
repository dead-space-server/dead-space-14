using Content.Shared._CM14.Weapons.Ranged.IFF;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Events;
namespace Content.Shared._CM14.Weapons.Ranged.IFF;
public sealed class GunIFFSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    private EntityQuery<UserIFFComponent> _userIFFQuery;
    public override void Initialize()
    {
        base.Initialize();
        _userIFFQuery = GetEntityQuery<UserIFFComponent>();
        SubscribeLocalEvent<GunIFFComponent, AmmoShotEvent>(OnGunIFFAmmoShot);
        SubscribeLocalEvent<ProjectileIFFComponent, PreventCollideEvent>(OnProjectileIFFPreventCollide);
    }
    private void OnGunIFFAmmoShot(Entity<GunIFFComponent> ent, ref AmmoShotEvent args)
    {
        GiveAmmoIFF(ent, ref args);
    }
    private void OnProjectileIFFPreventCollide(Entity<ProjectileIFFComponent> ent, ref PreventCollideEvent args)
    {
        if (ent.Comp.Faction is not { } faction)
            return;
        if (!TryComp(args.OtherEntity, out IFFFactionComponent? other))
            return;
        if (other.Faction != faction)
            return;
        args.Cancelled = true;
    }
    public void GiveAmmoIFF(EntityUid gun, ref AmmoShotEvent args)
    {
        if (!_container.TryGetContainingContainer((gun, null), out var container) ||
            !_userIFFQuery.HasComp(container.Owner))
        {
            return;
        }
        var ev = new GetIFFFactionEvent();
        RaiseLocalEvent(container.Owner, ref ev);
        if (ev.Faction is not { } id)
            return;
        foreach (var projectile in args.FiredProjectiles)
        {
            var iff = EnsureComp<ProjectileIFFComponent>(projectile);
            iff.Faction = id;
            Dirty(projectile, iff);
        }
    }
}
