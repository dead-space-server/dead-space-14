using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.BluespaceMiner;

[Serializable, NetSerializable]
public enum BluespaceMinerVisuals : byte
{
    Status,
}

[Serializable, NetSerializable]
public enum BluespaceMinerStatus : byte
{
    Ok,
    BadConditions,
    Unpowered,
}
