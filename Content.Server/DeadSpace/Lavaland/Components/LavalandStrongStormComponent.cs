// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Server.DeadSpace.Lavaland.Components;

public enum LavalandStrongStormStage : byte
{
    Idle,
    Warning,
    Active,
}

/// <summary>A lavaland's strong storm: when the next roll is, its chance now and where it stands.</summary>
[RegisterComponent]
public sealed partial class LavalandStrongStormComponent : Component
{
    [ViewVariables]
    public LavalandStrongStormStage Stage = LavalandStrongStormStage.Idle;

    [ViewVariables]
    public TimeSpan StageEnd;

    [ViewVariables]
    public TimeSpan NextDamage;

    [ViewVariables]
    public TimeSpan NextThrow;

    /// <summary>The storm already brought its boss this round.</summary>
    [ViewVariables]
    public bool BossBrought;
}
