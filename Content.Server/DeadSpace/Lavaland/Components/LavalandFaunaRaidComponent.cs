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
    public EntityUid? MusicStream;
    public readonly List<EntityUid> Entities = new();
}
