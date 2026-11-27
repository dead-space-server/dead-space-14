using Content.Shared.Bed.Cryostorage;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Bed.Cryostorage;

[UsedImplicitly]
public sealed class CryostorageBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private CryostorageMenu? _menu;

    public CryostorageBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<CryostorageMenu>();

        _menu.SlotRemoveButtonPressed += (entry, slot) =>
        {
            SendMessage(new CryostorageRemoveItemBuiMessage(entry, slot, CryostorageRemoveItemBuiMessage.RemovalType.Inventory)); //DS-14
        };

        _menu.HandRemoveButtonPressed += (entry, hand) =>
        {
            SendMessage(new CryostorageRemoveItemBuiMessage(entry, hand, CryostorageRemoveItemBuiMessage.RemovalType.Hand)); //DS-14
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        switch (state)
        {
            case CryostorageBuiState msg:
                _menu?.UpdateState(msg);
                break;
        }
    }
}
