// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Globalization;
using System.Linq;
using System.Text;
using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Storyteller;

public readonly record struct StoryActorRef(string Name, Sex Sex, string Role = "", string Kind = "");

public sealed class StoryLexicon
{
    public readonly List<(string Key, string Id)> Locations = new();
    public readonly List<(string Key, string Id)> Kinds = new();
    public readonly List<(string Key, string Id)> Roles = new();
    public readonly List<(string Key, string Id)> Items = new();

    internal readonly List<(string Stem, string Id)> EmoteStems = new();
    internal readonly List<(string[] Parts, string Id)> EmotePhrases = new();
    internal readonly Dictionary<string, string> LightWords = new();
    internal readonly List<(string Stem, string Id)> LightStems = new();
    internal readonly List<(string[] Parts, string Id)> LightPhrases = new();
    internal readonly HashSet<string> EmoteIds = new();

    internal readonly List<(string Stem, StoryVerb Verb)> VerbStems = new();
    internal readonly Dictionary<string, StoryVerb> VerbWords = new();
    internal readonly List<(string[] Parts, StoryVerb Verb)> VerbPhrases = new();
    internal readonly List<(string Word, StoryMove Move)> MoveWords = new();
    internal readonly List<(string[] Parts, StoryMove Move)> MovePhrases = new();
    internal readonly List<(string Word, StoryMove Move)> SideWords = new();
    internal readonly List<(string[] Parts, StoryMove Move)> SidePhrases = new();
    internal string[] PickUpStems = Array.Empty<string>();
    internal HashSet<string> Connectors = new();
    internal HashSet<string> Subordinators = new();
    internal HashSet<string> Stopwords = new();
    internal HashSet<string> FemaleWords = new();
    internal HashSet<string> MaleWords = new();
    internal HashSet<string> MaleNamesOnA = new();
    internal string[] FemaleNameEndings = Array.Empty<string>();
    internal HashSet<string> FemalePronouns = new();
    internal HashSet<string> MalePronouns = new();
    internal HashSet<string> OtherPronouns = new();
    internal HashSet<string> GroupWords = new();
    internal HashSet<string> SelfWords = new();
    internal HashSet<string> SelfObjectWords = new();
    internal HashSet<string> ReflexiveWords = new();
    internal HashSet<string> TargetPronouns = new();
    internal string[] FemaleVerbEndings = Array.Empty<string>();
    internal string[] FirstPersonEndings = Array.Empty<string>();
    internal string[] AdjectiveEndings = Array.Empty<string>();
    internal HashSet<string> LocationPrepositions = new();
    internal HashSet<string> DeathWords = new();
    internal HashSet<string> BackWords = new();
    internal HashSet<string> FrontWords = new();
    internal string[] CameraWords = Array.Empty<string>();
    internal string[] CloseUpStems = Array.Empty<string>();
    internal HashSet<string> CloseUpWords = new();
    internal string[] WideStems = Array.Empty<string>();
    internal HashSet<string> WideWords = new();
    internal string[] ShakeStems = Array.Empty<string>();
    internal List<(string From, string To)> DativeEndings = new();
    internal List<(string From, string To)> AccusativeEndings = new();
    internal string Narrator = "";

    public static StoryLexicon Build(IPrototypeManager prototypes)
    {
        var lexicon = new StoryLexicon();
        var settings = StoryStage.Settings(prototypes);
        lexicon.SetLanguage(prototypes.Index(settings.Language));

        foreach (var proto in prototypes.EnumeratePrototypes<StoryLocationPrototype>())
            lexicon.Add(lexicon.Locations, proto.Keywords, proto.ID);
        foreach (var proto in prototypes.EnumeratePrototypes<StoryKindPrototype>())
            lexicon.Add(lexicon.Kinds, proto.Keywords, proto.ID);
        foreach (var proto in prototypes.EnumeratePrototypes<StoryRolePrototype>())
            lexicon.Add(lexicon.Roles, proto.Keywords, proto.ID);
        foreach (var proto in prototypes.EnumeratePrototypes<StoryItemPrototype>())
            lexicon.Add(lexicon.Items, proto.Keywords, proto.ID);
        foreach (var proto in prototypes.EnumeratePrototypes<StoryEmotePrototype>())
            lexicon.AddEmote(proto.ID, proto.Stems, proto.Phrases);
        foreach (var proto in prototypes.EnumeratePrototypes<StoryLightPrototype>())
            lexicon.AddLight(proto.ID, proto.Words, proto.Stems, proto.Phrases);

        return lexicon;
    }

    public void Add(List<(string Key, string Id)> list, IEnumerable<string> keys, string id)
    {
        foreach (var key in keys)
        {
            var folded = StorytellerParser.Fold(key.Trim());
            if (folded.Length > 0)
                list.Add((folded, id));
        }

        list.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
    }

    public void AddEmote(string id, IEnumerable<string> stems, IEnumerable<string> phrases)
    {
        EmoteIds.Add(id);
        foreach (var stem in stems)
            EmoteStems.Add((StorytellerParser.Fold(stem), id));
        foreach (var phrase in phrases)
            EmotePhrases.Add((Parts(phrase), id));

        EmoteStems.Sort((a, b) => b.Stem.Length.CompareTo(a.Stem.Length));
    }

    public void AddLight(string id, IEnumerable<string> words, IEnumerable<string> stems, IEnumerable<string> phrases)
    {
        foreach (var word in words)
            LightWords[StorytellerParser.Fold(word)] = id;
        foreach (var stem in stems)
            LightStems.Add((StorytellerParser.Fold(stem), id));
        foreach (var phrase in phrases)
            LightPhrases.Add((Parts(phrase), id));
    }

    public void SetLanguage(StoryLanguagePrototype language)
    {
        Narrator = language.Narrator;
        VerbStems.Clear();
        foreach (var (verb, stems) in language.Stems)
        {
            foreach (var stem in stems)
                VerbStems.Add((StorytellerParser.Fold(stem), verb));
        }

        VerbStems.Sort((a, b) => b.Stem.Length.CompareTo(a.Stem.Length));

        VerbWords.Clear();
        foreach (var (verb, words) in language.Words)
        {
            foreach (var word in words)
                VerbWords[StorytellerParser.Fold(word)] = verb;
        }

        VerbPhrases.Clear();
        foreach (var (verb, phrases) in language.Phrases)
        {
            foreach (var phrase in phrases)
                VerbPhrases.Add((Parts(phrase), verb));
        }

        VerbPhrases.Sort((a, b) => b.Parts.Length.CompareTo(a.Parts.Length));
        Fill(MoveWords, MovePhrases, language.MoveWords, language.MovePhrases);
        Fill(SideWords, SidePhrases, language.SideWords, language.SidePhrases);

        PickUpStems = Folded(language.PickUpStems);
        Connectors = Set(language.Connectors);
        Subordinators = Set(language.Subordinators);
        Stopwords = Set(language.Stopwords);
        FemaleWords = Set(language.FemaleWords);
        MaleWords = Set(language.MaleWords);
        MaleNamesOnA = Set(language.MaleNamesOnA);
        FemaleNameEndings = Folded(language.FemaleNameEndings);
        FemalePronouns = Set(language.FemalePronouns);
        MalePronouns = Set(language.MalePronouns);
        OtherPronouns = Set(language.OtherPronouns);
        GroupWords = Set(language.GroupWords);
        SelfWords = Set(language.SelfWords);
        SelfObjectWords = Set(language.SelfObjectWords);
        ReflexiveWords = Set(language.ReflexiveWords);
        TargetPronouns = Set(language.TargetPronouns);
        FemaleVerbEndings = Folded(language.FemaleVerbEndings);
        FirstPersonEndings = Folded(language.FirstPersonEndings);
        AdjectiveEndings = Folded(language.AdjectiveEndings);
        LocationPrepositions = Set(language.LocationPrepositions);
        DeathWords = Set(language.DeathWords);
        BackWords = Set(language.BackWords);
        FrontWords = Set(language.FrontWords);
        CameraWords = Folded(language.CameraWords);
        CloseUpStems = Folded(language.CloseUpStems);
        CloseUpWords = Set(language.CloseUpWords);
        WideStems = Folded(language.WideStems);
        WideWords = Set(language.WideWords);
        ShakeStems = Folded(language.ShakeStems);
        DativeEndings = Endings(language.DativeEndings);
        AccusativeEndings = Endings(language.AccusativeEndings);
    }

    private static void Fill(
        List<(string, StoryMove)> words,
        List<(string[], StoryMove)> phrases,
        Dictionary<StoryMove, List<string>> sourceWords,
        Dictionary<StoryMove, List<string>> sourcePhrases)
    {
        words.Clear();
        phrases.Clear();
        foreach (var (move, list) in sourceWords)
        {
            foreach (var word in list)
                words.Add((StorytellerParser.Fold(word), move));
        }

        foreach (var (move, list) in sourcePhrases)
        {
            foreach (var phrase in list)
                phrases.Add((Parts(phrase), move));
        }
    }

    private static List<(string, string)> Endings(Dictionary<string, string> source)
    {
        return source
            .Select(p => (StorytellerParser.Fold(p.Key), StorytellerParser.Fold(p.Value)))
            .OrderByDescending(p => p.Item1.Length)
            .ToList();
    }

    private static string[] Parts(string phrase)
    {
        return StorytellerParser.Fold(phrase).Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string[] Folded(IEnumerable<string> words)
    {
        return words.Select(StorytellerParser.Fold).Where(w => w.Length > 0).ToArray();
    }

    private static HashSet<string> Set(IEnumerable<string> words)
    {
        return new HashSet<string>(words.Select(StorytellerParser.Fold).Where(w => w.Length > 0));
    }
}

public static class StorytellerParser
{
    public const int MaxLineLength = 200;
    public const int MaxNameLength = 24;
    public const int MaxSayLength = 120;
    public const int MaxTitleLength = 40;
    public const int MaxCues = 10;
    public const int MaxPages = 120;
    public const int MaxPageCues = 64;
    public const int MaxIdLength = 64;

    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    private sealed class Token
    {
        public string Text = "";
        public string Low = "";
        public bool Capital;
        public bool Quote;
        public bool SentenceStart;
    }

    private sealed class Who
    {
        public string Name = "";
        public bool Known;
        public bool Narrator;
        public string Kind = "";
        public string Role = "";
        public Sex? Sex;
        public StoryMove Side;
        public bool Group;
    }

    private sealed class State
    {
        public readonly List<StoryActorRef> Known = new();
        public StoryLexicon Lexicon = default!;
        public string Narrator = "";
        public string? LastSubject;
        public string? LastTarget;
        public string Speech = "";
        public bool SpeechUsed;
    }

    public static string Fold(string text)
    {
        return text.ToLower(Ru).Replace('ё', 'е');
    }

    public static bool SameName(string? left, string? right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            return false;

        return string.Equals(Fold(left), Fold(right), StringComparison.Ordinal);
    }

    public static bool CleanName(string? raw, out string name)
    {
        name = "";
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var trimmed = raw.Trim();
        if (trimmed.Length > MaxNameLength || HasControl(trimmed))
            return false;

        foreach (var c in trimmed)
        {
            if (c is '<' or '>' or '[' or ']' or '{' or '}' or '"' or '\\')
                return false;
        }

        name = trimmed;
        return true;
    }

    public static bool CleanText(string? raw, int max, out string text)
    {
        text = "";
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > max * 2)
            return false;

        var trimmed = raw.Trim();
        if (trimmed.Length > max)
            trimmed = trimmed[..max];

        if (HasControl(trimmed))
            return false;

        text = trimmed.Replace('[', '(').Replace(']', ')');
        return text.Length > 0;
    }

    public static bool HasControl(string text)
    {
        foreach (var c in text)
        {
            if (char.IsControl(c))
                return true;
        }

        return false;
    }

    public static bool StemMatch(string token, string key)
    {
        return token.Length >= key.Length && token.Length - key.Length <= 2 && token.StartsWith(key, StringComparison.Ordinal);
    }

    public static bool NameMatch(string token, string nameWord)
    {
        var word = Fold(nameWord);
        if (word.Length == 0)
            return false;

        if (token == word)
            return true;

        var stem = word;
        if (stem.Length > 3 && "аеиоуыэюяйь".Contains(stem[^1]))
            stem = stem[..^1];

        return stem.Length >= 3 && token.StartsWith(stem, StringComparison.Ordinal) && token.Length <= word.Length + 3;
    }

    public static Sex GuessSex(string name, StoryLexicon lexicon)
    {
        var first = Fold(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "");
        if (first.Length < 2 || lexicon.MaleNamesOnA.Contains(first))
            return Sex.Male;

        return lexicon.FemaleNameEndings.Any(e => first.EndsWith(e, StringComparison.Ordinal)) ? Sex.Female : Sex.Male;
    }

    public static string Nominative(string word, bool dative, StoryLexicon lexicon)
    {
        if (word.Length < 4)
            return word;

        var low = Fold(word);
        foreach (var (from, to) in dative ? lexicon.DativeEndings : lexicon.AccusativeEndings)
        {
            if (!low.EndsWith(from, StringComparison.Ordinal))
                continue;

            return word[..^from.Length] + to;
        }

        return word;
    }

    public static List<StoryCue> Parse(string line, string narrator, IReadOnlyList<StoryActorRef> actors, StoryLexicon lexicon)
    {
        var cues = new List<StoryCue>(MaxCues);
        if (string.IsNullOrWhiteSpace(line) || line.Length > MaxLineLength || HasControl(line))
            return cues;

        var state = new State { Lexicon = lexicon };
        state.Known.AddRange(actors);
        state.Narrator = CleanName(narrator, out var cleanNarrator) ? cleanNarrator : lexicon.Narrator;
        if (actors.Count > 0)
            state.LastSubject = actors[^1].Name;

        var text = ExtractSpeech(line.Trim(), out var speech);
        state.Speech = speech;

        foreach (var clause in Split(text, lexicon))
        {
            ParseClause(clause, state, cues);
            if (cues.Count >= MaxCues)
                break;
        }

        if (!state.SpeechUsed && state.Speech.Length > 0 && state.LastSubject != null)
            cues.Add(new StoryCue { Kind = StoryCueKind.Say, Actor = state.LastSubject, Text = state.Speech });

        if (cues.Count > MaxCues)
            cues.RemoveRange(MaxCues, cues.Count - MaxCues);

        return cues;
    }

    private static string ExtractSpeech(string line, out string speech)
    {
        speech = "";
        var pairs = new[] { ('«', '»'), ('"', '"'), ('“', '”'), ('„', '“') };
        foreach (var (open, close) in pairs)
        {
            var start = line.IndexOf(open);
            if (start < 0)
                continue;

            var end = line.IndexOf(close, start + 1);
            if (end <= start + 1)
                continue;

            speech = Clip(line[(start + 1)..end]);
            return line[..start] + " § " + line[(end + 1)..];
        }

        var colon = line.IndexOf(':');
        if (colon > 0 && colon < line.Length - 1)
        {
            speech = Clip(line[(colon + 1)..]);
            return line[..colon] + " §";
        }

        return line;
    }

    private static string Clip(string raw)
    {
        var text = raw.Trim().Trim('—', '-', ' ');
        if (text.Length > MaxSayLength)
            text = text[..MaxSayLength];

        return text.Replace('[', '(').Replace(']', ')');
    }

    private static List<List<Token>> Split(string text, StoryLexicon lexicon)
    {
        var sentences = new List<List<List<Token>>>();
        var sentence = new List<List<Token>>();
        var segment = new List<Token>();
        var word = new StringBuilder();
        var sentenceStart = true;

        void EndWord()
        {
            if (word.Length == 0)
                return;

            var raw = word.ToString();
            word.Clear();
            var low = Fold(raw);
            if (lexicon.Connectors.Contains(low))
            {
                EndSegment();
                return;
            }

            segment.Add(new Token
            {
                Text = raw,
                Low = low,
                Capital = char.IsUpper(raw[0]),
                Quote = raw == "§",
                SentenceStart = sentenceStart,
            });
            sentenceStart = false;
        }

        void EndSegment()
        {
            EndWord();
            if (segment.Count > 0)
                sentence.Add(segment);
            segment = new List<Token>();
        }

        void EndSentence()
        {
            EndSegment();
            if (sentence.Count > 0)
                sentences.Add(sentence);
            sentence = new List<List<Token>>();
            sentenceStart = true;
        }

        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c) || c is '§' or '\'')
            {
                word.Append(c);
                continue;
            }

            if (c is '.' or '!' or '?' or ';' or '…')
            {
                EndSentence();
                continue;
            }

            if (c is ',' or ':' or '—' or '–')
            {
                EndSegment();
                continue;
            }

            EndWord();
        }

        EndSentence();

        var clauses = new List<List<Token>>();
        foreach (var segments in sentences)
        {
            List<Token>? current = null;
            foreach (var next in segments)
            {
                if (current == null)
                {
                    current = new List<Token>(next);
                    continue;
                }

                var dependent = next.Count > 0 && lexicon.Subordinators.Contains(next[0].Low);
                if (!dependent && !HasSpeech(current, lexicon) && HasVerb(current, lexicon) && HasVerb(next, lexicon))
                {
                    clauses.Add(current);
                    current = new List<Token>(next);
                }
                else
                {
                    current.AddRange(next);
                }
            }

            if (current is { Count: > 0 })
                clauses.Add(current);
        }

        return clauses;
    }

    private static bool HasSpeech(List<Token> tokens, StoryLexicon lexicon)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            var verb = VerbAt(tokens, i, lexicon, out _, out _, out _);
            if (verb is StoryVerb.Say or StoryVerb.Scream)
                return !tokens.Any(t => t.Quote);
            if (verb != StoryVerb.None)
                return false;
        }

        return false;
    }

    private static bool HasVerb(List<Token> tokens, StoryLexicon lexicon)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            if (VerbAt(tokens, i, lexicon, out _, out _, out _) != StoryVerb.None)
                return true;
        }

        return false;
    }

    private static bool PhraseAt(List<Token> tokens, int index, string[] parts)
    {
        if (parts.Length == 0 || index + parts.Length > tokens.Count)
            return false;

        for (var k = 0; k < parts.Length; k++)
        {
            var token = tokens[index + k];
            if (token.Quote)
                return false;

            var part = parts[k];
            if (part.Length < 3 ? token.Low != part : !token.Low.StartsWith(part, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static StoryVerb VerbAt(List<Token> tokens, int index, StoryLexicon lexicon, out string emote, out bool female, out int length)
    {
        emote = "";
        female = false;
        length = 1;
        var low = tokens[index].Low;
        if (low.Length < 2 || tokens[index].Quote)
            return StoryVerb.None;

        female = lexicon.FemaleVerbEndings.Any(e => low.EndsWith(e, StringComparison.Ordinal));

        foreach (var (parts, id) in lexicon.EmotePhrases)
        {
            if (!PhraseAt(tokens, index, parts))
                continue;

            emote = id;
            length = parts.Length;
            return StoryVerb.Emote;
        }

        foreach (var (parts, verb) in lexicon.VerbPhrases)
        {
            if (!PhraseAt(tokens, index, parts))
                continue;

            length = parts.Length;
            return verb;
        }

        if (lexicon.VerbWords.TryGetValue(low, out var exact))
            return exact;

        foreach (var (stem, id) in lexicon.EmoteStems)
        {
            if (!low.StartsWith(stem, StringComparison.Ordinal))
                continue;

            emote = id;
            return StoryVerb.Emote;
        }

        foreach (var (stem, verb) in lexicon.VerbStems)
        {
            if (!low.StartsWith(stem, StringComparison.Ordinal))
                continue;

            var next = index + 1 < tokens.Count ? tokens[index + 1].Low : "";
            if (verb == StoryVerb.Stand && next.Length > 0 && !lexicon.LocationPrepositions.Contains(next) &&
                lexicon.PickUpStems.Any(p => low.StartsWith(p, StringComparison.Ordinal)))
            {
                return StoryVerb.Take;
            }

            return verb;
        }

        return StoryVerb.None;
    }

    private static bool FirstPerson(string low, StoryLexicon lexicon)
    {
        return lexicon.FirstPersonEndings.Any(e => low.EndsWith(e, StringComparison.Ordinal));
    }

    private static void ParseClause(List<Token> clause, State state, List<StoryCue> cues, Who? forced = null)
    {
        var lexicon = state.Lexicon;
        var verbAt = -1;
        var verb = StoryVerb.None;
        var emote = "";
        var female = false;
        var verbLength = 1;
        for (var i = 0; i < clause.Count; i++)
        {
            verb = VerbAt(clause, i, lexicon, out emote, out female, out verbLength);
            if (verb == StoryVerb.None)
                continue;

            verbAt = i;
            break;
        }

        if (verb == StoryVerb.Fall && clause.Any(t => lexicon.DeathWords.Contains(t.Low)))
            verb = StoryVerb.Die;

        var used = new HashSet<int>();
        for (var i = 0; verbAt >= 0 && i < verbLength; i++)
            used.Add(verbAt + i);

        var location = FindLocation(clause, lexicon, used);
        var light = FindLight(clause, lexicon, used);
        var item = FindKey(clause, lexicon.Items, used);
        var side = FindDirection(clause, lexicon.SideWords, lexicon.SidePhrases, used);
        var move = FindDirection(clause, lexicon.MoveWords, lexicon.MovePhrases, used);
        var hasQuote = clause.Any(t => t.Quote);

        if (forced == null)
        {
            foreach (var id in light)
                cues.Add(new StoryCue { Kind = StoryCueKind.Light, Id = id });
        }

        var existing = verb != StoryVerb.Enter;
        var before = verbAt >= 0 ? verbAt : clause.Count;
        var subject = forced ?? ResolveWho(clause, 0, before, state, used, false, existing);

        if (subject == null && verbAt >= 0 && verbAt + verbLength < clause.Count &&
            verb is StoryVerb.Enter or StoryVerb.Leave or StoryVerb.Say or StoryVerb.Scream or StoryVerb.Fall or StoryVerb.Die or
                StoryVerb.Emote or StoryVerb.Dance or StoryVerb.Sit or StoryVerb.Lie or StoryVerb.Stand or StoryVerb.Kneel)
        {
            subject = ResolveWho(clause, verbAt + verbLength, clause.Count, state, used, true, existing);
            if (subject is { Known: false, Name.Length: 0, Kind.Length: 0, Role.Length: 0 })
                subject = null;
        }

        if (subject is { Group: true })
        {
            if (verb is StoryVerb.None or StoryVerb.Be or StoryVerb.Enter)
                return;

            var group = state.Known.Select(a => a.Name).Distinct().ToList();
            foreach (var name in group)
            {
                ParseClause(clause, state, cues, new Who { Name = name, Known = true });
                if (cues.Count >= MaxCues)
                    break;
            }

            return;
        }

        if (verb == StoryVerb.Be)
        {
            ParseBe(clause, verbAt, state, subject, used, cues);
            return;
        }

        if (subject == null && verbAt >= 0 && FirstPerson(clause[verbAt].Low, lexicon))
            subject = new Who { Name = state.Narrator, Narrator = true, Known = IsKnown(state, state.Narrator) };

        if (subject == null && (verb != StoryVerb.None || hasQuote) && state.LastSubject != null)
            subject = new Who { Name = state.LastSubject, Known = IsKnown(state, state.LastSubject) };

        if (subject is { Known: false, Sex: null } && female && verbAt >= 0)
            subject.Sex = Sex.Female;

        if (subject != null)
            subject.Side = side;

        Who? target = null;
        if (verbAt >= 0 && verb is not (StoryVerb.Enter or StoryVerb.Leave or StoryVerb.Say or StoryVerb.Scream or
                StoryVerb.Emote or StoryVerb.Dance or StoryVerb.Take))
        {
            target = ResolveTarget(clause, verbAt + verbLength, state, used, subject,
                verb is StoryVerb.Give or StoryVerb.Walk or StoryVerb.Run);
        }

        var camera = FindCamera(clause, lexicon, out var closeUp, out var shake);

        if (verb == StoryVerb.None)
        {
            if (location != null)
                cues.Add(new StoryCue { Kind = StoryCueKind.Location, Id = location });

            if (hasQuote && subject != null && !state.SpeechUsed && state.Speech.Length > 0)
            {
                EnsureOnStage(subject, state, cues);
                cues.Add(new StoryCue { Kind = StoryCueKind.Say, Actor = subject.Name, Text = state.Speech });
                state.SpeechUsed = true;
                state.LastSubject = subject.Name;
            }
            else if (subject is { Known: false, Name.Length: > 0 } && (subject.Kind.Length > 0 || subject.Role.Length > 0))
            {
                EnsureOnStage(subject, state, cues);
            }

            AddCamera(cues, camera, closeUp, shake, subject?.Name ?? state.LastSubject);
            return;
        }

        if (subject == null)
        {
            if (location != null)
                cues.Add(new StoryCue { Kind = StoryCueKind.Location, Id = location });
            AddCamera(cues, camera, closeUp, shake, null);
            return;
        }

        switch (verb)
        {
            case StoryVerb.Enter:
                if (location != null)
                {
                    if (subject.Known)
                    {
                        cues.Add(new StoryCue { Kind = StoryCueKind.Location, Id = location, Actor = subject.Name });
                    }
                    else
                    {
                        cues.Add(new StoryCue { Kind = StoryCueKind.Location, Id = location });
                        EnsureOnStage(subject, state, cues);
                    }
                }
                else if (!subject.Known)
                {
                    EnsureOnStage(subject, state, cues);
                }

                if (move != StoryMove.None)
                    cues.Add(new StoryCue { Kind = StoryCueKind.Move, Actor = subject.Name, Move = move });
                break;
            case StoryVerb.Leave:
                if (subject.Known)
                    cues.Add(new StoryCue { Kind = StoryCueKind.Leave, Actor = subject.Name });
                break;
            case StoryVerb.Walk:
            case StoryVerb.Run:
                if (location != null && target == null)
                {
                    if (!subject.Known)
                    {
                        cues.Add(new StoryCue { Kind = StoryCueKind.Location, Id = location });
                        EnsureOnStage(subject, state, cues);
                    }
                    else
                    {
                        cues.Add(new StoryCue { Kind = StoryCueKind.Location, Id = location, Actor = subject.Name });
                    }

                    break;
                }

                EnsureOnStage(subject, state, cues);
                EnsureTarget(target, state, cues);
                cues.Add(new StoryCue
                {
                    Kind = StoryCueKind.Move,
                    Actor = subject.Name,
                    Target = target?.Name ?? "",
                    Move = target != null ? StoryMove.Target : move,
                    Run = verb == StoryVerb.Run,
                });
                break;
            case StoryVerb.Fall:
            case StoryVerb.Die:
            case StoryVerb.Kneel:
            case StoryVerb.Sit:
            case StoryVerb.Lie:
            case StoryVerb.Stand:
                EnsureOnStage(subject, state, cues);
                cues.Add(new StoryCue
                {
                    Kind = StoryCueKind.Pose,
                    Actor = subject.Name,
                    Pose = verb switch
                    {
                        StoryVerb.Fall => StoryPose.Fallen,
                        StoryVerb.Die => StoryPose.Dead,
                        StoryVerb.Kneel => StoryPose.Kneel,
                        StoryVerb.Sit => StoryPose.Sit,
                        StoryVerb.Lie => StoryPose.Lie,
                        _ => StoryPose.Stand,
                    },
                });
                break;
            case StoryVerb.Point:
            case StoryVerb.Hit:
            case StoryVerb.Shoot:
            case StoryVerb.Hug:
            case StoryVerb.Kiss:
            case StoryVerb.Give:
                EnsureOnStage(subject, state, cues);
                EnsureTarget(target, state, cues);
                if (verb is StoryVerb.Hug or StoryVerb.Kiss or StoryVerb.Hit && target == null)
                    break;

                cues.Add(new StoryCue
                {
                    Kind = verb switch
                    {
                        StoryVerb.Point => StoryCueKind.Point,
                        StoryVerb.Hit => StoryCueKind.Hit,
                        StoryVerb.Shoot => StoryCueKind.Shoot,
                        StoryVerb.Hug => StoryCueKind.Hug,
                        StoryVerb.Kiss => StoryCueKind.Kiss,
                        _ => StoryCueKind.Give,
                    },
                    Actor = subject.Name,
                    Target = target?.Name ?? "",
                    Id = item ?? "",
                });
                break;
            case StoryVerb.Take:
                EnsureOnStage(subject, state, cues);
                if (item != null)
                    cues.Add(new StoryCue { Kind = StoryCueKind.Hold, Actor = subject.Name, Id = item });
                break;
            case StoryVerb.Say:
            case StoryVerb.Scream:
            {
                EnsureOnStage(subject, state, cues);
                var addressee = Addressee(clause, verbAt + verbLength, state, used, subject.Name);
                if (addressee != null)
                {
                    cues.Add(new StoryCue { Kind = StoryCueKind.Face, Actor = subject.Name, Target = addressee, Move = StoryMove.Target });
                    state.LastTarget = addressee;
                }

                var said = state.Speech;
                if (said.Length == 0 || state.SpeechUsed)
                {
                    said = Remainder(clause, verbAt + verbLength, used);
                    if (addressee != null && said.Length > 0)
                        said = Clip($"{addressee}, {said}");
                }

                if (said.Length > 0)
                {
                    cues.Add(new StoryCue
                    {
                        Kind = StoryCueKind.Say,
                        Actor = subject.Name,
                        Text = verb == StoryVerb.Scream ? said.ToUpper(Ru) : said,
                    });
                    state.SpeechUsed |= state.Speech.Length > 0;
                }
                else if (verb == StoryVerb.Scream && lexicon.EmoteIds.Contains("scream"))
                {
                    cues.Add(new StoryCue { Kind = StoryCueKind.Emote, Actor = subject.Name, Id = "scream" });
                }

                break;
            }
            case StoryVerb.Emote:
                EnsureOnStage(subject, state, cues);
                cues.Add(new StoryCue { Kind = StoryCueKind.Emote, Actor = subject.Name, Id = emote });
                break;
            case StoryVerb.Dance:
                EnsureOnStage(subject, state, cues);
                cues.Add(new StoryCue { Kind = StoryCueKind.Dance, Actor = subject.Name });
                break;
            case StoryVerb.Turn:
            {
                EnsureOnStage(subject, state, cues);
                target ??= ResolveTarget(clause, verbAt + verbLength, state, used, subject, true);
                var facing = move;
                if (clause.Any(t => lexicon.BackWords.Contains(t.Low)))
                    facing = StoryMove.Up;
                else if (clause.Any(t => lexicon.FrontWords.Contains(t.Low)) && target == null)
                    facing = StoryMove.Down;

                cues.Add(new StoryCue
                {
                    Kind = StoryCueKind.Face,
                    Actor = subject.Name,
                    Target = target?.Name ?? "",
                    Move = target != null ? StoryMove.Target : facing,
                });
                break;
            }
        }

        if (hasQuote && !state.SpeechUsed && state.Speech.Length > 0)
        {
            cues.Add(new StoryCue { Kind = StoryCueKind.Say, Actor = subject.Name, Text = state.Speech });
            state.SpeechUsed = true;
        }

        AddCamera(cues, camera, closeUp, shake, target?.Name ?? subject.Name);
        state.LastSubject = subject.Name;
        if (target != null)
            state.LastTarget = target.Name;
    }

    private static void ParseBe(List<Token> clause, int verbAt, State state, Who? subject, HashSet<int> used, List<StoryCue> cues)
    {
        subject ??= state.LastSubject != null ? new Who { Name = state.LastSubject, Known = IsKnown(state, state.LastSubject) } : null;
        if (subject == null)
            return;

        var look = Describe(clause, verbAt + 1, state.Lexicon, used);
        if (look.Kind.Length == 0 && look.Role.Length == 0 && look.Sex == null)
            return;

        if (!subject.Known)
        {
            subject.Kind = look.Kind.Length > 0 ? look.Kind : subject.Kind;
            subject.Role = look.Role.Length > 0 ? look.Role : subject.Role;
            subject.Sex = look.Sex ?? subject.Sex;
            EnsureOnStage(subject, state, cues);
        }
        else
        {
            cues.Add(new StoryCue
            {
                Kind = StoryCueKind.Look,
                Actor = subject.Name,
                Id = look.Kind,
                Role = look.Role,
                Sex = look.Sex ?? Sex.Male,
                HasSex = look.Sex != null,
            });
            Remember(state, subject.Name, look);
        }

        state.LastSubject = subject.Name;
    }

    private static void AddCamera(List<StoryCue> cues, bool camera, bool closeUp, bool shake, string? focus)
    {
        if (camera)
            cues.Add(new StoryCue { Kind = StoryCueKind.Camera, Run = closeUp, Actor = closeUp ? focus ?? "" : "" });

        if (shake)
            cues.Add(new StoryCue { Kind = StoryCueKind.Shake });
    }

    private static void EnsureOnStage(Who who, State state, List<StoryCue> cues)
    {
        if (who.Known)
            return;

        var name = who.Name;
        var sex = who.Sex ?? (name.Length > 0 && !who.Narrator ? GuessSex(name, state.Lexicon) : Sex.Male);
        cues.Add(new StoryCue
        {
            Kind = StoryCueKind.Enter,
            Actor = name,
            Id = who.Kind,
            Role = who.Role,
            Sex = sex,
            HasSex = who.Sex != null || name.Length > 0,
            Move = who.Side,
            Text = who.Narrator ? "self" : "",
        });

        who.Known = true;
        if (name.Length > 0)
            state.Known.Add(new StoryActorRef(name, sex, who.Role, who.Kind));
    }

    private static void EnsureTarget(Who? target, State state, List<StoryCue> cues)
    {
        if (target == null || target.Known || target.Name.Length == 0)
            return;

        EnsureOnStage(target, state, cues);
    }

    private static void Remember(State state, string name, Who look)
    {
        for (var i = 0; i < state.Known.Count; i++)
        {
            var actor = state.Known[i];
            if (!SameName(actor.Name, name))
                continue;

            state.Known[i] = actor with
            {
                Role = look.Role.Length > 0 ? look.Role : actor.Role,
                Kind = look.Kind.Length > 0 ? look.Kind : actor.Kind,
                Sex = look.Sex ?? actor.Sex,
            };
        }
    }

    private static bool IsKnown(State state, string name)
    {
        return state.Known.Any(actor => SameName(actor.Name, name));
    }

    private static string? KnownByToken(State state, string low, string? exclude)
    {
        string? best = null;
        foreach (var actor in state.Known)
        {
            if (exclude != null && SameName(actor.Name, exclude))
                continue;

            foreach (var part in actor.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!NameMatch(low, part))
                    continue;

                best = actor.Name;
                break;
            }
        }

        return best;
    }

    private static Who? ResolveWho(List<Token> clause, int from, int to, State state, HashSet<int> used, bool after, bool existing)
    {
        var lexicon = state.Lexicon;
        var who = new Who();
        var names = new List<string>();
        var found = false;

        for (var i = from; i < to; i++)
        {
            var token = clause[i];
            if (used.Contains(i) || token.Quote)
                continue;

            var low = token.Low;
            if (lexicon.SelfWords.Contains(low))
            {
                used.Add(i);
                return new Who { Name = state.Narrator, Narrator = true, Known = IsKnown(state, state.Narrator) };
            }

            if (lexicon.GroupWords.Contains(low) && state.Known.Count > 0)
            {
                used.Add(i);
                return new Who { Group = true, Known = true };
            }

            if (!after && (lexicon.FemalePronouns.Contains(low) || lexicon.MalePronouns.Contains(low) || lexicon.OtherPronouns.Contains(low)))
            {
                used.Add(i);
                Sex? wanted = lexicon.FemalePronouns.Contains(low) ? Sex.Female : lexicon.MalePronouns.Contains(low) ? Sex.Male : null;
                var pick = state.Known.LastOrDefault(a => wanted == null || a.Sex == wanted).Name ?? state.LastSubject;
                if (pick != null)
                    return new Who { Name = pick, Known = true };
                continue;
            }

            if (names.Count == 0 && token.Capital && KnownByToken(state, low, null) is { } known)
            {
                used.Add(i);
                if (after && i + 1 < to && clause[i + 1].Capital && KnownByToken(state, clause[i + 1].Low, null) == known)
                    used.Add(i + 1);
                return new Who { Name = known, Known = true };
            }

            if (Match(low, lexicon.Kinds) is { } kind)
            {
                who.Kind = kind;
                used.Add(i);
                found = true;
                continue;
            }

            if (Match(low, lexicon.Roles) is { } role)
            {
                who.Role = role;
                used.Add(i);
                found = true;
                continue;
            }

            if (lexicon.FemaleWords.Contains(low))
            {
                who.Sex = Sex.Female;
                used.Add(i);
                found = true;
                continue;
            }

            if (lexicon.MaleWords.Contains(low))
            {
                who.Sex = Sex.Male;
                used.Add(i);
                found = true;
                continue;
            }

            if (!token.Capital || lexicon.Stopwords.Contains(low) || Match(low, lexicon.Locations) != null || Match(low, lexicon.Items) != null)
            {
                if (after && names.Count > 0)
                    break;
                continue;
            }

            if (token.SentenceStart && IsAdjective(low, lexicon))
                continue;

            names.Add(token.Text);
            used.Add(i);
            if (names.Count >= 3)
                break;
        }

        if (names.Count > 0)
        {
            var joined = string.Join(' ', names);
            if (!CleanName(joined, out var name))
                name = joined.Length > MaxNameLength ? joined[..MaxNameLength].Trim() : joined;

            foreach (var actor in state.Known)
            {
                if (SameName(actor.Name, name))
                    return new Who { Name = actor.Name, Known = true };
            }

            who.Name = name;
            return who;
        }

        if (!found)
            return null;

        if (existing)
        {
            for (var i = state.Known.Count - 1; i >= 0; i--)
            {
                var actor = state.Known[i];
                if (who.Role.Length > 0 ? actor.Role == who.Role : who.Kind.Length > 0 && actor.Kind == who.Kind)
                    return new Who { Name = actor.Name, Known = true };
            }
        }

        return who;
    }

    private static Who? ResolveTarget(List<Token> clause, int from, State state, HashSet<int> used, Who? subject, bool dative)
    {
        var lexicon = state.Lexicon;
        for (var i = from; i < clause.Count; i++)
        {
            if (used.Contains(i) || clause[i].Quote)
                continue;

            var low = clause[i].Low;
            if (lexicon.TargetPronouns.Contains(low))
            {
                used.Add(i);
                var pick = state.LastTarget != null && !SameName(state.LastTarget, subject?.Name)
                    ? state.LastTarget
                    : state.Known.LastOrDefault(a => !SameName(a.Name, subject?.Name)).Name;
                if (pick != null)
                    return new Who { Name = pick, Known = true };
                continue;
            }

            if (KnownByToken(state, low, subject?.Name) is { } known)
            {
                used.Add(i);
                if (i + 1 < clause.Count && KnownByToken(state, clause[i + 1].Low, subject?.Name) == known)
                    used.Add(i + 1);
                return new Who { Name = known, Known = true };
            }

            if (lexicon.ReflexiveWords.Contains(low))
            {
                used.Add(i);
                continue;
            }

            if (lexicon.SelfObjectWords.Contains(low))
            {
                used.Add(i);
                return new Who { Name = state.Narrator, Narrator = true, Known = IsKnown(state, state.Narrator) };
            }
        }

        for (var i = from; i < clause.Count; i++)
        {
            var token = clause[i];
            if (used.Contains(i) || token.Quote || !token.Capital || lexicon.Stopwords.Contains(token.Low))
                continue;

            if (token.Text.Length < 3 || !token.Text.Any(char.IsLower))
                continue;

            if (Match(token.Low, lexicon.Locations) != null || Match(token.Low, lexicon.Items) != null ||
                Match(token.Low, lexicon.Kinds) != null || Match(token.Low, lexicon.Roles) != null)
            {
                continue;
            }

            used.Add(i);
            var words = new List<string> { Nominative(token.Text, dative, lexicon) };
            if (i + 1 < clause.Count && clause[i + 1].Capital && !used.Contains(i + 1) && !lexicon.Stopwords.Contains(clause[i + 1].Low))
            {
                used.Add(i + 1);
                words.Add(Nominative(clause[i + 1].Text, dative, lexicon));
            }

            var joined = string.Join(' ', words);
            if (!CleanName(joined, out var name))
                continue;

            return new Who { Name = name, Sex = GuessSex(name, lexicon) };
        }

        return null;
    }

    private static Who Describe(List<Token> clause, int from, StoryLexicon lexicon, HashSet<int> used)
    {
        var who = new Who();
        for (var i = from; i < clause.Count; i++)
        {
            if (used.Contains(i) || clause[i].Quote)
                continue;

            var low = clause[i].Low;
            if (who.Kind.Length == 0 && Match(low, lexicon.Kinds) is { } kind)
                who.Kind = kind;
            else if (who.Role.Length == 0 && Match(low, lexicon.Roles) is { } role)
                who.Role = role;
            else if (lexicon.FemaleWords.Contains(low))
                who.Sex = Sex.Female;
            else if (lexicon.MaleWords.Contains(low))
                who.Sex = Sex.Male;
            else
                continue;

            used.Add(i);
        }

        return who;
    }

    private static string? Addressee(List<Token> clause, int from, State state, HashSet<int> used, string speaker)
    {
        for (var i = from; i < clause.Count; i++)
        {
            if (used.Contains(i) || clause[i].Quote)
                continue;

            if (state.Lexicon.Subordinators.Contains(clause[i].Low))
                return null;

            if (KnownByToken(state, clause[i].Low, speaker) is not { } known)
                continue;

            used.Add(i);
            if (i + 1 < clause.Count && KnownByToken(state, clause[i + 1].Low, speaker) == known)
                used.Add(i + 1);
            return known;
        }

        return null;
    }

    private static List<string> FindLight(List<Token> clause, StoryLexicon lexicon, HashSet<int> used)
    {
        var found = new List<string>();
        for (var i = 0; i < clause.Count; i++)
        {
            if (used.Contains(i) || clause[i].Quote)
                continue;

            string? light = null;
            var length = 1;
            foreach (var (parts, id) in lexicon.LightPhrases)
            {
                if (!PhraseAt(clause, i, parts))
                    continue;

                light = id;
                length = parts.Length;
                break;
            }

            var low = clause[i].Low;
            if (light == null && lexicon.LightWords.TryGetValue(low, out var word))
                light = word;

            if (light == null)
            {
                foreach (var (stem, id) in lexicon.LightStems)
                {
                    if (!low.StartsWith(stem, StringComparison.Ordinal))
                        continue;

                    light = id;
                    break;
                }
            }

            if (light == null || found.Contains(light))
                continue;

            for (var k = 0; k < length; k++)
                used.Add(i + k);
            found.Add(light);
        }

        return found;
    }

    private static string? Match(string low, List<(string Key, string Id)> list)
    {
        foreach (var (key, id) in list)
        {
            if (key.Length <= 3 ? low == key || StemMatch(low, key) && low.Length - key.Length <= 1 : StemMatch(low, key))
                return id;
        }

        return null;
    }

    private static bool IsAdjective(string low, StoryLexicon lexicon)
    {
        return low.Length > 3 && lexicon.AdjectiveEndings.Any(e => low.EndsWith(e, StringComparison.Ordinal));
    }

    private static string? FindKey(List<Token> clause, List<(string Key, string Id)> list, HashSet<int> used)
    {
        for (var i = 0; i < clause.Count; i++)
        {
            if (used.Contains(i) || clause[i].Quote)
                continue;

            if (Match(clause[i].Low, list) is not { } id)
                continue;

            used.Add(i);
            return id;
        }

        return null;
    }

    private static string? FindLocation(List<Token> clause, StoryLexicon lexicon, HashSet<int> used)
    {
        for (var i = 0; i < clause.Count; i++)
        {
            if (used.Contains(i) || clause[i].Quote)
                continue;

            var id = Match(clause[i].Low, lexicon.Locations);
            if (id == null)
                continue;

            var prev = i > 0 ? clause[i - 1].Low : "";
            if (clause[i].Low.Length <= 4 && !lexicon.LocationPrepositions.Contains(prev) && clause.Count > 1)
                continue;

            used.Add(i);
            return id;
        }

        return null;
    }

    private static StoryMove FindDirection(
        List<Token> clause,
        List<(string Word, StoryMove Move)> words,
        List<(string[] Parts, StoryMove Move)> phrases,
        HashSet<int> used)
    {
        for (var i = 0; i < clause.Count; i++)
        {
            if (used.Contains(i) || clause[i].Quote)
                continue;

            foreach (var (parts, move) in phrases)
            {
                if (!PhraseAt(clause, i, parts) || Enumerable.Range(i, parts.Length).Any(used.Contains))
                    continue;

                for (var k = 0; k < parts.Length; k++)
                    used.Add(i + k);
                return move;
            }

            foreach (var (word, move) in words)
            {
                if (clause[i].Low != word)
                    continue;

                used.Add(i);
                return move;
            }
        }

        return StoryMove.None;
    }

    private static bool FindCamera(List<Token> clause, StoryLexicon lexicon, out bool closeUp, out bool shake)
    {
        closeUp = false;
        shake = false;
        var camera = false;
        var named = clause.Any(t => lexicon.CameraWords.Any(w => t.Low.StartsWith(w, StringComparison.Ordinal)));
        foreach (var token in clause)
        {
            var low = token.Low;
            if (lexicon.ShakeStems.Any(s => low.StartsWith(s, StringComparison.Ordinal)))
                shake = true;

            if (lexicon.CloseUpStems.Any(s => low.StartsWith(s, StringComparison.Ordinal)) || named && lexicon.CloseUpWords.Contains(low))
            {
                closeUp = true;
                camera = true;
            }

            if (lexicon.WideStems.Any(s => low.StartsWith(s, StringComparison.Ordinal)) || named && lexicon.WideWords.Contains(low))
            {
                closeUp = false;
                camera = true;
            }
        }

        return camera;
    }

    private static string Remainder(List<Token> clause, int from, HashSet<int> used)
    {
        var words = new List<string>();
        for (var i = from; i < clause.Count; i++)
        {
            if (used.Contains(i) || clause[i].Quote)
                continue;

            words.Add(clause[i].Text);
        }

        return Clip(string.Join(' ', words));
    }
}
