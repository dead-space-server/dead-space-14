using Robust.Shared.Player;

namespace Content.Server.Ghost.Roles.Events;

public sealed class GhostRoleAvailabilityEvent(ICommonSession? player = null) : CancellableEntityEventArgs
{
    public readonly ICommonSession? Player = player;
}
