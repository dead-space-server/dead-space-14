// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.DeadSpace.PlanetSurvey;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class SurveyDebugCommand : LocalizedEntityCommands
{
    [Dependency] private SurveyDebugSystem _debug = default!;

    public override string Command => "surveydebug";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("cmd-player-only"));
            return;
        }

        shell.WriteLine(Loc.GetString(_debug.Toggle(player) ? "cmd-surveydebug-on" : "cmd-surveydebug-off"));
    }
}
