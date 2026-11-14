// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Wizard.Runes;

public enum RuneKind : byte
{
    Form,
    Element,
    Modifier,
}

[Prototype("wizardRune")]
public sealed partial class RunePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId Description;

    [DataField(required: true)]
    public RuneKind Kind;

    [DataField(required: true)]
    public List<Vector2> Stroke = new();

    [DataField(required: true)]
    public LocId Syllable;

    [DataField]
    public Color Color = Color.White;

    [DataField]
    public float Strain = 5f;

    [DataField]
    public int Loudness;

    [DataField]
    public int Absurdity;

    [DataField]
    public float Power = 1f;

    [DataField]
    public int Extra;

    [DataField]
    public float Range = 1f;

    [DataField]
    public float Duration = 1f;

    [DataField]
    public float StrainScale = 1f;

    [DataField]
    public int Order;

    [DataField]
    public bool Fusion;

    [DataField]
    public bool Ultimate;

    [DataField]
    public bool Echo;

    [DataField]
    public float BloodPrice;

    [DataField]
    public ProtoId<RuneSurgePrototype>? Surge;

    [DataField]
    public ProtoId<RuneSurgePrototype>? FatalSurge;

    [DataField]
    public RuneTargeting Targeting = RuneTargeting.Point;

    [DataField]
    public float SpellRange = 10f;

    [DataField]
    public float SpellStrain = 20f;

    [DataField]
    public float SpellCooldown = 10f;

    [DataField]
    public int SpellLoudness = 1;

    [DataField]
    public RuneEffectTarget AutoTarget = RuneEffectTarget.AroundPoint;

    [DataField]
    public RuneSpawnPattern AutoPattern = RuneSpawnPattern.AtPoint;
}
