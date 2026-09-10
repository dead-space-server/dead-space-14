// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Voting;
using Content.Server.Voting.Managers;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.Administration.GamePreset;
using Content.Shared.GameTicking.Components;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Administration.GamePreset;

public sealed class GamePresetServerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IVoteManager _voteManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;

    private ISawmill _sawmill = default!;

    private readonly List<string> _activePresets = new();
    private readonly List<CustomPresetData> _customPresets = new();
    private int _maxRdmRow;
    private int _voteDurationSeconds = 30;
    private int _currentPresetIndex;
    private int _rdmStreak;
    private bool _enabled = true;
    private bool _loaded;
    private bool _disableOocDuringVote;
    private bool _preventRepeatMode;
    private bool _checkPlayerLimit;
    private string? _lastPickedMode;
    private string? _pendingAppliedMode;
    private List<string> _whitelistModeIds = new();
    private int _activeOurVotesCount;
    private bool _originalOocEnabled;
    private bool _oocStateChangedExternally;
    private bool _ourOocChange;
    private bool _currentPresetProcessed;
    private bool _enableLowPlayerPreset;
    private int _lowPlayerThreshold;
    private string? _lowPlayerPresetId;

    private readonly List<string> _pendingAlerts = new();
    private readonly HashSet<string> _alertedKeys = new();

    private static readonly Regex CamelCaseRegex = new("([a-z])([A-Z])", RegexOptions.Compiled);
    private static readonly Regex UpperCaseRegex = new("([A-Z]+)([A-Z][a-z])", RegexOptions.Compiled);

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("game_preset");

        SubscribeNetworkEvent<RequestGamePresetsMessage>(OnRequestPresets);
        SubscribeNetworkEvent<SetSystemEnabledMessage>(OnSetSystemEnabled);
        SubscribeNetworkEvent<CreateCustomPresetMessage>(OnCreateCustomPreset);
        SubscribeNetworkEvent<UpdateCustomPresetMessage>(OnUpdateCustomPreset);
        SubscribeNetworkEvent<DeleteCustomPresetMessage>(OnDeleteCustomPreset);
        SubscribeNetworkEvent<SetActivePresetsMessage>(OnSetActivePresets);
        SubscribeNetworkEvent<UpdatePresetSettingsMessage>(OnUpdateSettings);
        SubscribeNetworkEvent<InitiateVoteNowMessage>(OnInitiateVoteNow);
        SubscribeNetworkEvent<SkipCurrentPresetMessage>(OnSkipCurrentPreset);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);

        _cfg.OnValueChanged(CCVars.OocEnabled, OnOocChangedExternally);

        _ = LoadFromDatabaseAsync();
    }

    private void OnOocChangedExternally(bool newValue)
    {
        if (_ourOocChange)
            return;

        if (_activeOurVotesCount > 0)
            _oocStateChangedExternally = true;
    }

    private async Task LoadFromDatabaseAsync()
    {
        try
        {
            var record = await _db.GetGamePresetConfigAsync();
            if (record != null)
            {
                _activePresets.Clear();
                _activePresets.AddRange(record.ActivePresetIds);
                _maxRdmRow = record.MaxRdmRow;
                _voteDurationSeconds = record.VoteDurationSeconds > 0 ? record.VoteDurationSeconds : 30;
                _currentPresetIndex = record.CurrentPresetIndex;
                _enabled = record.Enabled;
                _disableOocDuringVote = record.DisableOocDuringVote;
                _preventRepeatMode = record.PreventRepeatMode;
                _checkPlayerLimit = record.CheckPlayerLimit;
                _rdmStreak = 0;
                _currentPresetProcessed = false;
                _enableLowPlayerPreset = record.EnableLowPlayerPreset;
                _lowPlayerThreshold = record.LowPlayerThreshold;
                _lowPlayerPresetId = record.LowPlayerPresetId;

                if (!string.IsNullOrEmpty(record.CustomPresetsJson))
                {
                    var data = JsonSerializer.Deserialize<List<CustomPresetData>>(record.CustomPresetsJson);
                    if (data != null)
                    {
                        _customPresets.Clear();
                        _customPresets.AddRange(data);
                    }
                }

                _whitelistModeIds = string.IsNullOrEmpty(record.WhitelistModesJson)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(record.WhitelistModesJson) ?? new List<string>();
            }
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to load game preset config from database: {ex}");
        }
        finally
        {
            _loaded = true;
        }
    }

    private async Task SaveToDatabaseAsync()
    {
        if (!_loaded)
            return;

        try
        {
            var serverId = _cfg.GetCVar(CCVars.ServerId);
            var record = new GamePresetConfigRecord
            {
                ServerId = serverId,
                Enabled = _enabled,
                ActivePresetIds = new List<string>(_activePresets),
                MaxRdmRow = _maxRdmRow,
                VoteDurationSeconds = _voteDurationSeconds,
                CurrentPresetIndex = _currentPresetIndex,
                DisableOocDuringVote = _disableOocDuringVote,
                PreventRepeatMode = _preventRepeatMode,
                CheckPlayerLimit = _checkPlayerLimit,
                CustomPresetsJson = JsonSerializer.Serialize(_customPresets),
                WhitelistModesJson = JsonSerializer.Serialize(_whitelistModeIds),
                EnableLowPlayerPreset = _enableLowPlayerPreset,
                LowPlayerThreshold = _lowPlayerThreshold,
                LowPlayerPresetId = _lowPlayerPresetId,
            };
            await _db.UpsertGamePresetConfigAsync(record);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to save game preset config to database: {ex}");
        }
    }

    private void OnRequestPresets(RequestGamePresetsMessage msg, EntitySessionEventArgs args)
    {
        if (!_loaded)
            return;

        SendUpdate(args.SenderSession);
    }

    private void OnSetSystemEnabled(SetSystemEnabledMessage msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Server))
            return;

        _enabled = msg.Enabled;
        SendUpdate();
        _ = SaveToDatabaseAsync();
    }

    private void OnCreateCustomPreset(CreateCustomPresetMessage msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Server))
            return;

        var id = $"custom_{_customPresets.Count}_{Guid.NewGuid():N}";
        _customPresets.Add(new CustomPresetData(id, msg.PresetName, msg.Modes, msg.PresetType, msg.Secret));
        SendUpdate();
        _ = SaveToDatabaseAsync();
    }

    private void OnUpdateCustomPreset(UpdateCustomPresetMessage msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Server))
            return;

        var index = _customPresets.FindIndex(p => p.PresetId == msg.PresetId);
        if (index < 0)
            return;

        _customPresets[index] = new CustomPresetData(msg.PresetId, msg.PresetName, msg.Modes, msg.PresetType, msg.Secret);
        SendUpdate();
        _ = SaveToDatabaseAsync();
    }

    private void OnDeleteCustomPreset(DeleteCustomPresetMessage msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Server))
            return;

        _customPresets.RemoveAll(p => p.PresetId == msg.PresetId);
        _activePresets.Remove(msg.PresetId);
        SendUpdate();
        _ = SaveToDatabaseAsync();
    }

    private void OnSetActivePresets(SetActivePresetsMessage msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Server))
            return;

        _activePresets.Clear();
        _activePresets.AddRange(msg.PresetIds);
        if (_activePresets.Count == 0)
            _currentPresetIndex = 0;
        else
            _currentPresetIndex %= _activePresets.Count;
        _currentPresetProcessed = true;
        SendUpdate();
        _ = SaveToDatabaseAsync();
    }

    private void OnUpdateSettings(UpdatePresetSettingsMessage msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Server))
            return;

        _maxRdmRow = msg.MaxRdmRow;
        _voteDurationSeconds = msg.VoteDurationSeconds > 0 ? msg.VoteDurationSeconds : 30;
        _disableOocDuringVote = msg.DisableOocDuringVote;
        _preventRepeatMode = msg.PreventRepeatMode;
        _checkPlayerLimit = msg.CheckPlayerLimit;
        _enableLowPlayerPreset = msg.EnableLowPlayerPreset;
        _lowPlayerThreshold = msg.LowPlayerThreshold;
        _lowPlayerPresetId = msg.LowPlayerPresetId;
        _whitelistModeIds = msg.WhitelistModeIds ?? new List<string>();
        SendUpdate();
        _ = SaveToDatabaseAsync();
    }

    private void OnInitiateVoteNow(InitiateVoteNowMessage msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Server))
            return;

        if (_activePresets.Count == 0)
            return;

        _pendingAlerts.Clear();
        _alertedKeys.Clear();

        var originalIndex = _currentPresetIndex;
        var found = false;
        var attempts = 0;

        while (attempts < _activePresets.Count)
        {
            var candidateId = _activePresets[_currentPresetIndex];

            if (!HasAvailableModes(candidateId, manual: true, ignoreLimits: false))
            {
                var key = $"noplayers:{candidateId}";
                if (_alertedKeys.Add(key))
                    _pendingAlerts.Add(Loc.GetString("game-preset-skipped-no-modes", ("preset", GetPresetDisplayName(candidateId))));
                _currentPresetIndex = (_currentPresetIndex + 1) % _activePresets.Count;
                attempts++;
                continue;
            }

            found = true;
            break;
        }

        if (!found)
        {
            _pendingAlerts.Clear();
            _pendingAlerts.Add(Loc.GetString("game-preset-all-presets-skipped"));
        }

        foreach (var alert in _pendingAlerts)
        {
            _chatManager.SendAdminAlert(alert);
        }

        var presetId = _activePresets[_currentPresetIndex];
        ForceVoteForPreset(presetId, manual: true, ignoreLimits: !found);

        _currentPresetIndex = originalIndex;
        SendUpdate();
    }

    private void OnSkipCurrentPreset(SkipCurrentPresetMessage msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Server))
            return;

        if (_activePresets.Count == 0)
            return;

        _currentPresetIndex = (_currentPresetIndex + 1) % _activePresets.Count;
        _currentPresetProcessed = true;
        _ = SaveToDatabaseAsync();
        SendUpdate();
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New == GameRunLevel.PreRoundLobby && ev.Old != GameRunLevel.PreRoundLobby)
        {
            Timer.Spawn(TimeSpan.FromSeconds(1), StartVoteForNextPreset);
        }
        else if (ev.New == GameRunLevel.InRound)
        {
            if (_pendingAppliedMode != null)
            {
                _lastPickedMode = _pendingAppliedMode;
                _pendingAppliedMode = null;
            }
        }
    }

    private void StartVoteForNextPreset()
    {
        if (!_enabled)
            return;

        if (_activePresets.Count == 0)
        {
            _chatManager.SendAdminAlert(Loc.GetString("game-preset-vote-no-presets"));
            return;
        }

        if (_enableLowPlayerPreset &&
            _playerManager.PlayerCount <= _lowPlayerThreshold &&
            !string.IsNullOrEmpty(_lowPlayerPresetId))
        {
            var lowPresetId = _lowPlayerPresetId;
            _chatManager.SendAdminAlert(
                Loc.GetString("game-preset-low-player-preset",
                    ("preset", GetPresetDisplayName(lowPresetId))));

            if (ForceVoteForPreset(lowPresetId, manual: false))
            {
                _currentPresetProcessed = true;
                _ = SaveToDatabaseAsync();
                SendUpdate();
                return;
            }
        }

        if (_currentPresetProcessed)
        {
            _currentPresetIndex = (_currentPresetIndex + 1) % _activePresets.Count;
            _currentPresetProcessed = false;
            _ = SaveToDatabaseAsync();
        }

        _pendingAlerts.Clear();
        _alertedKeys.Clear();

        var found = false;
        var attempts = 0;

        while (attempts < _activePresets.Count)
        {
            var candidateId = _activePresets[_currentPresetIndex];
            var candidate = _customPresets.FirstOrDefault(p => p.PresetId == candidateId);

            if (_maxRdmRow > 0 && _rdmStreak >= _maxRdmRow)
            {
                if (candidate != null && candidate.PresetType == "rdm")
                {
                    var key = $"skip:{candidateId}";
                    if (_alertedKeys.Add(key))
                        _pendingAlerts.Add(Loc.GetString("game-preset-rdm-skipped", ("preset", candidate.PresetName)));
                    _currentPresetIndex = (_currentPresetIndex + 1) % _activePresets.Count;
                    attempts++;
                    continue;
                }

                if (candidate != null && candidate.PresetType == "democracy")
                {
                    var filtered = FilterRdmSubPresets(candidate, suppressAlerts: true);
                    if (filtered.Count == 0)
                    {
                        var key = $"skip:{candidateId}";
                        if (_alertedKeys.Add(key))
                            _pendingAlerts.Add(Loc.GetString("game-preset-democracy-all-rdm-skipped", ("preset", candidate.PresetName)));
                        _currentPresetIndex = (_currentPresetIndex + 1) % _activePresets.Count;
                        attempts++;
                        continue;
                    }
                }
            }

            if (!HasAvailableModes(candidateId, manual: false, ignoreLimits: false))
            {
                var key = $"noplayers:{candidateId}";
                if (_alertedKeys.Add(key))
                    _pendingAlerts.Add(Loc.GetString("game-preset-skipped-no-modes", ("preset", GetPresetDisplayName(candidateId))));
                _currentPresetIndex = (_currentPresetIndex + 1) % _activePresets.Count;
                attempts++;
                continue;
            }

            found = true;
            break;
        }

        if (!found)
        {
            _pendingAlerts.Clear();
            _pendingAlerts.Add(Loc.GetString("game-preset-all-presets-skipped"));
        }

        foreach (var alert in _pendingAlerts)
        {
            _chatManager.SendAdminAlert(alert);
        }

        var presetId = _activePresets[_currentPresetIndex];
        if (ForceVoteForPreset(presetId, manual: false, ignoreLimits: !found))
        {
            _currentPresetProcessed = true;
            _ = SaveToDatabaseAsync();
        }

        SendUpdate();
    }

    private bool HasAvailableModes(string presetId, bool manual, bool ignoreLimits)
    {
        return HasAvailableModes(presetId, manual, ignoreLimits, new HashSet<string>());
    }

    private bool HasAvailableModes(
        string presetId,
        bool manual,
        bool ignoreLimits,
        HashSet<string> visited)
    {
        var custom = _customPresets.FirstOrDefault(p => p.PresetId == presetId);
        if (custom != null && custom.PresetType == "democracy")
        {
            if (!visited.Add(presetId))
                return false;

            var hasAvailableModes = GetDemocracySubPresets(
                custom,
                manual,
                ignoreLimits,
                suppressAlerts: true,
                visited: visited).Count > 0;
            visited.Remove(presetId);
            return hasAvailableModes;
        }

        return GetFilteredPresetModes(presetId, ignoreLimits, suppressAlerts: true).Count > 0;
    }

    private bool IsPresetEntirelyRdm(CustomPresetData preset)
    {
        return IsPresetEntirelyRdm(preset, new HashSet<string>());
    }

    private bool IsPresetEntirelyRdm(CustomPresetData preset, HashSet<string> visited)
    {
        if (!visited.Add(preset.PresetId))
            return false;

        foreach (var subId in preset.Modes)
        {
            var subPreset = _customPresets.FirstOrDefault(p => p.PresetId == subId);
            if (subPreset == null)
            {
                visited.Remove(preset.PresetId);
                return false;
            }
            if (subPreset.PresetType == "rdm")
                continue;
            if (subPreset.PresetType == "democracy" && IsPresetEntirelyRdm(subPreset, visited))
                continue;
            visited.Remove(preset.PresetId);
            return false;
        }

        visited.Remove(preset.PresetId);
        return true;
    }

    private List<string> FilterRdmSubPresets(CustomPresetData democracyPreset, bool suppressAlerts = false)
    {
        return FilterRdmSubPresets(democracyPreset, suppressAlerts, new HashSet<string>());
    }

    private List<string> FilterRdmSubPresets(
        CustomPresetData democracyPreset,
        bool suppressAlerts,
        HashSet<string> visited)
    {
        var filtered = new List<string>();
        if (!visited.Add(democracyPreset.PresetId))
            return filtered;

        foreach (var subId in democracyPreset.Modes)
        {
            var subPreset = _customPresets.FirstOrDefault(p => p.PresetId == subId);
            if (subPreset == null)
            {
                filtered.Add(subId);
                continue;
            }

            if (subPreset.PresetType == "rdm")
            {
                if (!suppressAlerts)
                {
                    var key = $"remove:{subId}:{democracyPreset.PresetId}";
                    if (_alertedKeys.Add(key))
                        _pendingAlerts.Add(Loc.GetString("game-preset-democracy-rdm-removed", ("subpreset", subPreset.PresetName), ("parent", democracyPreset.PresetName)));
                }
                continue;
            }

            if (subPreset.PresetType == "democracy")
            {
                if (IsPresetEntirelyRdm(subPreset))
                {
                    if (!suppressAlerts)
                    {
                        var key = $"nested:{subId}:{democracyPreset.PresetId}";
                        if (_alertedKeys.Add(key))
                            _pendingAlerts.Add(Loc.GetString("game-preset-democracy-nested-all-rdm-removed", ("subpreset", subPreset.PresetName), ("parent", democracyPreset.PresetName)));
                    }
                    continue;
                }
                var nestedFiltered = FilterRdmSubPresets(subPreset, suppressAlerts, visited);
                if (nestedFiltered.Count == 0)
                {
                    if (!suppressAlerts)
                    {
                        var key = $"nested:{subId}:{democracyPreset.PresetId}";
                        if (_alertedKeys.Add(key))
                            _pendingAlerts.Add(Loc.GetString("game-preset-democracy-nested-all-rdm-removed", ("subpreset", subPreset.PresetName), ("parent", democracyPreset.PresetName)));
                    }
                    continue;
                }
                filtered.AddRange(nestedFiltered);
                continue;
            }

            filtered.Add(subId);
        }

        visited.Remove(democracyPreset.PresetId);
        return filtered;
    }

    private bool IsModePlayable(string modeId)
    {
        return IsModePlayable(modeId, new HashSet<string>());
    }

    private bool IsModePlayable(string modeId, HashSet<string> visited)
    {
        if (!_checkPlayerLimit)
            return true;

        var playerCount = _playerManager.PlayerCount;

        if (_prototypeManager.TryIndex<EntityPrototype>(modeId, out var entityProto))
        {
            if (entityProto.TryGetComponent<GameRuleComponent>(out var gameRule, _componentFactory))
            {
                if (gameRule.MinPlayers > playerCount)
                    return false;
            }
            return true;
        }

        if (_prototypeManager.TryIndex<GamePresetPrototype>(modeId, out var presetProto))
        {
            if (presetProto.MinPlayers > playerCount)
                return false;

            foreach (var rule in presetProto.Rules)
            {
                if (_prototypeManager.TryIndex<EntityPrototype>(rule.Id, out var ruleEntity))
                {
                    if (ruleEntity.TryGetComponent<GameRuleComponent>(out var ruleGameRule, _componentFactory))
                    {
                        if (ruleGameRule.MinPlayers > playerCount)
                            return false;
                    }
                }
            }
            return true;
        }

        var custom = _customPresets.FirstOrDefault(p => p.PresetId == modeId);
        if (custom != null)
        {
            if (!visited.Add(modeId))
                return false;

            var playable = custom.Modes.Any(m => IsModePlayable(m, visited));
            visited.Remove(modeId);
            return playable;
        }

        return true;
    }

    private bool IsPreset(string id)
    {
        if (_customPresets.Any(p => p.PresetId == id))
            return true;

        if (_prototypeManager.TryIndex<GamePresetPrototype>(id, out _))
        {
            if (_prototypeManager.TryIndex<EntityPrototype>(id, out var entity) &&
                entity.TryGetComponent<GameRuleComponent>(out _, _componentFactory))
            {
                return false;
            }
            return true;
        }

        return false;
    }

    private List<string> FilterModesByPlayerLimit(List<string> modes, bool suppressAlerts = false)
    {
        if (!_checkPlayerLimit)
            return modes;

        var filtered = new List<string>();
        var removedPresets = new List<string>();
        var removedModes = new List<string>();

        foreach (var modeId in modes)
        {
            if (IsModePlayable(modeId))
            {
                filtered.Add(modeId);
            }
            else
            {
                if (IsPreset(modeId))
                    removedPresets.Add(modeId);
                else
                    removedModes.Add(modeId);
            }
        }

        if (!suppressAlerts)
        {
            if (removedPresets.Count == 1)
            {
                _chatManager.SendAdminAlert(Loc.GetString("game-preset-player-limit-preset-removed-single", ("preset", GetPresetDisplayName(removedPresets[0]))));
            }
            else if (removedPresets.Count > 1)
            {
                var names = removedPresets.Select(id => GetPresetDisplayName(id));
                var list = string.Join(", ", names);
                _chatManager.SendAdminAlert(Loc.GetString("game-preset-player-limit-preset-removed-multiple", ("presets", list)));
            }

            if (removedModes.Count == 1)
            {
                _chatManager.SendAdminAlert(Loc.GetString("game-preset-player-limit-removed-single", ("mode", GetModeDisplayName(removedModes[0]))));
            }
            else if (removedModes.Count > 1)
            {
                var names = removedModes.Select(id => GetModeDisplayName(id));
                var list = string.Join(", ", names);
                _chatManager.SendAdminAlert(Loc.GetString("game-preset-player-limit-removed-multiple", ("modes", list)));
            }
        }

        return filtered;
    }

    private List<string> GetDemocracySubPresets(
        CustomPresetData democracyPreset,
        bool manual,
        bool ignoreLimits,
        bool suppressAlerts = false)
    {
        return GetDemocracySubPresets(
            democracyPreset,
            manual,
            ignoreLimits,
            suppressAlerts,
            new HashSet<string> { democracyPreset.PresetId });
    }

    private List<string> GetDemocracySubPresets(
        CustomPresetData democracyPreset,
        bool manual,
        bool ignoreLimits,
        bool suppressAlerts,
        HashSet<string> visited)
    {
        var subPresetIds = !manual &&
                           !ignoreLimits &&
                           _maxRdmRow > 0 &&
                           _rdmStreak >= _maxRdmRow
            ? FilterRdmSubPresets(democracyPreset, suppressAlerts)
            : democracyPreset.Modes.ToList();

        if (!ignoreLimits)
            subPresetIds = FilterModesByPlayerLimit(subPresetIds, suppressAlerts);

        return subPresetIds
            .Where(id => HasAvailableModes(id, manual, ignoreLimits, visited))
            .ToList();
    }

    private List<string> GetFilteredPresetModes(
        string presetId,
        bool ignoreLimits,
        bool suppressAlerts = false)
    {
        var modes = GetPresetModes(presetId);
        if (!ignoreLimits && _preventRepeatMode && _lastPickedMode != null)
            modes = modes.Where(IsModeRepeatAllowed).ToList();

        if (!ignoreLimits)
            modes = FilterModesByPlayerLimit(modes, suppressAlerts);

        return modes;
    }

    private bool IsModeRepeatAllowed(string modeId)
    {
        return modeId != _lastPickedMode || _whitelistModeIds.Contains(modeId);
    }

    private bool ForceVoteForPreset(string presetId, bool manual, bool ignoreLimits = false)
    {
        var preset = _customPresets.FirstOrDefault(p => p.PresetId == presetId);

        if (preset != null && preset.PresetType == "democracy")
        {
            var subIds = GetDemocracySubPresets(preset, manual, ignoreLimits);

            if (subIds.Count == 0)
                return false;

            BeginOurVote();

            if (subIds.Count == 1)
            {
                ProcessDemocracyWinner(subIds[0], new HashSet<string> { preset.PresetId }, manual, ignoreLimits);
                return true;
            }

            StartDemocracyVote(preset, subIds, manual: manual, ignoreLimits: ignoreLimits);
            return true;
        }

        var modes = GetFilteredPresetModes(presetId, ignoreLimits);

        if (modes.Count == 0)
            return false;

        if (!manual && !ignoreLimits)
        {
            if (preset != null && preset.PresetType == "rdm")
            {
                _rdmStreak++;
            }
            else if (preset == null || preset.PresetType == "calm")
            {
                _rdmStreak = 0;
            }
        }

        if (modes.Count == 1)
        {
            ApplyPreset(presetId, modes[0], announcePublic: !(preset?.Secret ?? false));
            return true;
        }

        StartModeVote(presetId, modes);
        return true;
    }

    private void ApplyPreset(string presetId, string pickedMode, bool announcePublic = true)
    {
        var preset = _customPresets.FirstOrDefault(p => p.PresetId == presetId);
        var secret = preset?.Secret ?? false;

        _pendingAppliedMode = pickedMode;

        if (secret)
        {
            _ticker.SetGamePreset(GetSecretPresetId(pickedMode));
            _chatManager.SendAdminAlert(Loc.GetString("game-preset-secret-win-admin", ("mode", GetModeDisplayName(pickedMode))));
        }
        else
        {
            _ticker.SetGamePreset(pickedMode);
            if (announcePublic)
            {
                _chatManager.DispatchServerAnnouncement(
                    Loc.GetString("game-preset-mode-set", ("mode", GetModeDisplayName(pickedMode))));
            }
        }
    }

    private void StartDemocracyVote(
        CustomPresetData democracyPreset,
        List<string> subPresetIds,
        HashSet<string>? visited = null,
        bool manual = false,
        bool ignoreLimits = false)
    {
        visited ??= new HashSet<string>();
        if (!visited.Add(democracyPreset.PresetId))
        {
            _sawmill.Warning($"Democracy cycle detected for preset {democracyPreset.PresetId}, aborting vote.");
            FinishOurVotes();
            return;
        }

        if (subPresetIds.Count == 0)
        {
            FinishOurVotes();
            return;
        }

        if (subPresetIds.Count == 1)
        {
            ProcessDemocracyWinner(subPresetIds[0], visited, manual, ignoreLimits);
            return;
        }

        var options = new VoteOptions
        {
            Title = Loc.GetString("game-preset-vote-title"),
            Duration = TimeSpan.FromSeconds(_voteDurationSeconds),
            DisplayVotes = false
        };
        options.SetInitiatorOrServer(null);

        foreach (var subId in subPresetIds)
        {
            var displayName = GetPresetDisplayName(subId);
            options.Options.Add((displayName, subId));
        }

        var vote = _voteManager.CreateVote(options);

        vote.OnFinished += (_, args) =>
        {
            var winner = args.Winner;
            if (winner == null && args.Winners.Length > 0)
                winner = _random.Pick(args.Winners);

            var winnerPresetId = winner?.ToString();
            if (string.IsNullOrEmpty(winnerPresetId))
            {
                FinishOurVotes();
                return;
            }

            Timer.Spawn(0, () => ProcessDemocracyWinner(winnerPresetId, visited, manual, ignoreLimits));
        };
    }

    private void ProcessDemocracyWinner(
        string winnerPresetId,
        HashSet<string> visited,
        bool manual,
        bool ignoreLimits)
    {
        var winnerPreset = _customPresets.FirstOrDefault(p => p.PresetId == winnerPresetId);
        if (winnerPreset != null && winnerPreset.PresetType == "democracy")
        {
            var availabilityPath = new HashSet<string>(visited) { winnerPreset.PresetId };
            var subPresetIds = GetDemocracySubPresets(
                winnerPreset,
                manual,
                ignoreLimits,
                suppressAlerts: false,
                visited: availabilityPath);
            if (subPresetIds.Count == 0)
            {
                FinishOurVotes();
                return;
            }

            StartDemocracyVote(winnerPreset, subPresetIds, visited, manual, ignoreLimits);
            return;
        }

        if (!manual && !ignoreLimits)
        {
            if (winnerPreset != null && winnerPreset.PresetType == "rdm")
            {
                _rdmStreak++;
            }
            else
            {
                _rdmStreak = 0;
            }
        }

        var modes = GetFilteredPresetModes(winnerPresetId, ignoreLimits);

        if (modes.Count == 0)
        {
            FinishOurVotes();
            return;
        }

        if (modes.Count == 1)
        {
            ApplyPreset(winnerPresetId, modes[0], announcePublic: !(winnerPreset?.Secret ?? false));
            FinishOurVotes();
            return;
        }
        StartModeVote(winnerPresetId, modes, manageOoc: false);
    }

    private void StartModeVote(string presetId, List<string> modes, bool manageOoc = true)
    {
        var options = new VoteOptions
        {
            Title = Loc.GetString("game-preset-vote-title"),
            Duration = TimeSpan.FromSeconds(_voteDurationSeconds),
            DisplayVotes = false
        };
        options.SetInitiatorOrServer(null);

        foreach (var mode in modes)
        {
            var modeName = GetModeDisplayName(mode);
            options.Options.Add((modeName, mode));
        }

        var vote = _voteManager.CreateVote(options);

        if (manageOoc)
        {
            BeginOurVote();
        }

        vote.OnFinished += (_, args) =>
        {
            string picked;
            if (args.Winner == null)
            {
                if (args.Winners.Length == 0)
                {
                    FinishOurVotes();
                    return;
                }

                picked = (string)_random.Pick(args.Winners);
                var preset = _customPresets.FirstOrDefault(p => p.PresetId == presetId);
                var secret = preset?.Secret ?? false;
                if (!secret)
                {
                    _chatManager.DispatchServerAnnouncement(
                        Loc.GetString("game-preset-mode-tie", ("picked", GetModeDisplayName(picked))));
                }
            }
            else
            {
                picked = (string)args.Winner;
                var preset = _customPresets.FirstOrDefault(p => p.PresetId == presetId);
                var secret = preset?.Secret ?? false;
                if (!secret)
                {
                    _chatManager.DispatchServerAnnouncement(
                        Loc.GetString("ui-vote-gamemode-win", ("winner", GetModeDisplayName(picked))));
                }
            }
            ApplyPreset(presetId, picked, announcePublic: false);
            FinishOurVotes();
        };
    }

    private void BeginOurVote()
    {
        if (!_disableOocDuringVote)
            return;

        if (_activeOurVotesCount == 0)
        {
            _originalOocEnabled = _cfg.GetCVar(CCVars.OocEnabled);
            _oocStateChangedExternally = false;

            _ourOocChange = true;
            _cfg.SetCVar(CCVars.OocEnabled, false);
            _ourOocChange = false;
        }
        _activeOurVotesCount++;
    }

    private void FinishOurVotes()
    {
        if (!_disableOocDuringVote)
            return;

        if (_activeOurVotesCount <= 0)
        {
            _sawmill.Warning("Tried to finish a game preset vote while no tracked votes were active.");
            return;
        }

        _activeOurVotesCount--;
        if (_activeOurVotesCount == 0)
        {
            if (!_oocStateChangedExternally && _ticker.RunLevel == GameRunLevel.PreRoundLobby)
            {
                _ourOocChange = true;
                _cfg.SetCVar(CCVars.OocEnabled, _originalOocEnabled);
                _ourOocChange = false;
            }
        }
    }

    private string GetPresetDisplayName(string presetId)
    {
        var custom = _customPresets.FirstOrDefault(p => p.PresetId == presetId);
        if (custom != null)
            return custom.PresetName;

        if (_prototypeManager.TryIndex<GamePresetPrototype>(presetId, out var proto))
            return Loc.GetString(proto.ModeTitle);

        return presetId;
    }

    private string GetModeDisplayName(string modeId)
    {
        if (_prototypeManager.TryIndex<EntityPrototype>(modeId, out var entityProto))
        {
            var name = entityProto.Name;
            if (!string.IsNullOrEmpty(name))
            {
                if (name == "secret-title")
                    return Loc.GetString("secret-title") + " " + GenerateSecretPresetName(modeId);

                var localized = Loc.GetString(name);
                if (!string.IsNullOrEmpty(localized) && localized != name)
                    return localized;
            }
        }

        if (_prototypeManager.TryIndex<GamePresetPrototype>(modeId, out var presetProto))
        {
            var modeTitle = presetProto.ModeTitle;
            if (!string.IsNullOrEmpty(modeTitle))
            {
                if (modeTitle == "secret-title")
                    return Loc.GetString("secret-title") + " " + GenerateSecretPresetName(modeId);

                var localized = Loc.GetString(modeTitle);
                if (!string.IsNullOrEmpty(localized) && localized != modeTitle)
                    return localized;
            }
        }

        var custom = _customPresets.FirstOrDefault(p => p.PresetId == modeId);
        if (custom != null)
            return custom.PresetName;

        return modeId;
    }

    private List<string> GetPresetModes(string presetId)
    {
        return GetPresetModes(presetId, new HashSet<string>());
    }

    private List<string> GetPresetModes(string presetId, HashSet<string> visited)
    {
        var custom = _customPresets.FirstOrDefault(p => p.PresetId == presetId);
        if (custom != null)
        {
            if (custom.PresetType == "democracy")
            {
                if (!visited.Add(presetId))
                    return new List<string>();

                var allModes = new List<string>();
                foreach (var subId in custom.Modes)
                {
                    allModes.AddRange(GetPresetModes(subId, visited));
                }

                visited.Remove(presetId);
                return allModes;
            }
            return custom.Modes;
        }

        if (_prototypeManager.TryIndex<GamePresetPrototype>(presetId, out var proto))
            return proto.Rules.Select(r => r.Id).ToList();

        return new List<string>();
    }

    private string GetSecretPresetId(string modeId)
    {
        var baseId = modeId;
        if (baseId.StartsWith("Secret"))
            return baseId;

        var secretId = "Secret" + baseId;
        if (_prototypeManager.TryIndex<GamePresetPrototype>(secretId, out _) ||
            _prototypeManager.TryIndex<EntityPrototype>(secretId, out _))
            return secretId;

        if (_prototypeManager.TryIndex<GamePresetPrototype>(baseId, out var proto))
        {
            foreach (var rule in proto.Rules)
            {
                if (rule.Id.StartsWith("Secret"))
                    return rule.Id;
            }
        }

        return baseId;
    }

    private void SendUpdate(ICommonSession? session = null)
    {
        var names = new Dictionary<string, string>();
        var modeNames = new Dictionary<string, string>();

        foreach (var preset in _prototypeManager.EnumeratePrototypes<GamePresetPrototype>())
        {
            if (!preset.ShowInAdminVote)
                continue;

            var name = Loc.GetString(preset.ModeTitle);
            if (preset.ModeTitle == "secret-title")
                name = Loc.GetString("secret-title") + " " + GenerateSecretPresetName(preset.ID);

            names[preset.ID] = name;

            foreach (var rule in preset.Rules)
            {
                if (!modeNames.ContainsKey(rule.Id))
                {
                    if (_prototypeManager.TryIndex<EntityPrototype>(rule.Id, out var entity) &&
                        entity.TryGetComponent<GameRuleComponent>(out _, _componentFactory))
                    {
                        modeNames[rule.Id] = GetModeDisplayName(rule.Id);
                    }
                }
            }
        }

        foreach (var custom in _customPresets)
        {
            names[custom.PresetId] = custom.PresetName;
            foreach (var mode in custom.Modes)
            {
                if (!modeNames.ContainsKey(mode))
                    modeNames[mode] = GetModeDisplayName(mode);
            }
        }

        var response = new GamePresetsResponseMessage(
            new List<string>(_activePresets),
            new List<CustomPresetData>(_customPresets),
            names,
            _maxRdmRow,
            _voteDurationSeconds,
            _currentPresetIndex,
            _enabled,
            modeNames,
            _disableOocDuringVote,
            _rdmStreak,
            _ticker.RunLevel == GameRunLevel.PreRoundLobby,
            _preventRepeatMode,
            _checkPlayerLimit,
            _whitelistModeIds,
            _enableLowPlayerPreset,
            _lowPlayerThreshold,
            _lowPlayerPresetId);

        if (session != null)
            RaiseNetworkEvent(response, session);
        else
            RaiseNetworkEvent(response, Filter.Broadcast());
    }

    private static string GenerateSecretPresetName(string id)
    {
        var name = id.Replace("Secret", "").Replace("secret", "");
        name = CamelCaseRegex.Replace(name, "$1 $2");
        name = UpperCaseRegex.Replace(name, "$1 $2");
        name = name.Trim();
        return name;
    }
}
