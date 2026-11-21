// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.DeadSpace.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class TickLoadCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entities = default!;

    public string Command => "tickload";
    public string Description => "Show which systems spend server tick time.";
    public string Help => "Usage: tickload [seconds]\nDefault 5 seconds, at most 30. The report has averages, the worst tick, and a physics split.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var seconds = 5;
        if (args.Length > 1)
        {
            shell.WriteError("Usage: tickload [seconds]");
            return;
        }

        if (args.Length == 1 && (!int.TryParse(args[0], out seconds) || seconds < 1 || seconds > 30))
        {
            shell.WriteError("Seconds must be from 1 to 30.");
            return;
        }

        var load = _entities.System<TickLoadSystem>();
        if (!load.Start(shell, seconds, out var error))
        {
            shell.WriteError(error);
            return;
        }

        shell.WriteLine($"Sampling {seconds} s. The report goes here and to the server terminal.");
    }
}
