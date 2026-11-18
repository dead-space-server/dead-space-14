// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Maths;

namespace Content.Shared.DeadSpace.Husbandry.Pen;

/// <summary>
/// A place animals live in: a set of tiles of one grid, made by the feeders and the troughs that stand in it. Not a
/// component: it is worked out by AnimalPenSystem, kept for minutes, and shared by all the animals in it.
/// </summary>
public sealed class AnimalPen
{
    public readonly EntityUid Grid;

    /// <summary>
    /// The tiles of the pen. Taken again when the pen is refreshed, so keep the pen, not this set.
    /// </summary>
    public HashSet<Vector2i> Tiles = new();

    /// <summary>
    /// The feeders and the troughs of the pen. The pen is kept as long as one of them is there: a device that is
    /// removed or added does not change the pen by itself.
    /// </summary>
    public readonly HashSet<EntityUid> Devices = new();

    /// <summary>
    /// The tile in the middle of the pen.
    /// </summary>
    public Vector2i Anchor;

    /// <summary>
    /// How far from the middle of the anchor tile the farthest tile reaches, with a margin: a circle that holds the
    /// whole pen, for looking things up.
    /// </summary>
    public float Radius;

    /// <summary>
    /// The place is bigger than the pen: the flood stopped at the limit, the pen is the tiles nearest to the devices.
    /// </summary>
    public bool Truncated;

    /// <summary>
    /// When the tiles are worked out again.
    /// </summary>
    public TimeSpan RefreshAt;

    /// <summary>
    /// The pen is gone (no device left, or it was taken into another pen, see <see cref="MergedInto"/>).
    /// </summary>
    public bool Removed;

    public AnimalPen? MergedInto;

    /// <summary>
    /// What was found in the pen by an area lookup, by kind, and when. Shared by everything that asks, for a few
    /// seconds.
    /// </summary>
    public readonly Dictionary<(Type Kind, bool Adjacent), PenFound> Found = new();

    public AnimalPen(EntityUid grid)
    {
        Grid = grid;
    }

    public bool Contains(Vector2i tile)
    {
        return Tiles.Contains(tile);
    }
}

/// <summary>
/// The result of an area lookup in a pen and when it was made.
/// </summary>
public sealed class PenFound
{
    public bool Fetched;
    public TimeSpan Time;
    public readonly List<EntityUid> Items = new();
}
