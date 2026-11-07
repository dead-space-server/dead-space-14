using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.DeadSpace.Terminal;

[AdminCommand(AdminFlags.Admin)]
public sealed class TerminalRootPasswordCommand : LocalizedEntityCommands
{
    [Dependency] private readonly TerminalSystem _terminal = default!;

    public override string Command => "terminalrootpassword";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        shell.WriteLine(Loc.GetString("cmd-terminalrootpassword-result", ("password", _terminal.RootPassword)));
    }
}
