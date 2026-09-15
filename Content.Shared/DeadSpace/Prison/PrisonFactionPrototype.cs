using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Prison;

[Prototype]
public sealed partial class PrisonFactionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Feature;

    [DataField(required: true)]
    public ProtoId<StartingGearPrototype> Gear;

    [DataField]
    public Color Color = Color.White;

    [DataField]
    public int Order;
}
