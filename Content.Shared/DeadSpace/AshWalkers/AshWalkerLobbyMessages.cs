using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.AshWalkers;

[Serializable, NetSerializable]
public sealed class AshWalkerLobbyStateEvent(bool waiting) : EntityEventArgs
{
    public readonly bool Waiting = waiting;
}

[Serializable, NetSerializable]
public sealed class AshWalkerLobbyStateRequestEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class AshWalkerLobbyCancelEvent : EntityEventArgs;
