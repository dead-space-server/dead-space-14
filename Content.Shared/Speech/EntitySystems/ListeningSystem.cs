using Content.Shared.Chat;
using Content.Shared.DeadSpace.Languages.Prototypes;
using Content.Shared.Ghost;
using Content.Shared.Speech.Components;
using Content.Shared.Telephone; // DS-14
using Robust.Shared.Prototypes;

namespace Content.Shared.Speech.EntitySystems;

/// <summary>
/// This system redirects local chat messages to listening entities (e.g., radio microphones).
/// </summary>
public sealed partial class ListeningSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _xforms = default!;

    [SubscribeLocalEvent]
    private void OnSpeak(EntitySpokeEvent ev)
    {
        PingListeners(ev.Source, ev.Message, ev.ObfuscatedMessage, ev.LanguageId); // DS14
    }

<<<<<<< ours
    // DS14-start
    public void PingListeners(
        EntityUid source,
        string message,
        string? obfuscatedMessage,
        ProtoId<LanguagePrototype> languageId,
        bool looc = false) // DS-14
    // DS14-end
||||||| base
    public void PingListeners(EntityUid source, string message, string? obfuscatedMessage)
=======
    /// <summary>
    /// Sends a speech message to entities listening within range.
    /// </summary>
    public void PingListeners(EntityUid source, string message, string? obfuscatedMessage)
>>>>>>> theirs
    {
        // DS14-start
        if (HasComp<SpectralComponent>(source))
            return;
        // DS14-end

        // TODO whispering / audio volume? Microphone sensitivity?
        // for now, whispering just arbitrarily reduces the listener's max range.

        var sourceXform = Transform(source);
        var sourcePos = _xforms.GetWorldPosition(sourceXform);

        var attemptEv = new ListenAttemptEvent(source);
        var ev = new ListenEvent(message, source, languageId, looc); // DS14
        var obfuscatedEv = obfuscatedMessage == null ? null : new ListenEvent(obfuscatedMessage, source, languageId); // DS14
        var query = EntityQueryEnumerator<ActiveListenerComponent, TransformComponent>();

        while(query.MoveNext(out var listenerUid, out var listener, out var xform))
        {
            if (looc && !HasComp<TelephoneComponent>(listenerUid)) // DS-14
                continue;

            if (xform.MapID != sourceXform.MapID)
                continue;

            // range checks
            // TODO proper speech occlusion
            var distance = (sourcePos - _xforms.GetWorldPosition(xform)).LengthSquared();
            if (distance > listener.Range * listener.Range)
                continue;

            RaiseLocalEvent(listenerUid, attemptEv);
            if (attemptEv.Cancelled)
            {
                attemptEv.Uncancel();
                continue;
            }

            if (obfuscatedEv != null && distance > SharedChatSystem.WhisperClearRange)
                RaiseLocalEvent(listenerUid, obfuscatedEv);
            else
                RaiseLocalEvent(listenerUid, ev);
        }
    }
}
