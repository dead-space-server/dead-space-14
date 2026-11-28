// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Procedural;
using Content.Shared.Weather;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.PlanetSurvey;

[Prototype]
public sealed partial class SurveyWorldPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public SurveyKind Kind;

    [DataField]
    public float Weight = 1f;

    [DataField(required: true)]
    public LocId Land;

    [DataField(required: true)]
    public List<ProtoId<BiomeTemplatePrototype>> Biomes = new();

    [DataField]
    public Color Light = Color.FromHex("#D8B059");

    [DataField]
    public float Temperature = 293.15f;

    [DataField]
    public List<ProtoId<DungeonConfigPrototype>> Dungeons = new();

    [DataField]
    public int DungeonCount = 2;

    [DataField]
    public float SiteMin = 40f;

    [DataField]
    public float SiteMax = 90f;

    [DataField]
    public float Spread = 32f;

    [DataField]
    public int Herds = 2;

    [DataField]
    public int Camps;

    [DataField]
    public ProtoId<DungeonConfigPrototype>? Outpost;

    [DataField]
    public int OutpostCount;

    [DataField]
    public bool Devices;

    [DataField]
    public EntProtoId? Lamp;

    [DataField]
    public EntProtoId? Border;

    [DataField]
    public string? ArenaTile;

    [DataField]
    public bool FactoryHalls;

    [DataField]
    public List<EntProtoId> CrowdPool = new();

    [DataField]
    public List<EntProtoId> Veterans = new();

    [DataField]
    public EntProtoId? Turret;

    [DataField]
    public int TurretCount;

    [DataField]
    public List<EntProtoId> Scatter = new();

    [DataField]
    public int ScatterCount;

    [DataField]
    public List<EntProtoId> Traps = new();

    [DataField]
    public int TrapCount;

    [DataField]
    public List<SurveySquadEntry> Squads = new();

    [DataField]
    public int SquadCount = 3;

    [DataField]
    public EntProtoId? Stash;

    [DataField]
    public EntProtoId? Stamp;

    [DataField]
    public int StampCount = 2;

    [DataField]
    public EntProtoId? Crowd;

    [DataField]
    public int CrowdCap = 6;

    [DataField]
    public float StampInterval = 12f;

    [DataField]
    public List<EntProtoId> Elites = new();

    [DataField]
    public int ElitesPerStamp = 2;

    [DataField]
    public List<EntProtoId> Predators = new();

    [DataField]
    public List<EntProtoId> Prey = new();

    [DataField]
    public int PackCount = 3;

    [DataField]
    public int PackSize = 3;

    [DataField]
    public int HerdSize = 4;

    [DataField]
    public SoundSpecifier? CallSound;

    [DataField]
    public SoundSpecifier? AlarmSound;

    [DataField]
    public ProtoId<WeatherPrototype>? Storm;

    [DataField]
    public ProtoId<WeatherPrototype>? Calm;

    [DataField]
    public DamageSpecifier? StormDamage;

    [DataField]
    public float StormCooling;

    [DataField]
    public float StormMinTemperature = 250f;

    [DataField]
    public TimeSpan StormGapMin = TimeSpan.FromMinutes(3);

    [DataField]
    public TimeSpan StormGapMax = TimeSpan.FromMinutes(6);

    [DataField]
    public TimeSpan StormLength = TimeSpan.FromMinutes(2);

    [DataField]
    public TimeSpan StormGrowth = TimeSpan.FromSeconds(30);

    [DataField]
    public List<EntProtoId> Sites = new();

    [DataField]
    public int SiteCount = 2;

    [DataField]
    public EntProtoId? DenSign;

    [DataField]
    public List<EntProtoId> Relics = new();

    [DataField]
    public int RelicCount = 2;

    [DataField]
    public List<EntProtoId> Cargo = new();

    [DataField]
    public int CargoCount = 3;

    [DataField]
    public EntProtoId? Scout;

    [DataField]
    public int ScoutCount;

    [DataField]
    public EntProtoId? Autodoc;

    [DataField]
    public int AutodocCount;

    [DataField]
    public EntProtoId? Repair;

    [DataField]
    public int RepairCount;
}

[DataDefinition]
public sealed partial class SurveySquadEntry
{
    [DataField(required: true)]
    public List<EntProtoId> Members = new();

    [DataField]
    public bool Ambush;
}
