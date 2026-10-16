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
    BaseFloor,
    BaseWall,
    Lava,
    MorkiteSearchArea,
    /// <summary>Purple interference around the Celestial pit; Size is the radius.</summary>
    CelestialNoise,
    /// <summary>A marker beacon with a message; the message is in Label.</summary>
    Beacon,
}

[Serializable, NetSerializable]
public sealed class LavalandRadarPoint(Vector2 position, Color color, float size, LavalandRadarPointKind kind, string? label = null)
{
    public Vector2 Position = position;
    public Color Color = color;
    public float Size = size;
    public LavalandRadarPointKind Kind = kind;

    /// <summary>Text shown when the mouse is over the point (beacon messages).</summary>
    public string? Label = label;
}

[Serializable, NetSerializable]
public sealed class LavalandSensorTabletState(
    List<LavalandRadarPoint> points,
    float scanRadius,
    List<LavalandRadarPoint>? basePoints = null,
    byte storm = 0)
    : BoundUserInterfaceState
{
    /// <summary>The ash storm on lavaland: 0 - calm, 1 - coming, 2 - raging. Shown in pink and red.</summary>
    public byte Storm = storm;

    /// <summary>What moves or changes: miners, fauna, terrain around miners, beacons.</summary>
    public List<LavalandRadarPoint> Points = points;
    public float ScanRadius = scanRadius;

    /// <summary>The outpost plan. Null when it has not changed since the last state: the client keeps its copy.</summary>
    public List<LavalandRadarPoint>? BasePoints = basePoints;
}
