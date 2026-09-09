namespace Content.Server.DeadSpace.AshWalkers;

[RegisterComponent, Access(typeof(AshWalkerSystem), typeof(AshWalkerNestSystem))]
public sealed partial class AshWalkerEggComponent : Component
{
    [DataField]
    public EntityUid? HomeMap;

    [DataField]
    public EntityUid? Nest;
}
