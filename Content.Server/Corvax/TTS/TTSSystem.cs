using System.Threading.Tasks;
using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Atmos;
using Content.Shared.DeadSpace.Audio;
using Content.Shared.Chat;
using Content.Shared.CCVar;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Corvax.TTS;
using Content.Shared.GameTicking;
using Content.Shared.Players.RateLimiting;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Server.DeadSpace.Languages;
using Content.Shared.DeadSpace.Languages.Prototypes;

namespace Content.Server.Corvax.TTS;

// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly TTSManager _ttsManager = default!;
    [Dependency] private readonly IRobustRandom _rng = default!;
    [Dependency] private readonly LanguageSystem _language = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!; // DS14

    // DS14-start
    private static readonly string[] SuitSlots = ["head", "outerClothing"];
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly BarotraumaSystem _barotrauma = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    // DS14-end

    private readonly List<string> _sampleText =
        new()
        {
            "Съешь же ещё этих мягких французских булок, да выпей чаю.",
            "Клоун, прекрати разбрасывать банановые кожурки офицерам под ноги!",
            "Капитан, вы уверены что хотите назначить клоуна на должность главы персонала?",
            "Эс Бэ! Тут человек в сером костюме, с тулбоксом и в маске! Помогите!!",
            "Я надеюсь что инженеры внимательно следят за сингулярностью...",
            "Вы слышали эти странные крики в техах? Мне кажется туда ходить небезопасно.",
            "Вы не видели Гамлета? Мне кажется он забегал к вам на кухню.",
            "Здесь есть доктор? Человек умирает от отравленного пончика! Нужна помощь!",
            "Возле эвакуационного шаттла разгерметизация! Инженеры, нам срочно нужна ваша помощь!",
            "Бармен, налей мне самого крепкого вина, которое есть в твоих запасах!"
        };

    private const int MaxMessageChars = 100 * 3; // same as SingleBubbleCharLimit * 3
    private bool _isEnabled = false;

    public override void Initialize()
    {
        _cfg.OnValueChanged(CCCVars.TTSEnabled, v => _isEnabled = v, true);

        SubscribeLocalEvent<TransformSpeechEvent>(OnTransformSpeech);
        SubscribeLocalEvent<TTSComponent, EntitySpokeEvent>(OnEntitySpoke);
        SubscribeLocalEvent<TTSComponent, EntitySpokeToEntityEvent>(OnEntitySpokeToEntity);
        SubscribeLocalEvent<RadioSpokeEvent>(OnRadioSpokeEvent);
        SubscribeLocalEvent<AnnounceSpokeEvent>(OnAnnounceSpokeEvent);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeNetworkEvent<RequestPreviewTTSEvent>(OnRequestPreviewTTS);

        RegisterRateLimits();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _ttsManager.ResetCache();
    }

    private async void OnRequestPreviewTTS(RequestPreviewTTSEvent ev, EntitySessionEventArgs args)
    {
        if (!_isEnabled ||
            !_prototypeManager.TryIndex<TTSVoicePrototype>(ev.VoiceId, out var protoVoice))
            return;

        if (HandleRateLimit(args.SenderSession) != RateLimitStatus.Allowed)
            return;

        var previewText = _rng.Pick(_sampleText);
        var soundData = await GenerateTTS(previewText, protoVoice.Speaker);
        if (soundData is null)
            return;

        RaiseNetworkEvent(new PlayTTSEvent(soundData), Filter.SinglePlayer(args.SenderSession));
    }

    private async void OnEntitySpoke(EntityUid uid, TTSComponent component, EntitySpokeEvent args)
    {
        var voiceId = component.VoicePrototypeId;
        if (!_isEnabled ||
            args.Message.Length > MaxMessageChars ||
            voiceId == null)
            return;

        var voiceEv = new TransformSpeakerVoiceEvent(uid, voiceId);
        RaiseLocalEvent(uid, voiceEv);
        voiceId = voiceEv.VoiceId;

        if (!_prototypeManager.TryIndex<TTSVoicePrototype>(voiceId, out var protoVoice))
            return;

        if (args.ObfuscatedMessage != null)
        {
            HandleWhisper(uid, args.Message, args.LexiconMessage, args.LanguageId, args.ObfuscatedMessage, protoVoice.Speaker, args.IsRadioSpeech); // DS14
            return;
        }

        HandleSay(uid, args.Message, args.LexiconMessage, args.LanguageId, protoVoice.Speaker, args.IsRadioSpeech); // DS14
    }

    private async void OnEntitySpokeToEntity(EntityUid uid, TTSComponent component, EntitySpokeToEntityEvent args)
    {
        var voiceId = component.VoicePrototypeId;
        if (!_isEnabled ||
            args.Message.Length > MaxMessageChars ||
            voiceId == null)
            return;

        var voiceEv = new TransformSpeakerVoiceEvent(uid, voiceId);
        RaiseLocalEvent(uid, voiceEv);
        voiceId = voiceEv.VoiceId;

        if (!_prototypeManager.TryIndex<TTSVoicePrototype>(voiceId, out var protoVoice))
            return;

        HandleDirectSay(args.Target, args.Message, args.LexiconMessage, args.LanguageId, protoVoice.Speaker);
    }

    // DS14-start
    private void OnRadioSpokeEvent(RadioSpokeEvent args)
    {
        var receivers = new HashSet<EntityUid>(args.Receivers) { args.Source }.ToArray();
        if (!_isEnabled || args.Message.Length > MaxMessageChars ||
            !TryComp<TTSComponent>(args.Source, out var component) || component.VoicePrototypeId == null)
        {
            SendRadioCues(receivers);
            return;
        }
        var voiceEv = new TransformSpeakerVoiceEvent(args.Source, component.VoicePrototypeId);
        RaiseLocalEvent(args.Source, voiceEv);
        if (!_prototypeManager.TryIndex<TTSVoicePrototype>(voiceEv.VoiceId, out var protoVoice))
        {
            SendRadioCues(receivers);
            return;
        }
        HandleRadio(args.Source, receivers, args.Message, args.LexiconMessage, args.LanguageId, protoVoice.Speaker);
    }

    private void SendRadioCues(EntityUid[] receivers)
    {
        foreach (var receiver in receivers)
        {
            if (!TerminatingOrDeleted(receiver))
                RaiseNetworkEvent(new PlayTTSEvent(Array.Empty<byte>(), isRadio: true), Filter.Entities(receiver));
        }
    }

    internal bool HasSealedSuit(EntityUid uid)
    {
        foreach (var slot in SuitSlots)
        {
            if (!_inventory.TryGetSlotEntity(uid, slot, out var clothing) ||
                !_barotrauma.TryGetPressureProtectionValues(clothing.Value, out _, out _, out var multiplier, out var modifier) ||
                multiplier + modifier <= Atmospherics.HazardLowPressure)
                return false;
        }
        return true;
    }

    private bool UseSuitRadio(EntityUid source, ICommonSession session, ChatSystem.ICChatRecipientData data)
    {
        if (data.AudioSourceOverride != null || session.AttachedEntity is not { } listener ||
            !HasSealedSuit(source) || !HasSealedSuit(listener))
            return false;
        return (_atmos.GetTileMixture(source)?.Pressure ?? 0f) < 10f ||
               (_atmos.GetTileMixture(listener)?.Pressure ?? 0f) < 10f;
    }
    // DS14-end

    private async void OnAnnounceSpokeEvent(AnnounceSpokeEvent args)
    {
        var voiceId = args.Voice;
        if (!_isEnabled ||
            args.Message.Length > _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength) ||
            voiceId == null)
            return;

        if (args.Source != null)
        {
            var voiceEv = new TransformSpeakerVoiceEvent(args.Source.Value, voiceId);
            RaiseLocalEvent(args.Source.Value, voiceEv);
            voiceId = voiceEv.VoiceId;
        }

        if (!_prototypeManager.TryIndex<TTSVoicePrototype>(voiceId, out var protoVoice))
            return;

        Timer.Spawn(6000, () => HandleAnnounce(args.Message, args.LexiconMessage, args.LanguageId, protoVoice.Speaker, args.Filter)); // Awful, but better than sending announce sound to client in resource file
    }

    private async void HandleSay(EntityUid uid, string message, string lexiconMessage, ProtoId<LanguagePrototype> languageId, string speaker, bool suppressEcho) // DS14
    {
        // DS14-start
        var recipientData = GetExpandedVoiceRecipients(uid, SharedChatSystem.VoiceRange);
        var recipients = recipientData.Keys;
        var suitRecipients = new HashSet<ICommonSession>();
        foreach (var (session, data) in recipientData)
        {
            if (UseSuitRadio(uid, session, data))
                suitRecipients.Add(session);
        }
        // DS14-end
        var soundData = await GenerateTTS(message, speaker);

        byte[]? soundLexiconData = null;
        var understanding = new HashSet<ICommonSession>(_language.GetUnderstanding(languageId));

        if (NeedsLexiconTTS(languageId, recipients, understanding))
            soundLexiconData = await GenerateTTS(lexiconMessage, speaker);

        if (soundData is null) return;

        // DS14-start: carry recipient-specific remote hearing attenuation and source.
        foreach (var (session, data) in recipientData)
        {
            var audioSource = GetNetEntity(data.AudioSourceOverride ?? uid);

            if (!understanding.Contains(session))
            {
                if (soundLexiconData is null)
                    RaiseNetworkEvent(new PlayTTSEvent(new byte[0], audioSource, isSoundLexicon: true, languageId: languageId, distanceOverride: data.AudioRangeOverride, isSuitRadio: suitRecipients.Contains(session), suppressEcho: suppressEcho), session);
                else
                    RaiseNetworkEvent(new PlayTTSEvent(soundLexiconData, audioSource, distanceOverride: data.AudioRangeOverride, isSuitRadio: suitRecipients.Contains(session), suppressEcho: suppressEcho), session);
            }
            else
                RaiseNetworkEvent(new PlayTTSEvent(soundData, audioSource, isSoundLexicon: false, distanceOverride: data.AudioRangeOverride, isSuitRadio: suitRecipients.Contains(session), suppressEcho: suppressEcho), session);
        }
        // DS14-end

    }

    private async void HandleDirectSay(EntityUid uid, string message, string lexiconMessage, ProtoId<LanguagePrototype> languageId, string speaker)
    {
        var soundData = await GenerateTTS(message, speaker);

        byte[]? soundLexiconData = null;

        if (_language.NeedGenerateDirectTTS(uid, languageId))
            soundLexiconData = await GenerateTTS(lexiconMessage, speaker);

        if (soundData is null) return;

        if (!_language.KnowsLanguage(uid, languageId))
        {
            if (soundLexiconData is null)
                RaiseNetworkEvent(new PlayTTSEvent(new byte[0], GetNetEntity(uid), isSoundLexicon: true, languageId: languageId), uid);
            else
                RaiseNetworkEvent(new PlayTTSEvent(soundLexiconData, GetNetEntity(uid)), uid);
        }
        else
            RaiseNetworkEvent(new PlayTTSEvent(soundData, GetNetEntity(uid)), uid);
    }

    private async void HandleRadio(EntityUid source, EntityUid[] uids, string message, string lexiconMessage, ProtoId<LanguagePrototype> languageId, string speaker) // DS14
    {
        var soundData = await GenerateTTS(message, speaker);

        byte[]? soundLexiconData = null;

        if (_language.NeedGenerateRadioTTS(languageId, uids, out var understandings, out var notUnderstandings))
            soundLexiconData = await GenerateTTS(lexiconMessage, speaker);

        // DS14-start
        if (soundData is null)
        {
            SendRadioCues(uids);
            return;
        }
        // DS14-end

        foreach (var uid in understandings)
        {
            RaiseNetworkEvent(new PlayTTSEvent(soundData, GetNetEntity(uid), isRadio: true, radioCueOnly: uid == source), Filter.Entities(uid)); // DS14
        }

        foreach (var uid in notUnderstandings)
        {
            if (soundLexiconData is null)
                RaiseNetworkEvent(new PlayTTSEvent(new byte[0], GetNetEntity(uid), isRadio: true, isSoundLexicon: true, languageId: languageId, radioCueOnly: uid == source), Filter.Entities(uid)); // DS14
            else
                RaiseNetworkEvent(new PlayTTSEvent(soundLexiconData, GetNetEntity(uid), isRadio: true, radioCueOnly: uid == source), Filter.Entities(uid)); // DS14
        }

    }

    private async void HandleAnnounce(string message, string lexiconMessage, ProtoId<LanguagePrototype> languageId, string speaker, Filter filter)
    {
        var soundData = await GenerateTTS(message, speaker);

        byte[]? soundLexiconData = null;
        List<ICommonSession> understanding = new List<ICommonSession>();

        if (_language.NeedGenerateFilterTTS(languageId, filter, out understanding))
            soundLexiconData = await GenerateTTS(lexiconMessage, speaker);

        if (soundData is null) return;

        foreach (var session in filter.Recipients)
        {
            if (!understanding.Contains(session))
            {
                if (soundLexiconData is null)
                    RaiseNetworkEvent(new PlayTTSEvent(new byte[0], isSoundLexicon: true, languageId: languageId), session);
                else
                    RaiseNetworkEvent(new PlayTTSEvent(soundLexiconData), session);
            }
            else
                RaiseNetworkEvent(new PlayTTSEvent(soundData), session);
        }
    }

    private async void HandleWhisper(EntityUid uid, string message, string lexiconMessage, ProtoId<LanguagePrototype> languageId, string obfMessage, string speaker, bool suppressEcho) // DS14
    {
        // DS14-start
        var recipientData = GetExpandedVoiceRecipients(uid, SpatialAudio.WhisperRange);
        var recipients = recipientData.Keys;
        var suitRecipients = new HashSet<ICommonSession>();
        foreach (var (session, data) in recipientData)
        {
            if (UseSuitRadio(uid, session, data))
                suitRecipients.Add(session);
        }
        // DS14-end
        var fullSoundData = await GenerateTTS(message, speaker, true);

        byte[]? lexiconSoundData = null;
        var understanding = new HashSet<ICommonSession>(_language.GetUnderstanding(languageId));

        if (NeedsLexiconTTS(languageId, recipients, understanding))
            lexiconSoundData = await GenerateTTS(lexiconMessage, speaker);

        // DS14-start: beyond clear whisper range, never deliver the full spoken text as audio.
        byte[]? obfuscatedSoundData = null;
        foreach (var data in recipientData.Values)
        {
            if ((data.AudioRangeOverride ?? data.Range) <= SharedChatSystem.WhisperClearRange)
                continue;
            obfuscatedSoundData = await GenerateTTS(obfMessage, speaker, true);
            break;
        }
        // DS14-end

        if (fullSoundData is null) return;

        // DS14-start: carry recipient-specific remote hearing attenuation and source.
        foreach (var (session, data) in recipientData)
        {
            var audioSource = GetNetEntity(data.AudioSourceOverride ?? uid);

            if ((data.AudioRangeOverride ?? data.Range) > SharedChatSystem.WhisperClearRange && understanding.Contains(session))
            {
                if (obfuscatedSoundData != null)
                    RaiseNetworkEvent(new PlayTTSEvent(obfuscatedSoundData, audioSource, isWhisper: true,
                        distanceOverride: data.AudioRangeOverride, isSuitRadio: suitRecipients.Contains(session), suppressEcho: suppressEcho), session);
                continue;
            }

            if (!understanding.Contains(session))
            {
                if (lexiconSoundData is null)
                    RaiseNetworkEvent(new PlayTTSEvent(new byte[0], audioSource, isWhisper: true, isSoundLexicon: true, languageId: languageId, distanceOverride: data.AudioRangeOverride, isSuitRadio: suitRecipients.Contains(session), suppressEcho: suppressEcho), session);
                else
                    RaiseNetworkEvent(new PlayTTSEvent(lexiconSoundData, audioSource, isWhisper: true, distanceOverride: data.AudioRangeOverride, isSuitRadio: suitRecipients.Contains(session), suppressEcho: suppressEcho), session);
            }
            else
                RaiseNetworkEvent(new PlayTTSEvent(fullSoundData, audioSource, isWhisper: true, distanceOverride: data.AudioRangeOverride, isSuitRadio: suitRecipients.Contains(session), suppressEcho: suppressEcho), session);

        }
        // DS14-end
    }

    // DS14-start: PVS can follow a movable remote eye, so establish ordinary listeners by their attached entities first.
    private Dictionary<ICommonSession, ChatSystem.ICChatRecipientData> GetExpandedVoiceRecipients(EntityUid source, float voiceRange)
    {
        var recipients = new Dictionary<ICommonSession, ChatSystem.ICChatRecipientData>();
        var sourceXform = Transform(source);
        var sourcePosition = _transform.GetWorldPosition(sourceXform);

        foreach (var session in Filter.Pvs(source).Recipients)
        {
            if (session.AttachedEntity is not { Valid: true } listener ||
                !TryComp(listener, out TransformComponent? listenerXform) ||
                listenerXform.MapID != sourceXform.MapID)
            {
                continue;
            }

            var distance = (sourcePosition - _transform.GetWorldPosition(listenerXform)).Length();
            if (distance >= voiceRange)
                continue;

            recipients.TryAdd(session, new ChatSystem.ICChatRecipientData(distance, false));
        }

        RaiseLocalEvent(new ExpandICChatRecipientsEvent(source, voiceRange, recipients));

        return recipients;
    }
    // DS14-end

    private bool NeedsLexiconTTS(
        ProtoId<LanguagePrototype> languageId,
        IEnumerable<ICommonSession> recipients,
        HashSet<ICommonSession> understanding)
    {
        if (string.IsNullOrEmpty(languageId))
            return false;

        if (!_prototypeManager.TryIndex(languageId, out var languageProto) || !languageProto.GenerateTTSForLexicon)
            return false;

        foreach (var session in recipients)
        {
            if (!understanding.Contains(session))
                return true;
        }

        return false;
    }

    // ReSharper disable once InconsistentNaming
    private async Task<byte[]?> GenerateTTS(string text, string speaker, bool isWhisper = false)
    {
        var textSanitized = Sanitize(text);
        if (textSanitized == "") return null;
        if (char.IsLetter(textSanitized[^1]))
            textSanitized += ".";

        var ssmlTraits = SoundTraits.RateFast;
        if (isWhisper)
            ssmlTraits = SoundTraits.PitchVerylow;
        var textSsml = ToSsmlText(textSanitized, ssmlTraits);

        return await _ttsManager.ConvertTextToSpeech(speaker, textSsml);
    }
}
