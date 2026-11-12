using System.Collections.Frozen;
using Content.Shared.Speech.Components;

namespace Content.Shared.Speech.EntitySystems;

public sealed partial class OwOAccentSystem : RelayAccentSystem<OwOAccentComponent>
{
    private static readonly FrozenDictionary<string, string> SpecialWords =
        new Dictionary<string, string>
        {
            { "you", "wu" },
            { "ты", "ти" }, // Corvax-Localization
            { "вы", "ви" }, // Corvax-Localization
        }.ToFrozenDictionary();

    public override string Accentuate(string message, Entity<OwOAccentComponent>? ent = null)
    {
        foreach (var (word, repl) in SpecialWords)
        {
            message = message.Replace(word, repl);
        }

        // Corvax-Localization: no "!" -> faces replacement
        return message
            // Corvax-Localization-Start
            .Replace("р", "в").Replace("Р", "В")
            .Replace("л", "в").Replace("Л", "В")
            // Corvax-Localization-End
            .Replace("r", "w")
            .Replace("R", "W")
            .Replace("l", "w")
            .Replace("L", "W");
    }
}
