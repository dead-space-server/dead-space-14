using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Lavaland.Bosses;

[Prototype]
public sealed partial class LavalandBossArenaPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public EntProtoId BossPrototype = default;

    [DataField]
    public string ArenaName = "Lavaland Boss Arena";

    [DataField]
    public string FloorTile = "FloorBasalt";

    [DataField]
    public EntProtoId? FloorVisualPrototype;

    [DataField]
    public EntProtoId? WallPrototype;

    [DataField]
    public EntProtoId? LightPrototype;

    [DataField]
    public List<LavalandBossArenaDecorationEntry> Decorations = new();

    [DataField]
    public int Size = 35;

    [DataField]
    public float FightStartDistance;

    [DataField]
    public int SpawnAttempts = 128;

    [DataField]
    public float MinDistance = 170f;

    [DataField]
    public float MaxDistance = 320f;

    [DataField]
    public float MapEdgePadding = 32f;

    [DataField]
    public float LandingExclusionRadius = 90f;

    [DataField]
    public float OutpostExclusionRadius = 100f;

    [DataField]
    public float FtlBeaconExclusionRadius = 64f;

    [DataField]
    public float GridSeparation = 32f;

    [DataField]
    public bool DeleteOnEmpty;

    [DataField]
    public bool DeleteOnBossDeath;

    [DataField]
    public bool ReturnParticipantsOnDelete;

    [DataField]
    public bool ResetOnEmpty = true;

    [DataField]
    public TimeSpan EmptyResetDelay = TimeSpan.FromSeconds(20);

    [DataField]
    public float EmptyResetHealFraction = 1f;

    [DataField]
    public float EmptyResetMinHeal;

    /// <summary>
    /// The entrances are walled off once the boss drops to this fraction of its health. 0 disables sealing.
    /// The walls disappear when the boss dies or resets.
    /// </summary>
    [DataField]
    public float SealHealthFraction = 0.5f;

    /// <summary>Players on this arena walk tile by tile (the beat of the Hierophant).</summary>
    [DataField]
    public bool TileMovement;

    /// <summary>
    /// Build the arena as a huge cave: the fight square stays open, a thick rock ring with uneven pockets surrounds
    /// it and winding passages lead out into the lavaland. The passages are walled off when the arena seals.
    /// </summary>
    [DataField]
    public LavalandBossArenaCaveConfig? Cave;

    /// <summary>
    /// Exact arena plan, one string per row (top row first): '#' wall, 'L' floor with a light,
    /// ',' the alternative floor, 'l' the alternative floor with a light, any other non-space character floor. The boss stands in the middle; floor on the outer edge is an entrance
    /// that gets walled off when the arena seals. Overrides size, cave and the default lights.
    /// </summary>
    [DataField]
    public List<string>? Layout;

    /// <summary>
    /// A ready-made arena grid (map file). The boss spawns in the middle of its floor and the fight stays within it;
    /// floor on the outer edge is an entrance walled off when the arena seals. Overrides size, layout and cave.
    /// </summary>
    [DataField]
    public Robust.Shared.Utility.ResPath? GridPath;

    /// <summary>
    /// The arena is built without its boss: the boss appears on its spawn point once someone has knocked
    /// on a Legion gate of the arena.
    /// </summary>
    [DataField]
    public bool SummonOnGate;

    /// <summary>
    /// The arena only brings the boss out: no leash, no reset, no seal. The boss hunts its targets across
    /// lavaland and carries its own health bar and music (the Legion).
    /// </summary>
    [DataField]
    public bool BossUnbound;

    /// <summary>A cutscene introducing the boss when the fight would start (see <see cref="LavalandBossIntroConfig"/>).</summary>
    [DataField]
    public LavalandBossIntroConfig? Intro;

    /// <summary>Tile of the alternative floor in <see cref="Layout"/> (',' and 'l').</summary>
    [DataField]
    public string? AltFloorTile;

    /// <summary>Floor visual of the alternative floor in <see cref="Layout"/>.</summary>
    [DataField]
    public EntProtoId? AltFloorVisualPrototype;

    /// <summary>
    /// The arena floor copies the natural lavaland floor under it (so it blends with the land around), using these
    /// storm-safe variants: natural tile id -> arena tile id. Tiles not listed become <see cref="FloorTile"/>.
    /// </summary>
    [DataField]
    public Dictionary<string, string> NaturalFloors = new()
    {
        { "FloorBasalt", "FloorBasaltArena" },
        { "FloorLavalandAsh", "FloorBasaltArena" },
        { "FloorLavalandPaleGrass", "FloorBasaltArena" },
    };

    /// <summary>Wall used to close the entrances. Falls back to <see cref="WallPrototype"/>.</summary>
    [DataField]
    public EntProtoId? SealWallPrototype;

    [DataField]
    public SoundSpecifier? SealSound = new SoundPathSpecifier("/Audio/Effects/explosionfar.ogg");

    /// <summary>Popup shown to everyone inside when the entrances close. Null disables it.</summary>
    [DataField]
    public LocId? SealMessage = "lavaland-boss-arena-sealed";
}

[DataDefinition]
public sealed partial class LavalandBossArenaCaveConfig
{
    /// <summary>Rock around the fight square, in tiles.</summary>
    [DataField]
    public int Thickness = 9;

    /// <summary>How deep the uneven pockets bite into the rock (0..1 of the thickness).</summary>
    [DataField]
    public float Roughness = 0.75f;

    [DataField]
    public int Passages = 3;

    /// <summary>Passage width is 2 * this + 1.</summary>
    [DataField]
    public int PassageHalfWidth = 1;

    /// <summary>How far the corridors wind on into the lavaland past the cave.</summary>
    [DataField]
    public int ExitLength = 30;

    /// <summary>How much the corridors wind (radians of random turn per step).</summary>
    [DataField]
    public float ExitWander = 0.35f;

    /// <summary>
    /// The rock is built on the lavaland itself, so it joins the surrounding rock without seams.
    /// Inner rock: ordinary rock you can mine, with ore veins.
    /// </summary>
    [DataField]
    public EntProtoId Rock = "WallRockBasalt";

    /// <summary>Outer layers of the rock that cannot be mined: no digging out of a sealed arena.</summary>
    [DataField]
    public int Shell = 3;

    [DataField]
    public EntProtoId ShellRock = "WallRockBasaltLavalandBoundary";

    /// <summary>Ore veins in the minable rock (at least this many).</summary>
    [DataField]
    public int OreVeins = 16;

    /// <summary>
    /// Share of the minable rock (the ring and the band around it) that is ore: more veins are added to reach it.
    /// </summary>
    [DataField]
    public float OreFraction = 0.35f;

    [DataField]
    public int OreVeinMin = 3;

    [DataField]
    public int OreVeinMax = 7;

    /// <summary>Ore rock and its weight.</summary>
    [DataField]
    public Dictionary<EntProtoId, float> Ores = new();

    /// <summary>
    /// Band of solid basalt laid around the cave over whatever open land is there,
    /// so the cave sits in one mass of rock and only its passages lead in.
    /// </summary>
    [DataField]
    public int Surround = 6;

    /// <summary>Floor of the passages dug into the lavaland.</summary>
    [DataField]
    public string ExitFloor = "FloorBasalt";

    /// <summary>Chance for each corridor to be caved in somewhere along its way: a plug of rock to mine through.</summary>
    [DataField]
    public float CollapseChance = 0.6f;

    /// <summary>Radius of the cave-in plug, in tiles.</summary>
    [DataField]
    public int CollapseRadius = 2;

    /// <summary>Chance for each passage and corridor tile to hold a loose boulder (minable rock).</summary>
    [DataField]
    public float RubbleChance = 0.08f;
}

[DataDefinition]
public sealed partial class LavalandBossArenaDecorationEntry
{
    /// <summary>Entity to spawn. Not needed when <see cref="Decals"/> are set.</summary>
    [DataField]
    public EntProtoId? Prototype;

    /// <summary>Decals to scatter instead of an entity (one random per tile), e.g. fiery flowers.</summary>
    [DataField]
    public List<ProtoId<Content.Shared.Decals.DecalPrototype>> Decals = new();

    [DataField]
    public Color? DecalColor;

    [DataField]
    public int Count = 1;

    [DataField]
    public int SpawnAttempts = 32;

    [DataField]
    public int ClusterSize = 1;

    [DataField]
    public int ClusterRadius = 1;

    /// <summary>
    /// Grow each cluster as one solid pool (through tile sides only) with a gap to other decorations,
    /// so smoothing entities such as lava get clean edges.
    /// </summary>
    [DataField]
    public bool Pool;

    [DataField]
    public float MinDistance;

    [DataField]
    public float MaxDistance;

    [DataField]
    public int EdgePadding = 2;

    [DataField]
    public int MinSeparation = 1;

    [DataField]
    public bool AvoidEntrances = true;
}
