using Robust.Shared.GameStates;
using static Content.Shared.Sirena.Animations.EmoteAnimationComponent;
using Robust.Shared.Prototypes;

namespace Content.Shared.Sirena.Animations;

public abstract partial class SharedEmoteAnimationSystem : EntitySystem
{
    public const string EmoteFlipActionPrototype = "EmoteFlip";
    public const string EmoteJumpActionPrototype = "EmoteJump";
    public const string EmoteTurnActionPrototype = "EmoteTurn";
    public const string EmoteStopTailActionPrototype = "EmoteStopTail";
    public const string EmoteStartTailActionPrototype = "EmoteStartTail";

    [Dependency] public SharedAppearanceSystem _appearance = default!;
    [Dependency] public IPrototypeManager _proto = default!;

    public override void Initialize()
    {
    }
}
