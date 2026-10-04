using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Celestial;

/// <summary>
/// Cutter: восемь лучей из точки вращаются вокруг центра, затем вспыхивают.
/// </summary>
[Serializable, NetSerializable]
public sealed class CelestialCutterEvent(
    Vector2 center,
    int mapId,
    float baseAngle,
    float length,
    float rotateTime,
    float fireTime) : EntityEventArgs
{
    public Vector2 Center = center;
    public int MapId = mapId;
    public float BaseAngle = baseAngle;
    public float Length = length;
    public float RotateTime = rotateTime;
    public float FireTime = fireTime;
}
