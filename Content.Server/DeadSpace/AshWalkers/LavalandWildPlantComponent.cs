using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.AshWalkers;

[RegisterComponent]
public sealed partial class LavalandWildPlantComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Plant;
}
