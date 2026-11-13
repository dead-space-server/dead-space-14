using Content.Shared.Dragon;
//DS14-start
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Random;
//DS14-end
using Robust.Shared.Prototypes;

namespace Content.Server.Dragon;

[RegisterComponent]
public sealed partial class DragonRiftComponent : SharedDragonRiftComponent
{
    /// <summary>
    /// Dragon that spawned this rift.
    /// </summary>
    [DataField("dragon")] public EntityUid? Dragon;

    /// <summary>
    /// How long the rift has been active.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("accumulator")]
    public float Accumulator = 0f;

    /// <summary>
    /// The maximum amount we can accumulate before becoming impervious.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("maxAccumuluator")] public float MaxAccumulator = 300f;

    /// <summary>
    /// Accumulation of the spawn timer.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("spawnAccumulator")]
    public float SpawnAccumulator = 30f;

    /// <summary>
    /// How long it takes for a new spawn to be added.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("spawnCooldown")]
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
}
