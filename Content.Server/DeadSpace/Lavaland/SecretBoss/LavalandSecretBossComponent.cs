// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.Lavaland.SecretBoss;

public enum LavalandSecretBossStage : byte
{
    /// <summary>Arena bosses are still alive.</summary>
    Dormant,
    /// <summary>All arena bosses died; the roar comes after a delay.</summary>
    Awakening,
    /// <summary>The pit is open: gather, jump, fall, solve the maze.</summary>
    PitOpen,
    /// <summary>The flower was touched: gas, sleep, darkness.</summary>
    Sleeping,
    /// <summary>The Celestial is fighting.</summary>
    Fight,
    /// <summary>The Celestial is dead. The pit is an ordinary pit again.</summary>
    Done,
}

/// <summary>State of the secret boss on one lavaland map. Lives on the lavaland map entity.</summary>
[RegisterComponent]
public sealed partial class LavalandSecretBossComponent : Component
{
    [ViewVariables]
    public LavalandSecretBossStage Stage = LavalandSecretBossStage.Dormant;

    /// <summary>Arena grids seen on this map; the roar needs all of them to be finished.</summary>
    [ViewVariables]
    public readonly HashSet<EntityUid> SeenArenas = new();

    [ViewVariables]
    public TimeSpan NextCheck;

    [ViewVariables]
    public TimeSpan StageTime;

    /// <summary>Sub-step of the sleeping stage: 0 - gas spreading, 1 - darkness before the fight.</summary>
    [ViewVariables]
    public int Step;

    [ViewVariables]
    public EntityUid? Pit;

    [ViewVariables]
    public Vector2 NoiseCenter;

    [ViewVariables]
    public EntityUid? MazeMap;

    [ViewVariables]
    public EntityUid? ArenaMap;

    /// <summary>Where landed players appear, in the maze start room.</summary>
    [ViewVariables]
    public MapCoordinates MazeStart;

    /// <summary>A tiny out-of-sight grid where falling players wait, so they only see each other.</summary>
    [ViewVariables]
    public MapCoordinates FallHolding;

    [ViewVariables]
    public EntityUid? Flower;

    /// <summary>Tiles of the flower room.</summary>
    [ViewVariables]
    public readonly List<MapCoordinates> FlowerRoom = new();

    /// <summary>Every open tile of the maze: the gas fills all of them when the flower is touched.</summary>
    [ViewVariables]
    public readonly List<MapCoordinates> MazeOpenTiles = new();

    [ViewVariables]
    public MapCoordinates ArenaCenter;

    [ViewVariables]
    public EntityUid? Boss;

    [ViewVariables]
    public float BossMaxHealth;

    /// <summary>Everyone who went down the pit this attempt.</summary>
    [ViewVariables]
    public readonly HashSet<EntityUid> Participants = new();

    /// <summary>Players with an open jump question, and when a declined question may come back.</summary>
    [ViewVariables]
    public readonly Dictionary<ICommonSession, TimeSpan> PromptCooldowns = new();

    [ViewVariables]
    public readonly HashSet<ICommonSession> OpenPrompts = new();

    [ViewVariables]
    public bool PlayersAwake;

    /// <summary>State of every fighter right before the fight, restored if the fight is lost.</summary>
    [ViewVariables]
    public readonly Dictionary<EntityUid, LavalandFighterSnapshot> Snapshots = new();

    /// <summary>
    /// The fight is a dream: each fighter on the arena is a copy of a player whose real body sleeps by the pit.
    /// Copy -> the original body.
    /// </summary>
    [ViewVariables]
    public readonly Dictionary<EntityUid, EntityUid> DreamOriginals = new();

    /// <summary>Copies that died in the dream: their players are already back in their sleeping bodies.</summary>
    [ViewVariables]
    public readonly HashSet<EntityUid> WokenFromDream = new();

    [ViewVariables]
    public TimeSpan? WipeSince;

    [ViewVariables]
    public TimeSpan NextHudUpdate;

    /// <summary>The next of the Celestial's words from its dream, and when it is said.</summary>
    [ViewVariables]
    public int AwakenLine = -1;

    [ViewVariables]
    public TimeSpan NextAwakenLine;

    /// <summary>The ground of lavaland shakes under every player there until then (its next to last words).</summary>
    [ViewVariables]
    public TimeSpan ShakeUntil;

    public TimeSpan NextShake;

    /// <summary>HUD id for the boss bar; arena HUD ids are positive.</summary>
    public const int HudId = -14;
}

public sealed class LavalandFighterSnapshot
{
    public Content.Shared.Damage.DamageSpecifier Damage = new();
    public float BloodLevel = 1f;
}

/// <summary>Sleeping off a lost fight with the Celestial.</summary>
[RegisterComponent]
public sealed partial class LavalandCelestialPenaltyComponent : Component
{
    [ViewVariables]
    public TimeSpan WakeAt;

    [ViewVariables]
    public Robust.Shared.Prototypes.EntProtoId SleepEffect;
}

/// <summary>Marks the spawned Celestial so its death can be handled.</summary>
[RegisterComponent]
public sealed partial class LavalandSecretBossMobComponent : Component
{
    [ViewVariables]
    public EntityUid LavalandMap;
}

/// <summary>Marks the Celestial maze map; the jaunter works there and leads back to this lavaland.</summary>
[RegisterComponent]
public sealed partial class LavalandCelestialMazeComponent : Component
{
    [ViewVariables]
    public EntityUid LavalandMap;
}

/// <summary>The pit that leads to the Celestial.</summary>
[RegisterComponent]
public sealed partial class LavalandCelestialPitComponent : Component
{
    [ViewVariables]
    public EntityUid LavalandMap;
}

/// <summary>Celestial blessing: passive healing.</summary>
[RegisterComponent]
public sealed partial class LavalandBlessingRegenComponent : Component
{
    [DataField]
    public float Amount = 5f;

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(10);

    [DataField]
    public List<string> Excluded = new() { "Cellular" };

    [ViewVariables]
    public TimeSpan NextHeal;
}

/// <summary>Server-side timers of jumping/falling players.</summary>
[RegisterComponent]
public sealed partial class LavalandPitDescentComponent : Component
{
    [ViewVariables]
    public EntityUid LavalandMap;

    [ViewVariables]
    public bool Falling;

    [ViewVariables]
    public TimeSpan NextStep;
}

/// <summary>A player's real body asleep by the Celestial pit while its copy fights in the dream: nothing harms it.</summary>
[RegisterComponent]
public sealed partial class LavalandCelestialDreamerComponent : Component;
