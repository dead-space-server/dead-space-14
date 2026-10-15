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
