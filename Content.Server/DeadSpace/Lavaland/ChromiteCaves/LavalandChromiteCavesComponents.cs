// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.Lavaland.ChromiteCaves;

/// <summary>On a lavaland map: its chromite caves and the pit leading down.</summary>
[RegisterComponent]
public sealed partial class LavalandChromiteCavesLinkComponent : Component
{
    [ViewVariables]
    public EntityUid? Caves;

    [ViewVariables]
    public MapCoordinates PitCenter;

    [ViewVariables]
    public readonly List<EntityUid> PitTiles = new();

    /// <summary>Who is being asked whether to jump, and when a declined question may come back.</summary>
    [ViewVariables]
    public readonly HashSet<ICommonSession> OpenPrompts = new();

    [ViewVariables]
    public readonly Dictionary<ICommonSession, TimeSpan> PromptCooldowns = new();

    /// <summary>The open question windows, closed when somebody falls in before answering.</summary>
    [ViewVariables]
    public readonly Dictionary<ICommonSession, LavalandCavePitEui> PromptWindows = new();

    [ViewVariables]
    public TimeSpan NextPromptCheck;
}

public enum LavalandCaveTravelStage : byte
{
    Jump,
    Fall,
    Hop,
    Rope,
    FlyUp,
    Hanging,
}

/// <summary>Server timers of a player going down into the caves or climbing out.</summary>
[RegisterComponent]
public sealed partial class LavalandCaveTravelComponent : Component
{
    [ViewVariables]
    public LavalandCaveTravelStage Stage;

    [ViewVariables]
    public EntityUid Caves;

    [ViewVariables]
    public TimeSpan NextStep;
}

/// <summary>On a caves map: who is being asked to climb out, and when a declined question may come back.</summary>
[RegisterComponent]
public sealed partial class LavalandCaveExitPromptsComponent : Component
{
    [ViewVariables]
    public readonly HashSet<ICommonSession> Open = new();

    [ViewVariables]
    public readonly Dictionary<ICommonSession, TimeSpan> Cooldowns = new();

    [ViewVariables]
    public TimeSpan NextCheck;
}
