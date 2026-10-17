// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland.Components;

[RegisterComponent]
public sealed partial class LavalandMorkiteMissionComponent : Component
{
    [DataField] public EntProtoId ExtractorPrototype = "LavalandMorkiteExtractor";
    [DataField] public int ExtractorCount = 4;
    [DataField] public float SpawnDistance = 40f;
    [DataField] public float MorkiteChance = 0.5f;
    [DataField] public EntProtoId OreObjective = "LavalandObjectivePaperOre";
    [DataField] public EntProtoId MorkiteObjective = "LavalandObjectivePaperMorkite";
    public bool IsMorkite;
    public bool Spawned;
}

/// <summary>
/// Morkite extractor. Powers every machine on its pipe network and fills the morkite bar
/// of a bluespace well whose tile holds a pipe device of the same network.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandMorkiteExtractorComponent : Component
{
    /// <summary>Time for a single connected extractor to fill a well's morkite bar to 100%.</summary>
    [DataField] public TimeSpan RequiredExtractionTime = TimeSpan.FromMinutes(8);

    /// <summary>Fauna this close to the extractor's pipeline leaves to chew on it.</summary>
    [DataField] public float FaunaAttractRange = 10f;

    /// <summary>Fauna ignores the pipeline while a player is this close.</summary>
    [DataField] public float FaunaPlayerAggroRange = 6f;

    [DataField] public float FaunaBiteRange = 1.3f;

    [DataField] public TimeSpan FaunaBiteInterval = TimeSpan.FromSeconds(1.5);

    [DataField] public DamageSpecifier FaunaBiteDamage = new();
}

/// <summary>Fauna currently chewing on a morkite pipeline instead of roaming.</summary>
[RegisterComponent]
public sealed partial class LavalandPipeSaboteurComponent : Component
{
    [ViewVariables] public EntityUid Pipe;
    public bool DisabledAi;
    public TimeSpan NextBite;
}

/// <summary>
/// Marks a machine that receives free power from a morkite extractor through pipes.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandMorkitePoweredComponent : Component;
