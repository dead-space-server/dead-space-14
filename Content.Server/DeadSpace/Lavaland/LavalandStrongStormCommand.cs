// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Administration;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// lavalandstrongstorm - the alarm of the strong storm on every lavaland, whatever the time of the round; the wind
/// and its boss follow as usual.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class LavalandStrongStormCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "lavalandstrongstorm";
    public string Description => "Вызвать сильную бурю на Лаваленде (через минуту тревоги).";
    public string Help => "Usage: lavalandstrongstorm";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var storms = _entities.System<LavalandStrongStormSystem>();
        var count = 0;
        var query = _entities.EntityQueryEnumerator<LavalandMapComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (storms.Force(uid))
                count++;
        }

        shell.WriteLine(count == 0
            ? "Лаваленд не найден, сильная буря выключена или уже идёт."
            : $"Сильная буря поднимается на {count} карт(ах).");
    }
}
