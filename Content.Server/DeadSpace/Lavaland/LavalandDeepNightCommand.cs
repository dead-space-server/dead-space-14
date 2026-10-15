// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Administration;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// lavalanddeepnight [phase] - starts the phase (night by default) on every lavaland, turning it into a deep night.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class LavalandDeepNightCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "lavalanddeepnight";
    public string Description => "Запустить глубокую ночь на Лаваленде (через минуту предупреждения).";
    public string Help => "Usage: lavalanddeepnight [phase index, default 1]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var phase = 1;
        if (args.Length >= 1 && !int.TryParse(args[0], out phase))
        {
            shell.WriteError("Неверный индекс фазы.");
            return;
        }

        var dayNight = _entities.System<LavalandDayNightSystem>();
        var count = 0;
        var query = _entities.EntityQueryEnumerator<LavalandMapComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            dayNight.SetPhase(uid, phase, true);
            count++;
        }

        shell.WriteLine(count == 0 ? "Лаваленд не найден." : $"Глубокая ночь запущена на {count} карт(ах).");
    }
}
