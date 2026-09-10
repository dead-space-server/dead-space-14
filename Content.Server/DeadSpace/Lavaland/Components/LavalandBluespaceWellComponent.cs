// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.DeadSpace.Lavaland.Components;

[RegisterComponent]
public sealed partial class LavalandBluespaceWellComponent : Component
{
    [DataField] public int RequiredOre = 1000;
    [DataField] public TimeSpan CollectionDelay = TimeSpan.FromMinutes(1);
    [DataField] public TimeSpan ScanInterval = TimeSpan.FromSeconds(5);
    [DataField] public TimeSpan ReturnDelay = TimeSpan.FromMinutes(1);
    [DataField] public EntProtoId TeleportEffect = "LavalandWellTeleportSmoke";
    [DataField] public SoundSpecifier TeleportSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");
    [DataField] public HashSet<ProtoId<StackPrototype>> OreTypes = new();
    [DataField] public bool WavesEnabled = true;
    [DataField] public TimeSpan WaveCheckInterval = TimeSpan.FromMinutes(30);
    [DataField] public float WaveChance = 0.35f;
    [DataField] public TimeSpan WaveWarningDuration = TimeSpan.FromMinutes(1);
    [DataField] public SoundSpecifier WaveWarningSound = new SoundPathSpecifier("/Audio/_DeadSpace/Announcements/attention.ogg");
    [DataField] public TimeSpan WaveDuration = TimeSpan.FromMinutes(3);
    [DataField] public float WaveMinDistance = 12f;
    [DataField] public float WaveMaxDistance = 20f;
    [DataField] public List<EntProtoId> WaveTendrils = new();
    [DataField] public List<SoundSpecifier> WaveMusic = new();
    public Container Ore = default!;
    public TimeSpan NextScan;
    public TimeSpan? ReturnAt;
    public EntityCoordinates ReturnCoordinates;
    public readonly Dictionary<EntityUid, TimeSpan> GroundSince = new();
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextWaveCheck;
    public TimeSpan WaveStageEnd;
    public TimeSpan NextTimerUpdate;
    public LavalandWellWaveStage WaveStage;
    public readonly HashSet<EntityUid> WaveEntities = new();
}

public enum LavalandWellWaveStage : byte
{
    Idle,
    Warning,
    Active,
}
