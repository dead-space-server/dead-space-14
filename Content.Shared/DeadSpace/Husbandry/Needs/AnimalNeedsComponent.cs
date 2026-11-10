// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Husbandry.Needs;

/// <summary>
/// Satiety and hydration of an animal. Both drain at a constant rate and hurt the animal once they are empty.
/// Handled by AnimalNeedsSystem.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AnimalNeedsComponent : Component
{
    [DataField]
    public NeedState Satiety = new() { DecayPerMinute = 2f };

    [DataField]
    public NeedState Hydration = new() { DecayPerMinute = 1f };

    /// <summary>
    /// What clients see, updated only when the level changes.
    /// </summary>
    [DataField, AutoNetworkedField]
    public NeedLevel SatietyLevel;

    [DataField, AutoNetworkedField]
    public NeedLevel HydrationLevel;

    /// <summary>
    /// How often the needs are processed.
    /// </summary>
    [DataField]
    public TimeSpan UpdateRate = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How often the deprivation damage is applied.
    /// </summary>
    [DataField]
    public TimeSpan DamageInterval = TimeSpan.FromSeconds(10);

    [DataField, AutoPausedField]
    public TimeSpan NextUpdate;

    [DataField, AutoPausedField]
    public TimeSpan NextDamage;

    /// <summary>
    /// Until when a search for food (water) that found nothing is not repeated, see the cvar husbandry.search_cooldown.
    /// Not saved.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextFoodSearch;

    [ViewVariables]
    public TimeSpan NextWaterSearch;
}
