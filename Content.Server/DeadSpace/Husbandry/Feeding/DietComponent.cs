// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Whitelist;

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
}
