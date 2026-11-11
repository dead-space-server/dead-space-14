// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Body.Limbs;

[Serializable, NetSerializable]
public enum LimbZone : byte
{
    Head = 0,
    Torso = 1,
    ArmLeft = 2,
    ArmRight = 3,
    LegLeft = 4,
    LegRight = 5,
    HandLeft = 6,
    HandRight = 7,
    FootLeft = 8,
    FootRight = 9,
    Groin = 10,
    Eyes = 11,
    Mouth = 12,
}
