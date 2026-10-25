// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Surgery;

[Serializable, NetSerializable]
public enum SurgeryVerbKind : byte
{
    Incise = 0,
    Retract = 1,
    Clamp = 2,
    SawBone = 3,
    Amputate = 4,
    Cauterize = 5,
    SetBone = 6,
    MendBone = 7,
    Extract = 8,
    Insert = 9,
    PullEmbedded = 10,
    Inspect = 11,
    Seat = 12,
}

[Serializable, NetSerializable]
public enum SurgeryGameKind : byte
{
    Cut = 0,
    Spread = 1,
    Saw = 2,
    Clamp = 3,
    Sever = 4,
    Twist = 5,
    Click = 6,
    Hold = 7,
    Tap = 8,
}

[Serializable, NetSerializable]
public enum SurgeryLayerKind : byte
{
    Skin = 0,
    Fat = 1,
    Muscle = 2,
    Bone = 3,
    Vessel = 4,
    Cavity = 5,
    Globe = 6,
}
