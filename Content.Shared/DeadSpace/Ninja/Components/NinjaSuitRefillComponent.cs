using Content.Shared.Materials;
using Robust.Shared.Prototypes;
using Content.Shared.Stacks;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent]
public sealed partial class NinjaSuitRefillComponent : Component
{
    [DataField]
    public Dictionary<EntProtoId, ActionMaterialCost> ActionMaterials = new();
}

[DataDefinition]
public sealed partial class ActionMaterialCost
{
    [DataField(required: true)]
    public ProtoId<StackPrototype> Stack = default!;

    [DataField(required: true)]
    public int Amount = 0;
}