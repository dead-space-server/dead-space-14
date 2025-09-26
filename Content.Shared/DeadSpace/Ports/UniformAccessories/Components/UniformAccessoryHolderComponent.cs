using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Ports.UniformAccessories.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedUniformAccessorySystem))]
public sealed partial class UniformAccessoryHolderComponent : Component
{
    /// <summary>
    /// Categories of accessories allowed on this holder.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> AllowedCategories = new();

    /// <summary>
    /// The ID of the container for storing accessories.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string ContainerId = "rmc_uniform_accessories";

    /// <summary>
    /// List of accessory prototype IDs to spawn on initialization.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntProtoId>? StartingAccessories;
}
