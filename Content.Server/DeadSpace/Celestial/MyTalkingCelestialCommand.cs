using Content.Shared.Administration;
using Robust.Shared.Console;
using Content.Server.Administration;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using System.Text.RegularExpressions;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Celestial;

/// <summary>
/// mytalkingcelestial "фраза" время_в_секундах —
/// показывает субтитры Селестиала всем игрокам без самого босса.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class MyTalkingCelestialCommand : IConsoleCommand
{
    private static readonly Regex PhraseRegex = new("\"(.+?)\"\\s+(\\d+(?:\\.\\d+)?)", RegexOptions.Compiled);

    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "mytalkingcelestial";
    public string Description => "Показать субтитры Селестиала всем игрокам.";
    public string Help => "Usage: mytalkingcelestial \"фраза1\" <сек> [\"фраза2\" <сек> ...]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        // парсим цепочку "фраза" время из сырой строки
        var matches = Regex.Matches(argStr, "\"(.+?)\"\\s+(\\d+(?:\\.\\d+)?)");
        if (matches.Count == 0)
        {
            shell.WriteError(Help);
            return;
        }

        if (!_entities.EntitySysManager.TryGetEntitySystem<CelestialSystem>(out var celestial))
        {
            shell.WriteError("Система Селестиала недоступна.");
            return;
        }

        var totalDelay = 0f;
        var spoken = 0;
        foreach (Match match in matches)
        {
            var text = match.Groups[1].Value;
            if (!float.TryParse(match.Groups[2].Value, out var duration) || duration <= 0f)
            {
                shell.WriteError($"Время для фразы \"{text}\" должно быть положительным числом.");
                continue;
            }

            var delay = totalDelay;
            var line = text;
            var dur = duration;

            if (delay <= 0f)
            {
                celestial.SpeakGlobal(line, dur);
            }
            else
            {
                Timer.Spawn(TimeSpan.FromSeconds(delay), () => celestial.SpeakGlobal(line, dur));
            }

            shell.WriteLine($"Селестиал говорит: \"{line}\" ({dur} сек, задержка {delay:0.#} сек)");
            totalDelay += duration;
            spoken++;
        }

        shell.WriteLine($"Всего фраз: {spoken}, общая длительность {totalDelay:0.#} сек.");
    }
}
