// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Globalization;
using Content.Shared.Administration;
using Content.Shared.DeadSpace.StationAI.UI;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;

namespace Content.Client.DeadSpace.StationAI;

[AnyCommand]
public sealed class StationAiTrackEntityCommand : IConsoleCommand
{
    [Dependency] private readonly IEntitySystemManager _entitySystem = default!;

    public string Command => "ai_track_entity";
    public string Description => Loc.GetString("cmd-ai-track-entity-desc");
    public string Help => Loc.GetString("cmd-ai-track-entity-help", ("command", Command));

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2 ||
            !NetEntity.TryParse(args[0], out var target) ||
            !double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var sourceSeconds))
        {
            shell.WriteLine(Help);
            return;
        }

        _entitySystem.GetEntitySystem<StationAiTrackEntitySystem>()
            .Track(target, TimeSpan.FromSeconds(sourceSeconds));
    }
}

public sealed class StationAiTrackEntitySystem : EntitySystem
{
    public void Track(NetEntity target, TimeSpan sourceTime)
    {
        RaiseNetworkEvent(new StationAiTrackEntityNetworkEvent(target, sourceTime));
    }
}
