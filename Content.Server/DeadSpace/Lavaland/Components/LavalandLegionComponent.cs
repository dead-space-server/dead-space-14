using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland.Components;

[RegisterComponent]
public sealed partial class LavalandLegionComponent : Component
{
    [DataField]
    public int MaxActiveHeads = 2;

    public readonly HashSet<EntityUid> ActiveHeads = new();
}

[RegisterComponent]
public sealed partial class LavalandLegionHeadComponent : Component
{
    [DataField]
    public EntProtoId InfestPrototype = "MobLavalandLegion";

    [DataField]
    public float InfestRange = 1.1f;

    [DataField]
    public TimeSpan InfestCheckInterval = TimeSpan.FromSeconds(0.5);

    public TimeSpan NextInfestCheck;
    public EntityUid? Source;
}

/// <summary>
/// On a corpse a head has raised a legion from (it cannot be raised twice) and on that legion, which carries
/// the corpse inside and drops it once it dies.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandLegionInfestedComponent : Component
{
    public const string BodyContainerId = "legion_body";

    [ViewVariables]
    public EntityUid? Body;
}
