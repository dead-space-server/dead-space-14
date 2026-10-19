using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland.Components;

[RegisterComponent]
public sealed partial class LavalandColossusComponent : Component
{
    [DataField]
    public EntProtoId ProjectilePrototype = "LavalandColossusDeathBolt";

    [DataField]
    public float ProjectileSpeed = 7f;

    [DataField]
    public float RageProjectileSpeedBonus = 3f;

    [DataField]
    public TimeSpan DefaultCooldown = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan ShotgunCooldown = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan RandomCooldown = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan AlternatingCooldown = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan SpiralStepDelay = TimeSpan.FromSeconds(0.085);

    [DataField]
    public TimeSpan AlternatingStepDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan DoubleSpiralWindup = TimeSpan.FromSeconds(1);

    [DataField]
    public int SpiralShots = 80;

    [DataField]
    public int RandomShotRadius = 12;

    [DataField]
    public float RandomShotChance = 0.075f;

    [DataField]
    public float MajorAttackChance = 0.3f;

    [DataField]
    public float RandomAttackChance = 0.25f;

    [DataField]
    public float ShotgunChance = 0.7f;

    /// <summary>Enraged it moves at its walk speed times this (NPCs move at their sprint speed otherwise).</summary>
    [DataField]
    public float EnragedMovementSpeedMultiplier = 2.4f;

    [DataField]
    public float MaxCooldownReduction = 0.3f;

    [DataField]
    public TimeSpan ShotSoundInterval = TimeSpan.FromSeconds(0.85);

    [DataField]
    public SoundSpecifier ShotSound = new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Colossus/invoke_general.ogg");

    [DataField]
    public SoundSpecifier TelegraphSound = new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Colossus/narsie_attack.ogg");

    [ViewVariables]
    public TimeSpan NextAttack;

    [ViewVariables]
    public TimeSpan BusyUntil;

    [ViewVariables]
    public bool Enraged;

    // DS14: fury below a quarter of health - angry sprite, runs at the players, marked blasts around them.

    [DataField]
    public float FuryHealthFraction = 0.25f;

    /// <summary>Running speed at the players while furious, tiles per second.</summary>
    [DataField]
    public float FuryChaseSpeed = 3f;

    [DataField]
    public float FuryChaseReach = 2f;

    [DataField]
    public TimeSpan FuryBlastInterval = TimeSpan.FromSeconds(3.5);

    /// <summary>Between the marks appearing and the blasts.</summary>
    [DataField]
    public TimeSpan FuryBlastDelay = TimeSpan.FromSeconds(1.5);

    /// <summary>Marks around each player (one is always under them).</summary>
    [DataField]
    public int FuryMarksPerPlayer = 4;

    [DataField]
    public float FuryMarkRadius = 3.5f;

    [DataField]
    public string FuryMarkPrototype = "LavalandColossusFuryMark";

    [DataField]
    public float FuryExplosionIntensity = 30f;

    [DataField]
    public float FuryExplosionSlope = 6f;

    [DataField]
    public float FuryExplosionMaxTile = 6f;

    // DS14: "Burst." - storm marks around every player, a blast on each a moment later.

    [DataField]
    public float BurstChance = 0.2f;

    [DataField]
    public int BurstMarksPerPlayer = 3;

    /// <summary>The marks land this far from the player.</summary>
    [DataField]
    public float BurstMarkRadius = 2.5f;

    [DataField]
    public TimeSpan BurstDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan BurstCooldown = TimeSpan.FromSeconds(4);

    [DataField]
    public string BurstMarkPrototype = "LavalandColossusBurstMark";

    [DataField]
    public float BurstExplosionIntensity = 25f;

    [DataField]
    public float BurstExplosionSlope = 5f;

    [DataField]
    public float BurstExplosionMaxTile = 5f;

    [ViewVariables]
    public bool Furious;

    [ViewVariables]
    public TimeSpan NextFuryBlast;

    /// <summary>Marked blasts waiting to go off: fury blasts and "Burst." blasts alike.</summary>
    [ViewVariables]
    public readonly List<LavalandColossusPendingBlast> PendingFuryBlasts = new();

    [ViewVariables]
    public TimeSpan NextShotSound;

    [ViewVariables]
    public readonly List<LavalandColossusPendingShot> PendingShots = new();

    [ViewVariables]
    public readonly HashSet<EntityUid> ActiveProjectiles = new();

    /// <summary>A target farther than this (off the screen) gets aimed shots instead of the usual patterns.</summary>
    [DataField]
    public float OffscreenRange = 8.5f;

    [DataField]
    public int AimedShots = 3;

    /// <summary>How far, in degrees, each aimed shot may stray from the target.</summary>
    [DataField]
    public float AimedSpread = 4f;

    [DataField]
    public TimeSpan AimedShotDelay = TimeSpan.FromSeconds(0.2);

    [DataField]
    public TimeSpan AimedCooldown = TimeSpan.FromSeconds(1.5);

    /// <summary>Players outside the arena it does not chase; they become targets again once back inside.</summary>
    [ViewVariables]
    public readonly HashSet<EntityUid> IgnoredOutside = new();

    /// <summary>Nobody is left inside the arena: its AI is off and it walks to the arena's middle.</summary>
    [ViewVariables]
    public bool ReturningToCenter;

    [ViewVariables]
    public TimeSpan NextArenaCheck;
}

public readonly record struct LavalandColossusPendingBlast(
    Robust.Shared.Map.MapCoordinates Coords,
    TimeSpan At,
    float Intensity,
    float Slope,
    float MaxTile);

public sealed class LavalandColossusPendingShot
{
    public Angle Angle;
    public TimeSpan FireAt;
    public bool PlaySound;

    /// <summary>Aimed at this entity when it fires, give or take <see cref="Jitter"/> degrees.</summary>
    public EntityUid? AimAt;
    public float Jitter;
}
