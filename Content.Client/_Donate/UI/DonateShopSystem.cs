// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared._Donate;
using Robust.Client.UserInterface;

namespace Content.Client._Donate.UI;

public sealed partial class DonateShopSystem : EntitySystem
{
    [Dependency] private IUserInterfaceManager _uiManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<UpdateDonateShopUIState>(OnMainStateUpdate);
        SubscribeNetworkEvent<UpdateInventoryState>(OnInventoryStateUpdate);
        SubscribeNetworkEvent<UpdateEnergyShopState>(OnEnergyShopUpdate);
        SubscribeNetworkEvent<PurchaseEnergyItemResult>(OnPurchaseResult);
        SubscribeNetworkEvent<UpdateDailyCalendarState>(OnCalendarStateUpdate);
        SubscribeNetworkEvent<ClaimCalendarRewardResult>(OnClaimResult);
        SubscribeNetworkEvent<LootboxOpenedResult>(OnLootboxOpenResult);
    }

    private void OnMainStateUpdate(UpdateDonateShopUIState ev)
    {
        var controller = _uiManager.GetUIController<DonateShopUIController>();
        controller.UpdateWindowState(ev.State);
    }

    private void OnInventoryStateUpdate(UpdateInventoryState ev)
    {
        var controller = _uiManager.GetUIController<DonateShopUIController>();
        controller.UpdateInventoryState(ev.State);
    }

    private void OnEnergyShopUpdate(UpdateEnergyShopState ev)
    {
        var controller = _uiManager.GetUIController<DonateShopUIController>();
        controller.UpdateEnergyShopState(ev.State);
    }

    private void OnPurchaseResult(PurchaseEnergyItemResult ev)
    {
        var controller = _uiManager.GetUIController<DonateShopUIController>();
        controller.HandlePurchaseResult(ev.Result);
    }

    private void OnCalendarStateUpdate(UpdateDailyCalendarState ev)
    {
        var controller = _uiManager.GetUIController<DonateShopUIController>();
        controller.UpdateCalendarState(ev.State);
    }

    private void OnClaimResult(ClaimCalendarRewardResult ev)
    {
        var controller = _uiManager.GetUIController<DonateShopUIController>();
        controller.HandleClaimResult(ev.Result);
    }

    private void OnLootboxOpenResult(LootboxOpenedResult ev)
    {
        var controller = _uiManager.GetUIController<DonateShopUIController>();
        controller.HandleLootboxOpenResult(ev.Result);
    }
}
