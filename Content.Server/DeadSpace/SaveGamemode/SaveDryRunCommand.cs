using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Log;
using Robust.Shared.IoC;

namespace Content.Server.DeadSpace.SaveGamemode;

/// <summary>
///     Прогон сохранения без записи на диск: показывает, на каком этапе из сохранения исчезают существа.
/// </summary>
/// <remarks>
///     Отдельная команда, а не флаг <c>saveaudit</c>: прогон сериализует карту дважды и занимает несколько
///     секунд, поэтому его включают осознанно и только посреди раунда, когда существа на карте есть.
/// </remarks>
[AdminCommand(AdminFlags.Debug)]
public sealed class SaveDryRunCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "savedryrun";
    public string Description => "Пробная запись сохраняемой карты: где теряются существа";
    public string Help => $"Usage: {Command}";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var report = _entities.System<SaveAuditSystem>().BuildDryRun();
        shell.WriteLine(report);
        Logger.GetSawmill("saveaudit").Info(report);
    }
}
