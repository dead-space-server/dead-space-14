// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Administration.GamePreset;

[Serializable, NetSerializable]
public sealed class RequestGamePresetsMessage : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class GamePresetsResponseMessage : EntityEventArgs
{
    public List<string> ActivePresetIds { get; }
    public List<CustomPresetData> CustomPresets { get; }
    public Dictionary<string, string> PresetNames { get; }
    public int MaxRdmRow { get; }
    public int VoteDurationSeconds { get; }
    public int CurrentPresetIndex { get; }
    public bool SystemEnabled { get; }
    public Dictionary<string, string> ModeNames { get; }
    public bool DisableOocDuringVote { get; }
    public int RdmStreak { get; }
    public bool IsLobby { get; }
    public bool PreventRepeatMode { get; }
    public bool CheckPlayerLimit { get; }
    public List<string> WhitelistModeIds { get; }
    public bool EnableLowPlayerPreset { get; }
    public int LowPlayerThreshold { get; }
    public string? LowPlayerPresetId { get; }

    public GamePresetsResponseMessage(
        List<string> activePresetIds,
        List<CustomPresetData> customPresets,
        Dictionary<string, string> presetNames,
        int maxRdmRow,
        int voteDurationSeconds,
        int currentPresetIndex,
        bool systemEnabled,
        Dictionary<string, string> modeNames,
        bool disableOocDuringVote,
        int rdmStreak,
        bool isLobby,
        bool preventRepeatMode,
        bool checkPlayerLimit,
        List<string> whitelistModeIds,
        bool enableLowPlayerPreset,
        int lowPlayerThreshold,
        string? lowPlayerPresetId)
    {
        ActivePresetIds = activePresetIds;
        CustomPresets = customPresets;
        PresetNames = presetNames;
        MaxRdmRow = maxRdmRow;
        VoteDurationSeconds = voteDurationSeconds;
        CurrentPresetIndex = currentPresetIndex;
        SystemEnabled = systemEnabled;
        ModeNames = modeNames;
        DisableOocDuringVote = disableOocDuringVote;
        RdmStreak = rdmStreak;
        IsLobby = isLobby;
        PreventRepeatMode = preventRepeatMode;
        CheckPlayerLimit = checkPlayerLimit;
        WhitelistModeIds = whitelistModeIds;
        EnableLowPlayerPreset = enableLowPlayerPreset;
        LowPlayerThreshold = lowPlayerThreshold;
        LowPlayerPresetId = lowPlayerPresetId;
    }
}

[Serializable, NetSerializable]
public sealed class CustomPresetData
{
    public string PresetId { get; }
    public string PresetName { get; }
    public List<string> Modes { get; }
    public string PresetType { get; }
    public bool Secret { get; }

    public CustomPresetData(string presetId, string presetName, List<string> modes, string presetType, bool secret = false)
    {
        PresetId = presetId;
        PresetName = presetName;
        Modes = modes;
        PresetType = presetType;
        Secret = secret;
    }
}

[Serializable, NetSerializable]
public sealed class SetSystemEnabledMessage : EntityEventArgs
{
    public bool Enabled { get; }

    public SetSystemEnabledMessage(bool enabled)
    {
        Enabled = enabled;
    }
}

[Serializable, NetSerializable]
public sealed class CreateCustomPresetMessage : EntityEventArgs
{
    public string PresetName { get; }
    public List<string> Modes { get; }
    public string PresetType { get; }
    public bool Secret { get; }

    public CreateCustomPresetMessage(string presetName, List<string> modes, string presetType, bool secret)
    {
        PresetName = presetName;
        Modes = modes;
        PresetType = presetType;
        Secret = secret;
    }
}

[Serializable, NetSerializable]
public sealed class UpdateCustomPresetMessage : EntityEventArgs
{
    public string PresetId { get; }
    public string PresetName { get; }
    public List<string> Modes { get; }
    public string PresetType { get; }
    public bool Secret { get; }

    public UpdateCustomPresetMessage(string presetId, string presetName, List<string> modes, string presetType, bool secret)
    {
        PresetId = presetId;
        PresetName = presetName;
        Modes = modes;
        PresetType = presetType;
        Secret = secret;
    }
}

[Serializable, NetSerializable]
public sealed class DeleteCustomPresetMessage : EntityEventArgs
{
    public string PresetId { get; }

    public DeleteCustomPresetMessage(string presetId)
    {
        PresetId = presetId;
    }
}

[Serializable, NetSerializable]
public sealed class SetActivePresetsMessage : EntityEventArgs
{
    public List<string> PresetIds { get; }

    public SetActivePresetsMessage(List<string> presetIds)
    {
        PresetIds = presetIds;
    }
}

[Serializable, NetSerializable]
public sealed class AddPresetToQueueMessage : EntityEventArgs
{
    public string PresetId { get; }

    public AddPresetToQueueMessage(string presetId)
    {
        PresetId = presetId;
    }
}

[Serializable, NetSerializable]
public sealed class RemovePresetFromQueueMessage : EntityEventArgs
{
    public string PresetId { get; }

    public RemovePresetFromQueueMessage(string presetId)
    {
        PresetId = presetId;
    }
}

[Serializable, NetSerializable]
public sealed class MovePresetInQueueMessage : EntityEventArgs
{
    public int FromIndex { get; }
    public int ToIndex { get; }

    public MovePresetInQueueMessage(int fromIndex, int toIndex)
    {
        FromIndex = fromIndex;
        ToIndex = toIndex;
    }
}

[Serializable, NetSerializable]
public sealed class UpdatePresetSettingsMessage : EntityEventArgs
{
    public int MaxRdmRow { get; }
    public int VoteDurationSeconds { get; }
    public bool DisableOocDuringVote { get; }
    public bool PreventRepeatMode { get; }
    public bool CheckPlayerLimit { get; }
    public List<string> WhitelistModeIds { get; }
    public bool EnableLowPlayerPreset { get; }
    public int LowPlayerThreshold { get; }
    public string? LowPlayerPresetId { get; }

    public UpdatePresetSettingsMessage(
        int maxRdmRow,
        int voteDurationSeconds,
        bool disableOocDuringVote,
        bool preventRepeatMode,
        bool checkPlayerLimit,
        List<string> whitelistModeIds,
        bool enableLowPlayerPreset,
        int lowPlayerThreshold,
        string? lowPlayerPresetId)
    {
        MaxRdmRow = maxRdmRow;
        VoteDurationSeconds = voteDurationSeconds;
        DisableOocDuringVote = disableOocDuringVote;
        PreventRepeatMode = preventRepeatMode;
        CheckPlayerLimit = checkPlayerLimit;
        WhitelistModeIds = whitelistModeIds;
        EnableLowPlayerPreset = enableLowPlayerPreset;
        LowPlayerThreshold = lowPlayerThreshold;
        LowPlayerPresetId = lowPlayerPresetId;
    }
}

[Serializable, NetSerializable]
public sealed class InitiateVoteNowMessage : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class SkipCurrentPresetMessage : EntityEventArgs
{
}