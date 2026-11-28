// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Weapons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SemiAutoFireRateComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ActiveMultiplier = 1.2f;

    [DataField, AutoNetworkedField]
    public bool OnlyWhileActive = true;
}
