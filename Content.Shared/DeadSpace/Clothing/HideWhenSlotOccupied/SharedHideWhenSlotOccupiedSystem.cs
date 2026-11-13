// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Whitelist;

namespace Content.Shared.DeadSpace.Clothing.HideWhenSlotOccupied;

public abstract partial class SharedHideWhenSlotOccupiedSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private EntityWhitelistSystem _entityWhitelistSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HideWhenSlotOccupiedComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<HideWhenSlotOccupiedComponent, GotUnequippedEvent>(OnGotUnequipped);

        SubscribeLocalEvent<InventoryComponent, DidEquipEvent>(OnDidEquip);
        SubscribeLocalEvent<InventoryComponent, DidUnequipEvent>(OnDidUnequip);
    }

    private void OnGotEquipped(Entity<HideWhenSlotOccupiedComponent> ent, ref GotEquippedEvent args)
    {
        ent.Comp.Wearer = args.Equipee;
        UpdateHiddenState(ent);
    }

    private void OnGotUnequipped(Entity<HideWhenSlotOccupiedComponent> ent, ref GotUnequippedEvent args)
    {
        ent.Comp.Wearer = null;

        if (ent.Comp.IsHidden)
        {
            ent.Comp.IsHidden = false;
            Dirty(ent);
        }
    }

    private void OnDidEquip(Entity<InventoryComponent> ent, ref DidEquipEvent args)
    {
        RefreshClothingForWearer(ent.Owner, args.Slot, args.Equipment);
    }

    private void OnDidUnequip(Entity<InventoryComponent> ent, ref DidUnequipEvent args)
    {
        RefreshClothingForWearer(ent.Owner, args.Slot);
    }

    private void RefreshClothingForWearer(EntityUid wearer, string changedSlot, EntityUid? equipment = null)
    {
        var query = EntityQueryEnumerator<HideWhenSlotOccupiedComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Wearer != wearer)
                continue;

            if (comp.WatchedSlot != changedSlot)
                continue;

            UpdateHiddenState((uid, comp));
        }
    }

    private void UpdateHiddenState(Entity<HideWhenSlotOccupiedComponent> ent)
    {
        if (ent.Comp.Wearer == null)
        {
            if (ent.Comp.IsHidden)
            {
                ent.Comp.IsHidden = false;
                Dirty(ent);
            }
            return;
        }

        var shouldBeHidden = false;

        if (_inventory.TryGetSlotEntity(ent.Comp.Wearer.Value, ent.Comp.WatchedSlot, out var slotEntity))
            shouldBeHidden = !_entityWhitelistSystem.IsWhitelistFail(ent.Comp.Whitelist, slotEntity.Value);

        if (ent.Comp.IsHidden != shouldBeHidden)
        {
            ent.Comp.IsHidden = shouldBeHidden;
            Dirty(ent);
        }
    }
}
