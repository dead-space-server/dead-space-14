// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Map;

namespace Content.Server.DeadSpace.Lavaland.Components;

[RegisterComponent]
public sealed partial class LavalandBluespaceWellComponent : Component
{
    [DataField] public int RequiredOre = 1000;
    [DataField] public TimeSpan CollectionDelay = TimeSpan.FromMinutes(1);
    [DataField] public TimeSpan ReturnDelay = TimeSpan.FromMinutes(1);
    [DataField] public EntProtoId TeleportEffect = "LavalandWellTeleportSmoke";
    [DataField] public SoundSpecifier TeleportSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");
    [DataField] public HashSet<ProtoId<StackPrototype>> OreTypes = new();

    /// <summary>Ore delivery: a fauna raid is guaranteed when the ore bar reaches each of these percents.</summary>
    [DataField] public List<int> OreRaidPercents = new() { 50 };

    /// <summary>Morkite mining: the same for the morkite bar. Empty - no guaranteed raids.</summary>
    [DataField] public List<int> MorkiteRaidPercents = new() { 20 };

    /// <summary>Thresholds already used in this filling of the well; cleared when it is sent.</summary>
    [ViewVariables] public readonly HashSet<int> RaidsTriggered = new();
    public Container Ore = default!;
    public TimeSpan? ReturnAt;
    public EntityCoordinates ReturnCoordinates;

    /// <summary>Morkite bar, 0-100. Filled by extractors connected through pipes.</summary>
    [ViewVariables] public float Morkite;

    /// <summary>Percent per second the morkite bar loses while the pipeline is cut.</summary>
    [DataField] public float MorkiteLeakRate = 0.05f;

    /// <summary>Morkite has flowed into the well in this filling: losing the flow now means a cut pipeline.</summary>
    [ViewVariables] public bool WasConnected;
}
