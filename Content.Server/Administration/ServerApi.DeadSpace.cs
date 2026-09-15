using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Web;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Roles;
using Robust.Server.ServerStatus;
using Robust.Shared.Network;

namespace Content.Server.Administration;

public sealed partial class ServerApi
{
    private const int DefaultRoundStatsDays = 7;
    private const int MaxRoundStatsDays = 365;

    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly ServerDbEntryManager _serverDbEntry = default!;
    [Dependency] private readonly PlayTimeTrackingManager _playTimeTracking = default!;

    /// <summary>
    /// Get players and active admins list
    /// </summary>
    private async Task GetPlayers(IStatusHandlerContext context)
    {
        var playersList = new JsonArray();
        foreach (var player in _playerManager.Sessions)
        {
            playersList.Add(player.Name);
        }

        var adminMgr = await RunOnMainThread(IoCManager.Resolve<IAdminManager>);
        var adminsDict = new JsonObject();

        foreach (var admin in adminMgr.AllAdmins)
        {
            var adminData = adminMgr.GetAdminData(admin, true)!;
            adminsDict[admin.Name] = new JsonObject
            {
                ["isActive"] = adminData.Active,
                ["isStealth"] = adminData.Stealth,
                ["title"] = adminData.Title,
                ["flags"] = JsonSerializer.SerializeToNode(adminData.Flags.ToString().Split(", ")),
            };
        }

        var jObject = new JsonObject
        {
            ["players"] = playersList,
            ["admins"] = adminsDict
        };

        await context.RespondJsonAsync(jObject);
    }

    private async Task GetPlaytime(IStatusHandlerContext context)
    {
        var query = HttpUtility.ParseQueryString(context.Url.Query);
        if (!TryParseApiUserId(query["userId"], out var userId, out var error))
        {
            await RespondBadRequest(context, error);
            return;
        }

        var playerRecord = await GetPlayerRecordOrRespond(context, userId, "Player");
        if (playerRecord == null)
            return;

        var playtimes = await GetPlaytimeSnapshot(userId);
        var jobs = await RunOnMainThread(BuildPlaytimeJobLookup);

        await context.RespondJsonAsync(BuildPlaytimeResponse(playerRecord, playtimes, jobs));
    }

    private async Task GetPlaytimeJobs(IStatusHandlerContext context)
    {
        var jobs = await RunOnMainThread(BuildPlaytimeJobInfos);

        await context.RespondJsonAsync(new PlaytimeJobsResponse
        {
            OverallTracker = PlayTimeTrackingShared.TrackerOverall,
            Jobs = jobs
        });
    }

    private async Task ActionPlaytimeAdd(IStatusHandlerContext context, Actor actor)
    {
        var request = await ReadJson<PlaytimeAddActionBody>(context);
        if (request == null)
            return;

        if (request.UserId == Guid.Empty)
        {
            await RespondBadRequest(context, "'userId' must be specified");
            return;
        }

        var userId = new NetUserId(request.UserId);
        var playerRecord = await GetPlayerRecordOrRespond(context, userId, "Player");
        if (playerRecord == null)
            return;

        if (!TryBuildPlaytimeAddEntries(request, out var requestedEntries, out var error))
        {
            await RespondBadRequest(context, error);
            return;
        }

        var additions = new Dictionary<string, TimeSpan>();
        foreach (var entry in requestedEntries)
        {
            if (entry == null)
            {
                await RespondBadRequest(context, "'entries' cannot contain null values");
                return;
            }

            var tracker = entry.Tracker?.Trim();
            if (string.IsNullOrWhiteSpace(tracker))
                tracker = PlayTimeTrackingShared.TrackerOverall;

            var trackerExists = await RunOnMainThread(() => _prototypeManager.HasIndex<PlayTimeTrackerPrototype>(tracker));
            if (!trackerExists)
            {
                await RespondBadRequest(context, $"Unknown playtime tracker '{tracker}'");
                return;
            }

            if (entry.Minutes <= 0)
            {
                await RespondBadRequest(context, "'minutes' must be greater than zero");
                return;
            }

            additions[tracker] = additions.GetValueOrDefault(tracker) + TimeSpan.FromMinutes(entry.Minutes);
        }

        var playtimes = await GetPlaytimeSnapshot(userId);
        var updates = new Dictionary<string, TimeSpan>();
        var responseEntries = new List<PlaytimeAddResponse.Entry>();

        foreach (var (tracker, added) in additions.OrderBy(pair => pair.Key))
        {
            var before = playtimes.GetValueOrDefault(tracker);
            var after = before + added;
            updates[tracker] = after;
            responseEntries.Add(new PlaytimeAddResponse.Entry
            {
                Tracker = tracker,
                AddedSeconds = ToSeconds(added),
                PreviousSeconds = ToSeconds(before),
                NewSeconds = ToSeconds(after)
            });
        }

        await ApplyPlaytimeUpdates(userId, updates);

        _sawmill.Info(
            $"Added playtime to {playerRecord.LastSeenUserName} ({userId.UserId}) by {FormatLogActor(actor)}. Reason: {request.Reason ?? "not provided"}");

        await context.RespondJsonAsync(new PlaytimeAddResponse
        {
            Player = BuildPlaytimePlayer(playerRecord),
            Reason = request.Reason,
            Entries = responseEntries.ToArray()
        });
    }

    private async Task GetRoundStats(IStatusHandlerContext context)
    {
        var query = HttpUtility.ParseQueryString(context.Url.Query);
        if (!TryResolveRoundStatsPeriod(query, out var fromUtc, out var toUtc, out var error))
        {
            await RespondBadRequest(context, error);
            return;
        }

        var server = await _serverDbEntry.ServerEntity;
        var rounds = await _db.GetRoundGameModeHistoryAsync(server.Id, fromUtc);
        rounds = rounds
            .Where(round => round.StartDate < toUtc)
            .OrderByDescending(round => round.StartDate)
            .ToList();

        var response = new RoundStatsResponse
        {
            Server = server.Name,
            From = fromUtc,
            To = toUtc,
            TotalRounds = rounds.Count,
            Modes = AggregateRoundStats(rounds.Select(round => NormalizeRoundStatsName(round.GamePresetName)), rounds.Count),
            Maps = AggregateRoundStats(rounds.Select(round => NormalizeRoundStatsName(round.MapName)), rounds.Count),
            Rounds = rounds
                .Select(round => new RoundStatsResponse.Round
                {
                    RoundId = round.RoundId,
                    StartedAt = round.StartDate,
                    GameMode = NormalizeRoundStatsName(round.GamePresetName),
                    Map = NormalizeRoundStatsName(round.MapName),
                    PlayerCount = round.PlayerCount
                })
                .ToArray()
        };

        await context.RespondJsonAsync(response);
    }

    private static bool TryParseApiUserId(string? value, out NetUserId userId, out string error)
    {
        userId = default;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Missing 'userId' value";
            return false;
        }

        if (!Guid.TryParse(value, out var guid))
        {
            error = "Invalid 'userId' value";
            return false;
        }

        userId = new NetUserId(guid);
        return true;
    }

    private async Task<PlayerRecord?> GetPlayerRecordOrRespond(IStatusHandlerContext context, NetUserId userId, string label)
    {
        var record = await _db.GetPlayerRecordByUserId(userId);
        if (record != null)
            return record;

        await RespondError(
            context,
            ErrorCode.PlayerNotFound,
            HttpStatusCode.UnprocessableContent,
            $"{label} player not found");

        return null;
    }

    private async Task<Dictionary<string, TimeSpan>> GetPlaytimeSnapshot(NetUserId userId)
    {
        var onlinePlaytimes = await RunOnMainThread<Dictionary<string, TimeSpan>?>(() =>
        {
            if (!_playerManager.TryGetSessionById(userId, out var session))
                return null;

            _playTimeTracking.FlushTracker(session);
            return _playTimeTracking.GetTrackerTimes(session).ToDictionary();
        });

        if (onlinePlaytimes != null)
            return onlinePlaytimes;

        var playtimes = await _db.GetPlayTimes(userId.UserId);
        return playtimes.ToDictionary(playtime => playtime.Tracker, playtime => playtime.TimeSpent);
    }

    private async Task ApplyPlaytimeUpdates(NetUserId userId, IReadOnlyDictionary<string, TimeSpan> updates)
    {
        if (updates.Count == 0)
            return;

        await RunOnMainThread(() =>
        {
            if (_playerManager.TryGetSessionById(userId, out var session))
                _playTimeTracking.SetTrackerTimes(session, updates);
        });

        await _db.UpdatePlayTimes(updates
            .Select(update => new PlayTimeUpdate(userId, update.Key, update.Value))
            .ToArray());
    }

    private static bool TryBuildPlaytimeAddEntries(
        PlaytimeAddActionBody request,
        out PlaytimeAddEntryBody[] entries,
        out string error)
    {
        error = string.Empty;

        if (request.Entries is { Length: > 0 })
        {
            entries = request.Entries;
            return true;
        }

        if (request.Minutes == null)
        {
            entries = [];
            error = "Either 'entries' or 'minutes' must be specified";
            return false;
        }

        entries =
        [
            new PlaytimeAddEntryBody
            {
                Tracker = request.Tracker,
                Minutes = request.Minutes.Value
            }
        ];

        return true;
    }

    private PlaytimeResponse BuildPlaytimeResponse(
        PlayerRecord player,
        IReadOnlyDictionary<string, TimeSpan> playtimes,
        IReadOnlyDictionary<string, PlaytimeJobInfo> jobs)
    {
        return new PlaytimeResponse
        {
            Player = BuildPlaytimePlayer(player),
            OverallSeconds = ToSeconds(playtimes.GetValueOrDefault(PlayTimeTrackingShared.TrackerOverall)),
            Trackers = playtimes
                .OrderBy(pair => pair.Key)
                .Select(pair => new PlaytimeResponse.TrackerTime
                {
                    Tracker = pair.Key,
                    Seconds = ToSeconds(pair.Value),
                    Job = jobs.GetValueOrDefault(pair.Key)
                })
                .ToArray()
        };
    }

    private static PlaytimePlayerInfo BuildPlaytimePlayer(PlayerRecord player)
    {
        return new PlaytimePlayerInfo
        {
            UserId = player.UserId.UserId,
            UserName = player.LastSeenUserName
        };
    }

    private Dictionary<string, PlaytimeJobInfo> BuildPlaytimeJobLookup()
    {
        var jobs = BuildPlaytimeJobInfos();
        var lookup = new Dictionary<string, PlaytimeJobInfo>();

        foreach (var job in jobs)
        {
            lookup[job.PlayTimeTracker] = job;
        }

        return lookup;
    }

    private PlaytimeJobInfo[] BuildPlaytimeJobInfos()
    {
        var departments = _prototypeManager.EnumeratePrototypes<DepartmentPrototype>().ToArray();
        var jobs = _prototypeManager.EnumeratePrototypes<JobPrototype>().ToList();

        // DS14-start - keep the local playtime API ordered by the new default job-weight profile.
        if (JobUIComparer.TryCreate(_prototypeManager, null, out var comparer))
            jobs.Sort(comparer);
        // DS14-end

        return jobs
            .Select(job => new PlaytimeJobInfo
            {
                Id = job.ID,
                Name = _loc.GetString(job.Name),
                PlayTimeTracker = job.PlayTimeTracker,
                Department = BuildPlaytimeDepartmentInfo(job, departments),
                SetPreference = job.SetPreference
            })
            .ToArray();
    }

    private PlaytimeDepartmentInfo? BuildPlaytimeDepartmentInfo(JobPrototype job, DepartmentPrototype[] departments)
    {
        foreach (var department in departments)
        {
            if (!department.Roles.Contains(job.ID))
                continue;

            return new PlaytimeDepartmentInfo
            {
                Id = department.ID,
                Name = _loc.GetString(department.Name),
                Primary = department.Primary
            };
        }

        return null;
    }

    private static long ToSeconds(TimeSpan time)
    {
        return (long)Math.Floor(time.TotalSeconds);
    }

    private static bool TryResolveRoundStatsPeriod(
        System.Collections.Specialized.NameValueCollection query,
        out DateTime fromUtc,
        out DateTime toUtc,
        out string error)
    {
        error = string.Empty;
        toUtc = DateTime.UtcNow;

        if (!TryParseRoundStatsDate(query["to"], out var parsedTo))
        {
            fromUtc = default;
            error = "Invalid 'to' value";
            return false;
        }

        if (parsedTo != null)
            toUtc = parsedTo.Value;

        if (!TryParseRoundStatsDate(query["from"], out var parsedFrom))
        {
            fromUtc = default;
            error = "Invalid 'from' value";
            return false;
        }

        if (parsedFrom != null)
        {
            fromUtc = parsedFrom.Value;
        }
        else
        {
            if (!TryResolveRoundStatsDays(query["period"], query["days"], out var days, out error))
            {
                fromUtc = default;
                return false;
            }

            fromUtc = toUtc.AddDays(-days);
        }

        if (fromUtc >= toUtc)
        {
            error = "'from' must be earlier than 'to'";
            return false;
        }

        if (toUtc - fromUtc > TimeSpan.FromDays(MaxRoundStatsDays))
        {
            error = $"Period cannot exceed {MaxRoundStatsDays} days";
            return false;
        }

        return true;
    }

    private static bool TryResolveRoundStatsDays(string? period, string? daysText, out int days, out string error)
    {
        error = string.Empty;
        days = DefaultRoundStatsDays;

        if (!string.IsNullOrWhiteSpace(period))
        {
            days = period.Trim().ToLowerInvariant() switch
            {
                "day" or "daily" or "today" => 1,
                "week" or "weekly" => 7,
                "month" or "monthly" => 30,
                _ => -1
            };

            if (days == -1)
            {
                error = "Invalid 'period' value";
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(daysText) && !int.TryParse(daysText, out days))
        {
            error = "Invalid 'days' value";
            return false;
        }

        if (days is < 1 or > MaxRoundStatsDays)
        {
            error = $"'days' must be between 1 and {MaxRoundStatsDays}";
            return false;
        }

        return true;
    }

    private static bool TryParseRoundStatsDate(string? value, out DateTime? date)
    {
        date = null;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        if (!DateTime.TryParse(
                value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return false;
        }

        date = parsed.ToUniversalTime();
        return true;
    }

    private static RoundStatsResponse.Stat[] AggregateRoundStats(IEnumerable<string> values, int total)
    {
        return values
            .GroupBy(value => value)
            .Select(group => new RoundStatsResponse.Stat
            {
                Name = group.Key,
                Count = group.Count(),
                Percent = total == 0 ? 0 : Math.Round(group.Count() * 100d / total, 2)
            })
            .OrderByDescending(stat => stat.Count)
            .ThenBy(stat => stat.Name)
            .ToArray();
    }

    private static string NormalizeRoundStatsName(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
    }

    private sealed class RoundStatsResponse
    {
        public required string Server { get; init; }
        public required DateTime From { get; init; }
        public required DateTime To { get; init; }
        public required int TotalRounds { get; init; }
        public required Stat[] Modes { get; init; }
        public required Stat[] Maps { get; init; }
        public required Round[] Rounds { get; init; }

        public sealed class Stat
        {
            public required string Name { get; init; }
            public required int Count { get; init; }
            public required double Percent { get; init; }
        }

        public sealed class Round
        {
            public required int RoundId { get; init; }
            public required DateTime StartedAt { get; init; }
            public required string GameMode { get; init; }
            public required string Map { get; init; }
            public required int? PlayerCount { get; init; }
        }
    }

    private sealed record PlaytimeAddActionBody
    {
        public required Guid UserId { get; init; }
        public string? Tracker { get; init; }
        public int? Minutes { get; init; }
        public PlaytimeAddEntryBody[]? Entries { get; init; }
        public string? Reason { get; init; }
    }

    private sealed class PlaytimeAddEntryBody
    {
        public string? Tracker { get; init; }
        public required int Minutes { get; init; }
    }

    private sealed class PlaytimeJobsResponse
    {
        public required string OverallTracker { get; init; }
        public required PlaytimeJobInfo[] Jobs { get; init; }
    }

    private sealed class PlaytimeResponse
    {
        public required PlaytimePlayerInfo Player { get; init; }
        public required long OverallSeconds { get; init; }
        public required TrackerTime[] Trackers { get; init; }

        public sealed class TrackerTime
        {
            public required string Tracker { get; init; }
            public required long Seconds { get; init; }
            public required PlaytimeJobInfo? Job { get; init; }
        }
    }

    private sealed class PlaytimeAddResponse
    {
        public required PlaytimePlayerInfo Player { get; init; }
        public required string? Reason { get; init; }
        public required Entry[] Entries { get; init; }

        public sealed class Entry
        {
            public required string Tracker { get; init; }
            public required long AddedSeconds { get; init; }
            public required long PreviousSeconds { get; init; }
            public required long NewSeconds { get; init; }
        }
    }

    private sealed class PlaytimePlayerInfo
    {
        public required Guid UserId { get; init; }
        public required string UserName { get; init; }
    }

    private sealed class PlaytimeJobInfo
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string PlayTimeTracker { get; init; }
        public required PlaytimeDepartmentInfo? Department { get; init; }
        public required bool SetPreference { get; init; }
    }

    private sealed class PlaytimeDepartmentInfo
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required bool Primary { get; init; }
    }
}
