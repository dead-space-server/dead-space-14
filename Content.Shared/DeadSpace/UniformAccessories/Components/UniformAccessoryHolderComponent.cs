using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.UniformAccessories.Components;

[RegisterComponent] [NetworkedComponent] [AutoGenerateComponentState(true)]
[Access(typeof(SharedUniformAccessorySystem))]
public sealed partial class UniformAccessoryHolderComponent : Component
{
    /// <summary>
    /// The container for storing accessories.
    /// </summary>
    [ViewVariables]
    public Container? AccessoryContainer;

    /// <summary>
    /// Categories of accessories allowed on this holder.
    /// </summary>
    [DataField] [AutoNetworkedField]
    public List<string> AllowedCategories = new();
}
