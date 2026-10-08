using Content.Shared.DeadSpace.Ninja.Components;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Ninja.Prototypes;

[Prototype("SpiderOS")]
public sealed partial class SpiderOSPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<NinjaSkill> AllSkills = new();
}

[DataRecord]
public partial record struct NinjaSkill()
{
    [DataField]
    public NinjaSkillsCategory Category;

    [DataField]
    public int Tier;

    [DataField]
    public LocId Name = string.Empty;

    [DataField]
    public LocId Description = string.Empty;

    [DataField]
    public SpriteSpecifier Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_DeadSpace/Actions/ninja_actions.rsi"), "nullaction");

    [DataField]
    public bool TransferOnSecondChance = true;

    [DataField]
    public ComponentRegistry Components = new();

    [DataField]
    public List<EntProtoId>? Actions;
}