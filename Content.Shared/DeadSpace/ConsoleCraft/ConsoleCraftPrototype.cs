// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.DeadSpace.ConsoleCraft;

[Prototype]
public sealed partial class ConsoleCraftPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public EntProtoId Item { get; private set; } = default!;

    [DataField]
    public float Time { get; private set; } = 5f;

    [DataField]
    public string Name { get; private set; } = string.Empty;

    [DataField]
    public string Description { get; private set; } = string.Empty;

    [DataField]
    public List<ConsoleCraftRequirement> RequestItems { get; private set; } = new();

    [DataField]
    public List<ProtoId<MinorItemModulePrototype>> MinorItems { get; private set; } = new();

    [DataField]
    public List<RandomRequestItemGroup> RandomRequestItems = new();
}

[DataDefinition]
public sealed partial class ConsoleCraftRequirement
{
    [DataField(required: true)]
    public EntProtoId ItemProto { get; private set; } = default!;

    [DataField]
    public int Amount { get; private set; } = 1;

    [DataField]
    public string Label { get; private set; } = string.Empty;
}

[DataDefinition]
public sealed partial class RandomRequestItemGroup
{
    [DataField(required: true)]
    public List<EntProtoId> Items = new();

    [DataField]
    public int Amount = 1;

    [DataField]
    public string Label = string.Empty;
}
