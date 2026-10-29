// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland.Components;

/// <summary>
/// A boulder of the lavaland that is not one. Seen by a player, it may take a liking to them: then, whenever nobody
/// is looking, it creeps after them and is a boulder again the moment it is seen. It harms no one - until it is hit:
/// then it gets up and fights back. Broken, it dies and spills a handful of ore.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandRockMimicComponent : Component
{
    /// <summary>The chance that a player who sees it is followed.</summary>
    [DataField]
    public float FollowChance = 0.35f;

    /// <summary>How far it sees, and is seen.</summary>
    [DataField]
    public float SightRange = 10f;

    /// <summary>It creeps no closer than this to the one it follows.</summary>
    [DataField]
    public float FollowDistance = 2.5f;

    /// <summary>The one it follows got this far away: it gives up and stays a boulder where it is.</summary>
    [DataField]
    public float LoseRange = 25f;

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.25);

    /// <summary>How long it takes to get up out of the boulder.</summary>
    [DataField]
    public TimeSpan WakeTime = TimeSpan.FromSeconds(0.45);

    /// <summary>Put on it when it is hit: the brain that fights back.</summary>
    [DataField]
    public ComponentRegistry AngryComponents = new();

    /// <summary>
    /// Not hit for this long, and whoever hit it is dead or out of its sight: it calms down and is a boulder again.
    /// </summary>
    [DataField]
    public TimeSpan CalmDelay = TimeSpan.FromSeconds(20);

    /// <summary>Spilled when it dies: one kind of these, 1 to 6 of it.</summary>
    [DataField]
    public List<ProtoId<StackPrototype>> Ores = new();

    [DataField]
    public int OreMin = 1;

    [DataField]
    public int OreMax = 6;

    [ViewVariables]
    public LavalandRockMimicState State = LavalandRockMimicState.Rock;

    [ViewVariables]
    public TimeSpan NextUpdate;

    [ViewVariables]
    public TimeSpan WakeUntil;

    /// <summary>Someone saw it at the last check.</summary>
    [ViewVariables]
    public bool WasSeen;

    /// <summary>Whom it creeps after.</summary>
    [ViewVariables]
    public EntityUid? Following;

    [ViewVariables]
    public bool Angry;

    /// <summary>The last one who hit it.</summary>
    [ViewVariables]
    public EntityUid? Enemy;

    [ViewVariables]
    public TimeSpan LastHit;
}
