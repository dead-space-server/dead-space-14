using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland.Components;

[RegisterComponent]
public sealed partial class LavalandBossComponent : Component
{
    [DataField]
    public string BossName = "boss";

    [DataField]
    public float MaxHealth = 1000f;

    [DataField]
    public SoundSpecifier? Music;

    [DataField]
    public List<SoundSpecifier> MusicPool = new();

    public SoundSpecifier? SelectedMusic;

    [DataField]
    public SoundSpecifier? DeathSound;

    [DataField]
    public List<EntProtoId> DeathRewards = new();

    [ViewVariables]
    public EntityUid? Arena;

    [ViewVariables]
    public bool DeathRewardsSpawned;
}

public sealed class LavalandBossResetEvent(EntityUid arena, Vector2i spawnTile) : EntityEventArgs
{
    public readonly EntityUid Arena = arena;
    public readonly Vector2i SpawnTile = spawnTile;
}

public sealed class LavalandBossFightStartedEvent(EntityUid arena) : EntityEventArgs
{
    public readonly EntityUid Arena = arena;
}
