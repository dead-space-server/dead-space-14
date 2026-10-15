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

    [DataField]
    public float EnragedMovementSpeedMultiplier = 3.33f;

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
    public float FuryChaseSpeed = 4.5f;

    [DataField]
    public float FuryChaseReach = 2f;

    [DataField]
    public TimeSpan FuryBlastInterval = TimeSpan.FromSeconds(5);

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

    [ViewVariables]
    public bool Furious;

    [ViewVariables]
    public TimeSpan NextFuryBlast;

    [ViewVariables]
    public readonly List<(Robust.Shared.Map.MapCoordinates Coords, TimeSpan At)> PendingFuryBlasts = new();

    [ViewVariables]
    public TimeSpan NextShotSound;

    [ViewVariables]
    public readonly List<LavalandColossusPendingShot> PendingShots = new();

    [ViewVariables]
    public readonly HashSet<EntityUid> ActiveProjectiles = new();
}

public sealed class LavalandColossusPendingShot
{
    public Angle Angle;
    public TimeSpan FireAt;
    public bool PlaySound;
}
