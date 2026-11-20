using System.Linq;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class PlayerPanelCommand : LocalizedCommands
{
    [Dependency] private IPlayerLocator _locator = default!;
    [Dependency] private EuiManager _euis = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IEntityManager _entities = default!; // DS14

    public override string Command => "playerpanel";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } admin)
        {
            shell.WriteError(Loc.GetString("cmd-playerpanel-server"));
            return;
        }

        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("cmd-playerpanel-invalid-arguments"));
            return;
        }

        // DS14-start
        LocatedPlayerData? queriedPlayer;
        if (NetEntity.TryParse(args[0], out var netEntity) &&
            _entities.TryGetEntity(netEntity, out var entity) &&
            _entities.TryGetComponent(entity.Value, out ActorComponent? actor))
        {
            queriedPlayer = await _locator.LookupIdAsync(actor.PlayerSession.UserId);
        }
        else
        {
            queriedPlayer = await _locator.LookupIdByNameOrIdAsync(args[0]);
        }
        // DS14-end

        if (queriedPlayer == null)
        {
            shell.WriteError(Loc.GetString("cmd-playerpanel-invalid-player"));
            return;
        }

        var ui = new PlayerPanelEui(queriedPlayer);
        _euis.OpenEui(ui, admin);
        ui.SetPlayerState();
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = _players.Sessions.OrderBy(c => c.Name).Select(c => c.Name).ToArray();

            return CompletionResult.FromHintOptions(options, LocalizationManager.GetString("cmd-playerpanel-completion"));
        }

        return CompletionResult.Empty;
    }
}
