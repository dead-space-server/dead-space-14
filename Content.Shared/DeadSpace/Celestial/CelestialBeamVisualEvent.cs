using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Celestial;

/// <summary>
/// Векторный луч Селестиала: клиент сам рисует толстую полосу от старта до конца.
/// </summary>
[Serializable, NetSerializable]
public sealed class CelestialBeamVisualEvent(
    Vector2 start,
    Vector2 end,
    int mapId,
    float pinkTime,
    float darkTime,
    float widthScale = 1f) : EntityEventArgs
{
    public Vector2 Start = start;
    public Vector2 End = end;
    public int MapId = mapId;
    public float PinkTime = pinkTime;
    public float DarkTime = darkTime;
    public float WidthScale = widthScale;
}
