// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Body.Limbs;

[DataDefinition, Serializable, NetSerializable]
public sealed partial class LimbImplantSlotDef
{
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField]
    public int Capacity = 1;
}

public static class LimbImplantSlots
{
    public const string Prefix = "limb-implant-";

    public const string Head = "Head";
    public const string Torso = "Torso";
    public const string Groin = "Groin";
    public const string Back = "Back";
    public const string ArmLeft = "ArmLeft";
    public const string ArmRight = "ArmRight";
    public const string HandLeft = "HandLeft";
    public const string HandRight = "HandRight";
    public const string LegLeft = "LegLeft";
    public const string LegRight = "LegRight";
    public const string FootLeft = "FootLeft";
    public const string FootRight = "FootRight";
    public const string Brain = "Brain";
    public const string Eyes = "Eyes";
    public const string Heart = "Heart";
    public const string Lungs = "Lungs";
    public const string Stomach = "Stomach";
    public const string Liver = "Liver";
    public const string Kidneys = "Kidneys";
    public const string Intestines = "Intestines";
    public const string Core = "Core";
    public const string Positronic = "Positronic";

    public static string ContainerId(string slot) => Prefix + slot;

    public static LimbZone? ZoneOf(string slot)
    {
        return slot switch
        {
            Head or Brain or Eyes => LimbZone.Head,
            Torso or Heart or Lungs or Stomach or Liver or Intestines or Back => LimbZone.Torso,
            Groin or Kidneys => LimbZone.Groin,
            ArmLeft => LimbZone.ArmLeft,
            ArmRight => LimbZone.ArmRight,
            HandLeft => LimbZone.HandLeft,
            HandRight => LimbZone.HandRight,
            LegLeft => LimbZone.LegLeft,
            LegRight => LimbZone.LegRight,
            FootLeft => LimbZone.FootLeft,
            FootRight => LimbZone.FootRight,
            Core or Positronic => LimbZone.Torso,
            _ => null,
        };
    }

    public static bool IsBack(string slot) => slot == Back;
}
