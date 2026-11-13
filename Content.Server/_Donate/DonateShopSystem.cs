// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Content.DeadSpace.Interfaces.Server;
using Content.Server.GameTicking;
using Content.Shared._Donate;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Donate;

public sealed partial class DonateShopSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private ISharedPlayerManager _playMan = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private SharedHandsSystem _handsSystem = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private ActorSystem _actorSystem = default!;

    private readonly ISawmill _sawmill = Logger.GetSawmill("donate.uptime");

    private const bool Testing = false;
    private const string TestUserId = "";

    private readonly ConcurrentDictionary<string, DonateShopState> _playerCache = new();
    private readonly ConcurrentDictionary<string, InventoryState> _inventoryCache = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _spawnedItems = new();
    private readonly ConcurrentDictionary<string, DateTime> _playerEntryTimes = new();
    private readonly List<PendingUptimeSession> _pendingSessions = new();

    private IDonateApiService? _donateApiService;
    private TimeSpan _lastRetryTime = TimeSpan.Zero;

    private record struct PendingUptimeSession(string UserId, DateTime Entry, DateTime Exit);

    private string GetApiUserId(string visitorId) => Testing ? TestUserId : visitorId;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RequestUpdateDonateShop>(OnRequestUpdate);
        SubscribeNetworkEvent<RequestInventory>(OnRequestInventory);
        SubscribeNetworkEvent<DonateShopSpawnEvent>(OnSpawnRequest);
        SubscribeNetworkEvent<RequestEnergyShopItems>(OnRequestEnergyShop);
        SubscribeNetworkEvent<RequestPurchaseEnergyItem>(OnPurchaseEnergyItem);
        SubscribeNetworkEvent<RequestDailyCalendar>(OnRequestDailyCalendar);
        SubscribeNetworkEvent<RequestClaimCalendarReward>(OnClaimCalendarReward);
        SubscribeNetworkEvent<RequestOpenLootbox>(OnOpenLootbox);

        _playMan.PlayerStatusChanged += OnPlayerStatusChanged;

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<StartingGearEquippedEvent>(OnStartingGearEquipped);

        IoCManager.Instance!.TryResolveType(out _donateApiService);

        _sawmill.Info($"DonateShopSystem initialized, API service: {(_donateApiService != null ? "OK" : "NULL")}");
    }

    private async void RunSafe(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception e)
        {
            _sawmill.Error($"Donate async error: {e}");
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingSessions.Count == 0)
            return;

        if (_gameTiming.CurTime - _lastRetryTime < TimeSpan.FromSeconds(60))
            return;

        _lastRetryTime = _gameTiming.CurTime;
        _sawmill.Info($"Retrying {_pendingSessions.Count} pending uptime sessions");

        var toRetry = _pendingSessions.ToList();
        _pendingSessions.Clear();

        foreach (var session in toRetry)
        {
            RunSafe(SendUptimeAsync(session.UserId, session.Entry, session.Exit));
        }
    }

    private void OnStartingGearEquipped(ref StartingGearEquippedEvent ev)
    {
        if (_donateApiService != null && _actorSystem.TryGetSession(ev.Entity, out var session) && session != null)
            _donateApiService.AddSpawnBanTimerForUser(session.UserId.ToString());
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _playerCache.Clear();
        _inventoryCache.Clear();
        _spawnedItems.Clear();
        _donateApiService?.ClearSpawnBanTimer();
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        var visitorId = e.Session.UserId.ToString();

        switch (e.NewStatus)
        {
            case SessionStatus.Connected:
                RunSafe(FetchAndCachePlayerDataAsync(visitorId));
                _playerEntryTimes[visitorId] = DateTime.UtcNow;
                _sawmill.Info($"Player connected: {visitorId}");
                break;

            case SessionStatus.Disconnected:
                _playerCache.TryRemove(visitorId, out _);
                _inventoryCache.TryRemove(visitorId, out _);

                if (_playerEntryTimes.TryGetValue(visitorId, out var entryTime))
                {
                    _playerEntryTimes.TryRemove(visitorId, out _);
                    RunSafe(SendUptimeAsync(visitorId, entryTime, DateTime.UtcNow));
                    _sawmill.Info($"Player disconnected: {visitorId}, sending uptime");
                }
                break;
        }
    }

    private void OnRequestUpdate(RequestUpdateDonateShop msg, EntitySessionEventArgs args)
    {
        RunSafe(HandleRequestUpdateAsync(args.SenderSession));
    }

    private async Task HandleRequestUpdateAsync(ICommonSession session)
    {
        var visitorId = session.UserId.ToString();
        var state = await GetOrFetchPlayerStateAsync(visitorId, session.Name);
        RaiseNetworkEvent(new UpdateDonateShopUIState(state), session.Channel);
    }

    private void OnRequestInventory(RequestInventory msg, EntitySessionEventArgs args)
    {
        RunSafe(HandleRequestInventoryAsync(args.SenderSession));
    }

    private async Task HandleRequestInventoryAsync(ICommonSession session)
    {
        var visitorId = session.UserId.ToString();
        var state = await GetOrFetchInventoryAsync(visitorId);
        RaiseNetworkEvent(new UpdateInventoryState(state), session.Channel);
    }

    private void OnRequestEnergyShop(RequestEnergyShopItems msg, EntitySessionEventArgs args)
    {
        RunSafe(HandleRequestEnergyShopAsync(msg.Page, args.SenderSession));
    }

    private async Task HandleRequestEnergyShopAsync(int page, ICommonSession session)
    {
        if (_donateApiService == null)
        {
            SendEnergyShopError(session, "Сервис недоступен");
            return;
        }

        var state = await _donateApiService.FetchEnergyShopItemsAsync(page);
        RaiseNetworkEvent(new UpdateEnergyShopState(state), session.Channel);
    }

    private void OnPurchaseEnergyItem(RequestPurchaseEnergyItem msg, EntitySessionEventArgs args)
    {
        RunSafe(HandlePurchaseAsync(msg.ItemId, msg.Period, args.SenderSession));
    }

    private async Task HandlePurchaseAsync(int itemId, PurchasePeriod period, ICommonSession session)
    {
        var visitorId = session.UserId.ToString();

        if (_donateApiService == null)
        {
            SendPurchaseResult(session, false, "Сервис недоступен");
            return;
        }

        if (!_playerCache.TryGetValue(visitorId, out var cachedData) || cachedData.User == 0)
        {
            SendPurchaseResult(session, false, "Данные пользователя не загружены");
            return;
        }

        var result = await _donateApiService.PurchaseEnergyItemAsync(cachedData.User, itemId, period);
        RaiseNetworkEvent(new PurchaseEnergyItemResult(result), session.Channel);

        if (result.Success)
        {
            InvalidatePlayerCache(visitorId);
            await SendUpdatedPlayerStateAsync(session);
            await SendUpdatedInventoryAsync(session);
        }
    }

    private void OnRequestDailyCalendar(RequestDailyCalendar msg, EntitySessionEventArgs args)
    {
        RunSafe(HandleRequestCalendarAsync(args.SenderSession));
    }

    private async Task HandleRequestCalendarAsync(ICommonSession session)
    {
        if (_donateApiService == null)
        {
            SendCalendarError(session, "Сервис недоступен");
            return;
        }

        var visitorId = session.UserId.ToString();
        var apiUserId = GetApiUserId(visitorId);
        var state = await _donateApiService.FetchDailyCalendarAsync(apiUserId);

        RaiseNetworkEvent(new UpdateDailyCalendarState(state), session.Channel);
    }

    private void OnClaimCalendarReward(RequestClaimCalendarReward msg, EntitySessionEventArgs args)
    {
        RunSafe(HandleClaimRewardAsync(msg.RewardId, msg.IsPremium, args.SenderSession));
    }

    private async Task HandleClaimRewardAsync(int rewardId, bool isPremium, ICommonSession session)
    {
        if (_donateApiService == null)
        {
            SendClaimResult(session, false, "Сервис недоступен");
            return;
        }

        var visitorId = session.UserId.ToString();
        var apiUserId = GetApiUserId(visitorId);
        var result = await _donateApiService.ClaimCalendarRewardAsync(apiUserId, rewardId);

        RaiseNetworkEvent(new ClaimCalendarRewardResult(result), session.Channel);

        if (result.Success)
        {
            InvalidatePlayerCache(visitorId);
            await SendUpdatedPlayerStateAsync(session);
            await SendUpdatedInventoryAsync(session);
        }
    }

    private void OnOpenLootbox(RequestOpenLootbox msg, EntitySessionEventArgs args)
    {
        RunSafe(HandleOpenLootboxAsync(msg.UserItemId, msg.StelsOpen, args.SenderSession));
    }

    private async Task HandleOpenLootboxAsync(int userItemId, bool stelsOpen, ICommonSession session)
    {
        if (_donateApiService == null)
        {
            SendLootboxResult(session, false, "Сервис недоступен");
            return;
        }

        var visitorId = session.UserId.ToString();
        var apiUserId = GetApiUserId(visitorId);
        var result = await _donateApiService.OpenLootboxAsync(apiUserId, userItemId, stelsOpen);

        RaiseNetworkEvent(new LootboxOpenedResult(result), session.Channel);

        if (result.Success)
        {
            InvalidatePlayerCache(visitorId);
            await SendUpdatedPlayerStateAsync(session);
            await SendUpdatedInventoryAsync(session);
        }
    }

    private void OnSpawnRequest(DonateShopSpawnEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var visitorId = session.UserId.ToString();

        if (!_playerCache.TryGetValue(visitorId, out var state))
            return;

        if (state.SpawnedItems.Contains(msg.ProtoId))
            return;

        if (session.AttachedEntity == null)
            return;

        var playerEntity = session.AttachedEntity.Value;

        if (!HasComp<HumanoidAppearanceComponent>(playerEntity) || !_mobState.IsAlive(playerEntity))
            return;

        if (!_inventoryCache.TryGetValue(visitorId, out var inventory))
            return;

        var item = inventory.Items.FirstOrDefault(i => i.ItemIdInGame == msg.ProtoId);
        if (item == null)
            return;

        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        var playerTransform = Transform(playerEntity);
        var spawnedEntity = Spawn(msg.ProtoId, _transform.GetMapCoordinates(playerTransform));
        _handsSystem.TryPickupAnyHand(playerEntity, spawnedEntity);

        if (!_spawnedItems.ContainsKey(visitorId))
            _spawnedItems[visitorId] = new HashSet<string>();

        _spawnedItems[visitorId].Add(msg.ProtoId);
        state.SpawnedItems.Add(msg.ProtoId);

        RaiseNetworkEvent(new UpdateDonateShopUIState(state), session.Channel);
    }

    private async Task<DonateShopState> GetOrFetchPlayerStateAsync(string visitorId, string playerName)
    {
        if (_playerCache.TryGetValue(visitorId, out var cachedState))
        {
            if (cachedState.PlayerUserName == "Unknown")
                cachedState.PlayerUserName = playerName;
            return cachedState;
        }

        var apiUserId = GetApiUserId(visitorId);
        var state = await FetchPlayerDataAsync(apiUserId);

        if (state.IsRegistered != false)
        {
            if (_spawnedItems.TryGetValue(visitorId, out var spawned))
                state.SpawnedItems = spawned;

            if (state.PlayerUserName == "Unknown")
                state.PlayerUserName = playerName;

            _playerCache[visitorId] = state;
        }

        return state;
    }

    private async Task<InventoryState> GetOrFetchInventoryAsync(string visitorId)
    {
        if (_inventoryCache.TryGetValue(visitorId, out var cachedState))
            return cachedState;

        var apiUserId = GetApiUserId(visitorId);
        var state = await FetchInventoryDataAsync(apiUserId);

        if (!state.HasError)
            _inventoryCache[visitorId] = state;

        return state;
    }

    private async Task FetchAndCachePlayerDataAsync(string visitorId)
    {
        var apiUserId = GetApiUserId(visitorId);
        var data = await FetchPlayerDataAsync(apiUserId);

        if (data.IsRegistered != false)
        {
            if (_spawnedItems.TryGetValue(visitorId, out var spawned))
                data.SpawnedItems = spawned;

            _playerCache[visitorId] = data;
        }

        var inventoryData = await FetchInventoryDataAsync(apiUserId);
        if (!inventoryData.HasError)
            _inventoryCache[visitorId] = inventoryData;
    }

    private async Task<DonateShopState> FetchPlayerDataAsync(string apiUserId)
    {
        if (_donateApiService == null)
            return new DonateShopState("Ведутся технические работы, сервис будет доступен позже.");

        var response = await _donateApiService.FetchUserDataAsync(apiUserId);
        return response ?? new DonateShopState("Ведутся технические работы, сервис будет доступен позже.");
    }

    private async Task<InventoryState> FetchInventoryDataAsync(string apiUserId)
    {
        if (_donateApiService == null)
            return new InventoryState("Ведутся технические работы, сервис будет доступен позже.");

        var response = await _donateApiService.FetchInventoryAsync(apiUserId);
        return response ?? new InventoryState("Ведутся технические работы, сервис будет доступен позже.");
    }

    private async Task SendUpdatedPlayerStateAsync(ICommonSession session)
    {
        var visitorId = session.UserId.ToString();
        var state = await GetOrFetchPlayerStateAsync(visitorId, session.Name);
        RaiseNetworkEvent(new UpdateDonateShopUIState(state), session.Channel);
    }

    private async Task SendUpdatedInventoryAsync(ICommonSession session)
    {
        var visitorId = session.UserId.ToString();
        _inventoryCache.TryRemove(visitorId, out _);
        var state = await GetOrFetchInventoryAsync(visitorId);
        RaiseNetworkEvent(new UpdateInventoryState(state), session.Channel);
    }

    private void InvalidatePlayerCache(string visitorId)
    {
        _playerCache.TryRemove(visitorId, out _);
        _inventoryCache.TryRemove(visitorId, out _);
    }

    private async Task SendUptimeAsync(string visitorId, DateTime entryTime, DateTime exitTime)
    {
        if (_donateApiService == null)
        {
            if (_pendingSessions.Count < 500)
            {
                _sawmill.Warning($"API service is null, queueing for retry: {visitorId}");
                _pendingSessions.Add(new PendingUptimeSession(visitorId, entryTime, exitTime));
            }
            return;
        }

        var apiUserId = GetApiUserId(visitorId);
        var duration = (exitTime - entryTime).TotalMinutes;
        var result = await _donateApiService.SendUptimeAsync(apiUserId, entryTime, exitTime);

        switch (result)
        {
            case UptimeResult.Success:
                _sawmill.Info($"Uptime sent: {visitorId}, duration: {duration:F1} min");
                break;

            case UptimeResult.NotFound:
                _sawmill.Info($"Uptime ignored (404): {visitorId}, duration: {duration:F1} min");
                break;

            case UptimeResult.NeedsRetry:
                if (_pendingSessions.Count < 500)
                {
                    _sawmill.Warning($"Uptime send failed, queueing for retry: {visitorId}, duration: {duration:F1} min");
                    _pendingSessions.Add(new PendingUptimeSession(visitorId, entryTime, exitTime));
                }
                else
                {
                    _sawmill.Warning($"Uptime retry queue full, dropping: {visitorId}");
                }
                break;
        }
    }

    private void SendEnergyShopError(ICommonSession session, string message)
    {
        RaiseNetworkEvent(new UpdateEnergyShopState(new EnergyShopState(message)), session.Channel);
    }

    private void SendCalendarError(ICommonSession session, string message)
    {
        RaiseNetworkEvent(new UpdateDailyCalendarState(new DailyCalendarState(message)), session.Channel);
    }

    private void SendPurchaseResult(ICommonSession session, bool success, string message)
    {
        RaiseNetworkEvent(new PurchaseEnergyItemResult(new PurchaseResult(success, message)), session.Channel);
    }

    private void SendClaimResult(ICommonSession session, bool success, string message)
    {
        RaiseNetworkEvent(new ClaimCalendarRewardResult(new ClaimRewardResult(success, message)), session.Channel);
    }

    private void SendLootboxResult(ICommonSession session, bool success, string message)
    {
        RaiseNetworkEvent(new LootboxOpenedResult(new LootboxOpenResult(success, message)), session.Channel);
    }

    public async Task RefreshPlayerCache(string visitorId)
    {
        await FetchAndCachePlayerDataAsync(visitorId);
    }

    public DonateShopState? GetCachedData(string visitorId)
    {
        return _playerCache.TryGetValue(visitorId, out var data) ? data : null;
    }

    public InventoryState? GetCachedInventory(string visitorId)
    {
        return _inventoryCache.TryGetValue(visitorId, out var data) ? data : null;
    }
}
