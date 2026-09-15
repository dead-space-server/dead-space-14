namespace Content.Shared.Inventory.Events;

public abstract class BeforeUnequipEventBase : EntityEventArgs
{
    /// <summary>
    /// The entity performing the interaction.
    /// </summary>
    public readonly EntityUid User;

    /// <summary>
    /// The entity whose equipment is being removed.
    /// </summary>
    public readonly EntityUid EquipTarget;

    public readonly EntityUid Equipment;
    public readonly string Slot;
    public readonly string SlotGroup;
    public readonly SlotFlags SlotFlags;

    protected BeforeUnequipEventBase(EntityUid user, EntityUid equipTarget, EntityUid equipment, SlotDefinition slotDefinition)
    {
        User = user;
        EquipTarget = equipTarget;
        Equipment = equipment;
        Slot = slotDefinition.Name;
        SlotGroup = slotDefinition.SlotGroup;
        SlotFlags = slotDefinition.SlotFlags;
    }
}

/// <summary>
/// Raised on an equipee immediately before <see cref="InventorySystem.TryUnequip(EntityUid, string, bool, bool, bool, InventoryComponent?, Clothing.Components.ClothingComponent?, bool, bool, bool)"/>
/// removes an item.
/// </summary>
public sealed class BeforeUnequipEvent : BeforeUnequipEventBase
{
    public BeforeUnequipEvent(EntityUid user, EntityUid equipTarget, EntityUid equipment, SlotDefinition slotDefinition)
        : base(user, equipTarget, equipment, slotDefinition)
    {
    }
}

/// <summary>
/// Raised on equipment immediately before it is unequipped.
/// </summary>
public sealed class BeforeGettingUnequippedEvent : BeforeUnequipEventBase
{
    public BeforeGettingUnequippedEvent(EntityUid user, EntityUid equipTarget, EntityUid equipment, SlotDefinition slotDefinition)
        : base(user, equipTarget, equipment, slotDefinition)
    {
    }
}
