// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Maths;

namespace Content.Shared.DeadSpace.Husbandry.Pen;

/// <summary>
/// The place an animal lives in at the moment: a set of tiles of one grid. Not a component, it is worked out by
/// AnimalPenSystem and kept for a few seconds.
/// </summary>
public sealed class AnimalPen
{
    public readonly EntityUid Grid;
    public readonly HashSet<Vector2i> Tiles;
    public readonly PenKind Kind;

    /// <summary>
    /// The tile the pen is counted from: the post, or the home.
    /// </summary>
    public readonly Vector2i Anchor;

    /// <summary>
    /// How far from the middle of the anchor tile the farthest tile reaches, in tiles, with a margin: a circle that
    /// holds the whole pen, for looking things up.
    /// </summary>
    public readonly float Radius;

    public AnimalPen(EntityUid grid, HashSet<Vector2i> tiles, PenKind kind, Vector2i anchor, float radius)
    {
        Grid = grid;
        Tiles = tiles;
        Kind = kind;
        Anchor = anchor;
        Radius = radius;
    }

    public bool Contains(Vector2i tile)
    {
        return Tiles.Contains(tile);
    }
}

public enum PenKind : byte
{
    /// <summary>
    /// Closed in by walls: the whole enclosure, the same for every animal in it.
    /// </summary>
    Enclosed,

    /// <summary>
    /// Nothing closes it in, the square round the home (as far as walls go) is all there is. The animal can wander off.
    /// </summary>
    Open,
}
