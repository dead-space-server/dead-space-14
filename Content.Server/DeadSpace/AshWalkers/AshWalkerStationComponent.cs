namespace Content.Server.DeadSpace.AshWalkers;

[RegisterComponent]
public sealed partial class AshWalkerStationComponent : Component
{
    [DataField]
    public EntityUid? Map;
}
