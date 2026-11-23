// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.DeadSpace.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class PvsDirtyCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entities = default!;

    public string Command => "pvsdirty";
    public string Description => "Show which components and prototypes change every tick and how many players see them.";
    public string Help => "Usage: pvsdirty [seconds]\nDefault 5 seconds, at most 30. Rows are sorted by entity x player per tick, the load on game state serialization.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var seconds = 5;
        if (args.Length > 1)
        {
            shell.WriteError("Usage: pvsdirty [seconds]");
            return;
        }

        if (args.Length == 1 && (!int.TryParse(args[0], out seconds) || seconds < 1 || seconds > 30))
        {
            shell.WriteError("Seconds must be from 1 to 30.");
            return;
        }

        var census = _entities.System<PvsDirtySystem>();
        if (!census.Start(shell, seconds, out var error))
        {
            shell.WriteError(error);
            return;
        }

        shell.WriteLine($"Sampling {seconds} s.");
    }
}
