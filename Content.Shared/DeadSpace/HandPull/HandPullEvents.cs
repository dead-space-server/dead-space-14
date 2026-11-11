using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.HandPull;

[Serializable, NetSerializable]
public sealed class HandPullOfferMessage(NetEntity user, string userName) : EntityEventArgs
{
    public readonly NetEntity User = user;
    public readonly string UserName = userName;
}

[Serializable, NetSerializable]
public sealed class HandPullAnswerMessage(NetEntity user, bool accepted) : EntityEventArgs
{
    public readonly NetEntity User = user;
    public readonly bool Accepted = accepted;
}

[Serializable, NetSerializable]
public sealed class HandPullOfferClosedMessage(NetEntity user) : EntityEventArgs
{
    public readonly NetEntity User = user;
}
