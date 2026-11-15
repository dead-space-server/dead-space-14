// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Husbandry.Pen;

namespace Content.Server.DeadSpace.Husbandry.Pen;

/// <summary>
/// Makes an animal live in a pen: the place that the feeders and the troughs of the room make (see
/// <see cref="AnimalPenSystem"/>). The animal takes the pen it stands in, does not go beyond it, and a breeding pair is
/// looked for in the same pen. An animal that is in no pen goes to the nearest one.
/// </summary>
[RegisterComponent]
[Access(typeof(AnimalPenSystem))]
public sealed partial class AnimalPenComponent : Component
{
    /// <summary>
    /// How long an animal that stands outside of its pen (it was carried away) takes before it forgets it. Until then it
    /// belongs to the pen and goes back to it.
    /// </summary>
    [DataField]
    public TimeSpan RehomeTime = TimeSpan.FromSeconds(60);

    /// <summary>
    /// How long an animal that has no pen to go to waits before it looks again.
    /// </summary>
    [DataField]
    public TimeSpan ReturnSearchDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long an animal that was sent to a pen waits before it is sent again: if the walk fails (it gets stuck at a wall,
    /// it is dragged off) it does not try on every plan.
    /// </summary>
    [DataField]
    public TimeSpan ReturnRetryDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The pen the animal belongs to. Not saved, found again after a load.
    /// </summary>
    [ViewVariables]
    public AnimalPen? Pen;

    /// <summary>
    /// Since when the animal stands outside of its pen. Empty when it is inside.
    /// </summary>
    [ViewVariables]
    public TimeSpan? OutsideSince;

    [ViewVariables]
    public TimeSpan NextReturnSearch;
}
