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
    Drill = 13,
    Graft = 14,
    Bandage = 15,
    Stanch = 16,
    Inject = 17,
    Append = 18,
    Shock = 19,
    Chop = 20,
    SealSap = 21,
}

[Serializable, NetSerializable]
public enum SurgeryGameKind : byte
{
    Cut = 0,
    Spread = 1,
    Saw = 2,
    Clamp = 3,
    Sever = 4,
    Hold = 7,
    Tap = 8,
    Drill = 9,
    Sear = 10,
    Glue = 11,
    Stanch = 12,
    Inject = 13,
    Dots = 14,
    Steady = 15,
    Chop = 16,
}

public enum SurgeryInk : byte
{
    Flesh,
    Machine,
    Gel,
    Bark,
    Acid,
}

public enum SurgerySide : byte
{
    Front,
    Back,
    Any,
}

[Serializable, NetSerializable]
public enum SurgeryLayerKind : byte
{
    Skin = 0,
    Bone = 3,
    Cavity = 5,
    Globe = 6,
}
