// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland.Components;

/// <summary>A marker in the flowers of lavaland: it lays an ordinary bear trap of the Circle there, armed, and is gone.</summary>
[RegisterComponent]
public sealed partial class LavalandGeistTrapSpawnerComponent : Component
{
    [DataField]
    public EntProtoId Trap = "BearTrap";
}

/// <summary>
/// Invisible trigger: a player who walks into it glimpses a geist of the Circle somewhere at the edge of his sight.
/// Coming closer, the geist runs off at speed and is gone.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandGeistWatchTriggerComponent : Component
{
    [DataField]
    public float TriggerRadius = 4f;

    [DataField]
    public EntProtoId Phantom = "LavalandGeistPhantom";

    /// <summary>Half size of what a player sees around himself: the geist appears near its edge.</summary>
    [DataField]
    public Vector2 ViewHalfSize = new(11f, 8f);

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(10);

    [ViewVariables]
    public TimeSpan NextTrigger;
}

/// <summary>
/// Invisible trigger: half of a geist of the Circle rises out of a hole by the player, marks the tiles around him and
/// throws bear traps onto them, then sinks away. A shot while it is aiming drops it back into the hole, traps unthrown.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandGeistAmbushTriggerComponent : Component
{
    [DataField]
    public float TriggerRadius = 1.5f;

    [DataField]
    public EntProtoId Phantom = "LavalandGeistPhantom";

    [DataField]
    public EntProtoId Trap = "BearTrap";

    /// <summary>It climbs out of a chasm this close to the player.</summary>
    [DataField]
    public float ChasmRange = 7f;

    [DataField]
    public int TrapCount = 7;

    /// <summary>The traps land on tiles this far around the player.</summary>
    [DataField]
    public int TrapRadius = 3;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(10);

    [ViewVariables]
    public TimeSpan NextTrigger;
}

public enum LavalandGeistStage : byte
{
    /// <summary>Standing at the edge of sight, watching.</summary>
    Watching,
    /// <summary>Running away from the player.</summary>
    Fleeing,
    /// <summary>Climbing half out of its hole.</summary>
    Rising,
    /// <summary>Picking the tiles to throw its traps on.</summary>
    Aiming,
    /// <summary>Sinking back into the hole.</summary>
    Sinking,
    /// <summary>Gone from sight already, deleted a moment later.</summary>
    Vanishing,
}

/// <summary>A geist of the Circle in lavaland: nothing can harm it.</summary>
[RegisterComponent]
public sealed partial class LavalandGeistPhantomComponent : Component
{
    [ViewVariables]
    public LavalandGeistStage Stage;

    [ViewVariables]
    public TimeSpan StageEnd;

    /// <summary>The player it watches, runs from, or ambushes.</summary>
    [ViewVariables]
    public EntityUid? Player;

    /// <summary>Coming this close makes it run.</summary>
    [DataField]
    public float FleeDistance = 9f;

    /// <summary>How long it runs before it is gone.</summary>
    [DataField]
    public TimeSpan FleeTime = TimeSpan.FromSeconds(4);

    /// <summary>A watcher nobody comes near is gone after this long anyway.</summary>
    [DataField]
    public TimeSpan WatchTime = TimeSpan.FromSeconds(40);

    [DataField]
    public TimeSpan RiseTime = TimeSpan.FromSeconds(1.2);

    [DataField]
    public TimeSpan AimTime = TimeSpan.FromSeconds(1.6);

    [DataField]
    public TimeSpan SinkTime = TimeSpan.FromSeconds(0.9);

    /// <summary>Ambush: where its traps go, and the trigger's settings.</summary>
    [ViewVariables]
    public readonly List<MapCoordinates> TrapSpots = new();

    [ViewVariables]
    public readonly List<EntityUid> Marks = new();

    [ViewVariables]
    public EntProtoId Mark = "LavalandLegionRockMark";

    [ViewVariables]
    public int TrapCount = 7;

    [ViewVariables]
    public int TrapRadius = 3;

    [ViewVariables]
    public EntProtoId Trap = "BearTrap";

    [ViewVariables]
    public bool Ambusher;

    /// <summary>The hole it rises from and the dust over it (the ambusher).</summary>
    [ViewVariables]
    public readonly List<EntityUid> HoleParts = new();

    /// <summary>Fleeing: where it was on the last check - not getting anywhere means a wall in its way.</summary>
    [ViewVariables]
    public Vector2 LastPosition;

    [ViewVariables]
    public TimeSpan NextStuckCheck;
}
