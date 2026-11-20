using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers; //DS-14

namespace Content.Server.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    //DS-14 start
    protected override void InitializeRevolver()
    {
        base.InitializeRevolver();
        SubscribeLocalEvent<RevolverAmmoProviderComponent, EntRemovedFromContainerMessage>(OnRevolverAmmoRemoved);
    }
    //DS-14 end

    protected override void SpinRevolver(Entity<RevolverAmmoProviderComponent> ent, EntityUid? user = null)
    {
        base.SpinRevolver(ent, user);
        var index = Random.Next(ent.Comp.Capacity);

        if (ent.Comp.CurrentIndex == index)
            return;

        ent.Comp.CurrentIndex = index;
        Dirty(ent);
    }
}
