// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.Map;

namespace Content.Shared.DeadSpace.Husbandry.Walking;

/// <summary>
/// Walks an animal along a route: straight lines from one waypoint to the next at a walk (or a run), with the input set
/// once per tick in the direction of the waypoint. This replaces the usual steering of the AI, which picks the
/// direction anew from 16 every tick and makes animals twitch against walls. Whoever wants the animal somewhere (the
/// wandering, the way to food and water) hands it a route and reads the status. Handled by AnimalWalkerSystem.
/// </summary>
[RegisterComponent]
public sealed partial class AnimalWalkerComponent : Component
{
    /// <summary>
    /// How close it has to get to a waypoint on the way to go on to the next one.
    /// </summary>
    [DataField]
    public float WaypointTolerance = 0.4f;

    /// <summary>
    /// It slows down for the last tiles before the end so it does not stop with a jerk.
    /// </summary>
    [DataField]
    public float SlowDistance = 1.5f;

    /// <summary>
    /// The slowest it walks while coming to a stop, as a part of the walking speed.
    /// </summary>
    [DataField]
    public float MinSpeedFraction = 0.35f;

    /// <summary>
    /// Half of the width of the strip that has to be free on the way, about the radius of the animal.
    /// </summary>
    [DataField]
    public float Clearance = 0.45f;

    /// <summary>
    /// It gives up if it moved less than <see cref="StuckDistance"/> for this long.
    /// </summary>
    [DataField]
    public TimeSpan StuckTime = TimeSpan.FromSeconds(1.5);

    [DataField]
    public float StuckDistance = 0.2f;

    /// <summary>
    /// It gives up a route that takes longer than this.
    /// </summary>
    [DataField]
    public TimeSpan MaxRouteTime = TimeSpan.FromSeconds(90);

    /// <summary>
    /// How far a waypoint of a smoothed route is looked for ahead of the current one. A route with many corners is
    /// cut short wherever a straight strip is free, this is how many waypoints are tried.
    /// </summary>
    [DataField]
    public int SmoothLookahead = 15;

    [ViewVariables]
    public WalkStatus Status;

    /// <summary>
    /// The waypoints that are left. The last one is where it is going.
    /// </summary>
    [ViewVariables]
    public Queue<EntityCoordinates> Route = new();

    /// <summary>
    /// The last waypoint of the route, kept apart so that every tick need not go through the queue for it.
    /// </summary>
    [ViewVariables]
    public EntityCoordinates Destination;

    /// <summary>
    /// How close to the last waypoint counts as being there.
    /// </summary>
    [ViewVariables]
    public float ArriveDistance;

    [ViewVariables]
    public bool Run;

    [ViewVariables]
    public TimeSpan StartTime;

    /// <summary>
    /// Where and when it last made progress, to notice that it is stuck.
    /// </summary>
    [ViewVariables]
    public TimeSpan ProgressTime;

    [ViewVariables]
    public Vector2 ProgressPosition;
}

public enum WalkStatus : byte
{
    /// <summary>
    /// Not walking anywhere.
    /// </summary>
    Idle,

    Walking,

    /// <summary>
    /// Got to the end of the route. Stays like this until the route is taken over or stopped.
    /// </summary>
    Arrived,

    /// <summary>
    /// Could not get to the end: stuck, took too long, dragged away or the target is gone.
    /// </summary>
    Failed,
}
