using Robust.Shared.Map;

namespace Content.Server.DeadSpace.AntagMaps;

[RegisterComponent]
public sealed partial class PauseMapWhenEmptyComponent : Component
{
    public MapId? Map;
}
