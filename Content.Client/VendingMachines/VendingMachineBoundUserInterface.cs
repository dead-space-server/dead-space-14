using Content.Client.UserInterface.Controls;
using Content.Client.VendingMachines.UI;
using Content.Shared.VendingMachines;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using System.Linq;
using Content.Shared.VendingMachines.Components;
using Content.Client.DeadSpace.VendingMachines.UI; // DS-14
using Content.Shared.DeadSpace.VendingMachines; // DS-14

namespace Content.Client.VendingMachines;

public sealed class VendingMachineBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private VendingMachineMenu? _menu;

    [ViewVariables]
    private VendingMachineKeypadMenu? _keypadMenu; // DS-14

    [ViewVariables]
    private List<VendingMachineInventoryEntry> _cachedInventory = new();

    protected override void Open()
    {
        base.Open();
        // DS-14-start
        var title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;

        if (EntMan.TryGetComponent(Owner, out VendingMachineKeypadComponent? keypad))
        {
            _keypadMenu = this.CreateWindowCenteredLeft<VendingMachineKeypadMenu>();
            _keypadMenu.Title = title;
            _keypadMenu.Configure(keypad.Columns, keypad.SpiralCapacity);
            _keypadMenu.OnCodeApproved += OnCodeApproved;
        }
        else
        {
            _menu = this.CreateWindowCenteredLeft<VendingMachineMenu>();
            _menu.Title = title;
            _menu.OnItemSelected += OnItemSelected;
        }
        // DS-14-end
        Refresh();
    }

    public void Refresh()
    {
        var enabled = EntMan.TryGetComponent(Owner, out VendingMachineEjectComponent? eject) && !eject.Ejecting;

        var system = EntMan.System<VendingMachineSystem>();
        _cachedInventory = system.GetAllInventory(Owner);

        _menu?.Populate(_cachedInventory, enabled);
        _keypadMenu?.Populate(_cachedInventory, enabled); // DS-14
    }

    public void UpdateAmounts()
    {
        var enabled = EntMan.TryGetComponent(Owner, out VendingMachineEjectComponent? eject) && !eject.Ejecting;

        var system = EntMan.System<VendingMachineSystem>();
        _cachedInventory = system.GetAllInventory(Owner);
        _menu?.UpdateAmounts(_cachedInventory, enabled);
        _keypadMenu?.UpdateAmounts(_cachedInventory, enabled); // DS-14
    }

    private void OnItemSelected(GUIBoundKeyEventArgs args, ListData data)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (data is not VendorItemsListData { ItemIndex: var itemIndex })
            return;

        Eject(itemIndex); // DS-14
    }
    // DS-14-start
    private void OnCodeApproved(int itemIndex)
    {
        Eject(itemIndex);
    }

    private void Eject(int itemIndex)
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

        if (_menu != null)
        {
            _menu.OnItemSelected -= OnItemSelected;
            _menu.OnClose -= Close;
            _menu.Dispose();
        }
        // DS-14-start
        if (_keypadMenu != null)
        {
            _keypadMenu.OnCodeApproved -= OnCodeApproved;
            _keypadMenu.Dispose();
        }
        // DS-14-end
    }
}
