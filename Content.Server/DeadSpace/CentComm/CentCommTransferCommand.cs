// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.DeadSpace.CentComm;
using Robust.Shared.Console;

namespace Content.Server.DeadSpace.CentComm;

[AdminCommand(AdminFlags.Fun)]
public sealed class CentCommTransferCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly CentCommTransferSystem _transfer = default!;
    [Dependency] private readonly EuiManager _eui = default!;

    public override string Command => "centcomm_transfer";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is { } player && !_admin.HasAdminFlag(player, AdminFlags.Fun))
        {
            shell.WriteError(Loc.GetString("centcomm-permission-denied"));
            return;
        }

        if (args.Length == 0 && shell.Player is { } session)
        {
            _eui.OpenEui(new CentCommTransferEui(), session);
            return;
        }

        if (args.Length != 3)
        {
            shell.WriteError(Help);
            return;
        }

        var temperature = args[1].ToLowerInvariant() switch
        {
            "space" or "космос" => CentCommTemperature.Space,
            "cold" or "холодно" => CentCommTemperature.Cold,
            "normal" or "нормально" => CentCommTemperature.Normal,
            "hot" or "горячо" => CentCommTemperature.Hot,
            _ => (CentCommTemperature?) null,
        };
        if (temperature == null)
        {
            shell.WriteError(Help);
            return;
        }

        var weather = args[2].ToLowerInvariant() is "none" or "null" or "нет" ? null : args[2];
        var request = new CentCommTransferRequest(args[0], temperature.Value, 0f, weather);
        if (_transfer.TryStart(shell.Player, request, out var result))
            shell.WriteLine(result);
        else
            shell.WriteError(result);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (shell.Player is { } player && !_admin.HasAdminFlag(player, AdminFlags.Fun))
            return CompletionResult.Empty;

        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(_transfer.GetParallaxes(), Loc.GetString("centcomm-transfer-parallax")),
            2 => CompletionResult.FromHintOptions(["космос", "холодно", "нормально", "горячо"], Loc.GetString("centcomm-transfer-temperature")),
            3 => CompletionResult.FromHintOptions(_transfer.GetWeather().Prepend("none"), Loc.GetString("centcomm-transfer-weather")),
            _ => CompletionResult.Empty,
        };
    }
}
