using Content.Shared.Store;
using JetBrains.Annotations;
using System.Linq;
using Content.Shared.Backmen.Store;
using Content.Shared.Store.Components;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Content.Client.DeadSpace.VendingMachines.UI; // DS14
using Content.Shared.DeadSpace.VendingMachines; // DS14
using Content.Shared.FixedPoint; // DS14

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

    // DS14-start
    [ViewVariables]
    private VendingMachineKeypadMenu? _keypadMenu;

    [ViewVariables]
    private readonly List<ListingDataWithCostModifiers> _keypadListings = new();

    [ViewVariables]
    private Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2> _balance = new();
    // DS14-end

    protected override void Open()
    {
        base.Open();

        // DS14-start
        if (EntMan.TryGetComponent(Owner, out VendingMachineKeypadComponent? keypad))
        {
            _keypadMenu = this.CreateWindowCenteredLeft<VendingMachineKeypadMenu>();
            _keypadMenu.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
            _keypadMenu.Configure(keypad.Columns, keypad.SpiralCapacity);
            _keypadMenu.OnCodeApproved += OnKeypadCodeApproved;
            _keypadMenu.OnWithdrawPressed += OnKeypadWithdraw;
            return;
        }
        // DS14-end

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
                // DS14-start
                if (_keypadMenu != null)
                {
                    UpdateKeypad(msg);
                    break;
                }
                // DS14-end
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

    // DS14-start
    private void UpdateKeypad(StoreUpdateState msg)
    {
        if (_keypadMenu == null)
            return;

        _balance = msg.Balance;
        var canBuyFromBank = EntMan.HasComponent<BuyStoreBankComponent>(Owner);
        var available = msg.Listings.ToDictionary(listing => listing.ID);

        for (var i = 0; i < _keypadListings.Count; i++)
        {
            if (available.TryGetValue(_keypadListings[i].ID, out var updated))
                _keypadListings[i] = updated;
        }

        var known = _keypadListings.Select(listing => listing.ID).ToHashSet();
        _keypadListings.AddRange(msg.Listings
            .Where(listing => !known.Contains(listing.ID))
            .OrderByDescending(listing => listing.Priority)
            .ThenBy(listing => ListingLocalisationHelpers.GetLocalisedNameOrEntityName(listing, _prototypeManager)));

        var entries = new List<VendingMachineKeypadEntry>(_keypadListings.Count);
        foreach (var listing in _keypadListings)
        {
            uint? amount = !available.ContainsKey(listing.ID)
                ? 0
                : listing.RemainingStock is { } remaining ? (uint) Math.Max(remaining, 0) : null;

            entries.Add(new VendingMachineKeypadEntry(
                listing.ProductEntity,
                ListingLocalisationHelpers.GetLocalisedNameOrEntityName(listing, _prototypeManager),
                amount,
                GetKeypadPrice(listing),
                canBuyFromBank || listing.CanBuyWith(msg.Balance)));
        }

        _keypadMenu.SetEntries(entries, true);
        _keypadMenu.SetBalance(GetKeypadBalance(msg.Balance));
        _keypadMenu.SetWithdrawVisible(GetWithdrawable().Any());
    }

    private void OnKeypadCodeApproved(int index)
    {
        if (index < 0 || index >= _keypadListings.Count)
            return;

        SendMessage(new StoreBuyListingMessage(_keypadListings[index].ID));
    }

    private void OnKeypadWithdraw()
    {
        foreach (var (currency, amount) in GetWithdrawable())
        {
            SendMessage(new StoreRequestWithdrawMessage(currency, amount));
        }
    }

    private IEnumerable<(string Currency, int Amount)> GetWithdrawable()
    {
        foreach (var (currency, amount) in _balance)
        {
            if (!_prototypeManager.TryIndex(currency, out var proto) || !proto.CanWithdraw || proto.Cash == null)
                continue;

            var whole = amount.Int();
            if (whole > 0)
                yield return (currency.Id, whole);
        }
    }

    private string GetKeypadPrice(ListingDataWithCostModifiers listing)
    {
        if (listing.Cost.Count < 1)
            return Loc.GetString("store-currency-free");

        var text = string.Empty;
        foreach (var (type, amount) in listing.Cost)
        {
            if (!_prototypeManager.TryIndex(type, out var currency))
                continue;

            text += Loc.GetString("store-ui-price-display",
                ("amount", amount),
                ("currency", Loc.GetString(currency.DisplayName, ("amount", amount)))) + " ";
        }

        return text.TrimEnd();
    }

    private string? GetKeypadBalance(Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2> balance)
    {
        var lines = new List<string>();
        foreach (var (type, amount) in balance)
        {
            if (!_prototypeManager.TryIndex(type, out var currency))
                continue;

            lines.Add(Loc.GetString("store-ui-balance-display",
                ("amount", amount),
                ("currency", Loc.GetString(currency.DisplayName, ("amount", 1)))));
        }

        return lines.Count == 0 ? null : string.Join(" ", lines);
    }
    // DS14-end
}
