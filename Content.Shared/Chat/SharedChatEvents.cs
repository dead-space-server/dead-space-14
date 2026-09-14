using Content.Shared.DeadSpace.Languages.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Radio;
using Content.Shared.Speech;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chat;

/// <summary>
/// This event should be sent everytime an entity talks (Radio, local chat, etc...).
/// The event is sent to both the entity itself, and all clothing (For stuff like voice masks).
/// </summary>
public sealed class TransformSpeakerNameEvent : EntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = SlotFlags.WITHOUT_POCKET;
    public EntityUid Sender;
    public string VoiceName;
    public ProtoId<SpeechVerbPrototype>? SpeechVerb;

    public TransformSpeakerNameEvent(EntityUid sender, string name)
    {
        Sender = sender;
        VoiceName = name;
        SpeechVerb = null;
    }
}

/// <summary>
/// Raised broadcast in order to transform speech.transmit
/// </summary>
public sealed class TransformSpeechEvent : CancellableEntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = SlotFlags.WITHOUT_POCKET;
    public EntityUid Sender;
    public string Message;

    public TransformSpeechEvent(EntityUid sender, string message)
    {
        Sender = sender;
        Message = message;
    }
}

public sealed class CheckIgnoreSpeechBlockerEvent : EntityEventArgs
{
    public EntityUid Sender;
    public bool IgnoreBlocker;

    public CheckIgnoreSpeechBlockerEvent(EntityUid sender, bool ignoreBlocker)
    {
        Sender = sender;
        IgnoreBlocker = ignoreBlocker;
    }
}

/// <summary>
///     Raised on an entity when it speaks, either through 'say' or 'whisper'.
/// </summary>
public sealed class EntitySpokeEvent : EntityEventArgs
{
    public readonly EntityUid Source;
    public readonly string Message;
    public readonly string OriginalMessage;
    public readonly string LexiconMessage; // DS14-Languages
    public readonly ProtoId<LanguagePrototype> LanguageId; // DS14-Languages
    public readonly string? ObfuscatedMessage; // not null if this was a whisper

    /// <summary>
    ///     If the entity was trying to speak into a radio, this was the channel they were trying to access. If a radio
    ///     message gets sent on this channel, this should be set to null to prevent duplicate messages.
    /// </summary>
    public RadioChannelPrototype? Channel;
    public readonly bool IsRadioSpeech; // DS14: Preserve radio intent after a transmitter consumes Channel.

    public EntitySpokeEvent(EntityUid source, string message, string originalMessage, string lexiconMessage, ProtoId<LanguagePrototype> languageId, RadioChannelPrototype? channel, string? obfuscatedMessage)
    {
        Source = source;
        Message = message;
        OriginalMessage = originalMessage; // Corvax-TTS: Spec symbol sanitize
        LexiconMessage = lexiconMessage; // DS14-Languages
        LanguageId = languageId; // DS14-Languages
        Channel = channel;
        IsRadioSpeech = channel != null; // DS14
        ObfuscatedMessage = obfuscatedMessage;
    }
}

/// <summary>
///     Raised on an entity when it sends direct message to another entity
/// </summary>
public sealed class EntitySpokeToEntityEvent : EntityEventArgs
{
    public readonly EntityUid Target;
    public readonly string Message;
    public readonly string LexiconMessage; // DS14-Languages
    public readonly ProtoId<LanguagePrototype> LanguageId; // DS14-Languages

    public EntitySpokeToEntityEvent(EntityUid target, string message, string lexiconMessage, ProtoId<LanguagePrototype> languageId)
    {
        Target = target;
        Message = message;
        LanguageId = languageId; // DS14-Languages
        LexiconMessage = lexiconMessage; // DS14-Languages
    }
}

/// <summary>
///     Raised on an entity after <see cref="EntitySpokeEvent"/> when it speaks using radio.
/// </summary>
public sealed class RadioSpokeEvent : EntityEventArgs
{
    public readonly EntityUid Source;
    public readonly string Message;
    public readonly string LexiconMessage; // DS14-Languages
    public readonly ProtoId<LanguagePrototype> LanguageId; // DS14-Languages

    /// <summary>
    ///     Of course, we can just use <see cref="EntitySpokeEvent"/>, but it's easier to send a message using RadioSystem
    /// </summary>
    public readonly EntityUid[] Receivers;

    public RadioSpokeEvent(EntityUid source, string message, string lexiconMessage, ProtoId<LanguagePrototype> languageId, EntityUid[] receivers)
    {
        Source = source;
        Message = message;
        LexiconMessage = lexiconMessage; // DS14-Languages
        LanguageId = languageId; // DS14-Languages
        Receivers = receivers;
    }
}

/// <summary>
///     Raised when we don't have direct source
/// </summary>
public sealed class AnnounceSpokeEvent : EntityEventArgs
{
    public readonly string Voice;
    public readonly string Message;
    public readonly string LexiconMessage; // DS14-Languages
    public readonly ProtoId<LanguagePrototype> LanguageId; // DS14-Languages
    public readonly EntityUid? Source;
    public readonly Filter Filter = Filter.Empty();

    public AnnounceSpokeEvent(string voice, string message, string lexiconMessage, ProtoId<LanguagePrototype> languageId, Filter filter, EntityUid? source)
    {
        Voice = voice;
        Message = message;
        LexiconMessage = lexiconMessage; // DS14-Languages
        LanguageId = languageId; // DS14-Languages
        Filter = filter; // DS14-Languages
        Source = source;
    }
}
