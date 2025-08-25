using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.UniformAccessories;

[RegisterComponent] [NetworkedComponent] [AutoGenerateComponentState(true)]
[Access(typeof(SharedUniformAccessorySystem))]
public sealed partial class UniformAccessoryHolderComponent : Component
{
    [DataField] [AutoNetworkedField]
    public List<string> AllowedCategories;

    [DataField] [AutoNetworkedField]
    public string ContainerId = "rmc_uniform_accessories";

    [DataField] [AutoNetworkedField]
    public List<EntProtoId>? StartingAccessories;
}
