using Content.Shared.Administration;
using Robust.Shared.Console;
using Content.Server.Administration;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using System.Text.RegularExpressions;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Lavaland.Celestial;

/// <summary>
/// mytalkingcelestial [режим 0-3] "фраза" время_в_секундах —
/// показывает субтитры Селестиала всем игрокам без самого босса.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class MyTalkingCelestialCommand : IConsoleCommand
{
    private static readonly Regex PhraseRegex = new("\"(.+?)\"\\s+(\\d+(?:\\.\\d+)?)", RegexOptions.Compiled);

    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "mytalkingcelestial";
    public string Description => "Показать субтитры Селестиала всем игрокам (режим 0-3).";
    public string Help => "Usage: mytalkingcelestial [режим 0-3] \"фраза1\" <сек> [\"фраза2\" <сек> ...]\n" +
                          "0 - розовый текст + звук Celestial_Talk_1..3\n" +
                          "1 - розовый текст + звук Celestial_Talk_6..8\n" +
                          "2 - палитра смерти (белые блоки) + Celestial_Talk_5, как в катсцене killcelestial\n" +
                          "3 - красные буквы + звук Level100Start_Final";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        // Необязательный первый аргумент — режим. ВАЖНО: argStr приходит ЦЕЛИКОМ,
        // вместе с именем команды ("mytalkingcelestial 3 \"фраза\" 5"), поэтому режим
        // берём из args[0], а не из начала строки.
        var mode = 0;
        if (args.Length >= 3 && args[0].Length == 1 && args[0][0] is >= '0' and <= '3')
            mode = args[0][0] - '0';

        // парсим цепочку "фраза" время из сырой строки
        var matches = PhraseRegex.Matches(argStr);
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
            var speakMode = mode;

            if (delay <= 0f)
            {
                celestial.SpeakGlobal(line, dur, speakMode);
            }
            else
            {
                Timer.Spawn(TimeSpan.FromSeconds(delay), () => celestial.SpeakGlobal(line, dur, speakMode));
            }

            shell.WriteLine($"Селестиал говорит: \"{line}\" ({dur} сек, задержка {delay:0.#} сек, режим {speakMode})");
            totalDelay += duration;
            spoken++;
        }

        shell.WriteLine($"Всего фраз: {spoken}, общая длительность {totalDelay:0.#} сек.");
    }
}
