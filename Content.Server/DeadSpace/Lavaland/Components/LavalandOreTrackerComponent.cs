using Content.Shared.Materials;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland.Components;

[RegisterComponent]
public sealed partial class LavalandOreTrackerComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<MaterialPrototype>, int> ProcessedMaterials = new();

    [DataField]
    public Dictionary<ProtoId<MaterialPrototype>, int> PendingProcessedMaterials = new();

    [DataField]
    public Dictionary<ProtoId<MaterialPrototype>, int> CreditedMaterials = new();

    [DataField]
    public Dictionary<ProtoId<MaterialPrototype>, int> PendingCreditedMaterials = new();
}
