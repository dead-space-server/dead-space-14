// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Maths;

namespace Content.Server.DeadSpace.Husbandry.Pen;

/// <summary>
/// Gives an animal a pen: the place it lives in, where a breeding pair is looked for (the same pen means the same herd). The pen is the walls round the
/// animal, but no more than a square round its home. Handled by AnimalPenSystem.
/// </summary>
[RegisterComponent]
[Access(typeof(AnimalPenSystem))]
public sealed partial class AnimalPenComponent : Component
{
    /// <summary>
    /// How far from the home the pen goes in each direction, in tiles: 7 makes a square of 15 by 15.
    /// </summary>
    [DataField]
    public int HalfSize = 7;

    /// <summary>
    /// A fill that gets bigger than this many tiles stops: it is a field and not a pen, the whole enclosure is not
    /// taken for the pen then, the square round the home is.
    /// </summary>
    [DataField]
    public int MaxTiles = 600;

    /// <summary>
    /// A fixed thing has to cover this share of a tile to count as a wall: a chair does not close a tile.
    /// </summary>
    [DataField]
    public float WallArea = 0.5f;

    /// <summary>
    /// How long an animal that stands outside of its pen (it was carried away) takes before the place where it stands
    /// becomes its new home. Until then the pen is the one round the old home, and the animal does not wander off.
    /// </summary>
    [DataField]
    public TimeSpan RehomeTime = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The grid and the tile the animal calls home: where it first was when the pen was asked for. The pen is worked
    /// out round it. Not saved, found again after a load.
    /// </summary>
    [ViewVariables]
    public EntityUid? HomeGrid;

    [ViewVariables]
    public Vector2i Home;

    /// <summary>
    /// Since when the animal stands outside of its pen. Empty when it is inside.
    /// </summary>
    [ViewVariables]
    public TimeSpan? OutsideSince;
}
