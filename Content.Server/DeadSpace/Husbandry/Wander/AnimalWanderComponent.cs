// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Server.DeadSpace.Husbandry.Wander;

/// <summary>
/// An animal that stands around and now and then goes for a stroll: a few walks in a row, each one a straight line in
/// the open that turns a bit from the last one, with a short stop in between. The walking itself is done by the
/// <c>AnimalWalker</c> of the animal. Handled by AnimalWanderSystem, started by the <c>AnimalWanderOperator</c> of the
/// brain.
/// </summary>
[RegisterComponent]
[Access(typeof(AnimalWanderSystem))]
public sealed partial class AnimalWanderComponent : Component
{
    /// <summary>
    /// The chance that the animal goes for a stroll whenever it is idle, otherwise it keeps standing.
    /// </summary>
    [DataField]
    public float WalkChance = 0.85f;

    /// <summary>
    /// How long it stands around after a stroll, in seconds (min, max).
    /// </summary>
    [DataField]
    public Vector2 StandTime = new(4f, 12f);

    /// <summary>
    /// How long it waits before it looks for room again when there is none, in seconds (min, max).
    /// </summary>
    [DataField]
    public Vector2 RetryTime = new(2f, 5f);

    /// <summary>
    /// How many legs a stroll has (min, max, both included). A leg is one straight walk.
    /// </summary>
    [DataField]
    public Vector2i Legs = new(3, 6);

    /// <summary>
    /// How far each leg is meant to be, in tiles. A leg is shorter where there is less room.
    /// </summary>
    [DataField]
    public float MinDistance = 6f;

    [DataField]
    public float MaxDistance = 16f;

    /// <summary>
    /// The shortest leg worth walking, in tiles. With less room than this in a direction it is not a place to walk to.
    /// </summary>
    [DataField]
    public float MinLegDistance = 2f;

    /// <summary>
    /// How far, in degrees, a leg can turn away from the one before it. Small values are a stroll in a line,
    /// large ones are a stroll in circles.
    /// </summary>
    [DataField]
    public float TurnAngle = 70f;

    /// <summary>
    /// How long it stops between two legs, in seconds (min, max).
    /// </summary>
    [DataField]
    public Vector2 LegPause = new(0.5f, 2.5f);

    /// <summary>
    /// How many random directions are looked at to find a place to walk to.
    /// </summary>
    [DataField]
    public int Probes = 10;

    /// <summary>
    /// How many of the <see cref="Probes"/> have to be good for the animal to start a stroll at all. With less it is
    /// cramped (a stall, a corner) and it just stands. The next legs only need one place.
    /// </summary>
    [DataField]
    public int MinClearProbes = 3;

    /// <summary>
    /// How close it has to get to the end of a leg.
    /// </summary>
    [DataField]
    public float ArriveDistance = 0.3f;

    /// <summary>
    /// How much room is kept between the end of a leg and what stops it, in tiles.
    /// </summary>
    [DataField]
    public float LegMargin = 0.2f;

    /// <summary>
    /// A social stroll gives up on a subject that is farther than <see cref="SocialRange"/> plus this.
    /// </summary>
    [DataField]
    public float SocialRangeSlack = 6f;

    /// <summary>
    /// A leg of a social stroll shorter than this is not made: the animal is where it wants to be.
    /// </summary>
    [DataField]
    public float MinSocialStep = 0.6f;

    /// <summary>
    /// It gives up a stroll that takes longer than this.
    /// </summary>
    [DataField]
    public TimeSpan MaxWalkTime = TimeSpan.FromSeconds(120);

    /// <summary>
    /// How many legs are left, the current one included. Zero when there is no stroll.
    /// </summary>
    [ViewVariables]
    public int LegsLeft;

    /// <summary>
    /// The direction of the last leg, the next one turns away from it by at most <see cref="TurnAngle"/>.
    /// </summary>
    [ViewVariables]
    public Vector2 Heading;

    /// <summary>
    /// When the stop between two legs ends. Empty when it is not stopping.
    /// </summary>
    [ViewVariables]
    public TimeSpan? PauseUntil;

    /// <summary>
    /// When the stroll began.
    /// </summary>
    [ViewVariables]
    public TimeSpan WalkStart;

    // Social strolls: instead of a random place the animal walks round something. Each stroll rolls one of them, the
    // rest is the plain roam. A stroll whose subject is not there (no player, no female, no feeder) is a roam.

    /// <summary>
    /// Chance of a stroll round the nearest player: the animal circles him at <see cref="OrbitRadius"/> tiles.
    /// </summary>
    [DataField]
    public float OrbitChance = 0.15f;

    /// <summary>
    /// Chance, for a male, of a stroll after the nearest female of its kind.
    /// </summary>
    [DataField]
    public float FollowMateChance = 0.3f;

    /// <summary>
    /// Chance of a stroll round a feeder or a water source that is near.
    /// </summary>
    [DataField]
    public float HangoutChance = 0.2f;

    /// <summary>
    /// How far the subject of a social stroll may be, in tiles.
    /// </summary>
    /// <summary>
    /// Chance of a stroll after a random animal of the same kind that is near (the herd).
    /// </summary>
    [DataField]
    public float HerdChance = 0.25f;

    [DataField]
    public float SocialRange = 12f;

    [DataField]
    public Vector2 OrbitRadius = new(2f, 4f);

    /// <summary>
    /// How close to the subject of a social stroll each leg ends (a mate, a feeder).
    /// </summary>
    [DataField]
    public Vector2 CompanionDistance = new(1.5f, 3f);

    [ViewVariables]
    public WanderMode Mode;

    [ViewVariables]
    public EntityUid? Subject;

    /// <summary>
    /// Where on the circle round the subject the last leg of an orbit ended, in radians.
    /// </summary>
    [ViewVariables]
    public float OrbitAngle;
}

public enum WanderMode : byte
{
    Roam,
    OrbitPlayer,
    FollowMate,
    Hangout,
    FollowHerd,
}
