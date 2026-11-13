// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Husbandry.Needs;

/// <summary>
/// A coarse state of a need, this is what clients know about.
/// </summary>
[Serializable, NetSerializable]
public enum NeedLevel : byte
{
    Satisfied,
    Low,
    Empty,
}
