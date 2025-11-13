using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Ghost.SharedGhostPing;

[Serializable, NetSerializable]
public sealed partial class PingMessage : EntityEventArgs
{
    public string ID { get; set; }
    public PingMessage(string id)
    {
        ID = id;
    }
}