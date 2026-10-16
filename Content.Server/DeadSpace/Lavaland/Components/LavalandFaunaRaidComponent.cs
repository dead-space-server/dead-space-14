// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Server.DeadSpace.Lavaland.Components;

public enum LavalandFaunaRaidStage : byte
{
    Idle,
    Warning,
    Active,
}

/// <summary>Raid state of one lavaland map.</summary>
[RegisterComponent]
public sealed partial class LavalandFaunaRaidComponent : Component
{
    [ViewVariables]
    public LavalandFaunaRaidStage Stage;

    /// <summary>Chance of the next roll.</summary>
    [ViewVariables]
    public float Chance;

    [ViewVariables]
    public TimeSpan NextCheck;

    public TimeSpan StageEnd;
    public TimeSpan NextTimerUpdate;

    /// <summary>When it is checked whether anybody came to defend the outpost; null once checked.</summary>
    public TimeSpan? DefenseCheckAt;
    public EntityUid? MusicStream;
    public readonly List<EntityUid> Entities = new();
}

/// <summary>
/// Fauna of a raid. Its own AI fights whoever it can see; when nobody is in sight (players inside the outpost, which
/// its pathfinding can't reach across grids) it walks straight at the nearest player and beats down what is in the way.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandRaiderComponent : Component
{
    /// <summary>Walking straight at the target, its own AI off.</summary>
    [ViewVariables]
    public bool Assaulting;

    [ViewVariables]
    public EntityUid? Target;

    /// <summary>
    /// The one goliath of the raid that keeps its distance and lashes out with its tentacles while the others
    /// beat down the walls.
    /// </summary>
    [ViewVariables]
    public bool Tentacler;

    public TimeSpan NextThink;
}

/// <summary>A tendril raised by a raid: it starts spawning at once and its fauna storm the outpost.</summary>
[RegisterComponent]
public sealed partial class LavalandRaidTendrilComponent : Component
{
    [ViewVariables]
    public EntityUid Map;
}
