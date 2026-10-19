// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Administration;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.DeadSpace.Lavaland.SecretBoss;

/// <summary>
/// celestialopen - opens the pit and the Celestial's maze on lavaland without waiting for the arena bosses to die:
/// on the lavaland the admin stands on, or on every lavaland otherwise.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class LavalandCelestialOpenCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "celestialopen";
    public string Description => "Открыть яму и лабиринт Селестиала на Лаваленде без выполнения условий.";
    public string Help => "Usage: celestialopen";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var system = _entities.System<LavalandSecretBossSystem>();

        // Where the admin stands, if that is a lavaland.
        if (shell.Player?.AttachedEntity is { } player &&
            _entities.GetComponent<TransformComponent>(player).MapUid is { } here &&
            _entities.HasComponent<LavalandMapComponent>(here))
        {
            shell.WriteLine(system.ForceOpen(here)
                ? "Яма Селестиала открывается на этом Лаваленде."
                : "Здесь яма уже открыта, бой пройден или Селестиал выключен.");
            return;
        }

        var opened = 0;
        var query = _entities.EntityQueryEnumerator<LavalandMapComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (system.ForceOpen(uid))
                opened++;
        }

        if (opened == 0)
            shell.WriteError("Не на чем открыть: Лаваленда нет, яма уже открыта или Селестиал выключен.");
        else
            shell.WriteLine($"Яма Селестиала открывается на {opened} Лаваленде(ах).");
    }
}
