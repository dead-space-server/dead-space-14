// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.RoundEnd;
using Content.Shared.GameTicking;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.RoundEnd.Credits;

[Serializable, NetSerializable]
public sealed class RoundEndCreditsVoteEvent : EntityEventArgs
{
    public int RoundId;
    public TimeSpan FadeEnd;
    public TimeSpan VoteEnd;
    public List<RoundEndCreditsVoteNomination> Nominations = new();
    public List<int> Candidates = new();
    public int[] OwnPlayers = Array.Empty<int>();
    public int[] OwnVotes = Array.Empty<int>();
}

[Serializable, NetSerializable]
public record struct RoundEndCreditsVoteNomination(string Prototype);

[Serializable, NetSerializable]
public sealed class RoundEndCreditsVoteCastEvent : EntityEventArgs
{
    public int RoundId;
    public int Nomination;
    public int Candidate;

    public RoundEndCreditsVoteCastEvent(int roundId, int nomination, int candidate)
    {
        RoundId = roundId;
        Nomination = nomination;
        Candidate = candidate;
    }
}

[Serializable, NetSerializable]
public sealed class RoundEndCreditsSkipEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class RoundEndCreditsStartEvent : EntityEventArgs
{
    public int RoundId;
    public TimeSpan StartTime;
    public float ContentDuration;
    public RoundEndCreditsOutcome Outcome = new();
    public RoundEndCreditsPlayerStatus[] Statuses = Array.Empty<RoundEndCreditsPlayerStatus>();
    public RoundEndCreditsMvp? Mvp;
    public RoundEndCreditsMvp? AntagMvp;
    public RoundEndCreditsShowcaseCast? Showcase;
    public List<RoundEndCreditsAward> Honors = new();
    public List<RoundEndCreditsAward> Nominations = new();
    public List<RoundEndCreditsVoteResult> Votes = new();
}

[Serializable, NetSerializable]
public sealed class RoundEndCreditsOutcome
{
    public RoundEndCreditsHeadline Headline = RoundEndCreditsHeadline.Undetermined;
    public string? HeadlineTeam;
    public List<RoundEndCreditsTeam> Teams = new();
    public int CrewTotal;
    public int CrewEscaped;
    public int CrewAlive;
    public int CrewDead;
    public int TotalDeaths;
}

[Serializable, NetSerializable]
public record struct RoundEndCreditsTeam(
    string Name,
    RoundEndCreditsTeamResult Result,
    int Members,
    int Survivors,
    int CompletedObjectives,
    int TotalObjectives);

[Serializable, NetSerializable]
public record struct RoundEndCreditsMvp(
    int Player,
    RoundEndCreditsStat Reason,
    float Value,
    int Score,
    RoundEndCreditsShowcase Showcase = RoundEndCreditsShowcase.None);

/// <summary>
/// A special MVP scene played instead of the usual portrait.
/// </summary>
[Serializable, NetSerializable]
public enum RoundEndCreditsShowcase : byte
{
    None,
    Architect,
}

/// <summary>
/// The numbers shown under the MVP in the special scene, and the two teammates shown next to them.
/// </summary>
[Serializable, NetSerializable]
public sealed class RoundEndCreditsShowcaseCast
{
    public int Kills;
    public int Assists;
    public List<RoundEndCreditsContender> Contenders = new();
}

/// <summary>
/// A worthy contender: the best teammate by kills, or by assists.
/// </summary>
[Serializable, NetSerializable]
public record struct RoundEndCreditsContender(string Name, int Kills, int Assists, RoundEndDollData? Doll);

/// <summary>
/// Plays the Architect MVP scene for a player on every client (sent by the admin command).
/// </summary>
[Serializable, NetSerializable]
public sealed class ArchitectMvpShowcaseEvent : EntityEventArgs
{
    public string Name = string.Empty;
    public RoundEndDollData? Doll;
    public RoundEndCreditsShowcaseCast Cast = new();
}

[Serializable, NetSerializable]
public record struct RoundEndCreditsAward(string Prototype, int Player, float Value);

[Serializable, NetSerializable]
public record struct RoundEndCreditsVoteResult(string Prototype, int Player, int Votes);

public static class RoundEndCreditsCast
{
    public static bool IsHiddenGhostRole(RoundEndMessageEvent.RoundEndPlayerInfo info)
    {
        return info.GhostRole && !(info.Antag && info.ShowInAntagManifest);
    }
}
