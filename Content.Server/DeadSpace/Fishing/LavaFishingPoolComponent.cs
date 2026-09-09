namespace Content.Server.DeadSpace.Fishing;

[RegisterComponent]
public sealed partial class LavaFishingPoolComponent : Component
{
    public readonly Dictionary<Vector2i, LavaFishingStock> Areas = new();
}

public sealed class LavaFishingStock
{
    public int Available = 6;
    public TimeSpan UpdatedAt;
}
