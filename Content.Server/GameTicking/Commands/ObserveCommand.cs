using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.GameTicking;
using Robust.Server.Console;
using Robust.Shared.Console;

namespace Content.Server.GameTicking.Commands
{
    [AnyCommand]
    sealed class ObserveCommand : IConsoleCommand
    {
        [Dependency] private readonly IEntityManager _e = default!;
        [Dependency] private readonly IAdminManager _adminManager = default!;
        // DS14-start
        [Dependency] private readonly IConGroupController _conGroupController = default!;
        [Dependency] private readonly IServerConsoleHost _consoleHost = default!;
        // DS14-end

        public string Command => "observe";
        public string Description => "";
        public string Help => "";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (shell.Player is not { } player)
            {
                shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
                return;
            }

            var ticker = _e.System<GameTicker>();

            if (ticker.RunLevel == GameRunLevel.PreRoundLobby)
            {
                shell.WriteError("Wait until the round starts.");
                return;
            }

            var isAdminCommand = args.Length > 0 && args[0].ToLower() == "admin";

            if (!isAdminCommand && _adminManager.IsAdmin(player))
            {
                _adminManager.DeAdmin(player);
            }

            if (ticker.PlayerGameStatuses.TryGetValue(player.UserId, out var status) &&
                status != PlayerGameStatus.JoinedGame)
            {
                ticker.JoinAsObserver(player);

                // DS14-start
                if (isAdminCommand && _conGroupController.CanCommand(player, "aghost"))
                    _consoleHost.ExecuteCommand(player, "aghost");
                // DS14-end
            }
            else
            {
                shell.WriteError($"{player.Name} is not in the lobby.   This incident will be reported.");
            }
        }
    }
}
