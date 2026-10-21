// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Movement.Components;

/// <summary>
/// SS13-style movement: the mob walks from tile center to tile center, one tile per step, and stops on a center.
/// Handled by SharedMoverController (TileStep part).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TileStepMovementComponent : Component
{
    /// <summary>A step to <see cref="Target"/> is under way.</summary>
    [AutoNetworkedField]
    public bool Stepping;

    /// <summary>Center of the tile we walk to, in the coordinates of <see cref="Parent"/>.</summary>
    [AutoNetworkedField]
    public Vector2 Target;

    /// <summary>The grid (or map) the step was planned on; another parent means the step is void.</summary>
    [AutoNetworkedField]
    public EntityUid? Parent;

    /// <summary>Center of the tile the step started from: turning back mid-step goes straight back there.</summary>
    [AutoNetworkedField]
    public Vector2 From;

    /// <summary>
    /// When we last got noticeably closer to the target: no progress for a while means something in the way.
    /// Networked with the step, so the client and the server give up on the same tick.
    /// </summary>
    [AutoNetworkedField]
    public TimeSpan LastProgress;

    /// <summary>Distance left to the target on the last check.</summary>
    [AutoNetworkedField]
    public float LastDistance;

    /// <summary>Give up a step after this long without getting closer.</summary>
    [DataField]
    public TimeSpan StuckTime = TimeSpan.FromSeconds(0.25);
}
