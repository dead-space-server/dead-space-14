using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.DeadSpace.AshWalkers;

[RegisterComponent, AutoGenerateComponentPause, Access(typeof(AshWalkerNestSystem))]
public sealed partial class AshWalkerIncubatingEggComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ReadyAt;
}
