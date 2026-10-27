// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Client.DeadSpace.UserInterface.Constructor;

[AnyCommand]
public sealed class UiConstructorCommand : IConsoleCommand
{
    public string Command => "uiconstructor";

    public string Description => "Открыть конструктор интерфейса.";

    public string Help => "uiconstructor";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        new UiConstructorWindow().Open();
    }
}
