// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Humanoid;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Surgery;

public enum SurgeryFilmMode : byte
{
    All,
    Bones,
    Organs,
    Foreign,
}

public enum SurgeryFilmKind : byte
{
    Line,
    Oval,
    Hole,
}

[DataDefinition]
public sealed partial class SurgeryFilmShape
{
    [DataField]
    public SurgeryFilmKind Kind = SurgeryFilmKind.Line;

    [DataField]
    public LimbZone Zone = LimbZone.Torso;

    [DataField]
    public float Ax;

    [DataField]
    public float Ay;

    [DataField]
    public float Bx;

    [DataField]
    public float By;

    [DataField]
    public float W = 1f;

    [DataField]
    public float H = 1f;

    [DataField]
    public Color? Color;

    [DataField]
    public bool Anchor;

    [DataField]
    public int Depth;

    [DataField]
    public string Sprite = "";

    [DataField]
    public string State = "";
}

[Prototype]
public sealed partial class SurgeryFilmPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public int Scale = 8;

    [DataField]
    public List<string> Species = new();

    [DataField]
    public Sex? Sex;

    [DataField]
    public bool GrayOrgans = true;

    [DataField]
    public string Skeleton = "";

    [DataField]
    public Dictionary<string, string> SkeletonStates = new();

    [DataField]
    public Color Bone = Color.FromHex("#e9eef2");

    [DataField]
    public Color Glow = Color.FromHex("#7fb2d6");

    [DataField]
    public Color Tint = Color.FromHex("#5f8fb0");

    [DataField]
    public Color Blood = Color.FromHex("#d0242e");

    [DataField]
    public Color Crack = Color.FromHex("#0b1620");

    [DataField]
    public Color Shard = Color.FromHex("#b8f4ff");

    [DataField]
    public Color Infection = Color.FromHex("#8fd14a");

    [DataField]
    public Color Missing = Color.FromHex("#9aa8b4");

    [DataField]
    public List<SurgeryFilmShape> Tissue = new();

    [DataField]
    public List<SurgeryFilmShape> Bones = new();

    [DataField]
    public List<SurgeryFilmShape> Items = new();
}
