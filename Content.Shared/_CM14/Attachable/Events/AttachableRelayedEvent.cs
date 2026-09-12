namespace Content.Shared._CM14.Attachable.Events;

/// <summary>
/// Wraps an event relayed from a holder so attachment changes can be copied
/// back to the original by-ref event.
/// </summary>
// DS14-start
public sealed class AttachableRelayedEvent<TEvent> : EntityEventArgs
{
    public TEvent Args;
    public EntityUid Holder;

    public AttachableRelayedEvent(TEvent args, EntityUid holder)
    {
        Args = args;
        Holder = holder;
    }
}
// DS14-end
