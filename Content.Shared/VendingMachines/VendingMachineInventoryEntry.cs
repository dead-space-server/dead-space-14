using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.VendingMachines;

[Serializable, NetSerializable, DataDefinition]
// DS14-start: the current serialization generator owns the parameterless constructor.
public sealed partial class VendingMachineInventoryEntry
{
    [DataField]
    public InventoryType Type;

    [DataField]
    public EntProtoId ID;

    [DataField]
    public uint Amount;

    public VendingMachineInventoryEntry(InventoryType type, EntProtoId id, uint amount)
    {
        Type = type;
        ID = id;
        Amount = amount;
    }

    public VendingMachineInventoryEntry(VendingMachineInventoryEntry entry) : this(entry.Type, entry.ID, entry.Amount) { }
}
// DS14-end

[Serializable, NetSerializable]
public enum InventoryType : byte
{
    Regular,
    Emagged,
    Contraband
}

[Serializable, NetSerializable]
public sealed class VendingMachineComponentState : ComponentState
{
    public Dictionary<string, VendingMachineInventoryEntry> Inventory = new();

    public Dictionary<string, VendingMachineInventoryEntry> EmaggedInventory = new();

    public Dictionary<string, VendingMachineInventoryEntry> ContrabandInventory = new();

    public bool Contraband;

    public bool Broken;
}
