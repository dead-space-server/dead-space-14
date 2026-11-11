// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Whitelist;

namespace Content.Shared.DeadSpace.Husbandry.Feeding;

/// <summary>
/// What an animal looks for to eat. Only edible things are ever considered, this narrows them down. The eating itself
/// is the usual one: bites into the stomach, which digests them (see AnimalSatiateEffectSystem).
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
}
