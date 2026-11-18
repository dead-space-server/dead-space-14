// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.Antag.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.RoundEnd.Credits;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Shuttles.Components;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.RoundEnd.Credits;

public sealed class RoundEndCreditsSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly EmergencyShuttleSystem _emergency = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly RoundEndCreditsStatsSystem _stats = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    private const float ObjectiveDoneProgress = 0.99f;
    private const float MassacreDeadRatio = 0.5f;
    private const int MassacreMinDead = 3;
    private const float MvpMinScore = 10f;
    private const int MaxHonors = 6;
    private const int MaxFunNominations = 8;
    private const int MaxAwardsPerPlayer = 2;
    private const int CastColumns = 3;

    private const float IntroSeconds = 7f;
    private const float OutcomeSeconds = 6f;
    private const float TeamSeconds = 1.5f;
    private const float MvpSeconds = 8f;
    private const float AntagMvpSeconds = 6f;
    private const float AwardSeconds = 3f;
    private const float AntagCardSeconds = 5f;
    private const float CastRowSeconds = 2.4f;
    private const float OutroSeconds = 5f;
    private const float WallMinSeconds = 8f;
    private const float MinContentSeconds = 20f;

    private enum Phase : byte
    {
        None,
        Pending,
        Vote,
        Credits,
    }

    private Phase _phase;
    private int _roundId;
    private TimeSpan _roundEndTime;
    private TimeSpan _fadeEnd;
    private TimeSpan _voteEnd;
    private TimeSpan? _restartTime;
    private float _voteTime;
    private float _plannedContent;
    private bool _mapsPaused;
    private RoundEndCreditsStartEvent? _credits;
    private NetUserId?[] _playerUsers = Array.Empty<NetUserId?>();
    private readonly List<string> _voteNominations = new();
    private readonly List<int> _candidates = new();
    private readonly Dictionary<NetUserId, int[]> _votes = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd);
        SubscribeLocalEvent<RoundStartingEvent>(_ => Reset());
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => Reset());
        SubscribeLocalEvent<NukeopsRuleComponent, RoundEndCreditsTeamResultEvent>(OnNukeopsTeamResult);
        SubscribeNetworkEvent<RoundEndCreditsVoteCastEvent>(OnVoteCast);
        SubscribeNetworkEvent<RoundEndCreditsSkipEvent>(OnSkip);

        _players.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    public bool Enabled => _cfg.GetCVar(CCCCVars.RoundEndCreditsEnabled) && !_cfg.GetCVar(CCCCVars.RoundEndLegacySummary);

    public RoundEndCreditsStartEvent? Credits => _credits;

    public bool CreditsStarted => _phase == Phase.Credits;

    public TimeSpan GetShuttleRoundEndDelay(TimeSpan fallback)
    {
        return Enabled
            ? TimeSpan.FromSeconds(Math.Max(0f, _cfg.GetCVar(CCCCVars.RoundEndCreditsShuttleDelay)))
            : fallback;
    }

    public TimeSpan? GetPlannedRestartDelay()
    {
        if (_phase != Phase.Pending)
            return null;

        var fade = _cfg.GetCVar(CCCCVars.RoundEndCreditsFadeTime);
        var vote = CanVote() ? _cfg.GetCVar(CCCCVars.RoundEndCreditsVoteTime) : 0f;
        var total = fade + vote + _plannedContent + WallMinSeconds;
        var min = _cfg.GetCVar(CCVars.RoundRestartTime);
        var max = Math.Max(min, _cfg.GetCVar(CCCCVars.RoundEndCreditsMaxTime));

        return TimeSpan.FromSeconds(Math.Clamp(total, min, max));
    }

    public void SetRestartDelay(TimeSpan delay)
    {
        if (_phase == Phase.None)
            return;

        _restartTime = _roundEndTime + delay;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_phase == Phase.None)
            return;

        if (_phase == Phase.Pending)
            StartPhases();

        var now = _timing.CurTime;

        if (!_mapsPaused && now >= _fadeEnd)
        {
            _mapsPaused = true;
            if (_cfg.GetCVar(CCCCVars.RoundEndCreditsPauseMaps))
                PauseMaps();
        }

        if (_phase == Phase.Vote && now >= _voteEnd)
            StartCredits();
    }

    private void Reset()
    {
        _phase = Phase.None;
        _restartTime = null;
        _mapsPaused = false;
        _credits = null;
        _playerUsers = Array.Empty<NetUserId?>();
        _voteNominations.Clear();
        _candidates.Clear();
        _votes.Clear();
    }

    private void OnRoundEnd(RoundEndMessageEvent ev)
    {
        if (!Enabled)
            return;

        Reset();

        var players = ev.AllPlayersEndInfo;
        var minds = _ticker.RoundEndSnapshotMinds;
        if (minds.Count != players.Length)
        {
            Log.Error($"Round end snapshot has {players.Length} players but {minds.Count} minds, credits skipped");
            return;
        }

        _roundId = ev.RoundId;
        _roundEndTime = _timing.CurTime;
        _fadeEnd = _roundEndTime + TimeSpan.FromSeconds(_cfg.GetCVar(CCCCVars.RoundEndCreditsFadeTime));
        _playerUsers = players.Select(p => p.PlayerGuid).ToArray();

        try
        {
            _credits = BuildCredits(ev, minds);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to build round end credits: {e}");
            _credits = new RoundEndCreditsStartEvent
            {
                RoundId = ev.RoundId,
                Statuses = new RoundEndCreditsPlayerStatus[players.Length],
            };
        }

        PrepareVote(players);
        _plannedContent = EstimateContentDuration(_credits, players);
        _phase = Phase.Pending;
    }

    private bool CanVote()
    {
        return _voteNominations.Count > 0 &&
               _candidates.Count >= 2 &&
               _cfg.GetCVar(CCCCVars.RoundEndCreditsVoteTime) > 0f;
    }

    private void StartPhases()
    {
        if (_credits == null)
        {
            Reset();
            return;
        }

        var available = _restartTime is { } restart
            ? (float) (restart - _fadeEnd).TotalSeconds
            : float.MaxValue;

        _voteTime = CanVote() ? _cfg.GetCVar(CCCCVars.RoundEndCreditsVoteTime) : 0f;
        if (available < _voteTime + MinContentSeconds + WallMinSeconds)
            _voteTime = 0f;

        var content = _plannedContent;
        if (available < float.MaxValue)
            content = Math.Min(content, Math.Max(MinContentSeconds, available - _voteTime - WallMinSeconds));

        _voteEnd = _fadeEnd + TimeSpan.FromSeconds(_voteTime);
        _credits.StartTime = _voteEnd;
        _credits.ContentDuration = content;

        if (_voteTime <= 0f)
        {
            StartCredits();
            return;
        }

        _phase = Phase.Vote;
        foreach (var session in _players.Sessions)
        {
            if (session.Status == SessionStatus.InGame)
                SendVote(session);
        }
    }

    private void StartCredits()
    {
        if (_credits == null)
            return;

        _credits.Votes = TallyVotes();
        _phase = Phase.Credits;
        RaiseNetworkEvent(_credits);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.InGame)
            return;

        switch (_phase)
        {
            case Phase.Vote:
                SendVote(args.Session);
                break;
            case Phase.Credits when _credits != null:
                RaiseNetworkEvent(_credits, args.Session);
                break;
        }
    }

    private void PauseMaps()
    {
        var query = AllEntityQuery<MapComponent>();
        while (query.MoveNext(out var uid, out var map))
        {
            _map.SetPaused((uid, map), true);
        }
    }

    private void PrepareVote(RoundEndMessageEvent.RoundEndPlayerInfo[] players)
    {
        for (var i = 0; i < players.Length; i++)
        {
            if (_credits?.Statuses.Length > i && _credits.Statuses[i] == RoundEndCreditsPlayerStatus.Observer)
                continue;

            if (players[i].PlayerGuid == null || RoundEndCreditsCast.IsHiddenGhostRole(players[i]))
                continue;

            _candidates.Add(i);
        }

        var pool = _proto.EnumeratePrototypes<RoundEndCreditsVoteNominationPrototype>()
            .Select(p => p.ID)
            .ToList();
        _random.Shuffle(pool);

        var count = Math.Max(0, _cfg.GetCVar(CCCCVars.RoundEndCreditsVoteNominations));
        _voteNominations.AddRange(pool.Take(count));
    }

    private void SendVote(ICommonSession session)
    {
        var own = new List<int>();
        for (var i = 0; i < _playerUsers.Length; i++)
        {
            if (_playerUsers[i] == session.UserId)
                own.Add(i);
        }

        var ev = new RoundEndCreditsVoteEvent
        {
            RoundId = _roundId,
            FadeEnd = _fadeEnd,
            VoteEnd = _voteEnd,
            Nominations = _voteNominations.Select(id => new RoundEndCreditsVoteNomination(id)).ToList(),
            Candidates = new List<int>(_candidates),
            OwnPlayers = own.ToArray(),
            OwnVotes = _votes.TryGetValue(session.UserId, out var votes) ? votes.ToArray() : Array.Empty<int>(),
        };

        RaiseNetworkEvent(ev, session);
    }

    private void OnVoteCast(RoundEndCreditsVoteCastEvent msg, EntitySessionEventArgs args)
    {
        if (_phase != Phase.Vote ||
            msg.RoundId != _roundId ||
            _timing.CurTime >= _voteEnd ||
            msg.Nomination < 0 ||
            msg.Nomination >= _voteNominations.Count)
        {
            return;
        }

        var user = args.SenderSession.UserId;
        if (msg.Candidate != -1 &&
            (!_candidates.Contains(msg.Candidate) || _playerUsers[msg.Candidate] == user))
        {
            return;
        }

        if (!_votes.TryGetValue(user, out var votes))
        {
            votes = Enumerable.Repeat(-1, _voteNominations.Count).ToArray();
            _votes[user] = votes;
        }

        votes[msg.Nomination] = msg.Candidate;
    }

    private void OnSkip(RoundEndCreditsSkipEvent msg, EntitySessionEventArgs args)
    {
        _ticker.ReturnToLobbyAfterRound(args.SenderSession);
    }

    private List<RoundEndCreditsVoteResult> TallyVotes()
    {
        var results = new List<RoundEndCreditsVoteResult>();

        for (var nomination = 0; nomination < _voteNominations.Count; nomination++)
        {
            var counts = new Dictionary<int, int>();
            foreach (var votes in _votes.Values)
            {
                var candidate = votes[nomination];
                if (candidate >= 0)
                    counts[candidate] = counts.GetValueOrDefault(candidate) + 1;
            }

            if (counts.Count == 0)
            {
                results.Add(new RoundEndCreditsVoteResult(_voteNominations[nomination], -1, 0));
                continue;
            }

            var max = counts.Values.Max();
            var leaders = counts.Where(pair => pair.Value == max).Select(pair => pair.Key).ToList();
            results.Add(new RoundEndCreditsVoteResult(_voteNominations[nomination], _random.Pick(leaders), max));
        }

        return results;
    }

    private RoundEndCreditsStartEvent BuildCredits(RoundEndMessageEvent ev, IReadOnlyList<EntityUid> minds)
    {
        var players = ev.AllPlayersEndInfo;
        var statuses = new RoundEndCreditsPlayerStatus[players.Length];

        for (var i = 0; i < players.Length; i++)
        {
            statuses[i] = GetStatus(minds[i], players[i]);
            var completed = (players[i].ManifestObjectives ?? Array.Empty<RoundEndMessageEvent.RoundEndObjectiveInfo>())
                .Count(o => o.Progress >= ObjectiveDoneProgress);
            _stats.SetStat(minds[i], RoundEndCreditsStat.ObjectivesCompleted, completed);
        }

        var credits = new RoundEndCreditsStartEvent
        {
            RoundId = ev.RoundId,
            Statuses = statuses,
            Outcome = BuildOutcome(players, minds, statuses),
        };

        credits.Mvp = PickMvp(players, minds, statuses, antag: false, exclude: -1);
        credits.AntagMvp = PickMvp(players, minds, statuses, antag: true, exclude: credits.Mvp?.Player ?? -1);
        BuildAwards(players, minds, statuses, credits);

        return credits;
    }

    private RoundEndCreditsPlayerStatus GetStatus(EntityUid mindId, RoundEndMessageEvent.RoundEndPlayerInfo info)
    {
        if (info.Observer && (info.JobPrototypes?.Length ?? 0) == 0 && (info.AntagPrototypes?.Length ?? 0) == 0)
            return RoundEndCreditsPlayerStatus.Observer;

        if (!TryComp<MindComponent>(mindId, out var mind))
            return RoundEndCreditsPlayerStatus.Dead;

        if (mind.OwnedEntity is not { } body || TerminatingOrDeleted(body))
            return RoundEndCreditsPlayerStatus.Dead;

        if (HasComp<GhostComponent>(body))
            return mind.TimeOfDeath.HasValue ? RoundEndCreditsPlayerStatus.Dead : RoundEndCreditsPlayerStatus.Alive;

        if (_mobState.IsDead(body))
            return RoundEndCreditsPlayerStatus.Dead;

        return IsEscaping(body) ? RoundEndCreditsPlayerStatus.Escaped : RoundEndCreditsPlayerStatus.Alive;
    }

    private bool IsEscaping(EntityUid body)
    {
        if (!_emergency.EmergencyShuttleArrived)
            return false;

        if (_emergency.IsTargetEscaping(body))
            return true;

        var xform = Transform(body);
        if (xform.GridUid is { } grid && HasComp<FTLComponent>(grid) && _emergency.ShuttlesLeft)
            return true;

        var centcomms = EntityQueryEnumerator<StationCentcommComponent>();
        while (centcomms.MoveNext(out var centcomm))
        {
            if (centcomm.MapEntity != null && centcomm.MapEntity == xform.MapUid)
                return true;
        }

        return false;
    }

    private RoundEndCreditsOutcome BuildOutcome(
        RoundEndMessageEvent.RoundEndPlayerInfo[] players,
        IReadOnlyList<EntityUid> minds,
        RoundEndCreditsPlayerStatus[] statuses)
    {
        var outcome = new RoundEndCreditsOutcome();
        var participants = 0;
        var dead = 0;

        for (var i = 0; i < players.Length; i++)
        {
            if (statuses[i] == RoundEndCreditsPlayerStatus.Observer)
                continue;

            participants++;
            if (statuses[i] == RoundEndCreditsPlayerStatus.Dead)
                dead++;

            if (players[i].Antag || (players[i].JobPrototypes?.Length ?? 0) == 0)
                continue;

            outcome.CrewTotal++;
            switch (statuses[i])
            {
                case RoundEndCreditsPlayerStatus.Escaped:
                    outcome.CrewEscaped++;
                    break;
                case RoundEndCreditsPlayerStatus.Alive:
                    outcome.CrewAlive++;
                    break;
                case RoundEndCreditsPlayerStatus.Dead:
                    outcome.CrewDead++;
                    break;
            }
        }

        outcome.TotalDeaths = dead;
        outcome.Teams = BuildTeams(players, minds, statuses);

        if (outcome.Teams.Count == 0)
        {
            outcome.Headline = dead >= MassacreMinDead && dead >= participants * MassacreDeadRatio
                ? RoundEndCreditsHeadline.Massacre
                : RoundEndCreditsHeadline.Calm;
            return outcome;
        }

        foreach (var team in outcome.Teams)
        {
            if (team.Result != RoundEndCreditsTeamResult.MajorVictory)
                continue;

            outcome.Headline = RoundEndCreditsHeadline.AntagVictory;
            outcome.HeadlineTeam = team.Name;
            return outcome;
        }

        foreach (var team in outcome.Teams)
        {
            if (team.Result != RoundEndCreditsTeamResult.MinorVictory)
                continue;

            outcome.Headline = RoundEndCreditsHeadline.AntagPartialVictory;
            outcome.HeadlineTeam = team.Name;
            return outcome;
        }

        outcome.Headline = outcome.Teams.All(t => t.Result is RoundEndCreditsTeamResult.MinorDefeat or RoundEndCreditsTeamResult.MajorDefeat)
            ? RoundEndCreditsHeadline.CrewVictory
            : RoundEndCreditsHeadline.Undetermined;

        return outcome;
    }

    private List<RoundEndCreditsTeam> BuildTeams(
        RoundEndMessageEvent.RoundEndPlayerInfo[] players,
        IReadOnlyList<EntityUid> minds,
        RoundEndCreditsPlayerStatus[] statuses)
    {
        var indexByMind = new Dictionary<EntityUid, int>();
        for (var i = 0; i < minds.Count; i++)
        {
            if (players[i].Antag && players[i].ShowInAntagManifest)
                indexByMind.TryAdd(minds[i], i);
        }

        var teams = new List<RoundEndCreditsTeam>();
        var assigned = new HashSet<int>();

        var rules = EntityQueryEnumerator<AntagSelectionComponent>();
        while (rules.MoveNext(out var rule, out var selection))
        {
            var members = new List<int>();
            foreach (var (mindId, _) in selection.AssignedMinds)
            {
                if (indexByMind.TryGetValue(mindId, out var index) && assigned.Add(index))
                    members.Add(index);
            }

            if (members.Count == 0)
                continue;

            var ev = new RoundEndCreditsTeamResultEvent(null);
            RaiseLocalEvent(rule, ref ev);
            teams.Add(MakeTeam(players, statuses, members, ev.Result));
        }

        var leftovers = indexByMind.Values
            .Where(i => !assigned.Contains(i))
            .GroupBy(i => players[i].AntagPrototypes?.FirstOrDefault() ?? string.Empty);

        foreach (var group in leftovers)
        {
            teams.Add(MakeTeam(players, statuses, group.ToList(), null));
        }

        return teams;
    }

    private static RoundEndCreditsTeam MakeTeam(
        RoundEndMessageEvent.RoundEndPlayerInfo[] players,
        RoundEndCreditsPlayerStatus[] statuses,
        List<int> members,
        RoundEndCreditsTeamResult? forcedResult)
    {
        var name = members
            .Select(i => players[i].AntagRoleNames?.FirstOrDefault())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .GroupBy(n => n!)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? string.Empty;

        var survivors = 0;
        var completed = 0;
        var total = 0;

        foreach (var i in members)
        {
            if (statuses[i] != RoundEndCreditsPlayerStatus.Dead && !players[i].InCustody)
                survivors++;

            foreach (var objective in players[i].ManifestObjectives ?? Array.Empty<RoundEndMessageEvent.RoundEndObjectiveInfo>())
            {
                total++;
                if (objective.Progress >= ObjectiveDoneProgress)
                    completed++;
            }
        }

        var result = forcedResult ?? GetObjectiveResult(completed, total, survivors);
        return new RoundEndCreditsTeam(name, result, members.Count, survivors, completed, total);
    }

    private static RoundEndCreditsTeamResult GetObjectiveResult(int completed, int total, int survivors)
    {
        if (total == 0)
            return survivors == 0 ? RoundEndCreditsTeamResult.MajorDefeat : RoundEndCreditsTeamResult.Unknown;

        if (completed == total)
            return RoundEndCreditsTeamResult.MajorVictory;

        var ratio = (float) completed / total;
        if (ratio >= 0.5f)
            return RoundEndCreditsTeamResult.MinorVictory;

        return completed > 0 ? RoundEndCreditsTeamResult.MinorDefeat : RoundEndCreditsTeamResult.MajorDefeat;
    }

    private void OnNukeopsTeamResult(Entity<NukeopsRuleComponent> ent, ref RoundEndCreditsTeamResultEvent args)
    {
        args.Result = ent.Comp.WinType switch
        {
            WinType.OpsMajor => RoundEndCreditsTeamResult.MajorVictory,
            WinType.OpsMinor => RoundEndCreditsTeamResult.MinorVictory,
            WinType.CrewMinor => RoundEndCreditsTeamResult.MinorDefeat,
            WinType.CrewMajor => RoundEndCreditsTeamResult.MajorDefeat,
            _ => RoundEndCreditsTeamResult.Unknown,
        };
    }

    private static readonly (RoundEndCreditsStat Stat, float Weight, float Cap)[] MvpWeights =
    {
        (RoundEndCreditsStat.AntagKills, 20f, float.MaxValue),
        (RoundEndCreditsStat.ObjectivesCompleted, 22f, float.MaxValue),
        (RoundEndCreditsStat.Revives, 18f, float.MaxValue),
        (RoundEndCreditsStat.Kills, 6f, float.MaxValue),
        (RoundEndCreditsStat.HealingGiven, 0.08f, float.MaxValue),
        (RoundEndCreditsStat.Messages, 0.1f, 12f),
    };

    private RoundEndCreditsMvp? PickMvp(
        RoundEndMessageEvent.RoundEndPlayerInfo[] players,
        IReadOnlyList<EntityUid> minds,
        RoundEndCreditsPlayerStatus[] statuses,
        bool antag,
        int exclude)
    {
        RoundEndCreditsMvp? best = null;
        var bestScore = MvpMinScore;

        for (var i = 0; i < players.Length; i++)
        {
            if (i == exclude || statuses[i] == RoundEndCreditsPlayerStatus.Observer)
                continue;

            if (antag && !(players[i].Antag && players[i].ShowInAntagManifest))
                continue;

            var score = statuses[i] == RoundEndCreditsPlayerStatus.Escaped ? 4f : 0f;
            var reason = RoundEndCreditsStat.Messages;
            var reasonScore = -1f;

            foreach (var (stat, weight, cap) in MvpWeights)
            {
                var part = Math.Min(_stats.GetStat(minds[i], stat) * weight, cap);
                score += part;
                if (part > reasonScore)
                {
                    reasonScore = part;
                    reason = stat;
                }
            }

            if (score < bestScore)
                continue;

            bestScore = score;
            best = new RoundEndCreditsMvp(i, reason, _stats.GetStat(minds[i], reason), (int) MathF.Round(score));
        }

        return best;
    }

    private void BuildAwards(
        RoundEndMessageEvent.RoundEndPlayerInfo[] players,
        IReadOnlyList<EntityUid> minds,
        RoundEndCreditsPlayerStatus[] statuses,
        RoundEndCreditsStartEvent credits)
    {
        var candidates = new List<(RoundEndCreditsNominationPrototype Proto, int Player, float Value, float Ratio)>();

        foreach (var proto in _proto.EnumeratePrototypes<RoundEndCreditsNominationPrototype>())
        {
            var bestPlayer = -1;
            var bestValue = 0f;

            for (var i = 0; i < players.Length; i++)
            {
                if (statuses[i] == RoundEndCreditsPlayerStatus.Observer)
                    continue;

                var isAntag = players[i].Antag;
                if (proto.AntagOnly && !isAntag || proto.CrewOnly && isAntag)
                    continue;

                var value = _stats.GetStat(minds[i], proto.Stat);
                if (value < proto.Minimum || value <= bestValue)
                    continue;

                bestPlayer = i;
                bestValue = value;
            }

            if (bestPlayer >= 0)
                candidates.Add((proto, bestPlayer, bestValue, bestValue / Math.Max(proto.Minimum, 1f)));
        }

        var perPlayer = new Dictionary<int, int>();
        foreach (var (proto, player, value, _) in candidates.OrderByDescending(c => c.Ratio))
        {
            var list = proto.Kind == RoundEndCreditsNominationKind.Honor ? credits.Honors : credits.Nominations;
            var limit = proto.Kind == RoundEndCreditsNominationKind.Honor ? MaxHonors : MaxFunNominations;
            if (list.Count >= limit || perPlayer.GetValueOrDefault(player) >= MaxAwardsPerPlayer)
                continue;

            perPlayer[player] = perPlayer.GetValueOrDefault(player) + 1;
            list.Add(new RoundEndCreditsAward(proto.ID, player, MathF.Round(value)));
        }
    }

    private float EstimateContentDuration(RoundEndCreditsStartEvent credits, RoundEndMessageEvent.RoundEndPlayerInfo[] players)
    {
        var cast = 0;
        var antags = 0;
        for (var i = 0; i < players.Length; i++)
        {
            if (credits.Statuses.Length > i && credits.Statuses[i] == RoundEndCreditsPlayerStatus.Observer)
                continue;

            if (RoundEndCreditsCast.IsHiddenGhostRole(players[i]))
                continue;

            cast++;
            if (players[i].Antag && players[i].ShowInAntagManifest)
                antags++;
        }

        var seconds = IntroSeconds + OutcomeSeconds + OutroSeconds;
        seconds += credits.Outcome.Teams.Count * TeamSeconds;
        seconds += credits.Mvp != null ? MvpSeconds : 0f;
        seconds += credits.AntagMvp != null ? AntagMvpSeconds : 0f;
        seconds += (credits.Honors.Count + credits.Nominations.Count + _voteNominations.Count) * AwardSeconds;
        seconds += antags * AntagCardSeconds;
        seconds += MathF.Ceiling(cast / (float) CastColumns) * CastRowSeconds;

        return seconds;
    }

}
