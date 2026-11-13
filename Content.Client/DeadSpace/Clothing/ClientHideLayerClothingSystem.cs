// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Inventory;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Client.GameObjects;

namespace Content.Client.DeadSpace.Clothing;

public sealed partial class ClientHideLayerClothingSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HideLayerClothingComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<HideLayerClothingComponent, GotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<MaskComponent, ItemMaskToggledEvent>(OnMaskToggled);
        SubscribeLocalEvent<ClothingComponent, EquipmentVisualsUpdatedEvent>(OnEquipmentVisualsUpdated);
    }

    private void OnGotEquipped(Entity<HideLayerClothingComponent> ent, ref GotEquippedEvent args)
    {
        RefreshSlots(args.Equipee);
    }

    private void OnGotUnequipped(Entity<HideLayerClothingComponent> ent, ref GotUnequippedEvent args)
    {
        RefreshSlots(args.Equipee);
    }

    private void OnMaskToggled(Entity<MaskComponent> ent, ref ItemMaskToggledEvent args)
    {
        if (args.Wearer is not { } wearer || !HasComp<HideLayerClothingComponent>(ent))
            return;

        RefreshSlots(wearer);
    }

    private void OnEquipmentVisualsUpdated(Entity<ClothingComponent> ent, ref EquipmentVisualsUpdatedEvent args)
    {
        RefreshSlots(args.Equipee, args.Slot);

        if (!TryComp(ent, out HideLayerClothingComponent? hide))
            return;

        foreach (var hiddenSlot in hide.ClothingSlots)
        {
            RefreshSlots(args.Equipee, hiddenSlot);
        }
    }

    private void RefreshSlots(EntityUid wearer, string? slot = null)
    {
        if (!TryComp(wearer, out InventoryComponent? inventory) ||
            !TryComp(wearer, out InventorySlotsComponent? inventorySlots) ||
            !TryComp(wearer, out SpriteComponent? sprite))
        {
            return;
        }

        if (slot != null)
        {
            RefreshSlot((wearer, inventory, inventorySlots, sprite), slot);
            return;
        }

        var slotsToRefresh = new List<string>(inventorySlots.VisualLayerKeys.Keys);
        var enumerator = _inventory.GetSlotEnumerator((wearer, inventory));
        while (enumerator.NextItem(out var item, out _))
        {
            if (!TryComp(item, out HideLayerClothingComponent? hide))
                continue;

            foreach (var hiddenSlot in hide.ClothingSlots)
            {
                AddUniqueSlot(slotsToRefresh, hiddenSlot);
            }
        }

        foreach (var hiddenSlot in slotsToRefresh)
        {
            RefreshSlot((wearer, inventory, inventorySlots, sprite), hiddenSlot);
        }
    }

    private void RefreshSlot(
        Entity<InventoryComponent, InventorySlotsComponent, SpriteComponent> wearer,
        string slot)
    {
        if (!wearer.Comp2.VisualLayerKeys.TryGetValue(slot, out var layers))
            return;

        var visible = !ShouldHideSlot(wearer, slot);
        foreach (var layerKey in layers)
        {
            if (!_sprite.LayerMapTryGet((wearer.Owner, wearer.Comp3), layerKey, out var layer, false))
                continue;

            _sprite.LayerSetVisible((wearer.Owner, wearer.Comp3), layer, visible);
        }
    }

    private bool ShouldHideSlot(Entity<InventoryComponent, InventorySlotsComponent, SpriteComponent> wearer, string slot)
    {
        var enumerator = _inventory.GetSlotEnumerator((wearer.Owner, wearer.Comp1));
        while (enumerator.NextItem(out var item, out _))
        {
            if (!TryComp(item, out HideLayerClothingComponent? hide) ||
                hide.ClothingSlots.Count == 0 ||
                !ContainsSlot(hide.ClothingSlots, slot))
            {
                continue;
            }

            if (!TryComp(item, out ClothingComponent? clothing) ||
                clothing.InSlotFlag is not { } inSlotFlag ||
                (clothing.Slots & inSlotFlag) == SlotFlags.NONE)
            {
                continue;
            }

            if (!IsEnabled(hide, CompOrNull<MaskComponent>(item)))
                continue;

            return true;
        }

        return false;
    }

    private static bool IsEnabled(HideLayerClothingComponent hide, MaskComponent? mask)
    {
        if (!hide.HideOnToggle)
            return true;

        if (mask == null)
            return true;

        return !mask.IsToggled;
    }

    private static void AddUniqueSlot(List<string> slots, string slot)
    {
        if (!ContainsSlot(slots, slot))
            slots.Add(slot);
    }

    private static bool ContainsSlot(IEnumerable<string> slots, string slot)
    {
        foreach (var candidate in slots)
        {
            if (candidate.Equals(slot, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
