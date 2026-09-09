using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Fishing;

[RegisterComponent]
public sealed partial class LavaFishingSessionComponent : Component
{
    public EntityUid Grid;
    public Vector2i Area;
    public EntProtoId Reward;
}
