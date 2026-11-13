// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Chemistry.Reagent;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Husbandry.Feeding;

/// <summary>
/// What an animal looks for to eat. Only edible things are ever considered, this narrows them down. The eating itself
/// is the usual one: bites into the stomach, which digests them (see AnimalSatiateEffectSystem).
/// </summary>
[RegisterComponent]
[Access(typeof(AnimalFeedingSystem))]
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
    /// How much goes into the stomach with one sip from a container that does not say (a bucket, a bowl); a
    /// <see cref="WaterSourceComponent"/> says it itself.
    /// </summary>
    [DataField]
    public float SipAmount = 10f;

    /// <summary>
    /// How long a sip from such a container takes.
    /// </summary>
    [DataField]
    public TimeSpan DrinkTime = TimeSpan.FromSeconds(2);

    /// <summary>
    /// What the animal drinks. A container is a drink only if everything that is in it is in this list: an animal does
    /// not drink blood, acid or a medicine out of a bucket, and does not drink a mixture with any of them.
    /// </summary>
    [DataField]
    public List<ProtoId<ReagentPrototype>> DrinkReagents = new() { "Water" };

    /// <summary>
    /// The thing the animal is eating now, so that it goes on with it bite after bite and does not start a new one.
    /// </summary>
    [ViewVariables]
    public EntityUid? Eating;

    /// <summary>
    /// How many bites (sips) the farm gives the animal in one cycle while nobody watches it.
    /// </summary>
    [DataField]
    public int OfflineBites = 4;
}
