// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland;

[Serializable, NetSerializable]
public enum LavalandSensorTabletUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum LavalandRadarPointKind : byte
{
    Terrain,
    Rock,
    Ore,
    Fauna,
    LivingMiner,
    DeadMiner,
}

[Serializable, NetSerializable]
public sealed class LavalandRadarPoint(Vector2 position, Color color, float size, LavalandRadarPointKind kind)
{
    public Vector2 Position = position;
    public Color Color = color;
    public float Size = size;
    public LavalandRadarPointKind Kind = kind;
}

[Serializable, NetSerializable]
public sealed class LavalandSensorTabletState(List<LavalandRadarPoint> points, float scanRadius)
    : BoundUserInterfaceState
{
    public List<LavalandRadarPoint> Points = points;
    public float ScanRadius = scanRadius;
}
