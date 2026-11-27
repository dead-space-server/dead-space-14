using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.DeadSpace.Animals;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class AnimalChatterComponent : Component
{
    [DataField(required: true)]
    public ProtoId<LocalizedDatasetPrototype> Dataset;

    [DataField]
    public TimeSpan MinInterval = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan MaxInterval = TimeSpan.FromSeconds(55);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextChatter = TimeSpan.Zero;
}
