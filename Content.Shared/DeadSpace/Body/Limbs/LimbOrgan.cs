// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Body.Limbs;

[Serializable, NetSerializable]
public enum LimbOrgan : byte
{
    Brain = 0,
    Eyes = 1,
    Heart = 2,
    Lungs = 3,
    Stomach = 4,
    Liver = 5,
    Kidneys = 6,
    Core = 7,
    Intestines = 8,
}

[Flags]
public enum LimbOrganMask : ushort
{
    None = 0,
    Brain = 1 << 0,
    Eyes = 1 << 1,
    Heart = 1 << 2,
    Lungs = 1 << 3,
    Stomach = 1 << 4,
    Liver = 1 << 5,
    Kidneys = 1 << 6,
    Core = 1 << 7,
    Intestines = 1 << 8,
    Standard = Brain | Eyes | Heart | Lungs | Stomach | Liver | Kidneys | Intestines,
}

[DataDefinition, Serializable, NetSerializable]
public partial struct LimbOrganState
{
    [DataField]
    public float Damage;

    [DataField]
    public bool Ruptured;
}
