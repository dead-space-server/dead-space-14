using Content.Shared.Dragon;
using Content.Shared.EntityTable.EntitySelectors;
//DS14-start
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Random;
//DS14-end

namespace Content.Server.Dragon;

// TODO: replace accumulators with timespan logic
[RegisterComponent]
public sealed partial class DragonRiftComponent : SharedDragonRiftComponent
{
    /// <summary>
    /// Dragon that spawned this rift.
    /// </summary>
    [DataField]
    public EntityUid? Dragon;

    /// <summary>
    /// How long the rift has been active.
    /// </summary>
    [DataField]
    public float Accumulator = 0f;

    /// <summary>
    /// The maximum amount we can accumulate before becoming impervious.
    /// </summary>
    [DataField("maxAccumualator")] // load bearing typo...
    public float MaxAccumulator = 300f;

    /// <summary>
    /// Accumulation of the spawn timer.
    /// </summary>
    [DataField]
    public float SpawnAccumulator = 30f;

    /// <summary>
    /// How long it takes for a new spawn to be added.
    /// </summary>
    [DataField]
    public float SpawnCooldown = 30f;
//DS14-start
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public float Chance = 1f;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public List<EntProtoId> Prototypes = new();

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public float RareChance = 0f;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public List<EntProtoId> RarePrototypes = new();
//DS14-end

    /// <summary>
    /// The table of entities to consider for spawning. One will be picked every spawn.
    /// DS14: optional, our rifts use <see cref="Prototypes"/>/<see cref="RarePrototypes"/> instead.
    /// </summary>
    [DataField]
    public EntityTableSelector? Spawn;
}
