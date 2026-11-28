// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.RoundEnd.Credits;

[Serializable, NetSerializable]
public enum RoundEndCreditsStat : byte
{
    Kills,
    AntagKills,
    Deaths,
    Crits,
    DamageTaken,
    DamageDealt,
    SelfDamage,
    HealingGiven,
    Revives,
    Slips,
    Bites,
    Vomits,
    Emotes,
    Screams,
    Messages,
    Shocks,
    Cuffed,
    ObjectivesCompleted,
    Assists,
}

[Serializable, NetSerializable]
public enum RoundEndCreditsNominationKind : byte
{
    Honor,
    Fun,
}

[Serializable, NetSerializable]
public enum RoundEndCreditsPlayerStatus : byte
{
    Escaped,
    Alive,
    Dead,
    Observer,
}

[Serializable, NetSerializable]
public enum RoundEndCreditsTeamResult : byte
{
    MajorVictory,
    MinorVictory,
    MinorDefeat,
    MajorDefeat,
    Unknown,
}

[Serializable, NetSerializable]
public enum RoundEndCreditsHeadline : byte
{
    CrewVictory,
    AntagVictory,
    AntagPartialVictory,
    Undetermined,
    Calm,
    Massacre,
}
