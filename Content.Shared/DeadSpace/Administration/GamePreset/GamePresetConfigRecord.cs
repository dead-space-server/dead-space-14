// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Collections.Generic;

namespace Content.Shared.DeadSpace.Administration.GamePreset;

public sealed class GamePresetConfigRecord
{
    public string ServerId { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int MaxRdmRow { get; set; }
    public int VoteDurationSeconds { get; set; } = 30;
    public int CurrentPresetIndex { get; set; }
    public List<string> ActivePresetIds { get; set; } = new();
    public string CustomPresetsJson { get; set; } = string.Empty;
    public bool DisableOocDuringVote { get; set; }
    public bool PreventRepeatMode { get; set; }
    public bool CheckPlayerLimit { get; set; }
    public string WhitelistModesJson { get; set; } = string.Empty;
    public bool EnableLowPlayerPreset { get; set; }
    public int LowPlayerThreshold { get; set; }
    public string? LowPlayerPresetId { get; set; }
}