// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Husbandry.Feeding;

/// <summary>
/// A place animals eat from: whatever lies in its container and fits their diet is food. The animals go to it as to food
/// on the floor, but it comes first (see FindFoodOperator). The container is not made here, a feeder is a storage.
/// </summary>
[RegisterComponent]
public sealed partial class AnimalFeederComponent : Component
{
    /// <summary>
    /// The container whose contents are food.
    /// </summary>
    [DataField]
    public string Container = "storagebase";
}
