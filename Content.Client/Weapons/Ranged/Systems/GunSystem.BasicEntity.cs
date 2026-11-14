using Content.Shared.Weapons.Ranged.Components;

namespace Content.Client.Weapons.Ranged.Systems;

public partial class GunSystem
{
    protected override void InitializeBasicEntity()
    {
        base.InitializeBasicEntity();
        SubscribeLocalEvent<BasicEntityAmmoProviderComponent, UpdateAmmoCounterEvent>(OnBasicEntityAmmoCount);
        SubscribeLocalEvent<BasicEntityAmmoProviderComponent, AfterAutoHandleStateEvent>(OnBasicEntityState); // DS14
    }

    // DS14-start
    /// <summary>The server alone recharges the ammo: its new count is drawn as it comes.</summary>
    private void OnBasicEntityState(Entity<BasicEntityAmmoProviderComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateAmmoCount(ent, prediction: false);
    }
    // DS14-end

    private void OnBasicEntityAmmoCount(Entity<BasicEntityAmmoProviderComponent> ent, ref UpdateAmmoCounterEvent args)
    {
        if (UsesAmmoSlot(ent)) // DS14
            return; // DS14

        if (args.Control is DefaultStatusControl control && ent.Comp.Count != null && ent.Comp.Capacity != null)
        {
            control.Update(ent.Comp.Count.Value, ent.Comp.Capacity.Value);
        }
    }
}
