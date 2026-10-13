// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.DeadSpace.Administration.GameRules;
using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.CentComm;

[AdminCommand(AdminFlags.Fun)]
public sealed class AddGameRuleCentCommCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly CentCommSystem _centcomm = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly GameRulesServerSystem _rules = default!;

    public override string Command => "addgamerulecentcomm";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is { } player && !_admin.HasAdminFlag(player, AdminFlags.Fun))
        {
            shell.WriteError(Loc.GetString("centcomm-permission-denied"));
            return;
        }

        if (args.Length == 0)
        {
            shell.WriteError(Help);
            return;
        }

        if (_ticker.RunLevel != GameRunLevel.InRound)
        {
            shell.WriteError(Loc.GetString("cmd-addgamerulecentcomm-round-required"));
            return;
        }

        var station = _station.GetOwningStation(_roundEnd.GetCentcommGridEntity());
        if (station is not { } target || !EntityManager.HasComponent<CentCommStationComponent>(target))
        {
            shell.WriteError(Loc.GetString("cmd-addgamerulecentcomm-no-station"));
            return;
        }

        foreach (var id in args)
        {
            if (!_prototypes.TryIndex<EntityPrototype>(id, out var prototype) || !_centcomm.IsAllowedRule(prototype))
            {
                shell.WriteError(Loc.GetString("cmd-addgamerulecentcomm-not-allowed", ("rule", id)));
                continue;
            }

            var rule = _ticker.AddGameRule(id, target);
            _rules.RecordAdmin(EntityManager.GetNetEntity(rule), shell.Player?.Name);
            _adminLog.Add(LogType.EventStarted, $"{shell.Player?.Name ?? "Server console"} added game rule {id} to CentComm station {EntityManager.ToPrettyString(target)}");
            _ticker.StartGameRule(rule);
            shell.WriteLine(Loc.GetString("cmd-addgamerulecentcomm-added", ("rule", id)));
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (shell.Player is { } player && !_admin.HasAdminFlag(player, AdminFlags.Fun))
            return CompletionResult.Empty;

        return CompletionResult.FromHintOptions(
            _ticker.GetAllGameRulePrototypes().Where(_centcomm.IsAllowedRule).Select(prototype => prototype.ID),
            Loc.GetString("cmd-addgamerulecentcomm-hint"));
    }
}
