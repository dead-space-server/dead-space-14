using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.SaveGamemode;

[AdminCommand(AdminFlags.Debug)]
public sealed class SaveGamemodeCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private static readonly ResPath SaveDir = new("/Saves/GameMode");

    public override string Command => "savegamemode";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine(Loc.GetString("shell-need-exactly-one-argument"));
            return;
        }

        if (shell.Player is null)
        {
            shell.WriteLine("Команда доступна только в игре от имени игрока.");
            return;
        }

        if (!TryGetAttachedEntityMap(shell.Player, out var mapId))
        {
            shell.WriteLine("Не удалось определить карту привязанной сущности.");
            return;
        }

        var saveSystem = _entityManager.System<SaveSystem>();
        var path = SaveDir / args[0] / "map.yml";

        if (saveSystem.TrySaveMap(mapId, path, force: true))
        {
            shell.WriteLine($"Сохранение '{args[0]}' выполнено успешно.");
        }
        else
        {
            shell.WriteLine($"Не удалось сохранить '{args[0]}'. Смотрите логи сервера.");
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHint("Название сохранения"),
            _ => CompletionResult.Empty,
        };
    }

    private bool TryGetAttachedEntityMap(ICommonSession player, out MapId mapId)
    {
        mapId = MapId.Nullspace;

        if (player.AttachedEntity is not { } uid)
            return false;

        var xform = _entityManager.GetComponent<TransformComponent>(uid);
        mapId = xform.MapID;

        return mapId != MapId.Nullspace;
    }
}
