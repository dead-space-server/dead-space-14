using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Ninja.Systems;

namespace Content.Shared.Weapons.Ranged.Systems;

public partial class SharedGunSystem
{
    [Dependency] private readonly SharedSpaceNinjaSystem _ninja = default!;
    private void InitializeNinja()
    {
        SubscribeLocalEvent<NinjaAmmoProviderComponent, TakeAmmoEvent>(OnNinjaTakeAmmo);
        SubscribeLocalEvent<NinjaAmmoProviderComponent, GetAmmoCountEvent>(OnNinjaAmmoCount);
    }

    private void OnNinjaTakeAmmo(Entity<NinjaAmmoProviderComponent> ent, ref TakeAmmoEvent args)
    {
        if (args.User == null)
            return;

        for (var i = 0; i < args.Shots; i++)
        {
            if (!_ninja.HasCharge(args.User.Value, ent.Comp.EnergyPerShoot))
                break;

            if (_netManager.IsServer)
            {
                _ninja.TryUseCharge(args.User.Value, ent.Comp.EnergyPerShoot);
                var spawned = Spawn(ent.Comp.Proto, args.Coordinates);
                args.Ammo.Add((spawned, EnsureShootable(spawned)));
            }
            else
            {
                var predicted = Spawn(ent.Comp.Proto, args.Coordinates);
                args.Ammo.Add((predicted, EnsureShootable(predicted)));
            }
        }
    }

    private void OnNinjaAmmoCount(Entity<NinjaAmmoProviderComponent> ent, ref GetAmmoCountEvent args)
    {
        args.Capacity = 1;
        args.Count = 1;
    }
}