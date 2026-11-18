// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.DeadSpace.CentCommFax;

[AdminCommand(AdminFlags.Fun)]
public sealed class CentCommFaxCommand : LocalizedEntityCommands
{
    [Dependency] private EuiManager _eui = default!;

    public override string Command => "centcommfax";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("cmd-player-only"));
            return;
        }

        _eui.OpenEui(new CentCommFaxEui(), player);
    }
}
