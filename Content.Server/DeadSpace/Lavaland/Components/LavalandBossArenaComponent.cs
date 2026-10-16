using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland.Components;

[RegisterComponent]
public sealed partial class LavalandBossArenaComponent : Component
{
    [ViewVariables]
    public int ArenaId;

    [ViewVariables]
    public EntityUid Grid = EntityUid.Invalid;

    [ViewVariables]
    public EntityUid Map = EntityUid.Invalid;

    [ViewVariables]
    public EntityUid Boss = EntityUid.Invalid;

    /// <summary>Boss not summoned yet (see <c>SummonOnGate</c> of the arena prototype).</summary>
    [ViewVariables]
    public EntProtoId? PendingBoss;

    /// <summary>The boss is not held by the arena (see <c>BossUnbound</c> of the arena prototype).</summary>
    [ViewVariables]
    public bool BossUnbound;

    /// <summary>The boss intro played once, when the fight first starts.</summary>
    [ViewVariables]
    public Content.Shared.DeadSpace.Lavaland.Bosses.LavalandBossIntroConfig? Intro;

    [ViewVariables]
    public bool IntroPlayed;

    /// <summary>The intro is playing: the fight starts when it ends.</summary>
    [ViewVariables]
    public TimeSpan? IntroEndsAt;

    /// <summary>Fight theme chosen for the intro of a boss not spawned yet: the fight plays the same one.</summary>
    [ViewVariables]
    public Robust.Shared.Audio.SoundSpecifier? IntroMusic;

    /// <summary>Players frozen by the intro (released when it ends).</summary>
    [ViewVariables]
    public readonly List<EntityUid> IntroWatchers = new();

    /// <summary>The gate-break intro: the gate and whoever knocked on it.</summary>
    [ViewVariables]
    public EntityUid? IntroGate;

    [ViewVariables]
    public EntityUid? IntroKnocker;

    /// <summary>Where the copy of the boss stands at the end of the intro: the real boss is brought out there.</summary>
    [ViewVariables]
    public MapCoordinates? IntroBossSpot;

    /// <summary>Where the watchers knocked flying in the intro really end up: they are moved there when it ends.</summary>
    [ViewVariables]
    public readonly List<(EntityUid Watcher, MapCoordinates Spot)> IntroKnockSpots = new();

    [ViewVariables]
    public Vector2i BossSpawnTile = Vector2i.Zero;

    /// <summary>Tile in the middle of the arena (zero for generated arenas, the floor center for loaded grids).</summary>
    [ViewVariables]
    public Vector2i Origin = Vector2i.Zero;

    [ViewVariables]
    public string BossName = "boss";

    [ViewVariables]
    public int Width = 35;

    [ViewVariables]
    public int Height = 35;

    [ViewVariables]
    public float MaxHealth = 1000f;

    [ViewVariables]
    public float FightStartDistance = 8f;

    [ViewVariables]
    public readonly HashSet<NetUserId> Participants = new();

    [ViewVariables]
    public readonly Dictionary<NetUserId, MapCoordinates> ReturnCoordinates = new();

    [ViewVariables]
    public TimeSpan NextParticipantScan;

    [ViewVariables]
    public TimeSpan NextHudUpdate;

    [ViewVariables]
    public TimeSpan NextBossLeashCheck;

    [ViewVariables]
    public TimeSpan? BossOutsideArenaSince;

    [ViewVariables]
    public TimeSpan? EmptySince;

    [ViewVariables]
    public TimeSpan? CleanupAt;

    [ViewVariables]
    public bool ResetOnEmpty = true;

    [ViewVariables]
    public TimeSpan EmptyResetDelay = TimeSpan.FromSeconds(20);

    [ViewVariables]
    public float EmptyResetHealFraction = 1f;

    [ViewVariables]
    public float EmptyResetMinHeal;

    [ViewVariables]
    public bool HasResetWhileEmpty;

    [ViewVariables]
    public bool DeleteOnEmpty;

    [ViewVariables]
    public bool DeleteOnBossDeath;

    [ViewVariables]
    public bool ReturnParticipantsOnDelete;

    [ViewVariables]
    public bool FightStarted;

    [ViewVariables]
    public bool Ended;

    [ViewVariables]
    public int PeakParticipantCount;
    
    [ViewVariables]
    public float ScaledMaxHealth;

    [ViewVariables]
    public float SealHealthFraction;

    /// <summary>Players here walk tile by tile (see LavalandHierophantBeatSystem).</summary>
    [ViewVariables]
    public bool TileMovement;

    [ViewVariables]
    public EntProtoId? SealWallPrototype;

    [ViewVariables]
    public SoundSpecifier? SealSound;

    [ViewVariables]
    public LocId? SealMessage;

    [ViewVariables]
    public bool Sealed;

    /// <summary>Sealed because players built something inside: jaunters don't work until the fight ends.</summary>
    [ViewVariables]
    public bool SealedByConstruction;

    [ViewVariables]
    public readonly List<EntityUid> SealWalls = new();

    /// <summary>Cave arenas: tiles walled off on sealing (across the passages), with the way into the cave.</summary>
    [ViewVariables]
    public readonly List<(Vector2i Tile, Vector2i Inward)> SealTiles = new();

    /// <summary>Cave arenas: players count as fighting up to this Chebyshev distance (inside the seal line).</summary>
    [ViewVariables]
    public int ParticipantExtent;
}
