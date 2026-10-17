using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.DeadSpace.Administration;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Eui;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Server.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Administration.UI;

public sealed class TimeTransferEui : BaseEui
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly PlayTimeTrackingManager _playTime = default!;

    private const int MaxMinutes = 1_000_000;

    private TimeTransferEuiState _state = new();

    public TimeTransferEui()
    {
        IoCManager.InjectDependencies(this);
    }

    public override EuiStateBase GetNewState()
    {
        return _state;
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not TimeTransferEuiMessage message)
            return;

        if (!_admin.HasAdminFlag(Player, AdminFlags.PlayTime))
        {
            Close();
            return;
        }

        var ckey = message.Ckey.Trim();

        if (!_players.TryGetSessionByUsername(ckey, out var session))
        {
            SetStatus(Loc.GetString("time-transfer-status-no-player", ("ckey", ckey)), false);
            return;
        }

        var set = message.Mode == TimeTransferMode.Set;
        var applied = 0;
        var skipped = 0;
        var overallChanged = false;
        var overallValue = 0;
        var seen = new HashSet<string>();

        if (message.OverallMinutes is { } overall && (set || overall != 0))
        {
            if (set ? overall is < 0 or > MaxMinutes : overall is > MaxMinutes or < -MaxMinutes)
            {
                skipped++;
            }
            else
            {
                var delta = set
                    ? TimeSpan.FromMinutes(overall) - _playTime.GetOverallPlaytime(session)
                    : TimeSpan.FromMinutes(overall);

                if (delta != TimeSpan.Zero)
                    _playTime.AddTimeToOverallPlaytime(session, delta);

                overallChanged = true;
                overallValue = overall;

                _adminLog.Add(LogType.Action,
                    LogImpact.Medium,
                    $"{Player:actor} {(set ? "set" : "added")} {overall} minutes of overall playtime for {session:subject} via TimeTransferui");
            }
        }

        foreach (var (tracker, minutes) in message.Entries)
        {
            if (!set && minutes == 0)
                continue;

            if (minutes > MaxMinutes
                || minutes < (set ? 0 : -MaxMinutes)
                || !seen.Add(tracker)
                || !_prototype.HasIndex<PlayTimeTrackerPrototype>(tracker))
            {
                skipped++;
                continue;
            }

            var delta = set
                ? TimeSpan.FromMinutes(minutes) - _playTime.GetPlayTimeForTracker(session, tracker)
                : TimeSpan.FromMinutes(minutes);

            if (delta != TimeSpan.Zero)
                _playTime.AddTimeToTracker(session, tracker, delta);

            applied++;

            _adminLog.Add(LogType.Action,
                LogImpact.Medium,
                $"{Player:actor} {(set ? "set" : "added")} {minutes} minutes on tracker {tracker} for {session:subject} via TimeTransferui");
        }

        if (applied == 0 && skipped == 0 && !overallChanged)
        {
            SetStatus(Loc.GetString("time-transfer-status-no-entries"), false);
            return;
        }

        SetStatus(Loc.GetString(set ? "time-transfer-status-set-success" : "time-transfer-status-success",
                ("player", session.Name),
                ("count", applied),
                ("overall", overallValue),
                ("skipped", skipped)),
            applied > 0 || overallChanged);
    }

    private void SetStatus(string status, bool success)
    {
        _state = new TimeTransferEuiState(status, success);
        StateDirty();
    }
}