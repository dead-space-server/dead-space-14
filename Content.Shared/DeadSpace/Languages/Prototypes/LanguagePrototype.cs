// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Audio;

namespace Content.Shared.DeadSpace.Languages.Prototypes;

[Prototype]
public sealed partial class LanguagePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public SpriteSpecifier? Icon = null;

    [DataField]
    public List<string> Lexicon = new();

    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField]
    public bool GenerateTTSForLexicon = true;

    /// <summary>
    ///     Whether entities without an explicit list of known languages understand this language.
    /// </summary>
    [DataField]
    public bool CanBeUnderstoodWithoutKnowledge = true;

    [DataField]
    public SoundSpecifier? LexiconSound;

    /// <summary>
    ///     Режим генерации речи для языка.
    ///     Определяет, какой метод трансформации будет применён:
    ///     - Lexicon — выбор слова из словаря.
    ///     - Alphabet — генерация строки из алфавита.
    ///     - Syllable — генерация набора слогов.
    ///     - Pattern — применение регулярных выражений.
    /// </summary>
    [DataField(required: true)]
    public SpeechMode SpeechMode;

    /// <summary>
    ///     Минимальное количество слогов, используемых при генерации слова
    ///     (актуально только для режима SpeechMode.Syllable).
    /// </summary>
    [DataField]
    public int MinSyllables = 0;

    /// <summary>
    ///     Максимальное количество слогов при генерации слова
    ///     (актуально для режима SpeechMode.Syllable).
    /// </summary>
    [DataField]
    public int MaxSyllables = 0;

    /// <summary>
    ///     Длина создаваемой строки при генерации через алфавит
    ///     (актуально для режима SpeechMode.Alphabet).
    /// </summary>
    [DataField]
    public int GenerateLength = 0;

    /// <summary>
    ///     Список шаблонов регулярных выражений для преобразования слов.
    ///     Каждый элемент Patterns[i] соответствует Replacements[i].
    ///     Используется в режиме SpeechMode.Pattern.
    /// </summary>
    [DataField]
    public List<string> Patterns = new();

    /// <summary>
    ///     Список строк-замен для шаблонов регулярных выражений.
    ///     Каждая замена применяется к соответствующему шаблону в Patterns.
    ///     Используется в режиме SpeechMode.Pattern.
    /// </summary>
    [DataField]
    public List<string> Replacements = new();

    /// <summary>
    ///     Список слогов, из которых конструируются слова.
    ///     Используется в режиме SpeechMode.Syllable.
    /// </summary>
    [DataField]
    public List<string> Syllables = new();

    /// <summary>
    ///     Список букв или символов, из которых создаются слова фиксированной длины.
    ///     Используется в режиме SpeechMode.Alphabet.
    /// </summary>
    [DataField]
    public List<string> Alphabet = new();

}
