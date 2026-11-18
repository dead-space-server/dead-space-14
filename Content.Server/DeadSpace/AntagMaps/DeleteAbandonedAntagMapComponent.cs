using Robust.Shared.Map;

namespace Content.Server.DeadSpace.AntagMaps;

[RegisterComponent]
public sealed partial class DeleteAbandonedAntagMapComponent : Component
{
    public MapId? Map;

    public bool SeenPlayer;

    public TimeSpan? EmptySince;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromMinutes(3);
}
