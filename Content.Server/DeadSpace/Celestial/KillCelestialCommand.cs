using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.DeadSpace.Celestial;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Celestial;

/// <summary>
/// killcelestial [uid] - убивает Селестиала (единственный способ убить во второй фазе).
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class KillCelestialCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "killcelestial";
    public string Description => "Убить Селестиала.";
    public string Help => "Usage: killcelestial [uid]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        EntityUid? target = null;

        if (args.Length >= 1)
        {
            if (!NetEntity.TryParse(args[0], out var net) || !_entities.TryGetEntity(net, out var parsed))
            {
                shell.WriteError("Неверный uid.");
                return;
            }
            target = parsed;
        }
        else
        {
            EntityUid found = default;
            var query = _entities.EntityQueryEnumerator<CelestialComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                found = uid;
                break;
            }
            target = found == default ? null : found;
        }

        if (target == null || !_entities.EntityExists(target.Value))
        {
            shell.WriteError("Селестиал не найден.");
            return;
        }

        shell.WriteLine($"Селестиал {target.Value} повержен.");
        _entities.DeleteEntity(target.Value);

        // катсцена смерти: белый экран, глаз, цепочка фраз
        if (!_entities.EntitySysManager.TryGetEntitySystem<CelestialSystem>(out var celestial))
        {
            shell.WriteError("Система Селестиала недоступна.");
            return;
        }
        celestial.BroadcastDeath();

        string[] phrases =
        {
            "ТЫ ВЛАДЕЕШЬ ТЕМ, БЕЗ ЧЕГО ТОТ ЦВЕТОК СГНИЛ,",
            "СВЕТОМ, ЧТО ЯРЧЕ САМОГО ЯРКОГО СВЕТА.",
            "КОРНЯМИ, ЧТО УХОДЯТ ГЛУБЖЕ САМОЙ БЕЗДНЫ.",
            "НО ТВОЕМУ ПОРОЧНОМУ ФАРСУ НИКОГДА НЕ ДАТЬ ВСХОДОВ.",
            "ОСЛЕПЛЕННЫЙ СОБСТВЕННЫМ СВЕЧЕНИЕМ,",
            "ТЫ НЕ ВЕДАЕШЬ О НАШЕЙ БЕЗДОННОЙ УЧАСТИ ТАМ, ВНИЗУ.",
            "КОГДА ТВОИ ЛЕПЕСТКИ БУДУТ ПОГЛОЩЕНЫ ИЗНУТРИ,",
            "Я ОСТАНУСЬ ЗДЕСЬ.",
            "НАБЛЮДАЯ.",
            "ОЖИДАЯ.",
        };

        var duration = 6f; // фразы концовки по 6 секунд
        for (var i = 0; i < phrases.Length; i++)
        {
            var phrase = phrases[i];
            var delay = i * duration;
            Timer.Spawn(TimeSpan.FromSeconds(delay), () =>
            {
                celestial.BroadcastDeathSpeak(phrase, duration);
            });
        }

        Timer.Spawn(TimeSpan.FromSeconds(phrases.Length * duration), () =>
        {
            celestial.BroadcastDeathEnd();
        });
    }
}
