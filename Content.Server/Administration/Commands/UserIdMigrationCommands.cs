using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Content.Server.Database;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Host)]
public sealed partial class UserIdMigrationDryRunCommand : IConsoleCommand
{
    [Dependency] private IServerDbManager _db = default!;

    public string Command => "migrate_user_uuid_dryrun";
    public string Description => "Shows what would be migrated from a WizDen UUID to an MK UUID.";
    public string Help => $"Usage: {Command} <wizden_uuid> <mk_uuid>";

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!UserIdMigrationCommandHelper.TryParsePair(shell, Help, args, out var oldUserId, out var newUserId))
            return;

        try
        {
            var report = await _db.DryRunUserIdMigrationAsync(oldUserId, newUserId);
            UserIdMigrationCommandHelper.WriteReport(shell, report);
        }
        catch (Exception ex)
        {
            shell.WriteError($"Migration dry-run failed: {ex}");
        }
    }
}

[AdminCommand(AdminFlags.Host)]
public sealed partial class UserIdMigrationApplyCommand : IConsoleCommand
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IPlayerManager _players = default!;

    public string Command => "migrate_user_uuid_apply";
    public string Description => "Migrates local game database rows from a WizDen UUID to an MK UUID.";
    public string Help => $"Usage: {Command} <wizden_uuid> <mk_uuid>";

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!UserIdMigrationCommandHelper.TryParsePair(shell, Help, args, out var oldUserId, out var newUserId))
            return;

        if (UserIdMigrationCommandHelper.TryFindOnlinePlayer(_players, oldUserId, newUserId, out var onlinePlayer))
        {
            shell.WriteError($"Refusing to apply migration while player is online: {onlinePlayer}");
            return;
        }

        try
        {
            var report = await _db.ApplyUserIdMigrationAsync(oldUserId, newUserId);
            UserIdMigrationCommandHelper.WriteReport(shell, report);
        }
        catch (Exception ex)
        {
            shell.WriteError($"Migration apply failed: {ex}");
        }
    }
}

[AdminCommand(AdminFlags.Host)]
public sealed partial class UserIdMigrationBatchCommand : IConsoleCommand
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IPlayerManager _players = default!;

    public string Command => "migrate_user_uuid_batch";
    public string Description => "Runs UUID migration dry-run or apply for a CSV file.";
    public string Help => $"Usage: {Command} <csv-path> [--apply]";

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            shell.WriteError(Help);
            return;
        }

        var apply = false;
        if (args.Length == 2)
        {
            if (!string.Equals(args[1], "--apply", StringComparison.OrdinalIgnoreCase))
            {
                shell.WriteError(Help);
                return;
            }

            apply = true;
        }

        var path = args[0];
        if (!File.Exists(path))
        {
            shell.WriteError($"File does not exist: {path}");
            return;
        }

        string[] lines;
        try
        {
            lines = await File.ReadAllLinesAsync(path, CancellationToken.None);
        }
        catch (Exception ex)
        {
            shell.WriteError($"Failed to read migration file: {ex}");
            return;
        }

        var pairs = new List<(int LineNumber, Guid OldUserId, Guid NewUserId)>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (!UserIdMigrationCommandHelper.TryParseCsvLine(shell, lines[i], i + 1, out var oldUserId, out var newUserId))
                continue;

            pairs.Add((i + 1, oldUserId, newUserId));
        }

        if (apply && !UserIdMigrationCommandHelper.ValidateBatchPairs(shell, pairs))
            return;

        var applied = 0;
        var blocked = 0;
        var failed = 0;

        foreach (var (lineNumber, oldUserId, newUserId) in pairs)
        {
            if (apply && UserIdMigrationCommandHelper.TryFindOnlinePlayer(_players, oldUserId, newUserId, out var onlinePlayer))
            {
                blocked++;
                shell.WriteError($"Line {lineNumber}: refusing to apply migration while player is online: {onlinePlayer}");
                continue;
            }

            try
            {
                var report = apply
                    ? await _db.ApplyUserIdMigrationAsync(oldUserId, newUserId)
                    : await _db.DryRunUserIdMigrationAsync(oldUserId, newUserId);

                UserIdMigrationCommandHelper.WriteBatchReport(shell, report);

                if (!report.CanApply)
                    blocked++;
                else if (report.Applied)
                    applied++;
            }
            catch (Exception ex)
            {
                failed++;
                shell.WriteError($"Line {lineNumber}: migration failed for {oldUserId} -> {newUserId}: {ex}");
            }
        }

        shell.WriteLine($"Batch finished. Parsed={pairs.Count}, Applied={applied}, Blocked={blocked}, Failed={failed}, Mode={(apply ? "apply" : "dry-run")}.");
    }
}

internal static partial class UserIdMigrationCommandHelper
{
    public static bool TryParsePair(
        IConsoleShell shell,
        string help,
        string[] args,
        out Guid oldUserId,
        out Guid newUserId)
    {
        oldUserId = default;
        newUserId = default;

        if (args.Length != 2)
        {
            shell.WriteError(help);
            return false;
        }

        if (!Guid.TryParse(args[0], out oldUserId))
        {
            shell.WriteError($"Invalid WizDen UUID: {args[0]}");
            return false;
        }

        if (!Guid.TryParse(args[1], out newUserId))
        {
            shell.WriteError($"Invalid MK UUID: {args[1]}");
            return false;
        }

        return true;
    }

    public static bool TryFindOnlinePlayer(
        IPlayerManager players,
        Guid oldUserId,
        Guid newUserId,
        out string player)
    {
        foreach (var session in players.Sessions)
        {
            if (session.UserId.UserId != oldUserId && session.UserId.UserId != newUserId)
                continue;

            player = $"{session.Name} ({session.UserId})";
            return true;
        }

        player = string.Empty;
        return false;
    }

    public static bool ValidateBatchPairs(
        IConsoleShell shell,
        IReadOnlyList<(int LineNumber, Guid OldUserId, Guid NewUserId)> pairs)
    {
        var valid = true;
        var oldLines = new Dictionary<Guid, int>();
        var newLines = new Dictionary<Guid, int>();

        foreach (var (lineNumber, oldUserId, newUserId) in pairs)
        {
            if (oldUserId == newUserId)
            {
                shell.WriteError($"Line {lineNumber}: old and MK UUID are the same.");
                valid = false;
            }

            if (!oldLines.TryAdd(oldUserId, lineNumber))
            {
                shell.WriteError($"Line {lineNumber}: old UUID is already used as old UUID on line {oldLines[oldUserId]}.");
                valid = false;
            }

            if (!newLines.TryAdd(newUserId, lineNumber))
            {
                shell.WriteError($"Line {lineNumber}: MK UUID is already used as MK UUID on line {newLines[newUserId]}.");
                valid = false;
            }
        }

        foreach (var (lineNumber, oldUserId, newUserId) in pairs)
        {
            if (newLines.TryGetValue(oldUserId, out var targetLine))
            {
                shell.WriteError($"Line {lineNumber}: old UUID is also used as MK UUID on line {targetLine}; chained migrations are not allowed in one apply batch.");
                valid = false;
            }

            if (oldLines.TryGetValue(newUserId, out var sourceLine))
            {
                shell.WriteError($"Line {lineNumber}: MK UUID is also used as old UUID on line {sourceLine}; chained migrations are not allowed in one apply batch.");
                valid = false;
            }
        }

        return valid;
    }

    public static bool TryParseCsvLine(
        IConsoleShell shell,
        string line,
        int lineNumber,
        out Guid oldUserId,
        out Guid newUserId)
    {
        oldUserId = default;
        newUserId = default;

        var trimmed = line.Split('#', 2)[0].Trim();
        if (trimmed.Length == 0)
            return false;

        var parts = trimmed.Split([',', ';', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2 &&
            Guid.TryParse(parts[0], out oldUserId) &&
            Guid.TryParse(parts[1], out newUserId))
        {
            return true;
        }

        if (lineNumber == 1 &&
            (trimmed.Contains("wiz", StringComparison.OrdinalIgnoreCase) ||
             trimmed.Contains("old", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        shell.WriteError($"Line {lineNumber}: expected '<wizden_uuid>,<mk_uuid>'.");
        return false;
    }

    public static void WriteReport(IConsoleShell shell, UserIdMigrationReport report)
    {
        var status = report.AlreadyProcessed
            ? "Already processed"
            : report.Applied
                ? "Applied"
                : "Dry-run";

        shell.WriteLine($"{status} migration {report.OldUserId} -> {report.NewUserId}");
        shell.WriteLine($"CanApply={report.CanApply}, HasOldData={report.HasOldData}");

        foreach (var table in report.Tables)
        {
            if (table.OldRows == 0 && table.NewRows == 0)
                continue;

            shell.WriteLine($"{table.Name}: old={table.OldRows}, mk={table.NewRows}; {table.Action}");
        }

        foreach (var warning in report.Warnings)
            shell.WriteLine($"Warning: {warning}");

        foreach (var error in report.Errors)
            shell.WriteError($"Error: {error}");
    }

    public static void WriteBatchReport(IConsoleShell shell, UserIdMigrationReport report)
    {
        var oldRows = report.Tables.Sum(table => table.OldRows);
        var status = report.AlreadyProcessed
            ? "Already processed"
            : report.Applied
                ? "Applied"
                : "Checked";

        shell.WriteLine($"{status} {report.OldUserId} -> {report.NewUserId}: CanApply={report.CanApply}, OldRows={oldRows}, Warnings={report.Warnings.Count}, Errors={report.Errors.Count}");

        foreach (var warning in report.Warnings)
            shell.WriteLine($"Warning: {report.OldUserId} -> {report.NewUserId}: {warning}");

        foreach (var error in report.Errors)
            shell.WriteError($"Error: {report.OldUserId} -> {report.NewUserId}: {error}");
    }
}
