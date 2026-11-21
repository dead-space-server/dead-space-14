using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.DeadSpace.Animals;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class PackCallComponent : Component
{
    [DataField(required: true)]
    public ProtoId<EmotePrototype> Emote;

    [DataField]
    public float Range = 6f;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan MinJoinDelay = TimeSpan.Zero;

    [DataField]
    public TimeSpan MaxJoinDelay = TimeSpan.Zero;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextCall = TimeSpan.Zero;
}
