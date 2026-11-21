// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Shared._Donate;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Donate.UI;

public sealed partial class DonateShopUIController : UIController
{
    [Dependency] private IEntityManager _manager = default!;

    private DonateShopWindow? _window;

    private MenuButton? DonateButton => UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>()?.DonateButton;

    public void UnloadButton()
    {
        if (DonateButton == null)
            return;

        DonateButton.Pressed = false;
        DonateButton.OnPressed -= OnPressed;
    }

    public void LoadButton()
    {
        if (DonateButton == null)
            return;

        DonateButton.OnPressed += OnPressed;
    }

    private void OnPressed(BaseButton.ButtonEventArgs obj)
    {
        ToggleWindow();
    }

    public void ToggleWindow()
    {
        if (_window == null)
        {
            _window = new DonateShopWindow();
            _window.OnClose += OnWindowClosed;
            _window.OpenCentered();
            _manager.EntityNetManager.SendSystemNetworkMessage(new RequestUpdateDonateShop());
            return;
        }

        if (_window.IsOpen)
        {
            _window.Close();
        }
        else
        {
            _window.OpenCentered();
            _manager.EntityNetManager.SendSystemNetworkMessage(new RequestUpdateDonateShop());
        }
    }

    private void OnWindowClosed()
    {
        _window = null;

        if (DonateButton != null)
            DonateButton.Pressed = false;
    }

    public void UpdateWindowState(DonateShopState state)
    {
        _window?.ApplyState(state);
    }

    public void UpdateInventoryState(InventoryState state)
    {
        _window?.ApplyInventoryState(state);
    }

    public void UpdateEnergyShopState(EnergyShopState state)
    {
        _window?.ApplyEnergyShopState(state);
    }

    public void UpdateCalendarState(DailyCalendarState state)
    {
        _window?.ApplyCalendarState(state);
    }

    public void HandlePurchaseResult(PurchaseResult result)
    {
        _window?.ShowPurchaseResult(result);
    }

    public void HandleClaimResult(ClaimRewardResult result)
    {
        _window?.ShowClaimResult(result);
    }

    public void HandleLootboxOpenResult(LootboxOpenResult result)
    {
        _window?.HandleLootboxOpenResult(result);
    }
}
