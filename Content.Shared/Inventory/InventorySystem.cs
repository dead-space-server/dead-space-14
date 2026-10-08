using Content.Shared.Hands.Components;

namespace Content.Shared.Inventory;

public partial class InventorySystem
{

    public override void Initialize()
    {
        base.Initialize();
        InitializeEquip();
        InitializeRelay();
        InitializeSlots();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        ShutdownSlots();
    }

    // DS14 Start
    public void SetInventorySpecies(EntityUid uid, string speciesId, InventoryComponent? inventory = null)
    {
        if (!Resolve(uid, ref inventory))
            return;

        inventory.SpeciesId = speciesId.ToLower();
        Dirty(uid, inventory);
    }
    //DS14 End
}
