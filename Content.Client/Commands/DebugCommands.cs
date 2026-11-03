using Content.Client.Markers;
using Content.Client.Popups;
using Content.Client.SubFloor;
using Content.Shared.Eye; //DS-14
using Robust.Shared.Console;

namespace Content.Client.Commands;

internal sealed class ShowMarkersCommand : LocalizedEntityCommands
{
    [Dependency] private readonly MarkerSystem _markerSystem = default!;

    public override string Command => "showmarkers";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _markerSystem.MarkersVisible ^= true;
    }
}

internal sealed class ShowSubFloor : LocalizedEntityCommands
{
    [Dependency] private readonly SubFloorHideSystem _subfloorSystem = default!;

    public override string Command => "showsubfloor";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        //DS-14 start
        if (args.Length == 0 || args[0] == "all")
        {
            _subfloorSystem.ShowAll = !_subfloorSystem.ShowAll;
            return;
        }

        var layer = args[0].ToLowerInvariant() switch
        {
            "cable" or "cables" => VisibilityFlags.SubfloorCable,
            "atmos" or "pipes" => VisibilityFlags.SubfloorAtmos,
            "disposal" or "mail" or "beacons" => VisibilityFlags.SubfloorDisposal,
            _ => VisibilityFlags.None
        };

        if (layer == VisibilityFlags.None)
        {
            shell.WriteError(Help);
            return;
        }

        _subfloorSystem.ToggleLayers(layer);
        //DS-14 end
    }

    //DS-14 start
    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
            return CompletionResult.Empty;

        return CompletionResult.FromOptions(new[]
        {
            "cable",
            "atmos",
            "disposal",
            "all",
        });
    }
    //DS-14 end
}

internal sealed class NotifyCommand : LocalizedEntityCommands
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;

    public override string Command => "notify";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _popupSystem.PopupCursor(args[0]);
    }
}
