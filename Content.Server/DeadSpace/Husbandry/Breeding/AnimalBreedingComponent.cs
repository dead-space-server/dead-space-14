// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Storage;
using Content.Shared.Whitelist;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.DeadSpace.Husbandry.Breeding;

/// <summary>
/// An animal that breeds. A female that is grown up, fed and watered, with a grown male of her kind close by, gets
/// pregnant now and then, and gives birth after <see cref="GestationDuration"/>. The sex and the growth stages are
/// the ones of the husbandry module. Handled by AnimalBreedingSystem.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(AnimalBreedingSystem))]
public sealed partial class AnimalBreedingComponent : Component
{
    /// <summary>
    /// What the mother gives birth to, picked like any other spawn collection.
    /// </summary>
    [DataField(required: true)]
    public List<EntitySpawnEntry> Offspring = new();

    /// <summary>
    /// Who can be the father. Null: any grown male with <see cref="AnimalBreedingComponent"/> and the same
    /// <see cref="Species"/>.
    /// </summary>
    [DataField]
    public EntityWhitelist? PartnerWhitelist;

    /// <summary>
    /// Animals of one species breed with each other, others are not partners.
    /// </summary>
    [DataField]
    public string Species = string.Empty;

    /// <summary>
    /// How close the father has to be.
    /// </summary>
    [DataField]
    public float BreedRange = 4f;

    /// <summary>
    /// How many animals of the same species may be within <see cref="CrowdRange"/> of the mother before she stops.
    /// </summary>
    [DataField]
    public int Capacity = 8;

    [DataField]
    public float CrowdRange = 8f;

    /// <summary>
    /// The chance of a pregnancy on an attempt, for a pair that can breed.
    /// </summary>
    [DataField, Access(Other = AccessPermissions.ReadWriteExecute)]
    public float BreedChance = 0.35f;

    [DataField]
    public TimeSpan MinAttemptInterval = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan MaxAttemptInterval = TimeSpan.FromSeconds(120);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField, Access(Other = AccessPermissions.ReadWriteExecute)]
    public TimeSpan NextAttempt;

    [DataField, Access(Other = AccessPermissions.ReadWriteExecute)]
    public TimeSpan GestationDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Pause after a birth during which the mother does not get pregnant again.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How full, as a share of the maximum, the satiety and the hydration of both have to be.
    /// </summary>
    [DataField]
    public float MinSatiety = 0.6f;

    [DataField]
    public float MinHydration = 0.4f;

    /// <summary>
    /// Satiety that a pregnancy takes from the mother and from the father, in points.
    /// </summary>
    [DataField]
    public float SatietyCost = 25f;

    /// <summary>
    /// The growth stage from which an animal can breed (by id). Null: only the last stage.
    /// </summary>
    [DataField]
    public string? AdultStage;

    /// <summary>
    /// The growth stage that the babies are born in. Null: the first one.
    /// </summary>
    [DataField]
    public string? NewbornStage;

    [DataField]
    public bool Gestating;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? GestationEndTime;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextAllowed;

    [DataField]
    public LocId BirthPopup = "husbandry-birth-popup";
}
