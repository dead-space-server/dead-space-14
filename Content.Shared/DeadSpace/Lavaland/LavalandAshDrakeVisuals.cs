using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland;

[Serializable, NetSerializable]
public enum LavalandAshDrakeVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum LavalandAshDrakeVisualLayers : byte
{
    Base,
}

[Serializable, NetSerializable]
public enum LavalandAshDrakeVisualState : byte
{
    Dragon,
    Shadow,
    Swoop,
    Dead,
}
/// <summary>The colossus below a quarter of its health (Goob-Station angry animation).</summary>
[Serializable, NetSerializable]
public enum LavalandColossusVisuals : byte
{
    Angry,
}

[Serializable, NetSerializable]
public enum LavalandColossusVisualLayers : byte
{
    Base,
}
