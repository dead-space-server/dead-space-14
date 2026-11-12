using System.Text.RegularExpressions;
using Content.Shared.Random.Helpers; // Corvax-Localization
using Content.Shared.Speech.Components;
using Robust.Shared.Random; // Corvax-Localization
using Robust.Shared.Timing; // Corvax-Localization

namespace Content.Shared.Speech.EntitySystems;

public sealed partial class MothAccentSystem : RelayAccentSystem<MothAccentComponent>
{
    // Corvax-Localization-Start
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    // Corvax-Localization-End

    private static readonly Regex RegexLowerBuzz = new("z{1,3}");
    private static readonly Regex RegexUpperBuzz = new("Z{1,3}");

    public override string Accentuate(string message, Entity<MothAccentComponent>? ent = null)
    {
        // buzzz
        message = RegexLowerBuzz.Replace(message, "zzz");
        // buZZZ
        message = RegexUpperBuzz.Replace(message, "ZZZ");

        // Corvax-Localization-Start
        var random = ent.HasValue
            ? SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(ent.Value))
            : _random;

        // ж => жжж
        message = Regex.Replace(message, "ж+", random.Pick(new List<string> { "жж", "жжж" }));
        // Ж => ЖЖЖ
        message = Regex.Replace(message, "Ж+", random.Pick(new List<string> { "ЖЖ", "ЖЖЖ" }));
        // з => ззз
        message = Regex.Replace(message, "з+", random.Pick(new List<string> { "зз", "ззз" }));
        // З => ЗЗЗ
        message = Regex.Replace(message, "З+", random.Pick(new List<string> { "ЗЗ", "ЗЗЗ" }));
        // Corvax-Localization-End

        return message;
    }
}
