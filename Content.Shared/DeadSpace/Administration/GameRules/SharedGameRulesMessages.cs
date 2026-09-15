// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Administration.GameRules;

[Serializable, NetSerializable]
public sealed class RequestGameRulesListMessage : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class GameRulesListResponseMessage : EntityEventArgs
{
    public List<RuleEntry> Entries { get; }
    public TimeSpan RoundDuration { get; }
    public bool RoundActive { get; }

    public GameRulesListResponseMessage(List<RuleEntry> entries, TimeSpan roundDuration, bool roundActive)
    {
        Entries = entries;
        RoundDuration = roundDuration;
        RoundActive = roundActive;
    }
}

[Serializable, NetSerializable]
public sealed class RuleEntry
{
    public TimeSpan Time { get; }
    public string RuleName { get; }
    public string? AddedByAdmin { get; }

    public RuleEntry(TimeSpan time, string ruleName, string? addedByAdmin = null)
    {
        Time = time;
        RuleName = ruleName;
        AddedByAdmin = addedByAdmin;
    }
}

[Serializable, NetSerializable]
public sealed class AddGameRuleRequestMessage : EntityEventArgs
{
    public string RuleId { get; }

    public AddGameRuleRequestMessage(string ruleId)
    {
        RuleId = ruleId;
    }
}
