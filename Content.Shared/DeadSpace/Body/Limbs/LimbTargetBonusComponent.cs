// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Body.Limbs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LimbTargetBonusComponent : Component
{
    [DataField, AutoNetworkedField]
    public float PassiveChance = 0.5f;

    [DataField, AutoNetworkedField]
    public float ActiveChance = 1f;
}
