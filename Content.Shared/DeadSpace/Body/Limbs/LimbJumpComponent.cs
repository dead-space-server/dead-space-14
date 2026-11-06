// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Movement.Components;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Body.Limbs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LimbJumpComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Distance = 1.5f;

    [DataField, AutoNetworkedField]
    public float Speed = 5f;

    [DataField, AutoNetworkedField]
    public bool CanCollide;

    [DataField, AutoNetworkedField]
    public EntProtoId Action = "ActionGravityJump";

    [DataField, AutoNetworkedField]
    public SoundSpecifier? Sound;
}
