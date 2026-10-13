// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Chemistry.Reagent;
using Content.Shared.DeadSpace.Pickles.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Pickles;

[Serializable, NetSerializable]
public enum PickleMethod : byte
{
    Vinegar = 0,
    Salt = 1,
    Alcohol = 2
}

[Prototype]
public sealed partial class PickleRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public EntProtoId Produce = default!;

    [DataField(required: true)]
    public PickleMethod Method;

    [DataField]
    public ProtoId<ReagentPrototype> RequiredReagent = "Vinegar";

    [DataField]
    public float MinReagent = 10f;

    [DataField]
    public float MinSugar;

    [DataField]
    public float DurationSeconds = 45f;

    [DataField]
    public int ProducePerJar = 3;

    [DataField]
    public bool LowBrine;

    [DataField]
    public string ContentsStyle = PickleJarComponent.DefaultContentsStyle;

    [DataField]
    public ProtoId<ReagentPrototype>? OutputDrinkReagent;

    [DataField]
    public Color BrineColor = Color.FromHex("#c4b48a");

    [DataField]
    public LocId PieceName = "pickle-piece-pickled";
}
