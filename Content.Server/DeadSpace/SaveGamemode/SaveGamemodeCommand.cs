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
        // «repair <слот>» чинит уже записанный файл: прогоняет по нему пост-процессор и подменяет его,
        // сохранив предыдущую версию в .bak. Нужен, когда сохранение уже есть, но не грузится.
        if (args.Length > 0 && args[0].Equals("repair", StringComparison.OrdinalIgnoreCase))
        {
            Repair(shell, args);
            return;
        }

        if (args.Length is < 1 or > 2)
        {
            shell.WriteLine("Использование: savegamemode <название сохранения> [raw] | savegamemode repair <название сохранения>");
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

        // По умолчанию сохраняем через пост-процессор — ровно так же, как это делает конец раунда.
        // Второй аргумент «raw» пишет карту как есть, для отладки самой сериализации.
        var raw = args.Length == 2 && args[1].Equals("raw", StringComparison.OrdinalIgnoreCase);

        if (raw)
        {
            if (saveSystem.TrySaveMap(mapId, path, force: true))
                shell.WriteLine($"Сохранение '{args[0]}' выполнено успешно (без пост-обработки).");
            else
                shell.WriteLine($"Не удалось сохранить '{args[0]}'. Смотрите логи сервера.");

            return;
        }

        if (saveSystem.TrySaveFilteredSlot(mapId, args[0], out var report))
            shell.WriteLine($"Сохранение '{args[0]}' выполнено успешно. {report}");
        else
            shell.WriteLine($"Не удалось сохранить '{args[0]}'. Старое сохранение не тронуто, смотрите логи сервера.");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(["repair"], "Название сохранения либо «repair»"),
            2 => args[0].Equals("repair", StringComparison.OrdinalIgnoreCase)
                ? CompletionResult.FromHint("Название исправляемого сохранения")
                : CompletionResult.FromHintOptions(["raw"], "raw — без пост-обработки карты"),
            _ => CompletionResult.Empty,
        };
    }

    /// <summary>
    ///     Повторно прогоняет пост-процессор по файлу сохранения на диске.
    /// </summary>
    private void Repair(IConsoleShell shell, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteLine("Использование: savegamemode repair <название сохранения>");
            return;
        }

        var saveSystem = _entityManager.System<SaveSystem>();

        if (!saveSystem.SlotExists(args[1]))
        {
            shell.WriteLine($"Сохранение '{args[1]}' не найдено.");
            return;
        }

        if (saveSystem.TryFilterSaveFile(SaveSystem.GetSavePath(args[1]), out var report))
            shell.WriteLine($"Сохранение '{args[1]}' исправлено. {report}");
        else
            shell.WriteLine($"Не удалось исправить '{args[1]}'. Файл не тронут, смотрите логи сервера.");
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
