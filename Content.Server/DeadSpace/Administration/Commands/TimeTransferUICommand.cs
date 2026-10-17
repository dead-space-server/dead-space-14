using Content.Server.DeadSpace.Administration.UI;
using Content.Shared.Administration;
using Content.Server.Administration;
using Robust.Shared.Console;
using Content.Server.EUI;

namespace Content.Server.DeadSpace.Administration.Commands;

[AdminCommand(AdminFlags.PlayTime)]
public sealed class TimeTransferCommand : LocalizedEntityCommands
{
    [Dependency] private readonly EuiManager _eui = default!;

    public override string Command => "timetransferui";

    public override string Description => Loc.GetString("time-transfer-command-description");

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("cmd-player-only"));
            return;
        }

        _eui.OpenEui(new TimeTransferEui(), player);
    }
}