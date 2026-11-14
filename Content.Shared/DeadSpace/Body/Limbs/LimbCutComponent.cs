// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Body.Limbs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LimbCutComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Factor = 1f;

    [DataField, AutoNetworkedField]
    public bool Sever;

    [DataField, AutoNetworkedField]
    public float SeverChance = 1f;
}
