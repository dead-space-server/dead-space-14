// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Audio;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.PdaArcade;

[Prototype]
public sealed partial class PdaArcadeProfilePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<SpriteSpecifier> Fruits = new();

    [DataField]
    public SoundSpecifier Start = default!;

    [DataField]
    public SoundSpecifier Action = default!;

    [DataField]
    public SoundSpecifier Win = default!;

    [DataField]
    public SoundSpecifier Lose = default!;

    [DataField]
    public ResPath Font;
}

[Prototype]
public sealed partial class PdaArcadeGamePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public LocId Name;

    [DataField]
    public int Order;

    [DataField]
    public bool Quiet;

    [DataField]
    public SpriteSpecifier? Floor;

    [DataField]
    public List<SpriteSpecifier> Floors = new();

    [DataField]
    public Dictionary<string, SpriteSpecifier> Sprites = new();

    [DataField]
    public Color? Field;

    [DataField]
    public Color? Well;

    [DataField]
    public Color? Settled;

    [DataField]
    public List<Color> Colors = new();
}
