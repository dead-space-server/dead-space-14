// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.DeadSpace.Voting;

[AdminCommand(AdminFlags.Round)]
public sealed partial class ToggleAutoMapVoteCommand : LocalizedEntityCommands
{
    [Dependency] private AutoMapVoteSystem _autoMapVote = default!;

    public override string Command => "toggleautomapvote";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteError(Loc.GetString("shell-need-between-arguments", ("lower", 0), ("upper", 1)));
            return;
        }

        var enabled = _autoMapVote.Enabled;
        if (args.Length == 0)
        {
            enabled = !enabled;
        }
        else if (!bool.TryParse(args[0], out enabled))
        {
            shell.WriteError(Loc.GetString("shell-argument-must-be-boolean"));
            return;
        }

        var result = await _autoMapVote.SetEnabledAsync(enabled);
        if (!result.Success)
        {
            shell.WriteError(result.Error ?? Loc.GetString("auto-map-vote-config-error-db"));
            return;
        }

        shell.WriteLine(Loc.GetString(enabled
            ? "toggle-auto-map-vote-command-enabled"
            : "toggle-auto-map-vote-command-disabled"));
    }
}

[AdminCommand(AdminFlags.Server)]
public sealed partial class SetAutoMapVoteConfigCommand : LocalizedEntityCommands
{
    [Dependency] private AutoMapVoteSystem _autoMapVote = default!;

    public override string Command => "setautomapvoteconfig";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 6 or > 8)
        {
            shell.WriteError(Loc.GetString("set-auto-map-vote-config-command-usage"));
            return;
        }

        if (!int.TryParse(args[0], out var smallMaxPlayers) ||
            !int.TryParse(args[1], out var mediumMaxPlayers) ||
            !int.TryParse(args[2], out var largeMaxPlayers))
        {
            shell.WriteError(Loc.GetString("shell-argument-must-be-number"));
            return;
        }

        var blacklistMaps = string.Empty;
        int? voteDurationSeconds = null;

        if (args.Length == 7)
        {
            if (int.TryParse(args[6], out var parsedDuration))
                voteDurationSeconds = parsedDuration;
            else
                blacklistMaps = args[6];
        }
        else if (args.Length == 8)
        {
            blacklistMaps = args[6];

            if (!int.TryParse(args[7], out var parsedDuration))
            {
                shell.WriteError(Loc.GetString("shell-argument-must-be-number"));
                return;
            }

            voteDurationSeconds = parsedDuration;
        }

        var result = await _autoMapVote.TryApplyConfigurationAsync(
            smallMaxPlayers,
            mediumMaxPlayers,
            largeMaxPlayers,
            args[3],
            args[4],
            args[5],
            blacklistMaps,
            voteDurationSeconds);

        if (!result.Success)
        {
            shell.WriteError(result.Error ?? Loc.GetString("auto-map-vote-config-error-db"));
            return;
        }

        shell.WriteLine(Loc.GetString("set-auto-map-vote-config-command-success"));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length is >= 1 and <= 3)
            return CompletionResult.FromHint(Loc.GetString("set-auto-map-vote-config-command-arg-players"));

        if (args.Length is >= 4 and <= 7)
            return CompletionResult.FromHint(Loc.GetString("set-auto-map-vote-config-command-arg-maps"));

        if (args.Length == 8)
            return CompletionResult.FromHint(Loc.GetString("set-auto-map-vote-config-command-arg-duration"));

        return CompletionResult.Empty;
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed partial class InitiateAutoMapVoteCommand : LocalizedEntityCommands
{
    [Dependency] private AutoMapVoteSystem _autoMapVote = default!;

    public override string Command => "initautomapvote";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteError(Loc.GetString("shell-need-exactly-arguments", ("count", 0)));
            return;
        }

        if (!_autoMapVote.TryInitiateVote(out var error))
        {
            shell.WriteError(error);
            return;
        }

        shell.WriteLine(Loc.GetString("init-auto-map-vote-command-started"));
    }
}
