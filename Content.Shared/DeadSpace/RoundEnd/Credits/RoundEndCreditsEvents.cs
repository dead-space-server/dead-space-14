// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

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
public record struct RoundEndCreditsMvp(int Player, RoundEndCreditsStat Reason, float Value, int Score);

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
