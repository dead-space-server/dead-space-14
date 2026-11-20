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
    /// <summary>A secret boss (the Ash Wolf): drawn as its head.</summary>
    BossHead,
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

/// <summary>
/// The answer of the sonar: a circle around where the nearest boss arena is thought to be. Fuzz goes from 0 (close,
/// sharp) to 1 (far, blurred): the farther, the wider the circle and the more it is off the arena's real place.
/// </summary>
[Serializable, NetSerializable]
public sealed class LavalandSonarPing(int id, bool caves, bool hasArena, Vector2 center, float radius, float fuzz, List<Vector2> minibosses)
{
    /// <summary>A new ping: the client restarts its animation when this changes.</summary>
    public int Id = id;

    /// <summary>
    /// The layer the pulse went out on - the one the player stood on. It is drawn only while that layer is shown:
    /// its bosses are of that layer, not of the other one the tablet may be looking at.
    /// </summary>
    public bool Caves = caves;

    /// <summary>An arena was found: the circle below is drawn.</summary>
    public bool HasArena = hasArena;
    public Vector2 Center = center;
    public float Radius = radius;
    public float Fuzz = fuzz;

    /// <summary>Mini-bosses the pulse caught close by: marked a little longer than the arena.</summary>
    public List<Vector2> Minibosses = minibosses;
}

[Serializable, NetSerializable]
public sealed class LavalandSensorTabletState(
    List<LavalandRadarPoint> points,
    float scanRadius,
    List<LavalandRadarPoint>? basePoints = null,
    byte storm = 0,
    bool caves = false,
    bool canSwitchLayer = false,
    int? sonarCharges = null,
    LavalandSonarPing? ping = null)
    : BoundUserInterfaceState
{
    /// <summary>The ash storm on lavaland: 0 - calm, 1 - coming, 2 - raging. Shown in pink and red.</summary>
    public byte Storm = storm;

    /// <summary>What moves or changes: miners, fauna, terrain around miners, beacons.</summary>
    public List<LavalandRadarPoint> Points = points;
    public float ScanRadius = scanRadius;

    /// <summary>The outpost plan. Null when it has not changed since the last state: the client keeps its copy.</summary>
    public List<LavalandRadarPoint>? BasePoints = basePoints;

    /// <summary>The layer shown: the chromite caves, or the surface of lavaland.</summary>
    public bool Caves = caves;

    /// <summary>The lead salvager's tablet: the layer is chosen, not the one the tablet is on.</summary>
    public bool CanSwitchLayer = canSwitchLayer;

    /// <summary>Charges left in the sonar cartridge; null without a cartridge.</summary>
    public int? SonarCharges = sonarCharges;

    /// <summary>The last sonar answer, if any.</summary>
    public LavalandSonarPing? Ping = ping;
}

/// <summary>Switch the shown layer (surface / caves) of a tablet that can.</summary>
[Serializable, NetSerializable]
public sealed class LavalandSensorTabletSwitchLayerMessage : BoundUserInterfaceMessage;

/// <summary>Spend a sonar charge to find the nearest boss arena.</summary>
[Serializable, NetSerializable]
public sealed class LavalandSensorTabletSonarMessage : BoundUserInterfaceMessage;
