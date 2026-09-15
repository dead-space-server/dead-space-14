// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.StationEvents.Events;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.StationEvents.Components;

[Content.Server.DeadSpace.CentComm.AllowedGameRuleOnCentComm]
[RegisterComponent, Access(typeof(SurvivalNecromorphBreachRule))]
public sealed partial class SurvivalNecromorphBreachRuleComponent : Component
{
    [DataField]
    public float TelegraphDelay = 8f;

    [DataField]
    public float SpawnDelayAfterExplosion = 1.75f;

    [DataField]
    public int BreachCount = 10;

    [DataField]
    public float BreachInterval = 1.25f;

    [DataField]
    public string ExplosionPrototype = "MicroBomb";

    [DataField]
    public float TotalIntensity = 60f;

    [DataField]
    public float Slope = 5f;

    [DataField]
    public float MaxTileIntensity = 12f;

    [DataField]
    public float TileBreakScale = 1f;

    [DataField]
    public int MaxTileBreak = 0;

    [DataField]
    public bool CanCreateVacuum;

    [DataField("telegraphEffect", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string TelegraphEffect = "SurvivalNecromorphBreachTelegraph";

    [DataField("smokeEffect", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string SmokeEffect = "SurvivalNecromorphBreachSmoke";

    [DataField("spawnEffect", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string SpawnEffect = "SurvivalNecromorphBreachSpawnEffect";

    [DataField]
    public SoundSpecifier? TelegraphSound = new SoundPathSpecifier(
        "/Audio/Ambience/anomaly_scary.ogg",
        AudioParams.Default.WithVolume(-1f).WithMaxDistance(12f));

    [DataField]
    public SoundSpecifier? SpawnSound = new SoundPathSpecifier(
        "/Audio/Effects/Fluids/splat.ogg",
        AudioParams.Default.WithVolume(2f).WithVariation(0.2f).WithMaxDistance(12f));

    [DataField]
    public List<SurvivalNecromorphBreachSpawnEntry> SpawnEntries = new()
    {
        new() { Prototype = "SlasherNecromorfSpawner", Weight = 55f },
        new() { Prototype = "InfectorNecromorfSpawner", Weight = 22f },
        new() { Prototype = "StalkerNecromorfSpawner", Weight = 24f, EarliestRoundTime = 20f },
        new() { Prototype = "PregnantNecromorfSpawner", Weight = 33f, EarliestRoundTime = 30f },
        new() { Prototype = "TwitcherNecromorfSpawner", Weight = 27f, EarliestRoundTime = 30f },
        new() { Prototype = "DevaNecromorfSpawner", Weight = 22f, EarliestRoundTime = 45f, MinimumPlayers = 25 },
        new() { Prototype = "BoomerNecromorfSpawner", Weight = 15f, EarliestRoundTime = 60f, MinimumPlayers = 30 },
        new() { Prototype = "BruteNecromorfSpawner", Weight = 24f, EarliestRoundTime = 60f, MinimumPlayers = 35 },
    };

    [ViewVariables(VVAccess.ReadOnly)]
    public readonly List<SurvivalNecromorphBreachSite> BreachSites = new();
}

[DataDefinition]
public sealed partial class SurvivalNecromorphBreachSpawnEntry
{
    [DataField("prototype", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string Prototype = "SlasherNecromorfSpawner";

    [DataField]
    public float Weight = 1f;

    /// <summary>
    /// Round time in minutes before this spawn can be picked.
    /// </summary>
    [DataField]
    public float EarliestRoundTime;

    [DataField]
    public int MinimumPlayers;
}

public sealed class SurvivalNecromorphBreachSite
{
    public readonly EntityCoordinates Coordinates;
    public readonly TimeSpan TelegraphTime;
    public readonly TimeSpan ExplosionTime;
    public readonly TimeSpan SpawnTime;

    public bool TelegraphSpawned;
    public bool Exploded;
    public bool Spawned;

    public SurvivalNecromorphBreachSite(EntityCoordinates coordinates, TimeSpan telegraphTime, TimeSpan explosionTime, TimeSpan spawnTime)
    {
        Coordinates = coordinates;
        TelegraphTime = telegraphTime;
        ExplosionTime = explosionTime;
        SpawnTime = spawnTime;
    }
}
