// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Atmos.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.DeadSpace.EntityConditions.Conditions.Inventory;
using Content.Shared.EntityConditions;
using Content.Shared.Inventory;
using Robust.Shared.Containers;

namespace Content.Server.DeadSpace.EntityConditions.Conditions;

/// <summary>
/// Returns true if a pressure-protective suit is worn with its attached pressure-protective helmet.
/// </summary>
/// <inheritdoc cref="EntityConditionSystem{T, TCondition}"/>
public sealed partial class WearingFullHardsuitEntityConditionSystem : EntityConditionSystem<InventoryComponent, WearingFullHardsuitCondition>
{
    [Dependency] private InventorySystem _inventory = default!;

    protected override void Condition(Entity<InventoryComponent> entity, ref EntityConditionEvent<WearingFullHardsuitCondition> args)
    {
        if (!TryComp<ContainerManagerComponent>(entity, out var containerManager))
            return;

        if (!_inventory.TryGetSlotEntity(entity.Owner, args.Condition.OuterClothingSlot, out var outerClothing, entity.Comp, containerManager) ||
            !_inventory.TryGetSlotEntity(entity.Owner, args.Condition.HeadSlot, out var head, entity.Comp, containerManager))
        {
            return;
        }

        if (!TryComp<ToggleableClothingComponent>(outerClothing.Value, out var toggleableClothing) ||
            toggleableClothing.Slot != args.Condition.HeadSlot ||
            toggleableClothing.ClothingUid != head.Value)
        {
            return;
        }

        if (!TryComp<AttachedClothingComponent>(head.Value, out var attachedClothing) ||
            attachedClothing.AttachedUid != outerClothing.Value)
        {
            return;
        }

        if (!HasComp<PressureProtectionComponent>(outerClothing.Value) ||
            !HasComp<PressureProtectionComponent>(head.Value))
        {
            return;
        }

        args.Result = true;
    }
}
