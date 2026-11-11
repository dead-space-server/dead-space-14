// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Husbandry.Feeding;

/// <summary>
/// A container that animals prefer to drink from, or one that has to be told how: any open container with something
/// to drink in it is a source anyway (see AnimalFeedingSystem), this only changes the size of a sip and how long
/// it takes. A trough has it.
/// </summary>
[RegisterComponent]
public sealed partial class WaterSourceComponent : Component
{
    /// <summary>
    /// The solution to drink from. Empty: the drainable solution of the container.
    /// </summary>
    [DataField]
    public string? Solution;

    /// <summary>
    /// How much goes into the stomach with one drink, at most.
    /// </summary>
    [DataField]
    public float AmountPerDrink = 15f;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(2);
}
