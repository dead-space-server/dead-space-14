using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland.Components;

/// <summary>Admin test: on spawn, a goldgrub digs out here and ambushes the nearest player (no chance, no cooldown).</summary>
[RegisterComponent]
public sealed partial class LavalandGoldgrubAmbushTestComponent : Component
{
    [DataField]
    public float Range = 15f;

    [DataField]
    public EntProtoId Prototype = "MobLavalandGoldgrub";
}

[RegisterComponent]
public sealed partial class LavalandGoldgrubComponent : Component
{
    [DataField]
    public int MaxStoredOre = 30;

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.5);

    [DataField]
    public TimeSpan InactiveUpdateInterval = TimeSpan.FromSeconds(5);

    [DataField]
    public float ActivationRange = 24f;

    [DataField]
    public TimeSpan BurrowDelay = TimeSpan.FromSeconds(10);

    [DataField]
    public float OreSearchRange = 8f;

    [DataField]
    public float EatRange = 0.9f;

    [DataField]
    public float ThreatRange = 5f;

    [DataField]
    public float FleeRange = 3.2f;

    [DataField]
    public float BurrowStartRange = 1.35f;

    [DataField]
    public float GreedyOreRange = 2.5f;

    [DataField]
    public float FleeTargetDistance = 6f;

    [DataField]
    public TimeSpan MinFleeTimeBeforeBurrow = TimeSpan.FromSeconds(2.5);

    [DataField]
    public TimeSpan WanderInterval = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan WanderRetryInterval = TimeSpan.FromSeconds(0.75);

    [DataField]
    public int WanderTargetAttempts = 10;

    [DataField]
    public float WanderDistanceMin = 2f;

    [DataField]
    public float WanderDistanceMax = 6f;

    [DataField]
    public List<ProtoId<StackPrototype>> InitialOre =
    [
        "SilverOre",
        "GoldOre",
        "UraniumOre",
        "DiamondOre",
        "BluespaceCrystalOre",
        "MagmiteOre",
        "ArtifactFragment"
    ];

    [DataField]
    public HashSet<ProtoId<StackPrototype>> EdibleOre =
    [
        "SilverOre",
        "GoldOre",
        "UraniumOre",
        "DiamondOre",
        "BluespaceCrystalOre",
        "MagmiteOre",
        "ArtifactFragment"
    ];

    [DataField]
    public Dictionary<ProtoId<StackPrototype>, int> StoredOre = new();

    // Ambush: digs out of the rock a miner breaks, jumps at them, stuns them and eats their ore, then flees to burrow.
    // How often - goldgrubAmbush in World/planets.yml.

    /// <summary>From digging out to the jump.</summary>
    [DataField]
    public TimeSpan AmbushLeapDelay = TimeSpan.FromSeconds(0.6);

    [DataField]
    public float AmbushLeapSpeed = 9f;

    /// <summary>From the jump to the hit.</summary>
    [DataField]
    public TimeSpan AmbushHitDelay = TimeSpan.FromSeconds(0.35);

    [DataField]
    public float AmbushHitRange = 1.8f;

    [DataField]
    public TimeSpan AmbushStun = TimeSpan.FromSeconds(2);

    /// <summary>How many whole stacks of ore it eats from the miner.</summary>
    [DataField]
    public int AmbushStacksMin = 2;

    [DataField]
    public int AmbushStacksMax = 4;

    /// <summary>Ore it eats from the miner (the ore bag included).</summary>
    [DataField]
    public HashSet<ProtoId<StackPrototype>> AmbushOre =
    [
        "SilverOre",
        "GoldOre",
        "UraniumOre",
        "DiamondOre",
        "BluespaceCrystalOre",
        "MagmiteOre",
    ];

    [ViewVariables]
    public EntityUid? AmbushVictim;

    [ViewVariables]
    public TimeSpan? AmbushLeapAt;

    [ViewVariables]
    public TimeSpan? AmbushHitAt;

    [ViewVariables]
    public bool AmbushDone;

    [ViewVariables]
    public TimeSpan NextUpdate;

    [ViewVariables]
    public TimeSpan? BurrowAt;

    [ViewVariables]
    public TimeSpan? FleeStartedAt;

    [ViewVariables]
    public TimeSpan NextWanderAt;

    [ViewVariables]
    public bool DroppedOre;
}
