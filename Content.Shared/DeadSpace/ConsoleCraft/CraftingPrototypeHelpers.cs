// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Containers.ItemSlots;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.ConsoleCraft;

public static class CraftingPrototypeHelpers
{
    /// <summary>
    /// Follows the default ammunition without spawning preview guns, magazines or cartridges.
    /// </summary>
    public static EntityPrototype? GetDefaultProjectile(
        EntityPrototype prototype,
        IPrototypeManager prototypes,
        IComponentFactory factory)
    {
        var visited = new HashSet<string>();
        while (visited.Add(prototype.ID))
        {
            string? next;
            if (prototype.TryGetComponent<BatteryAmmoProviderComponent>(out var battery, factory))
                next = battery.Prototype.Id;
            else if (prototype.TryGetComponent<BasicEntityAmmoProviderComponent>(out var basic, factory))
                next = basic.Proto;
            else if (prototype.TryGetComponent<RevolverAmmoProviderComponent>(out var revolver, factory))
                next = revolver.FillPrototype;
            else if (prototype.TryGetComponent<BallisticAmmoProviderComponent>(out var ballistic, factory))
                next = ballistic.Proto?.Id;
            else if (prototype.TryGetComponent<CartridgeAmmoComponent>(out var cartridge, factory))
                next = cartridge.Prototype.Id;
            else if (prototype.TryGetComponent<ChamberMagazineAmmoProviderComponent>(out _, factory) ||
                     prototype.TryGetComponent<MagazineAmmoProviderComponent>(out _, factory))
            {
                if (!prototype.TryGetComponent<ItemSlotsComponent>(out var slots, factory))
                    return null;

                IReadOnlyDictionary<string, ItemSlot> slotDefinitions = slots.Slots;
                next = null;
                if (prototype.TryGetComponent<ChamberMagazineAmmoProviderComponent>(out _, factory) &&
                    slotDefinitions.TryGetValue(SharedGunSystem.ChamberSlot, out var chamber))
                {
                    next = chamber.StartingItem;
                }

                if (string.IsNullOrEmpty(next) &&
                    slotDefinitions.TryGetValue(SharedGunSystem.MagazineSlot, out var magazine))
                {
                    next = magazine.StartingItem;
                }
            }
            else
                return prototype;

            if (string.IsNullOrEmpty(next) || !prototypes.TryIndex<EntityPrototype>(next, out var nextPrototype))
                return null;

            prototype = nextPrototype;
        }

        return null;
    }
}
