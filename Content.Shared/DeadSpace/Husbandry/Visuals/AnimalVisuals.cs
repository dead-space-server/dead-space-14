// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Husbandry.Visuals;

[Serializable, NetSerializable]
public enum AnimalVisuals : byte
{
    /// <summary>
    /// Bool, the animal is busy eating or drinking.
    /// </summary>
    Eating,

    /// <summary>
    /// Bool, a saddle sits in the saddle slot of the animal.
    /// </summary>
    Saddled,
}
