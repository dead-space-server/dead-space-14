// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Console;

namespace Content.Client.DeadSpace.Tiles;

public sealed class ShowInvisibleTilesCommand : LocalizedEntityCommands
{
    [Dependency] private readonly InvisibleTileOverlaySystem _overlay = default!;

    public override string Command => "showinvisibletiles";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_overlay.CanSee())
        {
            shell.WriteError(Loc.GetString("cmd-showinvisibletiles-denied"));
            return;
        }

        _overlay.Enabled = !_overlay.Enabled;
        shell.WriteLine(Loc.GetString(_overlay.Enabled ? "cmd-showinvisibletiles-on" : "cmd-showinvisibletiles-off"));
    }
}
