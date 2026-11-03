using Robust.Shared.Prototypes;
using Content.Shared.Stacks;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent]
public sealed partial class NinjaSuitRefillComponent : Component
{
    [DataField]
    public List<ActionMaterialCost> ActionMaterials = new();
}

[DataDefinition]
public sealed partial class ActionMaterialCost
{
    [DataField(required: true)]
    public EntProtoId Action;

    [DataField(required: true)]
    public ProtoId<StackPrototype> Stack;

    [DataField(required: true)]
    public int Amount = 1;

    [DataField]
    public int Charges = 1;
}