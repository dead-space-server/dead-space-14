using Content.Shared.Mobs.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Necromorphs.Necroobelisk;

[RegisterComponent, NetworkedComponent, EntityCategory("Spawner")]
public sealed partial class SuperMatterialNecroObeliskComponent : Component
{
    #region Sanity

    [DataField]
    [ViewVariables(VVAccess.ReadOnly)]
    public float RangeSanity = 8f;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan CheckDurationSanity = TimeSpan.FromSeconds(2);

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextCheckTimeSanity = TimeSpan.Zero;
    [ViewVariables]
    public HashSet<Entity<MobStateComponent>> MobsInRange = [];

    #endregion

    #region Pulse

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextPulseTime = TimeSpan.Zero;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan TimeUtilPulse = TimeSpan.FromSeconds(15);

    [DataField]
    public float SanityDamage = 3;

    [DataField]
    public int MobsForStageConvergence = 15;

    [ViewVariables(VVAccess.ReadOnly)]
    public int MobsAbsorbed = 0;

    #endregion

    #region Visualizer

    [DataField]
    public string State = "active";

    [DataField]
    public string UnactiveState = "unactive";

    #endregion

    #region Sounds

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string SoundDestruction = "/Audio/_DeadSpace/Necromorfs/unitolog_start.ogg";

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string SoundConvergence = "/Audio/_DeadSpace/Necromorfs/marker_convergence.ogg";

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string SoundInit = "/Audio/_DeadSpace/Necromorfs/marker_convergence.ogg";

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string Sound = "/Audio/_DeadSpace/Necromorfs/marker_red.ogg";

    [DataField]
    public TimeSpan SoundCooldown = TimeSpan.FromSeconds(15);

    [ViewVariables]
    public TimeSpan NextSoundTime = TimeSpan.Zero;

    #endregion

    #region Bool

    [DataField("warn")]
    public bool IsGivesWarnings = true;

    [DataField("stoper")]
    public bool IsStoper = true;

    [ViewVariables(VVAccess.ReadWrite)]
    public bool IsStageConvergence = false;

    [DataField("active")]
    public bool IsActive = true;

    [DataField]
    public bool EndAfterDestroy = false;

    [DataField("canConvergence")]
    public bool IsCanStartConvergence = true;

    [DataField("cudzu")]
    public bool SpawnCudzu = true;

    #endregion
    #region Super
    [ViewVariables]
    public bool SequenceStarted = false;

    /// <summary>The Circle's geist lifted the cap: it reaches 100% with no unitologists' mode running.</summary>
    [ViewVariables]
    public bool ForceCompletion;
    [ViewVariables(VVAccess.ReadWrite)]
    public int Percents = 0;
    [ViewVariables]
    public TimeSpan NextCheckPercents = TimeSpan.Zero;
    [DataField]
    public TimeSpan CheckTime = TimeSpan.FromSeconds(20);
    public SuperMatterialNecroObeliskState StateEnum = SuperMatterialNecroObeliskState.Stop;
    public TimeSpan NextCheckNecroSpawn = TimeSpan.Zero;
    [DataField]
    public string NecroPrototype = "MobSmallNecro";
    public TimeSpan NextCheckKudzuSpawn = TimeSpan.Zero;
    [DataField]
    public TimeSpan KudzuRespawnTime = TimeSpan.FromSeconds(60);
    [DataField]
    public float KudzuRespawnRange = 8f;
    [DataField]
    public string KudzuPrototype = "NecroKudzu";
    [DataField]
    public string AnomalyPrototype = "AnomalyNecro";
    #endregion
}
public enum SuperMatterialNecroObeliskState : byte
{
    Stop,
    Zero,
    TwentyFive,
    Fifty,
    Seventy,
    NinetyNine,
    Hundred
}
