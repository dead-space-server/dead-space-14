// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.DeadSpace.Maps;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Voting;
using Content.Server.Voting.Managers;
using Content.Shared.DeadSpace.Administration.Events;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Voting;

public sealed partial class AutoMapVoteSystem : EntitySystem
{
    private const int MaxVoteOptions = byte.MaxValue;
    private const string UnknownServerId = "unknown_server_id";

    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IGameMapManager _gameMapManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IVoteManager _voteManager = default!;

    private readonly Dictionary<AutoMapVoteCategory, AutoMapVoteCategoryConfig> _configs = new();
    private readonly Dictionary<AutoMapVoteCategory, HashSet<string>> _playedMaps = new();
    private readonly Dictionary<AutoMapVoteCategory, Queue<string>> _queuedMaps = new();
    private readonly HashSet<string> _blacklistedMaps = new(StringComparer.Ordinal);
    private readonly System.Threading.SemaphoreSlim _databaseSaveSemaphore = new(1, 1);

    private IVoteHandle? _activeVote;
    private AutoMapVoteCategory? _activeVoteCategory;
    private TimeSpan? _activeVoteEndTime;
    private string _blacklistMapsCsv = string.Empty;
    private bool _enabled;
    private bool? _lastReportedVoteActive;
    private bool? _lastReportedVoteBlocked;
    private int _lastHandledRoundId = -1;
    private string _serverId = UnknownServerId;
    private bool _usingDatabaseConfig;
    private int _databaseConfigVersion;
    private int _explicitConfigVersion;
    private int _voteDurationSeconds = 90;

    public override void Initialize()
    {
        base.Initialize();

        foreach (var category in Enum.GetValues<AutoMapVoteCategory>())
        {
            _configs[category] = new AutoMapVoteCategoryConfig();
            _playedMaps[category] = new HashSet<string>(StringComparer.Ordinal);
            _queuedMaps[category] = new Queue<string>();
        }

        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnGameRunLevelChanged);

        Subs.CVar(_config, CCVars.VoteAutoMapEnabled, OnEnabledChanged, true);
        Subs.CVar(_config, CCVars.VoteAutoMapSmallMaxPlayers, value => UpdateCategoryMaxPlayers(AutoMapVoteCategory.Small, value), true);
        Subs.CVar(_config, CCVars.VoteAutoMapMediumMaxPlayers, value => UpdateCategoryMaxPlayers(AutoMapVoteCategory.Medium, value), true);
        Subs.CVar(_config, CCVars.VoteAutoMapLargeMaxPlayers, value => UpdateCategoryMaxPlayers(AutoMapVoteCategory.Large, value), true);
        Subs.CVar(_config, CCVars.VoteAutoMapSmallMaps, value => UpdateCategoryMaps(AutoMapVoteCategory.Small, value), true);
        Subs.CVar(_config, CCVars.VoteAutoMapMediumMaps, value => UpdateCategoryMaps(AutoMapVoteCategory.Medium, value), true);
        Subs.CVar(_config, CCVars.VoteAutoMapLargeMaps, value => UpdateCategoryMaps(AutoMapVoteCategory.Large, value), true);
        Subs.CVar(_config, CCVars.VoteAutoMapBlacklistMaps, UpdateBlacklistMaps, true);
        Subs.CVar(_config, CCVars.VoteAutoMapDuration, OnVoteDurationChanged, true);

        _serverId = GetServerId();
        if (_serverId == UnknownServerId)
        {
            Log.Warning($"Auto map vote uses '{UnknownServerId}' as server.id; multiple servers sharing this database will share one auto map vote config.");
        }

        _ = LoadConfigurationFromDatabaseAsync();
        _adminManager.OnPermsChanged += OnAdminPermsChanged;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_activeVote is not { Finished: false } vote || _activeVoteCategory == null)
        {
            UpdateDerivedAdminState();
            return;
        }

        if (_activeVoteEndTime == null || _timing.CurTime < _activeVoteEndTime.Value)
        {
            UpdateDerivedAdminState();
            return;
        }

        FinishExpiredVote(vote, _activeVoteCategory.Value);
        UpdateDerivedAdminState();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _adminManager.OnPermsChanged -= OnAdminPermsChanged;
    }

    public bool Enabled => _enabled;

    public AutoMapVoteAdminState GetAdminState()
    {
        var availableIds = _gameMapManager
            .CurrentlyEligibleMaps()
            .Select(map => map.ID)
            .Where(id => !_blacklistedMaps.Contains(id))
            .ToHashSet(StringComparer.Ordinal);

        var availableMaps = _gameMapManager
            .AllMaps()
            .OrderBy(map => map.MapName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(map => map.ID, StringComparer.Ordinal)
            .Select(map => new AutoMapVoteMapEntry
            {
                Id = map.ID,
                Name = map.MapName,
                EligibleNow = availableIds.Contains(map.ID),
                Blacklisted = _blacklistedMaps.Contains(map.ID)
            })
            .ToArray();

        return new AutoMapVoteAdminState
        {
            Enabled = _enabled,
            VoteActive = HasActiveVote(),
            VoteBlocked = IsVoteBlocked(),
            CurrentPlayerCount = _playerManager.PlayerCount,
            CurrentCategory = ResolveCategory(_playerManager.PlayerCount),
            VoteDurationSeconds = _voteDurationSeconds,
            AvailableMaps = availableMaps,
            Categories =
            [
                BuildCategoryStatus(AutoMapVoteCategory.Small, availableIds),
                BuildCategoryStatus(AutoMapVoteCategory.Medium, availableIds),
                BuildCategoryStatus(AutoMapVoteCategory.Large, availableIds)
            ],
            Blacklist = BuildBlacklistStatus()
        };
    }

    public async Task<(bool Success, string? Error)> SetEnabledAsync(bool enabled, bool save = true)
    {
        if (save)
        {
            System.Threading.Interlocked.Increment(ref _explicitConfigVersion);
            var saveResult = await TrySaveConfigurationAsync(
                () => BuildConfigRecord(enabled),
                _ =>
                {
                    System.Threading.Interlocked.Increment(ref _databaseConfigVersion);
                    _usingDatabaseConfig = true;
                    ApplyEnabled(enabled, sendAdminState: false);
                });

            if (!saveResult.Success)
                return (false, saveResult.Error);

            SendAdminState();
            return (true, null);
        }

        ApplyEnabled(enabled);
        return (true, null);
    }

    public bool TryInitiateVote([NotNullWhen(false)] out string? error)
    {
        error = null;

        if (_gameTicker.RunLevel != GameRunLevel.PreRoundLobby)
        {
            error = Loc.GetString("auto-map-vote-initiate-error-not-lobby");
            return false;
        }

        if (_activeVote is { Finished: false })
        {
            error = Loc.GetString("auto-map-vote-initiate-error-already-running");
            return false;
        }

        if (!CanInitiateVote(out error))
            return false;

        _lastHandledRoundId = _gameTicker.RoundId;
        StartAutoMapVoteCycleCore();
        SendAdminState();
        return true;
    }

    public async Task<(bool Success, string? Error)> TryApplyConfigurationAsync(
        int smallMaxPlayers,
        int mediumMaxPlayers,
        int largeMaxPlayers,
        string smallMaps,
        string mediumMaps,
        string largeMaps,
        string blacklistMaps,
        int? voteDurationSeconds)
    {
        if (smallMaxPlayers < 0 || mediumMaxPlayers < 0 || largeMaxPlayers < 0)
        {
            return (false, Loc.GetString("auto-map-vote-config-error-negative-player-count"));
        }

        if (voteDurationSeconds != null && voteDurationSeconds.Value <= 0)
        {
            return (false, Loc.GetString("auto-map-vote-config-error-invalid-duration"));
        }

        var normalizedBlacklist = NormalizeMapsCsv(blacklistMaps);
        var blacklistIds = ParseMapIds(normalizedBlacklist).ToHashSet(StringComparer.Ordinal);
        var normalizedSmallMaps = NormalizeMapsCsv(smallMaps, blacklistIds);
        var normalizedMediumMaps = NormalizeMapsCsv(mediumMaps, blacklistIds);
        var normalizedLargeMaps = NormalizeMapsCsv(largeMaps, blacklistIds);
        var normalizedVoteDurationSeconds = voteDurationSeconds ?? _voteDurationSeconds;

        System.Threading.Interlocked.Increment(ref _explicitConfigVersion);
        var saveResult = await TrySaveConfigurationAsync(
            () => BuildConfigRecord(
                _enabled,
                smallMaxPlayers,
                mediumMaxPlayers,
                largeMaxPlayers,
                normalizedSmallMaps,
                normalizedMediumMaps,
                normalizedLargeMaps,
                normalizedBlacklist,
                normalizedVoteDurationSeconds),
            record =>
            {
                System.Threading.Interlocked.Increment(ref _databaseConfigVersion);
                _usingDatabaseConfig = true;
                ApplyConfiguration(record);
            });

        if (!saveResult.Success)
            return (false, saveResult.Error);

        SendAdminState();
        return (true, null);
    }

    private void OnEnabledChanged(bool value)
    {
        if (_usingDatabaseConfig)
            return;

        ApplyEnabled(value);
    }

    private void UpdateCategoryMaxPlayers(AutoMapVoteCategory category, int value)
    {
        if (_usingDatabaseConfig)
            return;

        _configs[category].MaxPlayers = value;
        SendAdminState();
    }

    private void UpdateCategoryMaps(AutoMapVoteCategory category, string value)
    {
        if (_usingDatabaseConfig)
            return;

        _configs[category].MapsCsv = NormalizeMapsCsv(value, _blacklistedMaps);
        SendAdminState();
    }

    private void UpdateBlacklistMaps(string value)
    {
        if (_usingDatabaseConfig)
            return;

        _blacklistMapsCsv = NormalizeMapsCsv(value);
        _blacklistedMaps.Clear();

        foreach (var id in ParseMapIds(_blacklistMapsCsv))
        {
            _blacklistedMaps.Add(id);
        }

        SendAdminState();
    }

    private void OnVoteDurationChanged(int value)
    {
        if (_usingDatabaseConfig)
            return;

        _voteDurationSeconds = value;
        SendAdminState();
    }

    public async Task LoadConfigurationFromDatabaseAsync()
    {
        var configVersion = System.Threading.Volatile.Read(ref _databaseConfigVersion);
        var explicitConfigVersion = System.Threading.Volatile.Read(ref _explicitConfigVersion);

        try
        {
            var record = await _db.GetAutoMapVoteConfigAsync(_serverId);
            if (record == null)
                return;

            if (configVersion != System.Threading.Volatile.Read(ref _databaseConfigVersion) ||
                explicitConfigVersion != System.Threading.Volatile.Read(ref _explicitConfigVersion))
                return;

            _usingDatabaseConfig = true;
            ApplyConfiguration(record);
            SendAdminState();
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load auto map vote database config for server.id '{_serverId}': {e}");
        }
    }

    private async Task<(bool Success, string? Error)> TrySaveConfigurationAsync(
        Func<AutoMapVoteConfigRecord?> buildRecord,
        Action<AutoMapVoteConfigRecord>? afterSave = null)
    {
        await _databaseSaveSemaphore.WaitAsync();
        AutoMapVoteConfigRecord? record = null;

        try
        {
            record = buildRecord();
            if (record == null)
                return (true, null);

            await _db.UpsertAutoMapVoteConfigAsync(record);
            afterSave?.Invoke(record);
            return (true, null);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to save auto map vote database config for server.id '{record?.ServerId ?? _serverId}': {e}");
            return (false, Loc.GetString("auto-map-vote-config-error-db"));
        }
        finally
        {
            _databaseSaveSemaphore.Release();
        }
    }

    private AutoMapVoteConfigRecord BuildConfigRecord(bool enabled)
    {
        return BuildConfigRecord(
            enabled,
            _configs[AutoMapVoteCategory.Small].MaxPlayers,
            _configs[AutoMapVoteCategory.Medium].MaxPlayers,
            _configs[AutoMapVoteCategory.Large].MaxPlayers,
            _configs[AutoMapVoteCategory.Small].MapsCsv,
            _configs[AutoMapVoteCategory.Medium].MapsCsv,
            _configs[AutoMapVoteCategory.Large].MapsCsv,
            _blacklistMapsCsv,
            _voteDurationSeconds);
    }

    private AutoMapVoteConfigRecord BuildConfigRecord(
        bool enabled,
        int smallMaxPlayers,
        int mediumMaxPlayers,
        int largeMaxPlayers,
        string smallMaps,
        string mediumMaps,
        string largeMaps,
        string blacklistMaps,
        int voteDurationSeconds)
    {
        var smallMapsCsv = NormalizeMapsCsv(smallMaps);
        var mediumMapsCsv = NormalizeMapsCsv(mediumMaps);
        var largeMapsCsv = NormalizeMapsCsv(largeMaps);

        return new AutoMapVoteConfigRecord(
            _serverId,
            enabled,
            smallMaxPlayers,
            mediumMaxPlayers,
            largeMaxPlayers,
            smallMapsCsv,
            mediumMapsCsv,
            largeMapsCsv,
            blacklistMaps,
            voteDurationSeconds,
            SerializePlayedMaps(AutoMapVoteCategory.Small, smallMapsCsv),
            SerializePlayedMaps(AutoMapVoteCategory.Medium, mediumMapsCsv),
            SerializePlayedMaps(AutoMapVoteCategory.Large, largeMapsCsv),
            SerializePoolQueueMaps(AutoMapVoteCategory.Small, smallMapsCsv),
            SerializePoolQueueMaps(AutoMapVoteCategory.Medium, mediumMapsCsv),
            SerializePoolQueueMaps(AutoMapVoteCategory.Large, largeMapsCsv));
    }

    private void ApplyConfiguration(AutoMapVoteConfigRecord record)
    {
        var normalizedBlacklist = NormalizeMapsCsv(record.BlacklistMaps);
        var blacklistIds = ParseMapIds(normalizedBlacklist).ToHashSet(StringComparer.Ordinal);

        _configs[AutoMapVoteCategory.Small].MaxPlayers = record.SmallMaxPlayers;
        _configs[AutoMapVoteCategory.Medium].MaxPlayers = record.MediumMaxPlayers;
        _configs[AutoMapVoteCategory.Large].MaxPlayers = record.LargeMaxPlayers;
        _configs[AutoMapVoteCategory.Small].MapsCsv = NormalizeMapsCsv(record.SmallMaps, blacklistIds);
        _configs[AutoMapVoteCategory.Medium].MapsCsv = NormalizeMapsCsv(record.MediumMaps, blacklistIds);
        _configs[AutoMapVoteCategory.Large].MapsCsv = NormalizeMapsCsv(record.LargeMaps, blacklistIds);
        ApplyBlacklistMaps(normalizedBlacklist);
        ApplyPoolState(AutoMapVoteCategory.Small, record.SmallPlayedMaps, record.SmallPoolQueueMaps);
        ApplyPoolState(AutoMapVoteCategory.Medium, record.MediumPlayedMaps, record.MediumPoolQueueMaps);
        ApplyPoolState(AutoMapVoteCategory.Large, record.LargePlayedMaps, record.LargePoolQueueMaps);
        _voteDurationSeconds = record.VoteDurationSeconds;
        ApplyEnabled(record.Enabled, sendAdminState: false);
    }

    private string SerializePlayedMaps(AutoMapVoteCategory category, string mapsCsv)
    {
        var played = _playedMaps[category];
        return string.Join(", ", ParseMapIds(mapsCsv).Where(played.Contains));
    }

    private string SerializePoolQueueMaps(AutoMapVoteCategory category, string mapsCsv)
    {
        var allowedIds = ParseMapIds(mapsCsv).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return string.Join(", ", _queuedMaps[category].Where(id => allowedIds.Contains(id) && seen.Add(id)));
    }

    private void ApplyPoolState(AutoMapVoteCategory category, string playedMaps, string poolQueueMaps)
    {
        var configuredIds = ParseMapIds(_configs[category].MapsCsv).ToHashSet(StringComparer.Ordinal);

        var played = _playedMaps[category];
        played.Clear();
        foreach (var id in ParseMapIds(playedMaps))
        {
            if (configuredIds.Contains(id) && !_blacklistedMaps.Contains(id))
                played.Add(id);
        }

        var queue = _queuedMaps[category];
        queue.Clear();
        var queuedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ParseMapIds(poolQueueMaps))
        {
            if (configuredIds.Contains(id) && !_blacklistedMaps.Contains(id) && queuedIds.Add(id))
                queue.Enqueue(id);
        }
    }

    private void ApplyEnabled(bool value, bool sendAdminState = true)
    {
        _enabled = value;

        if (!value)
        {
            CancelActiveVote();
            _gameMapManager.EndAutoMapVoteOverride();
            _gameTicker.UpdateInfoText();
        }

        if (sendAdminState)
            SendAdminState();
    }

    private void ApplyBlacklistMaps(string value)
    {
        _blacklistMapsCsv = NormalizeMapsCsv(value);
        _blacklistedMaps.Clear();

        foreach (var id in ParseMapIds(_blacklistMapsCsv))
        {
            _blacklistedMaps.Add(id);
        }
    }

    private string GetServerId()
    {
        var serverId = _config.GetCVar(CCVars.ServerId).Trim();
        return string.IsNullOrWhiteSpace(serverId)
            ? UnknownServerId
            : serverId;
    }

    private void SaveRuntimeConfiguration()
    {
        _ = SaveRuntimeConfigurationAsync(System.Threading.Volatile.Read(ref _explicitConfigVersion));
    }

    private async Task SaveRuntimeConfigurationAsync(int explicitConfigVersion)
    {
        var saveResult = await TrySaveConfigurationAsync(
            () => explicitConfigVersion == System.Threading.Volatile.Read(ref _explicitConfigVersion)
                ? BuildConfigRecord(_enabled)
                : null,
            _ =>
            {
                System.Threading.Interlocked.Increment(ref _databaseConfigVersion);
                _usingDatabaseConfig = true;
            });

        if (!saveResult.Success)
            return;
    }

    private void OnAdminPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (!args.IsAdmin)
            return;

        SendAdminState(args.Player);
    }

    private void OnGameRunLevelChanged(GameRunLevelChangedEvent args)
    {
        if (args.New == GameRunLevel.PreRoundLobby && args.Old != GameRunLevel.PreRoundLobby)
        {
            if (!_enabled)
                return;

            Timer.Spawn(0, StartAutoMapVoteCycle);
            return;
        }

        if (args.New != GameRunLevel.PreRoundLobby)
        {
            CancelActiveVote();
            _gameMapManager.EndAutoMapVoteOverride();
            _gameTicker.UpdateInfoText();
        }
    }

    private void StartAutoMapVoteCycle()
    {
        if (!_enabled || _gameTicker.RunLevel != GameRunLevel.PreRoundLobby)
            return;

        if (_lastHandledRoundId == _gameTicker.RoundId)
            return;

        _lastHandledRoundId = _gameTicker.RoundId;
        StartAutoMapVoteCycleCore();
    }

    private void StartAutoMapVoteCycleCore()
    {
        CancelActiveVote();

        if (IsVoteBlocked())
            return;

        _gameMapManager.BeginAutoMapVoteOverride();
        _gameTicker.UpdateInfoText();

        var category = ResolveCategory(_playerManager.PlayerCount);
        var candidates = BuildCandidatePool(category);

        if (candidates.Count == 0)
        {
            ApplyDefaultSelection();
            return;
        }

        var voteDuration = GetVoteDuration();
        var forceWithoutVote =
            candidates.Count == 1 ||
            _playerManager.PlayerCount == 0;

        if (forceWithoutVote)
        {
            var picked = candidates.Count == 1
                ? candidates[0]
                : _random.Pick(candidates);

            ApplySelectedMap(category, picked, announceImmediate: true);
            return;
        }

        CreateAutoVote(category, candidates, voteDuration);
    }

    private void CreateAutoVote(AutoMapVoteCategory category, List<GameMapPrototype> candidates, TimeSpan duration)
    {
        var options = new VoteOptions
        {
            Title = Loc.GetString("ui-vote-map-title"),
            Duration = duration
        };

        options.SetInitiatorOrServer(null);

        foreach (var map in candidates)
        {
            options.Options.Add((map.ID == "CorvaxSpectrum" ? "Spectrum2k" : map.MapName, map));
        }

        _activeVote = _voteManager.CreateVote(options);
        _activeVoteCategory = category;
        _activeVoteEndTime = _timing.CurTime + duration;
        _activeVote.OnFinished += OnAutoVoteFinished;
        _activeVote.OnCancelled += OnAutoVoteCancelled;
        SendAdminState();
    }

    private void OnAutoVoteFinished(IVoteHandle sender, VoteFinishedEventArgs args)
    {
        if (sender != _activeVote || _activeVoteCategory == null)
            return;

        var category = _activeVoteCategory.Value;
        ClearActiveVoteState();
        SendAdminState();

        if (_gameTicker.RunLevel != GameRunLevel.PreRoundLobby || !_gameTicker.CanUpdateMap())
            return;

        var picked = ResolveVoteWinner(args);
        if (picked == null)
            return;

        AnnounceVoteResult(args, picked);
        ApplySelectedMap(category, picked, announceImmediate: false);
    }

    private void OnAutoVoteCancelled(IVoteHandle sender)
    {
        if (sender != _activeVote)
            return;

        ClearActiveVoteState();
        SendAdminState();

        if (_gameTicker.RunLevel != GameRunLevel.PreRoundLobby || !_gameTicker.CanUpdateMap())
            return;

        if (_gameMapManager.GetSelectedMap() != null)
            return;

        ApplyDefaultSelection();
    }

    private void FinishExpiredVote(IVoteHandle vote, AutoMapVoteCategory category)
    {
        var picked = ResolveVoteWinner(vote);
        if (picked == null)
            return;

        CancelActiveVote();

        if (!_gameTicker.CanUpdateMap())
            return;

        AnnounceVoteResult(vote, picked);
        ApplySelectedMap(category, picked, announceImmediate: false);
    }

    private void ApplyDefaultSelection()
    {
        if (!_gameTicker.CanUpdateMap())
            return;

        var fallbackPool = _gameMapManager
            .CurrentlyEligibleMaps()
            .Where(map => !_blacklistedMaps.Contains(map.ID))
            .ToList();

        if (fallbackPool.Count > 0)
        {
            var picked = _random.Pick(fallbackPool);
            _gameMapManager.SelectMap(picked.ID, MapSelectionContext.AutoMapVote);
            _gameTicker.UpdateInfoText();
            _chatManager.DispatchServerAnnouncement(Loc.GetString("auto-map-vote-fallback-selection"));
            return;
        }

        _gameMapManager.SelectMapByConfigRules(MapSelectionContext.AutoMapVote);
        _gameTicker.UpdateInfoText();
        _chatManager.DispatchServerAnnouncement(Loc.GetString("auto-map-vote-fallback-selection"));
    }

    private void ApplySelectedMap(AutoMapVoteCategory category, GameMapPrototype map, bool announceImmediate)
    {
        if (!_gameTicker.CanUpdateMap())
            return;

        if (!_gameMapManager.TrySelectMapIfEligible(map.ID, MapSelectionContext.AutoMapVote))
        {
            ApplyDefaultSelection();
            return;
        }

        MarkMapPlayed(category, map.ID);
        SaveRuntimeConfiguration();
        _gameTicker.UpdateInfoText();

        if (announceImmediate)
        {
            _chatManager.DispatchServerAnnouncement(Loc.GetString("auto-map-vote-selected-immediately", ("winner", map.MapName)));
        }
    }

    private void MarkMapPlayed(AutoMapVoteCategory category, string mapId)
    {
        var played = _playedMaps[category];
        played.Add(mapId);

        var eligibleIds = BuildConfiguredEligiblePool(category)
            .Select(map => map.ID)
            .ToHashSet(StringComparer.Ordinal);

        if (eligibleIds.Count <= 1 || !eligibleIds.IsSubsetOf(played))
            return;

        played.Clear();
        played.Add(mapId);
    }

    private List<GameMapPrototype> BuildCandidatePool(AutoMapVoteCategory category)
    {
        var basePool = BuildConfiguredEligiblePool(category);
        if (basePool.Count == 0)
            return basePool;

        var played = _playedMaps[category];
        var available = basePool
            .Where(map => !played.Contains(map.ID))
            .ToList();

        if (available.Count != 0)
            return TakeCandidateBatch(category, available);

        played.Clear();
        return TakeCandidateBatch(category, basePool);
    }

    private List<GameMapPrototype> TakeCandidateBatch(AutoMapVoteCategory category, List<GameMapPrototype> available)
    {
        SynchronizeCandidateQueue(category, available);

        var queue = _queuedMaps[category];
        var byId = available.ToDictionary(map => map.ID, map => map, StringComparer.Ordinal);
        var count = Math.Min(MaxVoteOptions, queue.Count);
        var result = new List<GameMapPrototype>(count);

        for (var i = 0; i < count; i++)
        {
            var id = queue.Dequeue();
            queue.Enqueue(id);

            if (byId.TryGetValue(id, out var map))
                result.Add(map);
        }

        SaveRuntimeConfiguration();
        return result;
    }

    private void SynchronizeCandidateQueue(AutoMapVoteCategory category, List<GameMapPrototype> available)
    {
        var queue = _queuedMaps[category];
        var availableIds = available
            .Select(map => map.ID)
            .ToHashSet(StringComparer.Ordinal);
        var queuedIds = new HashSet<string>(StringComparer.Ordinal);
        var retained = new List<string>(queue.Count);

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (availableIds.Contains(id) && queuedIds.Add(id))
                retained.Add(id);
        }

        foreach (var map in available)
        {
            if (queuedIds.Add(map.ID))
                retained.Add(map.ID);
        }

        foreach (var id in retained)
        {
            queue.Enqueue(id);
        }
    }

    private List<GameMapPrototype> BuildConfiguredEligiblePool(AutoMapVoteCategory category)
    {
        var configuredMaps = _gameMapManager
            .CurrentlyEligibleMaps()
            .ToDictionary(map => map.ID, map => map, StringComparer.Ordinal);

        var result = new List<GameMapPrototype>();
        foreach (var id in ParseMapIds(_configs[category].MapsCsv))
        {
            if (_blacklistedMaps.Contains(id))
                continue;

            if (configuredMaps.TryGetValue(id, out var map))
                result.Add(map);
        }

        return result;
    }

    private GameMapPrototype? ResolveVoteWinner(IVoteHandle vote)
    {
        var winners = vote.VotesPerOption
            .GroupBy(entry => entry.Value)
            .OrderByDescending(group => group.Key)
            .FirstOrDefault()?
            .Select(entry => (GameMapPrototype) entry.Key)
            .ToArray();

        if (winners == null || winners.Length == 0)
            return null;

        return winners.Length == 1
            ? winners[0]
            : _random.Pick(winners);
    }

    private GameMapPrototype? ResolveVoteWinner(VoteFinishedEventArgs args)
    {
        if (args.Winner != null)
            return (GameMapPrototype) args.Winner;

        return args.Winners.Length == 0
            ? null
            : (GameMapPrototype) _random.Pick(args.Winners);
    }

    private void AnnounceVoteResult(VoteFinishedEventArgs args, GameMapPrototype picked)
    {
        if (args.Winner == null)
        {
            _chatManager.DispatchServerAnnouncement(Loc.GetString("ui-vote-map-tie", ("picked", picked.MapName)));
            return;
        }

        _chatManager.DispatchServerAnnouncement(Loc.GetString("ui-vote-map-win", ("winner", picked.MapName)));
    }

    private void AnnounceVoteResult(IVoteHandle vote, GameMapPrototype picked)
    {
        var maxVotes = vote.VotesPerOption.Values.DefaultIfEmpty(0).Max();
        var winners = vote.VotesPerOption.Count(entry => entry.Value == maxVotes);

        if (winners > 1)
        {
            _chatManager.DispatchServerAnnouncement(Loc.GetString("ui-vote-map-tie", ("picked", picked.MapName)));
            return;
        }

        _chatManager.DispatchServerAnnouncement(Loc.GetString("ui-vote-map-win", ("winner", picked.MapName)));
    }

    private AutoMapVoteCategoryStatus BuildCategoryStatus(AutoMapVoteCategory category, HashSet<string> availableIds)
    {
        var config = _configs[category];

        var unknown = new List<string>();
        var validCount = 0;
        var eligibleCount = 0;
        var configuredIds = ParseMapIds(config.MapsCsv);

        foreach (var id in configuredIds)
        {
            if (!_prototypeManager.TryIndex<GameMapPrototype>(id, out _) || _blacklistedMaps.Contains(id))
            {
                if (!_blacklistedMaps.Contains(id))
                    unknown.Add(id);
                continue;
            }

            validCount++;
            if (availableIds.Contains(id))
                eligibleCount++;
        }

        return new AutoMapVoteCategoryStatus
        {
            Category = category,
            MaxPlayers = config.MaxPlayers,
            MapsCsv = config.MapsCsv,
            ConfiguredMapCount = configuredIds.Count,
            ValidMapCount = validCount,
            EligibleMapCount = eligibleCount,
            UnknownMaps = unknown.ToArray()
        };
    }

    private AutoMapVoteBlacklistStatus BuildBlacklistStatus()
    {
        var unknown = new List<string>();
        var validCount = 0;
        var configuredIds = ParseMapIds(_blacklistMapsCsv);

        foreach (var id in configuredIds)
        {
            if (!_prototypeManager.TryIndex<GameMapPrototype>(id, out _))
            {
                unknown.Add(id);
                continue;
            }

            validCount++;
        }

        return new AutoMapVoteBlacklistStatus
        {
            MapsCsv = _blacklistMapsCsv,
            ConfiguredMapCount = configuredIds.Count,
            ValidMapCount = validCount,
            UnknownMaps = unknown.ToArray()
        };
    }

    private AutoMapVoteCategory ResolveCategory(int playerCount)
    {
        if (playerCount <= _configs[AutoMapVoteCategory.Small].MaxPlayers)
            return AutoMapVoteCategory.Small;

        if (playerCount <= _configs[AutoMapVoteCategory.Medium].MaxPlayers)
            return AutoMapVoteCategory.Medium;

        return AutoMapVoteCategory.Large;
    }

    private TimeSpan GetVoteDuration()
    {
        return TimeSpan.FromSeconds(_voteDurationSeconds);
    }

    private bool CanInitiateVote([NotNullWhen(false)] out string? error)
    {
        error = null;

        if (!_gameTicker.CanUpdateMap())
        {
            error = Loc.GetString("auto-map-vote-initiate-error-map-update-closed");
            return false;
        }

        if (_gameTicker.TimeUntilMapChangeCloses() <= GetVoteDuration())
        {
            error = Loc.GetString("auto-map-vote-initiate-error-blocked");
            return false;
        }

        return true;
    }

    private bool HasActiveVote()
    {
        return _activeVote is { Finished: false };
    }

    private bool IsVoteBlocked()
    {
        if (HasActiveVote())
            return false;

        if (_gameTicker.RunLevel != GameRunLevel.PreRoundLobby)
            return true;

        if (!_gameTicker.CanUpdateMap())
            return true;

        return _gameTicker.TimeUntilMapChangeCloses() <= GetVoteDuration();
    }

    private void UpdateDerivedAdminState()
    {
        var voteActive = HasActiveVote();
        var voteBlocked = IsVoteBlocked();

        if (_lastReportedVoteActive == voteActive && _lastReportedVoteBlocked == voteBlocked)
            return;

        SendAdminState();
    }

    private void CancelActiveVote()
    {
        if (_activeVote is not { Finished: false } vote)
        {
            ClearActiveVoteState();
            return;
        }

        vote.OnFinished -= OnAutoVoteFinished;
        vote.OnCancelled -= OnAutoVoteCancelled;
        vote.Cancel();
        ClearActiveVoteState();
    }

    public void OnForcedMapSelected()
    {
        CancelActiveVote();
        _gameTicker.UpdateInfoText();
    }

    public void OnForcedMapCleared()
    {
        _gameTicker.UpdateInfoText();
    }

    private void ClearActiveVoteState()
    {
        _activeVote = null;
        _activeVoteCategory = null;
        _activeVoteEndTime = null;
    }

    private void SendAdminState(ICommonSession? player = null)
    {
        _lastReportedVoteActive = HasActiveVote();
        _lastReportedVoteBlocked = IsVoteBlocked();
        var ev = new AutoMapVoteAdminStateChangedEvent(GetAdminState());

        if (player != null)
        {
            RaiseNetworkEvent(ev, player.Channel);
            return;
        }

        foreach (var admin in _adminManager.AllAdmins)
        {
            RaiseNetworkEvent(ev, admin);
        }
    }

    private static string NormalizeMapsCsv(string csv, HashSet<string>? excludedIds = null)
    {
        var ids = ParseMapIds(csv);

        if (excludedIds != null && excludedIds.Count > 0)
            ids.RemoveAll(excludedIds.Contains);

        return string.Join(", ", ids);
    }

    private static List<string> ParseMapIds(string csv)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in csv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!seen.Add(entry))
                continue;

            result.Add(entry);
        }

        return result;
    }

    private sealed partial class AutoMapVoteCategoryConfig
    {
        public int MaxPlayers;
        public string MapsCsv = string.Empty;
    }
}
