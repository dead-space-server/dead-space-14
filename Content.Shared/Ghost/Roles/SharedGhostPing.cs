using Robust.Shared.Serialization;

namespace Content.Shared.Ghost.SharedGhostPing;

[Serializable, NetSerializable]
public sealed partial class PingMessege : EntityEventArgs
{
    public string ID { get; set; }
    public PingMessege(string id)
    {
        ID = id;
    }
}