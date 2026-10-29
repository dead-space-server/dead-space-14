using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.HandPull;

[Serializable, NetSerializable]
public sealed class HandPullOfferMessage : EntityEventArgs
{
    public readonly int RequestID;
    public readonly string UserName;

    public HandPullOfferMessage(int requestID, string userName)
    {
        RequestID = requestID;
        UserName = userName;
    }
}

[Serializable, NetSerializable]
public sealed class HandPullAnswerMessage : EntityEventArgs
{
    public readonly int RequestID;
    public readonly bool Accepted;

    public HandPullAnswerMessage(int requestID, bool accepted)
    {
        RequestID = requestID;
        Accepted = accepted;
    }
}

[Serializable, NetSerializable]
public sealed class HandPullOfferClosedMessage : EntityEventArgs
{
    public readonly int RequestID;

    public HandPullOfferClosedMessage(int requestID)
    {
        RequestID = requestID;
    }
}
