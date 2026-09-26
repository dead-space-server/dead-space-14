using Content.Shared.DeadSpace.Languages.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Speech;

public sealed class ListenEvent : EntityEventArgs
{
    public readonly string Message;
    public readonly EntityUid Source;
    public readonly ProtoId<LanguagePrototype> LanguageId; // DS14

    public ListenEvent(string message, EntityUid source, ProtoId<LanguagePrototype> languageId) // DS14
    {
        Message = message;
        Source = source;
        LanguageId = languageId; // DS14
    }
}

public sealed class ListenAttemptEvent : CancellableEntityEventArgs
{
    public readonly EntityUid Source;

    public ListenAttemptEvent(EntityUid source)
    {
        Source = source;
    }
}
