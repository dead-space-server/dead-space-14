// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Surgery;

[Prototype]
public sealed partial class SurgeryAnatomyPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<SurgeryLayerKind> Layers = new();

    [DataField]
    public ProtoId<SurgeryOperationPrototype>? Operation;
}

[Prototype]
public sealed partial class SurgeryMarkPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Prefix = string.Empty;

    [DataField]
    public int Ax;

    [DataField]
    public int Ay;

    [DataField]
    public int Bx;

    [DataField]
    public int By;

    [DataField]
    public int Hx;

    [DataField]
    public int Hy;

    [DataField]
    public int Hw;

    [DataField]
    public int Hh;

    [DataField]
    public List<SurgeryVerbKind> Start = new();

    [DataField]
    public bool Self = true;
}

[Prototype]
public sealed partial class SurgeryMissPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public DamageSpecifier Damage = new();
}

[Prototype]
public sealed partial class SurgeryHintPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<SurgeryHintIcon> Icons = new();
}

[DataDefinition]
public sealed partial class SurgeryHintIcon
{
    [DataField(required: true)]
    public string Sprite = string.Empty;

    [DataField(required: true)]
    public string State = string.Empty;
}

[Prototype]
public sealed partial class SurgerySpeciesPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public ProtoId<SurgeryAnatomyPrototype>? Limb;

    [DataField]
    public ProtoId<SurgeryAnatomyPrototype>? Torso;

    [DataField]
    public ProtoId<SurgeryAnatomyPrototype>? Head;

    [DataField]
    public ProtoId<SurgeryAnatomyPrototype>? Groin;

    [DataField]
    public ProtoId<SurgeryAnatomyPrototype>? Eyes;
}

[Prototype]
public sealed partial class SurgeryOrganPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Slot = string.Empty;

    [DataField]
    public string Zone = "Torso";

    [DataField]
    public bool Vital;

    [DataField]
    public List<SurgeryOrganSpot> Spots = new();
}

[DataDefinition]
public sealed partial class SurgeryOrganSpot
{
    [DataField(required: true)]
    public string State = string.Empty;

    [DataField]
    public int X;

    [DataField]
    public int Y;

    [DataField]
    public int W = 1;

    [DataField]
    public int H = 1;

    [DataField]
    public float Cx = 0.5f;

    [DataField]
    public float Cy = 0.5f;

    [DataField]
    public float Height = 0.2f;

    [DataField]
    public int HitX;

    [DataField]
    public int HitY;

    [DataField]
    public int HitW;

    [DataField]
    public int HitH;
}

[Prototype]
public sealed partial class SurgeryPlacePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public float Slip = 0.7f;

    [DataField]
    public float BoneFloor = 25f;

    [DataField]
    public float Jerk = 0.35f;
}

[Prototype]
public sealed partial class SurgeryOperationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<SurgeryStep> Steps = new();

    [DataField]
    public string Zone = "";

    [DataField]
    public string Name = "";

    [DataField]
    public List<SurgeryStep> Close = new();
}

[Prototype]
public sealed partial class SurgeryPartPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Zone = "";

    [DataField(required: true)]
    public string Anatomy = "";

    [DataField]
    public string Operation = "";

    [DataField]
    public string State = "";

    [DataField]
    public int Ax;

    [DataField]
    public int Ay;

    [DataField]
    public int Bx;

    [DataField]
    public int By;

    [DataField]
    public int Order;
}

[Prototype]
public sealed partial class SurgerySpeechPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Accent = "Mumble";
}

[Prototype]
public sealed partial class SurgeryLodgePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<string> Blocked = new();
}

[DataDefinition]
public sealed partial class SurgeryStep
{
    [DataField(required: true)]
    public SurgeryVerbKind Verb;

    [DataField(required: true)]
    public SurgeryLayerKind Layer;

    [DataField]
    public string Organ = "";

    [DataField]
    public string Game = "";

    [DataField]
    public string Speech = "";

    [DataField]
    public string Undo = "";
}
