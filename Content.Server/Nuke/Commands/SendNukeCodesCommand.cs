using Content.Server.Administration;
using Content.Server.DeadSpace.Nuke;
using Content.Shared.Administration;
using Content.Shared.DeadSpace.Nuke;
using Content.Shared.Station.Components;
using Robust.Shared.Console;

namespace Content.Server.Nuke.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed partial class SendNukeCodesCommand : LocalizedEntityCommands
{
    [Dependency] private NukeCodeSendQueueSystem _nukeCodeQueue = default!; // DS14

    public override string Command => "nukecodes";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-need-exactly-one-argument"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var uidNet) || !EntityManager.TryGetEntity(uidNet, out var uid))
        {
            shell.WriteError(Loc.GetString("shell-entity-uid-must-be-number"));
            return;
        }

        // DS14-Start: route manual nuke-code sends through the approval queue.
        var requester = shell.Player?.Name ?? Loc.GetString("nuke-codes-requester-server-console");
        if (!_nukeCodeQueue.TryQueueAdminRequest(
                uid.Value,
                NukeCodeSendReasonIds.Manual,
                requester,
                out var result))
        {
            shell.WriteError(result ?? Loc.GetString("nuke-codes-admin-queue-failed"));
            return;
        }

        shell.WriteLine(result ?? Loc.GetString("nuke-codes-admin-queued"));
        // DS14-End
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
            return CompletionResult.Empty;

        var stations = new List<CompletionOption>();
        var query = EntityManager.EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            var meta = EntityManager.GetComponent<MetaDataComponent>(uid);

            stations.Add(new CompletionOption(uid.ToString(), meta.EntityName));
        }

        return CompletionResult.FromHintOptions(stations, null);
    }
}
