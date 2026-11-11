using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland.Celestial;

/// <summary>
/// УВЯНЬ В ЦВЕТУ: крест из 4 очень длинных лучей, вращающийся вокруг центра.
/// Стадия 1 - розовые полупрозрачные (без урона), стадия 2 - чёрные с обводкой (урон).
/// </summary>
[Serializable, NetSerializable]
public sealed class CelestialBloomEvent(
    Vector2 center,
    int mapId,
    float baseAngle,
    float length,
    float rotateSpeed,
    float sweepTime,
    float fireTime) : EntityEventArgs
{
    public Vector2 Center = center;
    public int MapId = mapId;
    public float BaseAngle = baseAngle;
    public float Length = length;
    public float RotateSpeed = rotateSpeed;
    public float SweepTime = sweepTime;
    public float FireTime = fireTime;
}
