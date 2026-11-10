// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Husbandry.Core;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Husbandry.Growth;

/// <summary>
/// An animal that grows up through a number of stages. Handled by GrowthSystem and GrowthEffectsSystem.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class GrowthComponent : Component
{
    [DataField(required: true)]
    public List<GrowthStageDef> Stages = new();

    /// <summary>
    /// Id of the stage the animal starts in. The last stage if not set.
    /// </summary>
    [DataField]
    public string? InitialStage;

    /// <summary>
    /// What the animal turns into when butchered, stages decide how much of it.
    /// </summary>
    [DataField]
    public EntProtoId Meat = "FoodMeat";

    /// <summary>
    /// Saved with the animal: a map that is loaded does not run map init again.
    /// </summary>
    [DataField]
    public int CurrentStage = -1;

    /// <summary>
    /// When the current stage ends. Null when it is the last one.
    /// </summary>
    [DataField, AutoPausedField]
    public TimeSpan? StageEndTime;
}

/// <summary>
/// One stage of growth, see <see cref="GrowthComponent"/>.
/// </summary>
[DataDefinition]
public sealed partial class GrowthStageDef
{
    [DataField(required: true)]
    public string Id = string.Empty;

    /// <summary>
    /// How long the stage lasts. Null means it is the final one.
    /// </summary>
    [DataField]
    public TimeSpan? Duration;

    /// <summary>
    /// The name of the animal in this stage, by sex.
    /// </summary>
    [DataField]
    public Dictionary<AnimalSexKind, LocId> Names = new();

    [DataField]
    public Vector2 Scale = Vector2.One;

    /// <summary>
    /// How much damage the animal can take before it dies in this stage. Zero leaves it as it is.
    /// </summary>
    [DataField]
    public float MaxHealth;

    /// <summary>
    /// How many pieces of meat it gives when butchered.
    /// </summary>
    [DataField]
    public int MeatCount;

    /// <summary>
    /// What the animal is worth.
    /// </summary>
    [DataField]
    public double Price;

    /// <summary>
    /// The satiety at which the animal starts looking for food in this stage. Null leaves it as it is.
    /// </summary>
    [DataField]
    public float? SatietySeekBelow;

    /// <summary>
    /// How much satiety the animal loses per minute in this stage, so how much it has to eat. Null leaves it as it is.
    /// </summary>
    [DataField]
    public float? SatietyDecayPerMinute;

    /// <summary>
    /// Added to the animal when it enters the stage.
    /// </summary>
    [DataField]
    public ComponentRegistry Components = new();
}
