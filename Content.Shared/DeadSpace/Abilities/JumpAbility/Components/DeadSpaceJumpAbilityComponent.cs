// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Serialization;
using Robust.Shared.Map;

namespace Content.Shared.DeadSpace.Abilities.JumpAbility.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class DeadSpaceJumpAbilityComponent : Component
{
    [DataField]
    [ViewVariables(VVAccess.ReadOnly)]
    public EntProtoId ActionJump = "ActionJump";

    [DataField]
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? ActionJumpEntity;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public EntityCoordinates? Target = null;

    [DataField]
    [ViewVariables(VVAccess.ReadOnly)]
    public float Strenght = 3f;

    [DataField]
    [ViewVariables(VVAccess.ReadOnly)]
    public float JumpDuration = 1f;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool IsJumps = false;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan TimeUntilEndJump = TimeSpan.Zero;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan TimeUntilNextJump = TimeSpan.Zero;

    [DataField]
    [ViewVariables(VVAccess.ReadOnly)]
    public float Interval = 0.01f;

    [DataField]
    public SoundSpecifier? JumpSound = null;
}

[Serializable, NetSerializable]
public sealed class JumpAnimationComponentState : ComponentState
{
    public JumpAnimationComponentState()
    {
    }
}
