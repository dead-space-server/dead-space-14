// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Administration;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>lavalandraid - starts a fauna raid on the outpost of every lavaland right now (the warning first).</summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class LavalandFaunaRaidCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "lavalandraid";
    public string Description => "Принудительно запустить рейд фауны на базу утилей Лаваленда (с предупреждением).";
    public string Help => "Usage: lavalandraid";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var raids = _entities.System<LavalandFaunaRaidSystem>();
        var started = 0;
        var running = 0;
        var query = _entities.EntityQueryEnumerator<LavalandMapComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (raids.IsRaidRunning(uid))
            {
                running++;
                continue;
            }

            raids.ForceRaid(uid);
            if (raids.IsRaidRunning(uid))
                started++;
        }

        if (started > 0)
            shell.WriteLine($"Рейд фауны запущен на {started} карт(ах).");
        else if (running > 0)
            shell.WriteLine("Рейд уже идёт.");
        else
            shell.WriteError("Лаваленд с базой утилей не найден или рейды на нём выключены.");
    }
}
