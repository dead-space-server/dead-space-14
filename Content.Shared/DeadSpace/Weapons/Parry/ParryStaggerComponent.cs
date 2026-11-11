// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Weapons.Parry;

/// <summary>
/// A fast weapon (mantis blades, swinging several times a second) thrown off its stroke when parried: it cannot
/// strike again for a moment. Otherwise one parry stopped one swing of many.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ParryStaggerComponent : Component
{
    [DataField]
    public TimeSpan Stagger = TimeSpan.FromSeconds(1.2);
}
