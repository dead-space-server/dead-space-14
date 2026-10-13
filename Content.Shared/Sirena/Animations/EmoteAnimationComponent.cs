using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Sirena.Animations;

/// <summary>
/// </summary>
public sealed partial class EmoteFlipActionEvent : InstantActionEvent { };

/// <summary>
/// </summary>
public sealed partial class EmoteJumpActionEvent : InstantActionEvent { };

/// <summary>
/// </summary>
public sealed partial class EmoteTurnActionEvent : InstantActionEvent { };

public sealed partial class EmoteStopTailActionEvent : InstantActionEvent { };
public sealed partial class EmoteStartTailActionEvent : InstantActionEvent { };

[RegisterComponent]
[NetworkedComponent]
public sealed partial class EmoteAnimationComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public string AnimationId = "none";

    public int PlaySerial; //DS-14

    public EntityUid? FlipAction;
    public EntityUid? JumpAction;
    public EntityUid? TurnAction;
    public EntityUid? StopTailAction;
    public EntityUid? StartTailAction;

    [Serializable, NetSerializable]
    public sealed class EmoteAnimationComponentState : ComponentState
    {
        public string AnimationId { get; init; }

        public int PlaySerial { get; init; } //DS-14

        public EmoteAnimationComponentState(string animationId, int playSerial) //DS-14
        {
            AnimationId = animationId;
            PlaySerial = playSerial; //DS-14
        }
    }
}
