// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Languages.Prototypes;
using Content.Shared.DeadSpace.Languages.Components;
using Content.Shared.DeadSpace.Administration;
using Content.Server.DeadSpace.Prison.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Content.Shared.DeadSpace.Languages;
using Robust.Server.Player;
using Content.Shared.Chat;
using System.Linq;
using Content.Shared.Polymorph;
using Robust.Shared.GameStates;
using System.Text;
using System.Text.RegularExpressions;

namespace Content.Server.DeadSpace.Languages;

public sealed class LanguageSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    public static readonly ProtoId<LanguagePrototype> DefaultLanguageId = "GeneralLanguage";
    public static readonly ProtoId<LanguagePrototype> PrisonLanguageId = "PrisonLanguage";
    private readonly Dictionary<ProtoId<LanguagePrototype>, List<Regex>> _regexCache = new();
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LanguageComponent, ComponentGetState>(OnGetState);

        SubscribeLocalEvent<LanguageComponent, PolymorphedEvent>(OnPolymorphed);

        SubscribeAllEvent<SelectLanguageEvent>(OnSelectLanguage);
    }

    private void OnSelectLanguage(SelectLanguageEvent msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;

        if (!player.HasValue)
            return;

        if (HasComp<PrisonBoundComponent>(player.Value) && msg.PrototypeId != PrisonLanguageId)
            return;

        if (!TryComp<LanguageComponent>(player, out var language) ||
            !language.KnownLanguages.Contains(msg.PrototypeId) ||
            language.CantSpeakLanguages.Contains(msg.PrototypeId) ||
            !_prototypeManager.HasIndex(msg.PrototypeId))
        {
            return;
        }

        language.SelectedLanguage = msg.PrototypeId;
        Dirty(player.Value, language);
    }

    private void OnGetState(EntityUid uid, LanguageComponent component, ref ComponentGetState args)
    {
        args.State = new LanguageComponentState(component.KnownLanguages, component.CantSpeakLanguages);
    }

    private void OnPolymorphed(EntityUid uid, LanguageComponent component, PolymorphedEvent args)
    {
        var lang = EnsureComp<LanguageComponent>(args.NewEntity);
        lang.CopyFrom(component);
    }

    public static int GetDeterministicHashCode(string str) { unchecked { int hash1 = (5381 << 16) + 5381; int hash2 = hash1; for (int i = 0; i < str.Length; i += 2) { hash1 = ((hash1 << 5) + hash1) ^ str[i]; if (i + 1 < str.Length) hash2 = ((hash2 << 5) + hash2) ^ str[i + 1]; } return hash1 + (hash2 * 1566083941); } }

    public string TransformWord(string word, ProtoId<LanguagePrototype>? languageId)
    {
        if (!_prototypeManager.TryIndex(languageId, out var proto))
            return word;

        var hash = GetDeterministicHashCode(word + proto.ID);

        switch (proto.SpeechMode)
        {
            case SpeechMode.Lexicon:
                return TransformLexicon(hash, proto);

            case SpeechMode.Alphabet:
                return TransformAlphabet(hash, proto);

            case SpeechMode.Syllable:
                return TransformSyllableText(word, proto);

            case SpeechMode.Pattern:
                return TransformPattern(word, proto);

            default:
                return word;
        }
    }

    private string TransformLexicon(int hash, LanguagePrototype proto)
    {
        var list = proto.Lexicon;
        return list[Math.Abs(hash) % list.Count];
    }

    private string TransformAlphabet(int hash, LanguagePrototype proto)
    {
        var alphabet = proto.Alphabet;
        int len = proto.GenerateLength;

        var sb = new StringBuilder();

        int localHash = hash;
        for (int i = 0; i < len; i++)
        {
            int index = Math.Abs(localHash) % alphabet.Count;
            sb.Append(alphabet[index]);

            localHash = GetDeterministicHashCode(localHash.ToString());
        }

        return sb.ToString();
    }

    private string TransformSyllableText(string text, LanguagePrototype proto)
    {
        if (string.IsNullOrWhiteSpace(text) || proto.Syllables.Count == 0)
            return text;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();

        foreach (var word in words)
        {
            var hash = GetDeterministicHashCode(word + proto.ID);

            int count = proto.MinSyllables +
                (Math.Abs(hash) % (proto.MaxSyllables - proto.MinSyllables + 1));

            var localHash = hash;
            for (int i = 0; i < count; i++)
            {
                sb.Append(proto.Syllables[Math.Abs(localHash) % proto.Syllables.Count]);
                localHash = GetDeterministicHashCode(localHash.ToString());
            }

            sb.Append(' ');
        }

        return sb.ToString().Trim();
    }

    private string TransformPattern(string word, LanguagePrototype proto)
    {
        if (!_regexCache.TryGetValue(proto.ID, out var regexList))
        {
            regexList = new List<Regex>(proto.Patterns.Count);

            for (int i = 0; i < proto.Patterns.Count; i++)
            {
                var r = new Regex(
                    proto.Patterns[i],
                    RegexOptions.IgnoreCase | RegexOptions.Compiled
                );
                regexList.Add(r);
            }

            _regexCache[proto.ID] = regexList;
        }

        string result = word;
        for (int i = 0; i < regexList.Count; i++)
            result = regexList[i].Replace(result, proto.Replacements[i]);

        return result;
    }

    public string GetLangName(ProtoId<LanguagePrototype>? languageId)
    {
        var name = "Неизвестно";

        if (String.IsNullOrEmpty(languageId))
            return name;

        if (_prototypeManager.TryIndex(languageId, out var languageProto))
            name = languageProto.Name;

        return name;
    }

    public string GetLangName(EntityUid uid, LanguageComponent? component = null)
    {
        var name = "Неизвестно";

        if (!Resolve(uid, ref component, false))
            return name;

        if (String.IsNullOrEmpty(component.SelectedLanguage))
            return name;

        if (_prototypeManager.TryIndex<LanguagePrototype>(component.SelectedLanguage, out var languageProto))
            name = languageProto.Name;

        return name;
    }

    public HashSet<ProtoId<LanguagePrototype>>? GetKnownLanguages(EntityUid entity)
    {
        if (!TryComp<LanguageComponent>(entity, out var component))
            return null;

        return component.KnownLanguages;
    }

    public bool KnowsLanguage(EntityUid receiver, ProtoId<LanguagePrototype> senderLanguageId)
    {
        if (HasComp<AdminGhostVisibilityComponent>(receiver))
            return true;

        var languages = GetKnownLanguages(receiver);

        if (languages == null)
            return CanUnderstandWithoutKnowledge(senderLanguageId);

        return languages.Contains(senderLanguageId);
    }

    private bool CanUnderstandWithoutKnowledge(ProtoId<LanguagePrototype> languageId)
    {
        return !_prototypeManager.TryIndex(languageId, out var prototype) ||
               prototype.CanBeUnderstoodWithoutKnowledge;
    }

    public LanguageComponent SetExclusiveLanguage(
        EntityUid uid,
        ProtoId<LanguagePrototype> languageId,
        LanguageComponent? component = null)
    {
        component ??= EnsureComp<LanguageComponent>(uid);
        component.KnownLanguages.Clear();
        component.KnownLanguages.Add(languageId);
        component.CantSpeakLanguages.Clear();
        component.UnlockLanguagesAfterMakeSentient.Clear();
        component.SelectedLanguage = languageId;
        Dirty(uid, component);
        return component;
    }

    public void AddKnowLanguage(EntityUid uid, ProtoId<LanguagePrototype> languageId, LanguageComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        component.KnownLanguages.Add(languageId);
        Dirty(uid, component);
    }

    public bool NeedGenerateTTS(EntityUid sourceUid, ProtoId<LanguagePrototype> prototypeId, bool isWhisper)
    {
        if (String.IsNullOrEmpty(prototypeId))
            return false;

        if (!_prototypeManager.TryIndex(prototypeId, out var languageProto))
            return false;

        if (!languageProto.GenerateTTSForLexicon)
            return false;

        float range = isWhisper ? SharedChatSystem.WhisperMuffledRange : SharedChatSystem.VoiceRange;

        var ents = _lookup.GetEntitiesInRange<ActorComponent>(_transform.GetMapCoordinates(sourceUid, Transform(sourceUid)), range).ToList();

        var hasListener = ents.Any(ent =>
            ent.Comp.PlayerSession is { AttachedEntity: not null }
            && !KnowsLanguage(ent.Owner, prototypeId));

        return hasListener;
    }

    public bool NeedGenerateDirectTTS(EntityUid uid, ProtoId<LanguagePrototype> prototypeId)
    {
        if (String.IsNullOrEmpty(prototypeId))
            return false;

        if (!_prototypeManager.TryIndex(prototypeId, out var languageProto))
            return false;

        if (!languageProto.GenerateTTSForLexicon)
            return false;

        if (KnowsLanguage(uid, prototypeId))
            return false;

        return true;
    }

    public bool NeedGenerateFilterTTS(ProtoId<LanguagePrototype> prototypeId, Filter filter, out List<ICommonSession> understandings)
    {
        var temp = GetUnderstanding(prototypeId);

        understandings = new List<ICommonSession>();

        foreach (var session in filter.Recipients)
        {
            if (temp.Contains(session))
                understandings.Add(session);
        }

        if (String.IsNullOrEmpty(prototypeId))
            return false;

        if (!_prototypeManager.TryIndex(prototypeId, out var languageProto))
            return false;

        if (!languageProto.GenerateTTSForLexicon)
            return false;

        if (understandings.Count <= 0)
            return false;

        return true;
    }

    public bool NeedGenerateRadioTTS(ProtoId<LanguagePrototype> prototypeId, EntityUid[] recivers, out List<EntityUid> understandings, out List<EntityUid> notUnderstandings)
    {
        understandings = new List<EntityUid>();
        notUnderstandings = new List<EntityUid>();
        bool result = false;

        foreach (var uid in recivers)
        {
            if (!KnowsLanguage(uid, prototypeId))
            {
                notUnderstandings.Add(uid);
                result = true;
            }
            else
            {
                understandings.Add(uid);
            }
        }

        return result;
    }

    public List<ICommonSession> GetUnderstanding(ProtoId<LanguagePrototype> languageId)
    {
        var understanding = new List<ICommonSession>();

        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity == null)
            {
                if (CanUnderstandWithoutKnowledge(languageId))
                    understanding.Add(session);
                continue;
            }

            if (KnowsLanguage(session.AttachedEntity.Value, languageId))
                understanding.Add(session);
        }

        return understanding;
    }
}
