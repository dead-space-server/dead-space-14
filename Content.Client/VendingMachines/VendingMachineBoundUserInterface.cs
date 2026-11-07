using Content.Shared.VendingMachines;
using Robust.Client.UserInterface;
using System.Linq;
using Content.Shared.VendingMachines.Components;
using Content.Client.DeadSpace.VendingMachines.UI; // DS-14
using Content.Shared.DeadSpace.VendingMachines; // DS-14

namespace Content.Client.VendingMachines;

public sealed class VendingMachineBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private VendingMachineKeypadMenu? _keypadMenu; // DS-14

    [ViewVariables]
    private List<VendingMachineInventoryEntry> _cachedInventory = new();

    protected override void Open()
    {
        base.Open();
        // DS-14-start
        if (!EntMan.TryGetComponent(Owner, out VendingMachineKeypadComponent? keypad))
            keypad = new VendingMachineKeypadComponent();

        _keypadMenu = this.CreateWindowCenteredLeft<VendingMachineKeypadMenu>();
        _keypadMenu.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _keypadMenu.Configure(keypad.Columns, keypad.SpiralCapacity);
        _keypadMenu.OnCodeApproved += OnCodeApproved;
        // DS-14-end
        Refresh();
    }

    public void Refresh()
    {
        var enabled = EntMan.TryGetComponent(Owner, out VendingMachineEjectComponent? eject) && !eject.Ejecting;

        var system = EntMan.System<VendingMachineSystem>();
        _cachedInventory = system.GetAllInventory(Owner);

        _keypadMenu?.Populate(_cachedInventory, enabled); // DS-14
    }

    public void UpdateAmounts()
    {
        var enabled = EntMan.TryGetComponent(Owner, out VendingMachineEjectComponent? eject) && !eject.Ejecting;

        var system = EntMan.System<VendingMachineSystem>();
        _cachedInventory = system.GetAllInventory(Owner);
        _keypadMenu?.UpdateAmounts(_cachedInventory, enabled); // DS-14
    }
    // DS-14-start
    private void OnCodeApproved(int itemIndex)
    {
        var selectedItem = _cachedInventory.ElementAtOrDefault(itemIndex);
        if (selectedItem == null)
            return;

        SendPredictedMessage(new VendingMachineEjectMessage(selectedItem.Type, selectedItem.ID));
    }
    // DS-14-end

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;
        // DS-14-start
        if (_keypadMenu != null)
        {
            _keypadMenu.OnCodeApproved -= OnCodeApproved;
            _keypadMenu.Dispose();
        }
        // DS-14-end
    }
}
