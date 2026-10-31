// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Body.Limbs;

[Serializable, NetSerializable]
public enum LimbMark : byte
{
    Blunt = 0,
    Edge = 1,
    Point = 2,
    Heat = 3,
    Liquid = 4,
}
