// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Lavaland.SecretBoss;

/// <summary>
/// Secret lavaland boss (Celestial). Once every arena boss of the planet is dead, a pit somewhere on lavaland
/// becomes a way down: players gather at it, jump, fall, solve a maze, step on the flower and fight the Celestial.
/// Everything here is set in the lavaland planet prototype (secretBoss).
/// </summary>
[DataDefinition]
public sealed partial class LavalandSecretBossConfig
{
    [DataField]
    public bool Enabled = true;

    #region Awakening

    /// <summary>Delay between the last arena boss dying and the roar.</summary>
    [DataField]
    public TimeSpan AwakenDelay = TimeSpan.FromSeconds(10);

    /// <summary>
    /// What the Celestial says from its dream as the way down opens, one subtitle after another,
    /// heard around the pit (<see cref="AwakenMessageRange"/>).
    /// </summary>
    [DataField]
    public List<LocId> AwakenLines = new()
    {
        "lavaland-secret-boss-awaken-line-1",
        "lavaland-secret-boss-awaken-line-2",
        "lavaland-secret-boss-awaken-line-3",
        "lavaland-secret-boss-awaken-line-4",
        "lavaland-secret-boss-awaken-line-5",
        "lavaland-secret-boss-awaken-line-6",
        "lavaland-secret-boss-awaken-line-7",
        "lavaland-secret-boss-awaken-line-8",
        "lavaland-secret-boss-awaken-line-9",
        "lavaland-secret-boss-awaken-line-10",
    };

    /// <summary>How long each of <see cref="AwakenLines"/> stays on screen.</summary>
    [DataField]
    public float AwakenLineDuration = 3.2f;

    /// <summary>Its voice under each of <see cref="AwakenLines"/>: one of these at random, like when it speaks in the fight.</summary>
    [DataField]
    public List<SoundSpecifier> AwakenTalkSounds = new()
    {
        new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Celestial/sounds/Celestial_Talk_1.ogg"),
        new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Celestial/sounds/Celestial_Talk_2.ogg"),
        new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Celestial/sounds/Celestial_Talk_3.ogg"),
    };

    [DataField]
    public float AwakenTalkVolume = -2f;

    /// <summary>Only players this close to the pit hear the awakening (0 - everyone on lavaland).</summary>
    [DataField]
    public float AwakenMessageRange = 60f;

    [DataField]
    public SoundSpecifier? AwakenSound = new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Celestial/sounds/Roar_Phase2.ogg");

    [DataField]
    public EntProtoId PitPrototype = "LavalandCelestialPit";

    /// <summary>The pit is placed this far from the lavaland outpost.</summary>
    [DataField]
    public float PitMinDistance = 80f;

    [DataField]
    public float PitMaxDistance = 170f;

    /// <summary>Radius of the purple interference on the sensor tablet. The pit is somewhere inside it.</summary>
    [DataField]
    public float NoiseRadius = 70f;

    [DataField]
    public Color NoiseColor = Color.FromHex("#b04dffcc");

    /// <summary>No fauna is allowed this close to the open pit.</summary>
    [DataField]
    public float FaunaClearRadius = 25f;

    #endregion

    #region Tablet beeping

    /// <summary>A sensor tablet closer than this to the pit starts beeping, faster the closer it is.</summary>
    [DataField]
    public float BeepRange = 15f;

    [DataField]
    public SoundSpecifier? BeepSound = new SoundPathSpecifier("/Audio/Effects/beep1.ogg");

    [DataField]
    public float BeepVolume = 6f;

    [DataField]
    public TimeSpan BeepIntervalFar = TimeSpan.FromSeconds(1.6);

    [DataField]
    public TimeSpan BeepIntervalNear = TimeSpan.FromSeconds(0.2);

    #endregion

    #region Gathering and the jump

    /// <summary>Players this close to the pit count as gathered.</summary>
    [DataField]
    public float GatherRange = 5f;

    /// <summary>The jump is offered only when every living player on lavaland is at the pit.</summary>
    [DataField]
    public bool RequireAllPlayers = true;

    /// <summary>After declining, the question comes back after this long.</summary>
    [DataField]
    public TimeSpan PromptCooldown = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan JumpDuration = TimeSpan.FromSeconds(1.2);

    /// <summary>How long the dark falling screen lasts.</summary>
    [DataField]
    public TimeSpan FallDuration = TimeSpan.FromSeconds(10);

    #endregion

    #region Maze

    /// <summary>Maze size in cells per side. Each cell is 2x2 floor with 1 tile walls between.</summary>
    [DataField]
    public int MazeCells = 15;

    [DataField]
    public string MazeFloorTile = "FloorDark";

    [DataField]
    public EntProtoId MazeWall = "WallRockBasaltLavalandBoundary";

    /// <summary>Night blue, but not pitch black.</summary>
    [DataField]
    public Color MazeLightColor = Color.FromHex("#2E447F");

    [DataField]
    public EntProtoId FlowerPrototype = "LavalandCelestialFlower";

    [DataField]
    public EntProtoId GasPrototype = "LavalandCelestialSleepGas";

    /// <summary>Stepping on the flower: gas, then everyone falls asleep after this delay.</summary>
    [DataField]
    public TimeSpan SleepDelay = TimeSpan.FromSeconds(5);

    /// <summary>Forced sleep without stun: the sleeper can't wake up himself and keeps what is in his hands.</summary>
    [DataField]
    public EntProtoId SleepStatusEffect = "StatusEffectLavalandCelestialSleep";

    /// <summary>Screen stays dark this long before the Celestial appears.</summary>
    [DataField]
    public TimeSpan DarkDelay = TimeSpan.FromSeconds(5);

    #endregion

    #region Fight

    /// <summary>Side of the reinforced glass square the fight takes place on.</summary>
    [DataField]
    public int ArenaSize = 33;

    [DataField]
    public string ArenaTile = "FloorRGlass";

    /// <summary>Permanent invisible walls along the edge of the square and around the hole.</summary>
    [DataField]
    public EntProtoId ArenaBorder = "LavalandCelestialInvisibleWall";

    /// <summary>Half-size of the hole in the middle the Celestial hangs over; players can't get into it.</summary>
    [DataField]
    public int ArenaHoleRadius = 4;

    /// <summary>Fence around the hole: stops players but lets shots and blows through.</summary>
    [DataField]
    public EntProtoId HoleFence = "LavalandCelestialHoleFence";

    /// <summary>Parallax behind the arena (The Circle base).</summary>
    [DataField]
    public string ArenaParallax = "AsteroidsOutpost";

    [DataField]
    public Color ArenaLightColor = Color.FromHex("#3B2A55");

    [DataField]
    public EntProtoId BossPrototype = "MobCelestialLavaland";

    [DataField]
    public LocId BossName = "lavaland-secret-boss-name";

    [DataField]
    public float BossHealth = 4500f;

    /// <summary>Added for each fighter after the first.</summary>
    [DataField]
    public float BossHealthPerExtraPlayer = 750f;

    /// <summary>Health fraction at which the Celestial enters its second phase.</summary>
    [DataField]
    public float Phase2Fraction = 0.5f;

    /// <summary>Time from the boss appearing to the end of its intro; players wake up then.</summary>
    [DataField]
    public TimeSpan IntroLength = TimeSpan.FromSeconds(22);

    [DataField]
    /// <summary>A collection: one of its tracks is picked at random for each fight.</summary>
    public SoundSpecifier? FightMusic = new SoundCollectionSpecifier("LavalandCelestialFight");

    /// <summary>With no living fighter for this long, the attempt fails and the pit opens again.</summary>
    [DataField]
    public TimeSpan WipeDelay = TimeSpan.FromSeconds(20);

    /// <summary>
    /// After a lost fight everyone comes back by the pit exactly as they were before the fight
    /// (alive, same wounds and blood) and sleeps this long as a penalty.
    /// </summary>
    [DataField]
    public TimeSpan LossSleep = TimeSpan.FromMinutes(1);

    /// <summary>After a lost fight the way down moves to another pit (new interference area on the tablet).</summary>
    [DataField]
    public bool RelocatePitOnLoss = true;

    #endregion

    #region Reward

    /// <summary>Length of the defeat cutscene; players sleep near the pit until it ends.</summary>
    [DataField]
    public TimeSpan RewardSleep = TimeSpan.FromSeconds(60);

    /// <summary>Each survivor gets one of these at random.</summary>
    [DataField]
    public List<LavalandBlessing> Blessings = new()
    {
        LavalandBlessing.NoSlip,
        LavalandBlessing.Armor,
        LavalandBlessing.Health,
        LavalandBlessing.NoBreathing,
        LavalandBlessing.Regeneration,
    };

    [DataField]
    public DamageModifierSet ArmorModifiers = new()
    {
        Coefficients = new()
        {
            { "Blunt", 0.85f },
            { "Slash", 0.85f },
            { "Piercing", 0.85f },
            { "Heat", 0.9f },
        },
    };

    /// <summary>Added to every damage threshold (crit, death).</summary>
    [DataField]
    public float HealthBonus = 35f;

    /// <summary>Healed from every damage type except <see cref="RegenExcluded"/> each <see cref="RegenInterval"/>.</summary>
    [DataField]
    public float RegenAmount = 5f;

    [DataField]
    public TimeSpan RegenInterval = TimeSpan.FromSeconds(10);

    [DataField]
    public List<string> RegenExcluded = new() { "Cellular" };

    #endregion
}

public enum LavalandBlessing : byte
{
    NoSlip,
    Armor,
    Health,
    NoBreathing,
    Regeneration,
}
