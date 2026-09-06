namespace Content.Server.DeadSpace.AshWalkers;

[RegisterComponent, Access(typeof(AshWalkerSystem))]
public sealed partial class AshWalkerTribeMemberComponent : Component
{
    [DataField]
    public EntityUid? HomeMap;
}
