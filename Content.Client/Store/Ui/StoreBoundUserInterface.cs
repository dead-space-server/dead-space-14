using Content.Shared.Store;
using JetBrains.Annotations;
using System.Linq;
using Content.Shared.Backmen.Store;
using Content.Shared.Store.Components;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.Store.Ui;

[UsedImplicitly]
public sealed class StoreBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!; // DS14

    [ViewVariables]
    private StoreMenu? _menu;

    [ViewVariables]
    private string _search = string.Empty;

    [ViewVariables]
    private HashSet<ListingDataWithCostModifiers> _listings = new();

    [ViewVariables]
    private HashSet<ProtoId<StoreCategoryPrototype>> _categories = new(); // DS14

    public StoreBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this); // DS14
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<StoreMenu>();
        if (EntMan.TryGetComponent<StoreComponent>(Owner, out var store))
            _menu.Title = Loc.GetString(store.Name);

        _menu.OnListingButtonPressed += (_, listing) =>
        {
            SendMessage(new StoreBuyListingMessage(listing.ID));
        };

        _menu.OnCategoryButtonPressed += (_, category) =>
        {
            // DS14-start
            if (_menu.CurrentCategory == category)
                return;

            _menu.CurrentCategory = category;
            _menu.UpdateListing(resetScroll: true);
            // DS14-end
        };

        _menu.OnWithdrawAttempt += (_, type, amount) =>
        {
            SendMessage(new StoreRequestWithdrawMessage(type, amount));
        };

        _menu.SearchTextUpdated += (_, search) =>
        {
            // DS14-start
            var normalizedSearch = search.Trim().ToLowerInvariant();
            if (_search == normalizedSearch)
                return;

            _search = normalizedSearch;
            UpdateListingsWithSearchFilter(resetScroll: true);
            // DS14-end
        };

        _menu.OnRefundAttempt += (_) =>
        {
            SendMessage(new StoreRequestRefundMessage());
        };
    }
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        switch (state)
        {
            case StoreUpdateState msg:
                // start-backmen: bank
                _menu?.SetCanBuyFromBank(EntMan.HasComponent<BuyStoreBankComponent>(Owner)); // backmen: currency
                // end-backmen: bank

                _listings = msg.Listings;
                _categories = msg.Categories; // DS14

                _menu?.UpdateBalance(msg.Balance);

                UpdateListingsWithSearchFilter(resetScroll: false); // DS14
                _menu?.SetFooterVisibility(msg.ShowFooter);
                _menu?.UpdateRefund(msg.AllowRefund);
                break;
        }
    }

    private void UpdateListingsWithSearchFilter(bool resetScroll) // DS14
    {
        if (_menu == null)
            return;

        var filteredListings = new HashSet<ListingDataWithCostModifiers>(_listings);
        if (!string.IsNullOrEmpty(_search))
        {
            filteredListings.RemoveWhere(listingData => !ListingLocalisationHelpers.GetLocalisedNameOrEntityName(listingData, _prototypeManager).Trim().ToLowerInvariant().Contains(_search) &&
                                                        !ListingLocalisationHelpers.GetLocalisedDescriptionOrEntityDescription(listingData, _prototypeManager).Trim().ToLowerInvariant().Contains(_search));
        }
        // DS14-start
        _menu.PopulateStoreCategoryButtons(filteredListings, _categories);
        _menu.UpdateListing(
            filteredListings.ToList(),
            _listings.Select(listing => listing.ID).ToHashSet(),
            resetScroll);
        // DS14-end
    }
}
