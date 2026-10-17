using Content.Shared.Inventory;
using Content.Shared.Whitelist;

namespace Content.Shared.DeadSpace.Surgery.Conditions;

public sealed partial class SurgeryConditionEquipment : SurgeryConditionBase<SurgeryConditionEquipment>
{
    [DataField(required: true)]
    public string Slot = string.Empty;

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public SurgeryEntityTarget Target = SurgeryEntityTarget.Surgeon;
}

public sealed partial class SurgeryConditionEquipmentSystem : SurgeryConditionSystem<SurgeryConditionEquipment>
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    protected override void OnCondition(ref SurgeryConditionEvent<SurgeryConditionEquipment> args)
    {
        args.Result = false;

        if (args.Context.GetEntity(args.Condition.Target) is not { } wearer)
            return;

        if (!_inventory.TryGetSlotEntity(wearer, args.Condition.Slot, out var item))
            return;

        if (args.Condition.Whitelist is { } whitelist && !_whitelist.IsValid(whitelist, item.Value))
            return;

        args.Result = true;
    }
}
