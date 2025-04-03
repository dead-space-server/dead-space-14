using Content.Server.DeadSpace.Armutant.Abilities.Fist;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.DeadSpace.Armutant.Abilities.Components.FistAbilities;

[RegisterComponent]
[Access(typeof(FistStunAroundSystem))]
public sealed partial class FistStunAroundComponent : Component
{
    [DataField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(2);

    [DataField]
    public EntProtoId AroundEffect = "FistStunAroundEffect";

    [DataField]
    public EntityUid? ActionFistStunAroundEntity;

    [DataField("actionToggleFistStunAround", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionToggleFistStunAround = "ActionToggleFistStunAround";

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_DeadSpace/Armutant/sound_effects_splat.ogg");
}
