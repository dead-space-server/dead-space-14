// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Whitelist;

namespace Content.Shared.DeadSpace.Husbandry.Feeding;

/// <summary>
/// What an animal eats. Only entities with a <c>Food</c> component are ever considered, this narrows them down.
/// Handled by AnimalFeedingSystem.
/// </summary>
[RegisterComponent]
public sealed partial class DietComponent : Component
{
    /// <summary>
    /// Food has to pass this. Null means everything.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Food must not pass this.
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;

    /// <summary>
    /// The solution of the food that is counted as its nutrition.
    /// </summary>
    [DataField]
    public string Solution = "food";

    /// <summary>
    /// How much satiety a unit of the food solution is worth.
    /// </summary>
    [DataField]
    public float NutritionPerUnit = 1f;

    [DataField]
    public TimeSpan EatDelay = TimeSpan.FromSeconds(2);
}
