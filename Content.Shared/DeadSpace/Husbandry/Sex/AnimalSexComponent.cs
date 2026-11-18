// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Husbandry.Core;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Husbandry.Sex;

/// <summary>
/// The sex of an animal. Rolled on map init unless <see cref="Randomize"/> is turned off.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(AnimalSexSystem))]
public sealed partial class AnimalSexComponent : Component
{
    [DataField, AutoNetworkedField]
    public AnimalSexKind Sex = AnimalSexKind.Female;

    [DataField]
    public bool Randomize = true;

    /// <summary>
    /// Chance of rolling a male.
    /// </summary>
    [DataField]
    public float MaleChance = 0.5f;
}
