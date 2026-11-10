// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Maths;

namespace Content.Shared.DeadSpace.Husbandry.Pen;

/// <summary>
/// Gives an animal a pen: the place it lives in, which the farm looks at when nobody is around to watch (food and
/// water within the pen, where the feeders are). The pen is the walls round the animal, but no more than a square round its
/// home. Handled by AnimalPenSystem.
/// </summary>
[RegisterComponent]
public sealed partial class AnimalPenComponent : Component
{
    /// <summary>
    /// How far from the home the pen goes in each direction, in tiles: 7 makes a square of 15 by 15.
    /// </summary>
    [DataField]
    public int HalfSize = 7;

    /// <summary>
    /// How likely it is, per hour, that an animal of an open pen (nothing round it but the square) wanders off the map
    /// while nobody is around: it is gone. 0 turns it off.
    /// </summary>
    [DataField]
    public float EscapeChancePerHour = 0.005f;

    /// <summary>
    /// The same for an animal that is hungry or thirsty and finds nothing to eat or drink in the pen.
    /// </summary>
    [DataField]
    public float EscapeChanceStarvingPerHour = 0.25f;

    /// <summary>
    /// The grid and the tile the animal calls home: where it first was when the pen was asked for. It is moved to
    /// where the animal is if it was taken out of its square. Not saved, found again after a load.
    /// </summary>
    [ViewVariables]
    public EntityUid? HomeGrid;

    [ViewVariables]
    public Vector2i Home;
}
