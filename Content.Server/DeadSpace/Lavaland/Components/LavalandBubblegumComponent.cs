using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;

namespace Content.Server.DeadSpace.Lavaland.Components;

[RegisterComponent]
public sealed partial class LavalandBubblegumComponent : Component
{
    [DataField]
    public TimeSpan RangedCooldown = TimeSpan.FromSeconds(3.1);

    [DataField]
    public TimeSpan ForcePressureAfter = TimeSpan.FromSeconds(5.5);

    [DataField]
    public TimeSpan TargetSwitchCooldown = TimeSpan.FromSeconds(3.5);

    [DataField]
    public TimeSpan TargetPressureMemory = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan BloodSmackDelay = TimeSpan.FromSeconds(0.35);

    [DataField]
    public TimeSpan BloodGrabDelay = TimeSpan.FromSeconds(0.65);

    [DataField]
    public TimeSpan BloodWarpDelay = TimeSpan.FromSeconds(0.35);

    [DataField]
    public TimeSpan BloodHandRecover = TimeSpan.FromSeconds(0.45);

    [DataField]
    public TimeSpan BloodReactionCooldown = TimeSpan.FromSeconds(1.85);

    [DataField]
    public TimeSpan BloodSprayStepDelay = TimeSpan.FromSeconds(0.055);

    [DataField]
    public TimeSpan ChargeWindup = TimeSpan.FromSeconds(0.45);

    [DataField]
    public TimeSpan ChargeStepDelay = TimeSpan.FromSeconds(0.05);

    [DataField]
    public TimeSpan ChargeRecover = TimeSpan.FromSeconds(0.4);

    [DataField]
    public TimeSpan ChainedChargeDelay = TimeSpan.FromSeconds(0.3);

    [DataField]
    public TimeSpan SummonCooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan MovementCooldown = TimeSpan.FromSeconds(4.5);

    [DataField]
    public TimeSpan MovementCriticalCooldown = TimeSpan.FromSeconds(3.25);

    [DataField]
    public int MovementDistance = 5;

    [DataField]
    public int MovementCriticalDistance = 4;

    [DataField]
    public int BloodSprayBaseRange = 8;

    [DataField]
    public float BloodSprayRageRangeMultiplier = 0.4f;

    [DataField]
    public int MaxBloodPools = 90;

    [DataField]
    public int MaxPendingBloodTiles = 80;

    [DataField]
    public int ChargeMaxSteps = 36;

    [DataField]
    public int TripleChargeSteps = 28;

    [DataField]
    public int MaxActiveSlaughterlings = 6;

    [DataField]
    public int MaxSummonsPerCast = 6;

    [DataField]
    public float ChargeThrowSpeed = 7f;

    [DataField]
    public float BloodGrabChance = 0.25f;

    [DataField]
    public float BloodGrabChanceBelowHalf = 0.4f;

    [DataField]
    public string BloodPoolPrototype = "LavalandBubblegumBloodPool";

    [DataField]
    public string BloodSplatterPrototype = "LavalandBubblegumBloodSplatter";

    [DataField]
    public string BloodGibsPrototype = "LavalandBubblegumBloodGibs";

    // Walls of blood: a player it has not caught up with for long enough gets a line of blood across the way,
    // sooner and sooner as its health goes down.

    [DataField]
    public string BloodWallPrototype = "LavalandBubblegumBloodWall";

    /// <summary>
    /// Closer than this (tiles) it has caught up with the player: the running away starts over. Its charges and warps
    /// pass by often, so only really getting at them counts.
    /// </summary>
    [DataField]
    public float KiteDistance = 3f;

    /// <summary>Seconds of getting away from it before a wall, at full health.</summary>
    [DataField]
    public float KiteTimeFullHealth = 5f;

    /// <summary>Seconds of getting away from it before a wall, near death.</summary>
    [DataField]
    public float KiteTimeLowHealth = 1.5f;

    /// <summary>Tiles across the way.</summary>
    [DataField]
    public int BloodWallLength = 7;

    /// <summary>How far ahead of the runner (tiles) the wall rises.</summary>
    [DataField]
    public float BloodWallAhead = 2.5f;

    [DataField]
    public SoundSpecifier? BloodWallSound = new SoundPathSpecifier("/Audio/_DeadSpace/Necromorfs/TheCircle/scout_blink.ogg");

    [ViewVariables]
    public readonly Dictionary<EntityUid, float> KiteTime = new();

    [ViewVariables]
    public readonly List<EntityUid> BloodWalls = new();

    [DataField]
    public string LandingPrototype = "LavalandBubblegumLanding";

    [DataField]
    public string RightSmackPrototype = "LavalandBubblegumRightSmack";

    [DataField]
    public string LeftSmackPrototype = "LavalandBubblegumLeftSmack";

    [DataField]
    public string RightPawPrototype = "LavalandBubblegumRightPaw";

    [DataField]
    public string LeftPawPrototype = "LavalandBubblegumLeftPaw";

    [DataField]
    public string RightThumbPrototype = "LavalandBubblegumRightThumb";

    [DataField]
    public string LeftThumbPrototype = "LavalandBubblegumLeftThumb";

    [DataField]
    public string SlaughterlingPrototype = "MobLavalandBubblegumSlaughterling";

    [DataField]
    public string ClonePrototype = "LavalandBubblegumClone";

    // DS14: tgstation hallucination charges and the deadly hand.

    /// <summary>Chance to use the hallucination ring (clones around the target, all charging at once) instead of a charge.</summary>
    [DataField]
    public float HallucinationRingChance = 0.3f;

    /// <summary>Chance to use the hallucination surround (clones from close by, then the real charge).</summary>
    [DataField]
    public float HallucinationSurroundChance = 0.2f;

    [DataField]
    public int HallucinationRingClones = 6;

    [DataField]
    public int HallucinationRingRadius = 6;

    /// <summary>Below half health the ring comes in this many waves, closer and faster each time.</summary>
    [DataField]
    public int HallucinationEnragedWaves = 3;

    [DataField]
    public TimeSpan HallucinationWaveDelay = TimeSpan.FromSeconds(0.9);

    [DataField]
    public int HallucinationSurroundRounds = 4;

    [DataField]
    public TimeSpan HallucinationSurroundDelay = TimeSpan.FromSeconds(0.6);

    [DataField]
    public string DeadlyHandPrototype = "LavalandBubblegumDeadlyHand";

    /// <summary>Chance that a hand attack brings the deadly red hand along.</summary>
    [DataField]
    public float DeadlyHandChance = 0.6f;

    [ViewVariables]
    public readonly List<LavalandBubblegumHallucinationWave> HallucinationWaves = new();

    [DataField]
    public float CloneHealthThreshold = 0.5f;

    [DataField]
    public float CloneCriticalHealthThreshold = 0.25f;

    [DataField]
    public int CloneCount = 2;

    [DataField]
    public int CloneCriticalCount = 3;

    [DataField]
    public int MaxActiveClones = 8;

    [DataField]
    public TimeSpan CriticalCloneCooldown = TimeSpan.FromSeconds(2.5);

    [DataField]
    public int CloneMinOffset = 2;

    [DataField]
    public int CloneMaxOffset = 5;

    [DataField]
    public TimeSpan CloneLinger = TimeSpan.FromSeconds(0.25);

    [DataField]
    public float CloneSwapChance = 0.25f;

    [DataField]
    public float CloneSwapCriticalChance = 0.4f;

    [DataField]
    public TimeSpan CloneSwapCooldown = TimeSpan.FromSeconds(1.25);

    [DataField]
    public DamageSpecifier SmackDamage = new()
    {
        DamageDict = new()
        {
            { "Slash", FixedPoint2.New(25) },
        },
    };

    [DataField]
    public DamageSpecifier GrabDamage = new()
    {
        DamageDict = new()
        {
            { "Slash", FixedPoint2.New(15) },
        },
    };

    [DataField]
    public DamageSpecifier ChargeDamage = new()
    {
        DamageDict = new()
        {
            { "Blunt", FixedPoint2.New(40) },
        },
    };

    [DataField]
    public DamageSpecifier CloneChargeDamage = new()
    {
        DamageDict = new()
        {
            { "Slash", FixedPoint2.New(10) },
        },
    };
    [DataField]
    public SoundSpecifier AttackSound = new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Bubblegum/demon_attack1.ogg");

    [DataField]
    public SoundSpecifier EnterBloodSound = new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Bubblegum/enter_blood.ogg");

    [DataField]
    public SoundSpecifier ExitBloodSound = new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Bubblegum/exit_blood.ogg");

    [DataField]
    public SoundSpecifier ImpactSound = new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Bubblegum/meteorimpact.ogg");

    [DataField]
    public SoundSpecifier SplatSound = new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Bubblegum/splat.ogg");

    [ViewVariables]
    public TimeSpan NextAttack;

    [ViewVariables]
    public TimeSpan BusyUntil;

    [ViewVariables]
    public TimeSpan NextSummon;

    [ViewVariables]
    public TimeSpan NextBloodReaction;

    [ViewVariables]
    public TimeSpan LastPressureAt;

    [ViewVariables]
    public string LastAttackKind = string.Empty;

    [ViewVariables]
    public EntityUid? CurrentPrimaryTarget;

    [ViewVariables]
    public TimeSpan LastTargetSwitchAt;

    [ViewVariables]
    public readonly Dictionary<EntityUid, TimeSpan> LastPressureByTarget = new();

    [ViewVariables]
    public bool Charging;

    [ViewVariables]
    public Vector2i ChargeTargetTile;

    [ViewVariables]
    public int ChargeRemainingSteps;

    [ViewVariables]
    public TimeSpan NextChargeStep;

    [ViewVariables]
    public int PendingCharges;

    [ViewVariables]
    public int PendingChargeSteps;

    [ViewVariables]
    public TimeSpan NextQueuedCharge;

    [ViewVariables]
    public readonly HashSet<EntityUid> ChargeHitEntities = new();

    [ViewVariables]
    public readonly List<EntityUid> BloodPools = new();

    [ViewVariables]
    public readonly List<EntityUid> Slaughterlings = new();

    [ViewVariables]
    public readonly List<LavalandBubblegumActiveClone> ActiveClones = new();

    [ViewVariables]
    public readonly List<LavalandBubblegumCloneCharge> CloneCharges = new();

    [ViewVariables]
    public TimeSpan NextCloneSwap;

    [ViewVariables]
    public TimeSpan LastMovementAt;

    [ViewVariables]
    public readonly List<LavalandBubblegumPendingBloodTile> PendingBloodTiles = new();

    [ViewVariables]
    public readonly List<LavalandBubblegumPendingHandAttack> PendingHandAttacks = new();

    [ViewVariables]
    public TimeSpan NextCriticalCloneSpawn;
}

[RegisterComponent]
public sealed partial class LavalandBubblegumBloodPoolComponent : Component
{
    [ViewVariables]
    public EntityUid Grid;

    [ViewVariables]
    public Vector2i Tile;
}

public sealed class LavalandBubblegumPendingBloodTile
{
    public EntityUid Grid;
    public Vector2i Tile;
    public TimeSpan SpawnAt;
    public bool Fake;
}

public sealed class LavalandBubblegumPendingHandAttack
{
    public EntityUid Grid;
    public Vector2i Tile;
    public TimeSpan AttackAt;
    public bool Grab;
    public bool RightHand;
}

public sealed class LavalandBubblegumActiveClone
{
    public EntityUid Entity;
    public TimeSpan DespawnAt;
}

/// <summary>A wave of the hallucination charge, scheduled after the first one.</summary>
public sealed class LavalandBubblegumHallucinationWave
{
    public EntityUid Target;
    public TimeSpan At;
    public int Clones;
    public int Radius;
    public bool IncludeSelf;
}

/// <summary>The one hand that is redder than the rest: standing on it for a moment kills.</summary>
[RegisterComponent]
public sealed partial class LavalandBubblegumDeadlyHandComponent : Component
{
    /// <summary>Seconds a player may stand on it.</summary>
    [DataField]
    public float KillTime = 1f;

    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new() { { "Blunt", 400 } },
    };

    [ViewVariables]
    public readonly Dictionary<EntityUid, float> StandTime = new();
}

public sealed class LavalandBubblegumCloneCharge
{
    public EntityUid Entity;
    public Vector2i TargetTile;
    public int RemainingSteps;
    public TimeSpan NextStep;
    public DamageSpecifier? ChargeDamage;
    public readonly HashSet<EntityUid> HitEntities = new();
}
