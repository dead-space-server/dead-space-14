// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Client._Donate.Emerald;
using Content.Shared._Donate;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Donate.UI;

public sealed partial class DonateShopWindow : EmeraldDefaultWindow
{
    [Dependency] private IUriOpener _url = default!;
    [Dependency] private IEntityManager _entManager = default!;

    private enum Tab
    {
        Profile,
        Calendar,
        Inventory,
        Shop
    }

    private DonateShopState? _state;
    private InventoryState? _inventoryState;
    private EnergyShopState? _energyShopState;
    private DailyCalendarState? _calendarState;

    private string? _currentCategory;
    private string? _currentShopCategory;
    private List<string> _categories = new();
    private List<string> _shopCategories = new();
    private string _searchQuery = "";
    private string _shopSearchQuery = "";

    private bool _shopLoading;
    private bool _calendarLoading;
    private bool _inventoryLoading;
    private bool _isPurchasing;
    private bool _isClaimingReward;
    private bool _showingLootboxOpener;

    private Tab _currentTab = Tab.Profile;
    private Tab _previousTab = Tab.Inventory;

    private EmeraldLevelBar _levelBar = default!;
    private EmeraldBatteryDisplay _batteryDisplay = default!;
    private EmeraldCrystalDisplay _crystalDisplay = default!;
    private EmeraldShopButton _shopButton = default!;

    private Control _topPanel = default!;
    private Control _tabsContainer = default!;

    private EmeraldButton _profileTabButton = default!;
    private EmeraldButton _calendarTabButton = default!;
    private EmeraldButton _inventoryTabButton = default!;
    private EmeraldButton _shopTabButton = default!;

    private BoxContainer _contentContainer = default!;
    private Control _profileContent = default!;
    private Control _calendarContent = default!;
    private Control _inventoryContent = default!;
    private Control _shopContent = default!;

    private BoxContainer _profilePanel = default!;
    private BoxContainer _categoryTabsContainer = default!;
    private BoxContainer _categoryItemsPanel = default!;
    private EmeraldSearchBox _searchBox = default!;
    private GridContainer? _itemsGrid;
    private GridContainer? _perksGrid;

    private BoxContainer _shopCategoryTabsContainer = default!;
    private BoxContainer _shopItemsPanel = default!;
    private EmeraldSearchBox _shopSearchBox = default!;
    private GridContainer? _shopItemsGrid;

    private BoxContainer _calendarPanel = default!;
    private EmeraldLootboxOpener? _lootboxOpener;

    public DonateShopWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = "MK TERMINAL";
        MinSize = new Vector2(816, 817);
        SetSize = new Vector2(816, 817);

        BuildUI();
        ShowLoading();
    }

    private void BuildUI()
    {
        var mainContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 0
        };

        _topPanel = BuildTopPanel();
        mainContainer.AddChild(_topPanel);
        mainContainer.AddChild(new Control { MinHeight = 20 });

        _tabsContainer = BuildTabsContainer();
        mainContainer.AddChild(_tabsContainer);
        mainContainer.AddChild(new Control { MinHeight = 20 });

        _contentContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true
        };
        mainContainer.AddChild(_contentContainer);

        _profileContent = BuildProfileContent();
        _calendarContent = BuildCalendarContent();
        _inventoryContent = BuildInventoryContent();
        _shopContent = BuildShopContent();

        AddContent(mainContainer);
        SwitchTab(Tab.Profile);
    }

    private Control BuildTopPanel()
    {
        var panel = new EmeraldPanel
        {
            HorizontalExpand = true,
            Visible = false,
            Margin = new Thickness(2, 0, 0, 0)
        };

        var mainContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(8, 6),
            SeparationOverride = 6
        };

        var topPanelWrap = new WrapContainer
        {
            HorizontalExpand = true,
            LayoutAxis = Axis.Horizontal,
            SeparationOverride = 10,
            CrossSeparationOverride = 10
        };

        _levelBar = new EmeraldLevelBar
        {
            MinWidth = 200,
            HorizontalExpand = true,
            Margin = new Thickness(0, 4)
        };
        topPanelWrap.AddChild(_levelBar);

        _batteryDisplay = new EmeraldBatteryDisplay
        {
            IconTexturePath = "/Textures/Interface/battery.png",
            MinWidth = 140,
            Margin = new Thickness(0, 2)
        };
        topPanelWrap.AddChild(_batteryDisplay);

        _crystalDisplay = new EmeraldCrystalDisplay
        {
            IconTexturePath = "/Textures/Interface/crystal.png",
            MinWidth = 140,
            Margin = new Thickness(0, 2)
        };
        topPanelWrap.AddChild(_crystalDisplay);

        mainContainer.AddChild(topPanelWrap);

        _shopButton = new EmeraldShopButton
        {
            Text = "МАГАЗИН",
            Margin = new Thickness(0, 2),
            HorizontalAlignment = HAlignment.Center
        };
        _shopButton.OnPressed += () => _url.OpenUri("https://deadspace14.net");
        mainContainer.AddChild(_shopButton);

        panel.AddChild(mainContainer);
        return panel;
    }

    private Control BuildTabsContainer()
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Visible = false,
            SeparationOverride = 10,
            Margin = new Thickness(2, 0, 2, 0)
        };

        _profileTabButton = new EmeraldButton { Text = "ПРОФИЛЬ", HorizontalExpand = true };
        _profileTabButton.OnPressed += () => SwitchTab(Tab.Profile);
        container.AddChild(_profileTabButton);

        _calendarTabButton = new EmeraldButton { Text = "КАЛЕНДАРЬ", HorizontalExpand = true };
        _calendarTabButton.OnPressed += () => SwitchTab(Tab.Calendar);
        container.AddChild(_calendarTabButton);

        _inventoryTabButton = new EmeraldButton { Text = "ИНВЕНТАРЬ", HorizontalExpand = true };
        _inventoryTabButton.OnPressed += () => SwitchTab(Tab.Inventory);
        container.AddChild(_inventoryTabButton);

        _shopTabButton = new EmeraldButton { Text = "ENERGY SHOP", HorizontalExpand = true };
        _shopTabButton.OnPressed += () => SwitchTab(Tab.Shop);
        container.AddChild(_shopTabButton);

        return container;
    }

    private Control BuildProfileContent()
    {
        var scrollContainer = new EmeraldScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true
        };

        _profilePanel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 12,
            Margin = new Thickness(2)
        };

        scrollContainer.SetContent(_profilePanel);
        return scrollContainer;
    }

    private Control BuildCalendarContent()
    {
        var scrollContainer = new EmeraldScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true
        };

        _calendarPanel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 12,
            Margin = new Thickness(2)
        };

        scrollContainer.SetContent(_calendarPanel);
        return scrollContainer;
    }

    private Control BuildInventoryContent()
    {
        var mainContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 10
        };

        _searchBox = new EmeraldSearchBox
        {
            HorizontalExpand = true,
            Placeholder = "ПОИСК...",
            Margin = new Thickness(2, 0, 2, 0)
        };
        _searchBox.OnTextChanged += OnInventorySearchChanged;
        mainContainer.AddChild(_searchBox);

        _categoryTabsContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 5,
            Margin = new Thickness(2, 0, 2, 0)
        };
        mainContainer.AddChild(_categoryTabsContainer);

        var scrollContainer = new EmeraldScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true
        };

        _categoryItemsPanel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 12,
            Margin = new Thickness(2)
        };

        scrollContainer.SetContent(_categoryItemsPanel);
        mainContainer.AddChild(scrollContainer);

        return mainContainer;
    }

    private Control BuildShopContent()
    {
        var mainContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 10
        };

        _shopSearchBox = new EmeraldSearchBox
        {
            HorizontalExpand = true,
            Placeholder = "ПОИСК В МАГАЗИНЕ...",
            Margin = new Thickness(2, 0, 2, 0)
        };
        _shopSearchBox.OnTextChanged += OnShopSearchChanged;
        mainContainer.AddChild(_shopSearchBox);

        _shopCategoryTabsContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 5,
            Margin = new Thickness(2, 0, 2, 0)
        };
        mainContainer.AddChild(_shopCategoryTabsContainer);

        var scrollContainer = new EmeraldScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true
        };

        _shopItemsPanel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 12,
            Margin = new Thickness(2)
        };

        scrollContainer.SetContent(_shopItemsPanel);
        mainContainer.AddChild(scrollContainer);

        return mainContainer;
    }

    private void OnInventorySearchChanged(string text)
    {
        _searchQuery = text.ToLower();
        if (_inventoryState != null && _currentCategory != null)
            RefreshInventoryItems();
    }

    private void OnShopSearchChanged(string text)
    {
        _shopSearchQuery = text.ToLower();
        if (_energyShopState != null && _currentShopCategory != null)
            RefreshShopItems();
    }

    private void SwitchTab(Tab tab)
    {
        if (_showingLootboxOpener)
            return;

        _currentTab = tab;

        _profileTabButton.IsActive = tab == Tab.Profile;
        _calendarTabButton.IsActive = tab == Tab.Calendar;
        _inventoryTabButton.IsActive = tab == Tab.Inventory;
        _shopTabButton.IsActive = tab == Tab.Shop;

        _contentContainer.RemoveAllChildren();

        switch (tab)
        {
            case Tab.Profile:
                _contentContainer.AddChild(_profileContent);
                break;

            case Tab.Calendar:
                _contentContainer.AddChild(_calendarContent);
                if (_calendarState == null && !_calendarLoading)
                    RequestCalendarData();
                break;

            case Tab.Inventory:
                _contentContainer.AddChild(_inventoryContent);
                if (_inventoryState == null && !_inventoryLoading)
                    RequestInventoryData();
                break;

            case Tab.Shop:
                _contentContainer.AddChild(_shopContent);
                if (_energyShopState == null && !_shopLoading)
                    RequestShopData();
                break;
        }
    }

    private void ShowLoading()
    {
        _profilePanel.RemoveAllChildren();
        _profilePanel.AddChild(CreateCenteredMessage("ИДЁТ ЗАГРУЗКА...", "Подождите, пожалуйста"));
    }

    private void ShowCalendarLoading()
    {
        _calendarPanel.RemoveAllChildren();
        _calendarPanel.AddChild(CreateCenteredMessage("ЗАГРУЗКА КАЛЕНДАРЯ..."));
    }

    private void ShowShopLoading()
    {
        _shopItemsPanel.RemoveAllChildren();
        _shopItemsPanel.AddChild(CreateCenteredMessage("ЗАГРУЗКА МАГАЗИНА..."));
    }

    private void ShowInventoryLoading()
    {
        _categoryItemsPanel.RemoveAllChildren();
        _categoryItemsPanel.AddChild(CreateCenteredMessage("ЗАГРУЗКА ИНВЕНТАРЯ..."));
    }

    private Control CreateCenteredMessage(string title, string? subtitle = null, Color? titleColor = null)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            SeparationOverride = 12
        };

        var titleLabel = new EmeraldLabel
        {
            Text = title,
            HorizontalAlignment = HAlignment.Center,
            Alignment = EmeraldLabel.TextAlignment.Center
        };
        if (titleColor.HasValue)
            titleLabel.TextColor = titleColor.Value;
        container.AddChild(titleLabel);

        if (!string.IsNullOrEmpty(subtitle))
        {
            container.AddChild(new EmeraldLabel
            {
                Text = subtitle,
                HorizontalAlignment = HAlignment.Center,
                Alignment = EmeraldLabel.TextAlignment.Center,
                TextColor = Color.FromHex("#6d5a8a")
            });
        }

        return container;
    }

    public void ApplyState(DonateShopState state)
    {
        _state = state;

        if (state.HasError)
        {
            ShowErrorState(state.ErrorMessage);
            return;
        }

        if (!state.IsRegistered)
        {
            ShowRegistrationRequired();
            return;
        }

        _topPanel.Visible = true;
        _tabsContainer.Visible = true;

        RefreshTopPanel();
        RefreshProfile();
    }

    public void ApplyInventoryState(InventoryState state)
    {
        _inventoryState = state;
        _inventoryLoading = false;

        if (state.HasError)
        {
            ShowInventoryError(state.ErrorMessage);
            return;
        }

        RefreshInventoryCategories();
    }

    public void ApplyEnergyShopState(EnergyShopState state)
    {
        _energyShopState = state;
        _shopLoading = false;

        if (state.HasError)
        {
            ShowShopError(state.ErrorMessage);
            return;
        }

        RefreshShopCategories();
    }

    public void ApplyCalendarState(DailyCalendarState state)
    {
        _calendarState = state;
        _calendarLoading = false;

        if (state.HasError)
        {
            ShowCalendarError(state.ErrorMessage);
            return;
        }

        RefreshCalendar();
    }

    private void RefreshTopPanel()
    {
        if (_state == null)
            return;

        _levelBar.Level = _state.Level;
        _levelBar.Experience = _state.Experience;
        _levelBar.RequiredExp = _state.RequiredExp;
        _levelBar.ToNextLevel = _state.ToNextLevel;
        _levelBar.Progress = _state.Progress;

        _batteryDisplay.Amount = (int)_state.Energy;
        _crystalDisplay.Amount = (int)_state.Crystals;
    }

    private void RefreshProfile()
    {
        if (_state == null)
            return;

        _profilePanel.RemoveAllChildren();

        var profileCard = new EmeraldProfileCard
        {
            PlayerName = _state.PlayerUserName.ToUpper(),
            PlayerId = $"ID {_state.Ss14PlayerId}",
            HorizontalExpand = true
        };
        _profilePanel.AddChild(profileCard);

        _perksGrid = new GridContainer
        {
            Columns = CalculatePerkColumns(),
            HorizontalExpand = true,
            HSeparationOverride = 8,
            VSeparationOverride = 8
        };

        var oocColor = _state.OocColor.StartsWith("#") ? _state.OocColor : "#" + _state.OocColor;

        _perksGrid.AddChild(new EmeraldPerkCard
        {
            Title = "ЦВЕТ OOC",
            Value = oocColor,
            ValueColor = Color.FromHex(oocColor)
        });

        _perksGrid.AddChild(new EmeraldPerkCard
        {
            Title = "СЛОТЫ ПЕРСОНАЖЕЙ",
            Value = $"+{_state.ExtraSlots}",
            ValueColor = Color.FromHex("#a589c9")
        });

        _perksGrid.AddChild(new EmeraldPerkCard
        {
            Title = "ПРИОРИТЕТ ВХОДА",
            Value = _state.HavePriorityJoinGame ? "ДА" : "НЕТ",
            ValueColor = _state.HavePriorityJoinGame ? Color.FromHex("#a589c9") : Color.FromHex("#6d5a8a")
        });

        _perksGrid.AddChild(new EmeraldPerkCard
        {
            Title = "ПРИОРИТЕТ АНТАГА",
            Value = _state.HavePriorityAntageGame ? "ДА" : "НЕТ",
            ValueColor = _state.HavePriorityAntageGame ? Color.FromHex("#a589c9") : Color.FromHex("#6d5a8a")
        });

        _perksGrid.AddChild(new EmeraldPerkCard
        {
            Title = "ДОСТУП КО ВСЕМ ДОЛЖНОСТЯМ",
            Value = _state.AllowJob ? "ДА" : "НЕТ",
            ValueColor = _state.AllowJob ? Color.FromHex("#a589c9") : Color.FromHex("#6d5a8a")
        });

        _profilePanel.AddChild(_perksGrid);

        if (_state.CurrentPremium != null)
        {
            _profilePanel.AddChild(new EmeraldPremiumCard
            {
                IsActive = _state.CurrentPremium.Active,
                Level = _state.CurrentPremium.PremiumLevel.Level,
                PremName = _state.CurrentPremium.PremiumLevel.Name.ToUpper(),
                Description = _state.CurrentPremium.PremiumLevel.Description,
                BonusXp = _state.CurrentPremium.PremiumLevel.BonusXp,
                BonusEnergy = _state.CurrentPremium.PremiumLevel.BonusEnergy,
                BonusSlots = _state.CurrentPremium.PremiumLevel.BonusSlots,
                ExpiresIn = _state.CurrentPremium.ExpiresIn,
                HorizontalExpand = true
            });
        }
        else
        {
            var buyPremiumCard = new EmeraldBuyPremiumCard { HorizontalExpand = true };
            buyPremiumCard.OnBuyPressed += () => _url.OpenUri("https://deadspace14.net");
            _profilePanel.AddChild(buyPremiumCard);
        }

        if (_state.AdminRank != null)
        {
            _profilePanel.AddChild(new EmeraldSubscriptionCard
            {
                NameSub = $"[ADMIN] {_state.AdminRank.Name}".ToUpper(),
                Price = "БЕСПЛАТНО",
                Dates = "Навсегда",
                ItemCount = 0,
                IsAdmin = true,
                HorizontalExpand = true
            });
        }

        if (_state.ActiveSubscription != null)
        {
            var sub = _state.ActiveSubscription;
            var isAdmin = sub.Name.StartsWith("[ADMIN]");
            _profilePanel.AddChild(new EmeraldSubscriptionCard
            {
                NameSub = sub.Name.ToUpper(),
                Price = isAdmin ? "БЕСПЛАТНО" : "ПОДПИСКА",
                Dates = isAdmin ? "Навсегда" : $"{sub.StartDate} - {sub.FinishDate}",
                ItemCount = 0,
                IsAdmin = isAdmin,
                HorizontalExpand = true
            });
        }
        else if (_state.AdminRank == null)
        {
            var buySubCard = new EmeraldBuySubscriptionCard { HorizontalExpand = true };
            buySubCard.OnBuyPressed += () => _url.OpenUri("https://deadspace14.net");
            _profilePanel.AddChild(buySubCard);
        }
    }

    private void RefreshInventoryCategories()
    {
        if (_inventoryState == null)
            return;

        _categoryTabsContainer.RemoveAllChildren();

        var allItems = _inventoryState.Items;

        if (allItems.Count == 0)
        {
            _categoryItemsPanel.RemoveAllChildren();
            _categoryItemsPanel.AddChild(new EmeraldLabel
            {
                Text = "НЕТ ПРЕДМЕТОВ",
                TextColor = Color.FromHex("#6d5a8a"),
                Alignment = EmeraldLabel.TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });
            return;
        }

        _categories = allItems.Select(i => i.Category).Distinct().OrderBy(c => c).ToList();

        foreach (var category in _categories)
        {
            var categoryTab = new EmeraldCategoryTab { Text = category.ToUpper() };
            var capturedCategory = category;
            categoryTab.OnPressed += () =>
            {
                _currentCategory = capturedCategory;
                UpdateCategoryTabsState();
                RefreshInventoryItems();
            };
            _categoryTabsContainer.AddChild(categoryTab);
        }

        var targetCategory = _currentCategory != null && _categories.Contains(_currentCategory)
            ? _currentCategory
            : _categories[0];

        _currentCategory = targetCategory;
        UpdateCategoryTabsState();
        RefreshInventoryItems();
    }

    private void RefreshInventoryItems()
    {
        if (_inventoryState == null || _currentCategory == null)
            return;

        _categoryItemsPanel.RemoveAllChildren();

        var categoryItems = _inventoryState.Items
            .Where(i => i.Category == _currentCategory)
            .Where(i => string.IsNullOrEmpty(_searchQuery) || i.Name.ToLower().Contains(_searchQuery))
            .ToList();

        if (categoryItems.Count == 0)
        {
            _categoryItemsPanel.AddChild(new EmeraldLabel
            {
                Text = "НЕТ ПРЕДМЕТОВ В ЭТОЙ КАТЕГОРИИ",
                TextColor = Color.FromHex("#6d5a8a"),
                Alignment = EmeraldLabel.TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });
            return;
        }

        _itemsGrid = new GridContainer
        {
            Columns = CalculateColumns(),
            HorizontalExpand = true,
            VSeparationOverride = 8,
            HSeparationOverride = 8
        };

        foreach (var item in categoryItems)
        {
            var isSubscription = item.Source == "subscription";

            var displayName = (item.IsLootbox && item.StelsHidden) ? "???" : item.Name.ToUpper();
            var itemCard = new EmeraldItemCard
            {
                ItemName = displayName,
                ProtoId = item.ItemIdInGame ?? "",
                TimeFinish = item.TimeFinish,
                TimeAllways = item.TimeAllways,
                IsActive = true,
                IsSpawned = _state?.SpawnedItems.Contains(item.ItemIdInGame ?? "") ?? false,
                IsTimeUp = _state?.IsTimeUp ?? false,
                SourceSubscription = isSubscription ? item.Source : null,
                IsLootbox = item.IsLootbox,
                UserItemId = item.UserItemId,
                StelsHidden = item.StelsHidden
            };

            itemCard.OnSpawnRequest += OnSpawnItemRequest;
            itemCard.OnOpenLootboxRequest += OnOpenLootboxRequest;

            _itemsGrid.AddChild(itemCard);
        }

        _categoryItemsPanel.AddChild(_itemsGrid);
    }

    private void UpdateCategoryTabsState()
    {
        foreach (var child in _categoryTabsContainer.Children)
        {
            if (child is EmeraldCategoryTab tab)
                tab.IsActive = tab.Text == _currentCategory?.ToUpper();
        }
    }

    private void RefreshShopCategories()
    {
        if (_energyShopState == null)
            return;

        _shopCategoryTabsContainer.RemoveAllChildren();

        var ownedItemIds = GetOwnedItemIds();
        var availableItems = _energyShopState.Items
            .Where(i => !i.Owned && !ownedItemIds.Contains(i.ItemIdInGame))
            .ToList();

        if (availableItems.Count == 0)
        {
            _shopItemsPanel.RemoveAllChildren();
            _shopItemsPanel.AddChild(new EmeraldLabel
            {
                Text = "ВСЕ ТОВАРЫ КУПЛЕНЫ!",
                TextColor = Color.FromHex("#00FFAA"),
                Alignment = EmeraldLabel.TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });
            return;
        }

        _shopCategories = availableItems.Select(i => i.Category).Distinct().OrderBy(c => c).ToList();

        foreach (var category in _shopCategories)
        {
            var categoryTab = new EmeraldCategoryTab { Text = category.ToUpper() };
            var capturedCategory = category;
            categoryTab.OnPressed += () =>
            {
                _currentShopCategory = capturedCategory;
                UpdateShopCategoryTabsState();
                RefreshShopItems();
            };
            _shopCategoryTabsContainer.AddChild(categoryTab);
        }

        var targetCategory = _currentShopCategory != null && _shopCategories.Contains(_currentShopCategory)
            ? _currentShopCategory
            : _shopCategories[0];

        _currentShopCategory = targetCategory;
        UpdateShopCategoryTabsState();
        RefreshShopItems();
    }

    private void RefreshShopItems()
    {
        if (_energyShopState == null || _currentShopCategory == null)
            return;

        _shopItemsPanel.RemoveAllChildren();

        var ownedItemIds = GetOwnedItemIds();
        var categoryItems = _energyShopState.Items
            .Where(i => i.Category == _currentShopCategory)
            .Where(i => string.IsNullOrEmpty(_shopSearchQuery) || i.Name.ToLower().Contains(_shopSearchQuery))
            .Where(i => !i.Owned && !ownedItemIds.Contains(i.ItemIdInGame))
            .ToList();

        if (categoryItems.Count == 0)
        {
            _shopItemsPanel.AddChild(new EmeraldLabel
            {
                Text = "НЕТ ТОВАРОВ В ЭТОЙ КАТЕГОРИИ",
                TextColor = Color.FromHex("#6d5a8a"),
                Alignment = EmeraldLabel.TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });
            return;
        }

        _shopItemsGrid = new GridContainer
        {
            Columns = CalculateShopColumns(),
            HorizontalExpand = true,
            VSeparationOverride = 8,
            HSeparationOverride = 8
        };

        foreach (var item in categoryItems)
        {
            var itemCard = new EmeraldShopItemCard
            {
                ItemName = item.Name.ToUpper(),
                ItemId = item.Id,
                ProtoId = item.ItemIdInGame,
                Prices = item.Prices,
                Owned = item.Owned
            };

            itemCard.OnPurchaseRequest += OnPurchaseRequest;
            _shopItemsGrid.AddChild(itemCard);
        }

        _shopItemsPanel.AddChild(_shopItemsGrid);
    }

    private void UpdateShopCategoryTabsState()
    {
        foreach (var child in _shopCategoryTabsContainer.Children)
        {
            if (child is EmeraldCategoryTab tab)
                tab.IsActive = tab.Text == _currentShopCategory?.ToUpper();
        }
    }

    private void RefreshCalendar()
    {
        if (_calendarState == null)
            return;

        _calendarPanel.RemoveAllChildren();

        if (_calendarState.NormalRewards.Count == 0 && _calendarState.PremiumRewards.Count == 0)
        {
            _calendarPanel.AddChild(CreateCenteredMessage(
                "НЕТ АКТИВНЫХ КАЛЕНДАРЕЙ",
                "В данный момент нет доступных ежедневных наград",
                Color.FromHex("#d4a574")
            ));
            return;
        }

        var currentDay = _calendarState.Progress?.CurrentDay ?? 1;
        var totalDays = Math.Max(_calendarState.NormalRewards.Count, _calendarState.PremiumRewards.Count);
        if (totalDays == 0) totalDays = 7;

        var hasPremium = _state?.CurrentPremium?.Active ?? false;

        _calendarPanel.AddChild(new EmeraldCalendarHeader
        {
            CurrentDay = currentDay,
            TotalDays = totalDays,
            IsPremiumTrack = false,
            CalendarName = _calendarState.CalendarName,
            HorizontalExpand = true
        });

        if (_calendarState.NormalPreview?.Today != null)
        {
            _calendarPanel.AddChild(new EmeraldTodayRewardCard
            {
                ItemName = _calendarState.NormalPreview.Today.Item?.Name ?? "Unknown",
                StatusText = _calendarState.NormalPreview.Today.Status == CalendarRewardStatus.Available ? "ДОСТУПНО" : "ПОЛУЧЕНО",
                IsAvailable = _calendarState.NormalPreview.Today.Status == CalendarRewardStatus.Available,
                IsPremium = false,
                HorizontalExpand = true
            });
        }

        var normalGrid = new GridContainer
        {
            Columns = CalculateCalendarColumns(),
            HorizontalExpand = true,
            HSeparationOverride = 6,
            VSeparationOverride = 6
        };

        foreach (var reward in _calendarState.NormalRewards)
        {
            var dayCard = new EmeraldCalendarDayCard
            {
                Day = reward.Day,
                ItemName = reward.Item.Name,
                ProtoId = reward.Item.ItemIdInGame,
                RewardId = reward.RewardId,
                Status = reward.Status,
                IsPremium = false,
                IsCurrentDay = reward.Day == currentDay,
                IsHidden = reward.Item.IsHidden,
                IsLootbox = reward.Item.IsLootbox
            };

            dayCard.OnClaimRequest += OnClaimRewardRequest;
            normalGrid.AddChild(dayCard);
        }

        _calendarPanel.AddChild(normalGrid);
        _calendarPanel.AddChild(new Control { MinHeight = 12 });

        _calendarPanel.AddChild(new EmeraldCalendarHeader
        {
            CurrentDay = currentDay,
            TotalDays = totalDays,
            IsPremiumTrack = true,
            HorizontalExpand = true
        });

        if (!hasPremium)
        {
            var unlockBanner = new EmeraldPremiumUnlockBanner { HorizontalExpand = true };
            unlockBanner.OnBuyPremiumPressed += () => _url.OpenUri("https://deadspace14.net");
            _calendarPanel.AddChild(unlockBanner);
        }

        if (_calendarState.PremiumPreview?.Today != null && hasPremium)
        {
            _calendarPanel.AddChild(new EmeraldTodayRewardCard
            {
                ItemName = _calendarState.PremiumPreview.Today.Item?.Name ?? "Unknown",
                StatusText = _calendarState.PremiumPreview.Today.Status == CalendarRewardStatus.Available ? "ДОСТУПНО" : "ПОЛУЧЕНО",
                IsAvailable = _calendarState.PremiumPreview.Today.Status == CalendarRewardStatus.Available,
                IsPremium = true,
                HorizontalExpand = true
            });
        }

        var premiumGrid = new GridContainer
        {
            Columns = CalculateCalendarColumns(),
            HorizontalExpand = true,
            HSeparationOverride = 6,
            VSeparationOverride = 6
        };

        foreach (var reward in _calendarState.PremiumRewards)
        {
            var dayCard = new EmeraldCalendarDayCard
            {
                Day = reward.Day,
                ItemName = reward.Item.Name,
                ProtoId = reward.Item.ItemIdInGame,
                RewardId = reward.RewardId,
                Status = reward.Status,
                IsPremium = true,
                IsCurrentDay = reward.Day == currentDay,
                IsHidden = reward.Item.IsHidden,
                IsLootbox = reward.Item.IsLootbox,
                IsLocked = !hasPremium
            };

            if (hasPremium)
                dayCard.OnClaimRequest += OnClaimRewardRequest;

            premiumGrid.AddChild(dayCard);
        }

        _calendarPanel.AddChild(premiumGrid);
    }

    private void OnSpawnItemRequest(string protoId)
    {
        _entManager.EntityNetManager.SendSystemNetworkMessage(new DonateShopSpawnEvent(protoId));
    }

    private void OnOpenLootboxRequest(string name, int userItemId, bool stelsHidden)
    {
        OpenLootboxOpener(name, userItemId, stelsHidden);
    }

    private void OnPurchaseRequest(int itemId, PurchasePeriod period)
    {
        if (_isPurchasing)
            return;

        _isPurchasing = true;
        ShowPurchaseProcessing();
        _entManager.EntityNetManager.SendSystemNetworkMessage(new RequestPurchaseEnergyItem(itemId, period));
    }

    private void OnClaimRewardRequest(int rewardId, bool isPremium)
    {
        if (_isClaimingReward)
            return;

        _isClaimingReward = true;
        _entManager.EntityNetManager.SendSystemNetworkMessage(new RequestClaimCalendarReward(rewardId, isPremium));
    }

    private void OpenLootboxOpener(string name, int userItemId, bool stelsHidden)
    {
        _showingLootboxOpener = true;
        _previousTab = _currentTab;

        _contentContainer.RemoveAllChildren();

        SetTabButtonsDisabled(true);

        var openerContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center
        };

        _lootboxOpener = new EmeraldLootboxOpener
        {
            HorizontalExpand = true,
            VerticalExpand = true
        };

        _lootboxOpener.SetLootbox(name, userItemId, stelsHidden);
        _lootboxOpener.OnOpenRequested += (itemId, stels) =>
        {
            _entManager.EntityNetManager.SendSystemNetworkMessage(new RequestOpenLootbox(itemId, stels));
        };
        _lootboxOpener.OnCloseRequested += CloseLootboxOpener;

        openerContainer.AddChild(_lootboxOpener);
        _contentContainer.AddChild(openerContainer);

        if (!stelsHidden)
        {
            _lootboxOpener.OnOpenPressed();
        }
    }

    private void CloseLootboxOpener()
    {
        _showingLootboxOpener = false;
        _lootboxOpener = null;

        SetTabButtonsDisabled(false);

        RequestMainData();
        RequestInventoryData();
        SwitchTab(_previousTab);
    }

    private void SetTabButtonsDisabled(bool disabled)
    {
        _profileTabButton.Disabled = disabled;
        _calendarTabButton.Disabled = disabled;
        _inventoryTabButton.Disabled = disabled;
        _shopTabButton.Disabled = disabled;
    }

    public void HandleLootboxOpenResult(LootboxOpenResult result)
    {
        _lootboxOpener?.HandleOpenResult(result);
    }

    public void ShowClaimResult(ClaimRewardResult result)
    {
        _isClaimingReward = false;

        ShowRewardResultPage(result);
    }

    private void ShowRewardResultPage(ClaimRewardResult result)
    {
        _calendarPanel.RemoveAllChildren();

        var mainContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center
        };

        var rewardDisplay = new EmeraldRewardResultDisplay
        {
            IsSuccess = result.Success,
            Message = result.Message,
            ItemName = result.ClaimedItem?.Name ?? "Unknown",
            ProtoId = result.ClaimedItem?.ItemIdInGame,
            IsPremium = result.IsPremium,
            IsLootbox = result.ClaimedItem?.IsLootbox ?? false,
            HorizontalAlignment = HAlignment.Center
        };

        rewardDisplay.OnClosePressed += () =>
        {
            RequestMainData();
            RequestCalendarData();
            RequestInventoryData();
        };

        mainContainer.AddChild(rewardDisplay);
        _calendarPanel.AddChild(mainContainer);
    }

    public void ShowPurchaseResult(PurchaseResult result)
    {
        _isPurchasing = false;

        _shopItemsPanel.RemoveAllChildren();

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            SeparationOverride = 16
        };

        container.AddChild(new EmeraldLabel
        {
            Text = result.Success ? "ПОКУПКА УСПЕШНА!" : "ОШИБКА ПОКУПКИ",
            TextColor = result.Success ? Color.FromHex("#00FFAA") : Color.FromHex("#ff6b6b"),
            HorizontalAlignment = HAlignment.Center,
            Alignment = EmeraldLabel.TextAlignment.Center
        });

        container.AddChild(new EmeraldLabel
        {
            Text = result.Message,
            HorizontalAlignment = HAlignment.Center,
            Alignment = EmeraldLabel.TextAlignment.Center
        });

        var backButton = new EmeraldButton
        {
            Text = "ВЕРНУТЬСЯ В МАГАЗИН",
            MinSize = new Vector2(220, 40),
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };

        backButton.OnPressed += () =>
        {
            RequestMainData();
            RequestShopData();
            RequestInventoryData();
        };

        container.AddChild(backButton);
        _shopItemsPanel.AddChild(container);
    }

    private void ShowPurchaseProcessing()
    {
        _shopItemsPanel.RemoveAllChildren();
        _shopItemsPanel.AddChild(CreateCenteredMessage("ОБРАБОТКА ПОКУПКИ...", "Подождите, пожалуйста"));
    }

    private void ShowErrorState(string message)
    {
        _topPanel.Visible = false;
        _tabsContainer.Visible = false;

        _profilePanel.RemoveAllChildren();

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            SeparationOverride = 16
        };

        container.AddChild(new EmeraldLabel
        {
            Text = "ОШИБКА",
            HorizontalAlignment = HAlignment.Center,
            Alignment = EmeraldLabel.TextAlignment.Center
        });

        container.AddChild(new EmeraldLabel
        {
            Text = message,
            HorizontalAlignment = HAlignment.Center,
            Alignment = EmeraldLabel.TextAlignment.Center
        });

        var retryButton = new EmeraldButton
        {
            Text = "ПОПРОБОВАТЬ СНОВА",
            MinSize = new Vector2(220, 40),
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };
        retryButton.OnPressed += () =>
        {
            ShowLoading();
            RequestMainData();
        };
        container.AddChild(retryButton);

        _profilePanel.AddChild(container);
    }

    private void ShowRegistrationRequired()
    {
        _topPanel.Visible = false;
        _tabsContainer.Visible = false;

        _profilePanel.RemoveAllChildren();

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            SeparationOverride = 16
        };

        container.AddChild(new EmeraldLabel
        {
            Text = "ТРЕБУЕТСЯ РЕГИСТРАЦИЯ",
            HorizontalAlignment = HAlignment.Center,
            Alignment = EmeraldLabel.TextAlignment.Center
        });

        container.AddChild(new EmeraldLabel
        {
            Text = "Перейдите по ссылке и зарегистрируйтесь в веб ресурсе.\nДля регистрации может потребоваться VPN.",
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center
        });

        var button = new EmeraldButton
        {
            Text = "ПЕРЕЙТИ НА САЙТ",
            MinSize = new Vector2(200, 40),
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };
        button.OnPressed += () => _url.OpenUri("https://deadspace14.net");
        container.AddChild(button);

        _profilePanel.AddChild(container);
    }

    private void ShowCalendarError(string message)
    {
        _calendarPanel.RemoveAllChildren();

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            SeparationOverride = 16
        };

        container.AddChild(new EmeraldLabel
        {
            Text = "ОШИБКА КАЛЕНДАРЯ",
            TextColor = Color.FromHex("#ff6b6b"),
            HorizontalAlignment = HAlignment.Center,
            Alignment = EmeraldLabel.TextAlignment.Center
        });

        container.AddChild(new EmeraldLabel
        {
            Text = message,
            HorizontalAlignment = HAlignment.Center,
            Alignment = EmeraldLabel.TextAlignment.Center
        });

        var retryButton = new EmeraldButton
        {
            Text = "ПОПРОБОВАТЬ СНОВА",
            MinSize = new Vector2(220, 40),
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };
        retryButton.OnPressed += RequestCalendarData;
        container.AddChild(retryButton);

        _calendarPanel.AddChild(container);
    }

    private void ShowShopError(string message)
    {
        _shopItemsPanel.RemoveAllChildren();

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            SeparationOverride = 16
        };

        container.AddChild(new EmeraldLabel
        {
            Text = "ОШИБКА МАГАЗИНА",
            TextColor = Color.FromHex("#ff6b6b"),
            HorizontalAlignment = HAlignment.Center,
            Alignment = EmeraldLabel.TextAlignment.Center
        });

        container.AddChild(new EmeraldLabel
        {
            Text = message,
            HorizontalAlignment = HAlignment.Center,
            Alignment = EmeraldLabel.TextAlignment.Center
        });

        var retryButton = new EmeraldButton
        {
            Text = "ПОПРОБОВАТЬ СНОВА",
            MinSize = new Vector2(220, 40),
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };
        retryButton.OnPressed += RequestShopData;
        container.AddChild(retryButton);

        _shopItemsPanel.AddChild(container);
    }

    private void ShowInventoryError(string message)
    {
        _categoryItemsPanel.RemoveAllChildren();

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            SeparationOverride = 16
        };

        container.AddChild(new EmeraldLabel
        {
            Text = "ОШИБКА ИНВЕНТАРЯ",
            TextColor = Color.FromHex("#ff6b6b"),
            HorizontalAlignment = HAlignment.Center,
            Alignment = EmeraldLabel.TextAlignment.Center
        });

        container.AddChild(new EmeraldLabel
        {
            Text = message,
            HorizontalAlignment = HAlignment.Center,
            Alignment = EmeraldLabel.TextAlignment.Center
        });

        var retryButton = new EmeraldButton
        {
            Text = "ПОПРОБОВАТЬ СНОВА",
            MinSize = new Vector2(220, 40),
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };
        retryButton.OnPressed += RequestInventoryData;
        container.AddChild(retryButton);

        _categoryItemsPanel.AddChild(container);
    }

    private void RequestMainData()
    {
        _entManager.EntityNetManager.SendSystemNetworkMessage(new RequestUpdateDonateShop());
    }

    private void RequestInventoryData()
    {
        _inventoryLoading = true;
        _inventoryState = null;
        ShowInventoryLoading();
        _entManager.EntityNetManager.SendSystemNetworkMessage(new RequestInventory());
    }

    private void RequestCalendarData()
    {
        _calendarLoading = true;
        _calendarState = null;
        ShowCalendarLoading();
        _entManager.EntityNetManager.SendSystemNetworkMessage(new RequestDailyCalendar());
    }

    private void RequestShopData()
    {
        _shopLoading = true;
        _energyShopState = null;
        ShowShopLoading();
        _entManager.EntityNetManager.SendSystemNetworkMessage(new RequestEnergyShopItems());
    }

    private HashSet<string> GetOwnedItemIds()
    {
        var ownedItemIds = new HashSet<string>();

        if (_inventoryState == null)
            return ownedItemIds;

        foreach (var item in _inventoryState.Items)
        {
            if (!string.IsNullOrEmpty(item.ItemIdInGame))
                ownedItemIds.Add(item.ItemIdInGame);
        }

        return ownedItemIds;
    }

    private int CalculateColumns()
    {
        const float itemWidth = 145f;
        const float spacing = 8f;
        const float padding = 20f;

        var availableWidth = Size.X - padding;
        return Math.Max(1, (int)((availableWidth + spacing) / (itemWidth + spacing)));
    }

    private int CalculateShopColumns()
    {
        const float itemWidth = 160f;
        const float spacing = 8f;
        const float padding = 20f;

        var availableWidth = Size.X - padding;
        return Math.Max(1, (int)((availableWidth + spacing) / (itemWidth + spacing)));
    }

    private int CalculatePerkColumns()
    {
        const float perkWidth = 140f;
        const float spacing = 8f;
        const float padding = 20f;

        var availableWidth = Size.X - padding;
        return Math.Max(1, (int)((availableWidth + spacing) / (perkWidth + spacing)));
    }

    private int CalculateCalendarColumns()
    {
        const float cardWidth = 85f;
        const float spacing = 6f;
        const float padding = 20f;

        var availableWidth = Size.X - padding;
        return Math.Max(1, Math.Min(7, (int)((availableWidth + spacing) / (cardWidth + spacing))));
    }

    protected override void Resized()
    {
        base.Resized();

        if (_itemsGrid != null)
            _itemsGrid.Columns = CalculateColumns();

        if (_shopItemsGrid != null)
            _shopItemsGrid.Columns = CalculateShopColumns();

        if (_perksGrid != null)
            _perksGrid.Columns = CalculatePerkColumns();
    }
}
