// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Communications;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands
{
    [AdminCommand(AdminFlags.Round)]
    public sealed partial class LockShuttleCommand : LocalizedEntityCommands
    {
        [Dependency] private CommunicationsConsoleSystem _comms = default!;

        public override string Command => "lockshuttle";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            _comms.ToggleLockEvac();
        }
    }
}
