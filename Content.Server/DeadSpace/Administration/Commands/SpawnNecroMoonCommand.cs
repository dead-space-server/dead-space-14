// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.DeadSpace.Necromorphs.Necroobelisk;
using Robust.Shared.Console;

namespace Content.Server.DeadSpace.Administration.Commands;

/// <summary>
/// spawnnecromoon [uid] - принудительно вызывает появление луны из обелиска.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class SpawnNecroMoonCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "spawnnecromoon";
    public string Description => "Принудительно заспавнить луну из обелиска.";
    public string Help => "Usage: spawnnecromoon [uid]\nБез аргумента берётся первый найденный обелиск.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        EntityUid? obelisk = null;

        if (args.Length >= 1)
        {
            if (!NetEntity.TryParse(args[0], out var net) || !_entities.TryGetEntity(net, out var parsed))
            {
                shell.WriteError("Неверный uid.");
                return;
            }

            obelisk = parsed;
            if (!_entities.EntityExists(obelisk))
            {
                shell.WriteError($"Сущность {obelisk} не существует.");
                return;
            }

            if (!_entities.HasComponent<NecroobeliskComponent>(obelisk)
                && !_entities.HasComponent<SuperMatterialNecroObeliskComponent>(obelisk))
            {
                shell.WriteError($"Сущность {obelisk} не является обелиском.");
                return;
            }
        }
        else
        {
            var query = _entities.EntityQueryEnumerator<NecroobeliskComponent>();
            if (query.MoveNext(out var firstObelisk, out _))
                obelisk = firstObelisk;

            if (obelisk == null)
            {
                var superQuery = _entities.EntityQueryEnumerator<SuperMatterialNecroObeliskComponent>();
                if (superQuery.MoveNext(out var superObelisk, out _))
                    obelisk = superObelisk;
            }

            if (obelisk == null)
            {
                shell.WriteError("Обелиск не найден. Укажите uid: spawnnecromoon <uid>.");
                return;
            }
        }

        var ev = new NecroMoonAppearanceEvent();
        _entities.EventBus.RaiseLocalEvent(obelisk.Value, ref ev);

        shell.WriteLine($"Луна вызвана из обелиска {obelisk}.");
    }
}
