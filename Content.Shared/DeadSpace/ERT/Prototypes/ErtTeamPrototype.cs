// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;
using Content.Shared.DeadSpace.TimeWindow;
using Content.Shared.Storage;
using Robust.Shared.Audio;
using Robust.Shared.Maths;

namespace Content.Shared.DeadSpace.ERT.Prototypes;

[Prototype]
public sealed partial class ErtTeamPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Name { get; private set; } = string.Empty;

    [DataField]
    public string Description { get; private set; } = string.Empty;

    [DataField]
    public LocId? Notification = null;

    [DataField]
    public LocId? Sender = null;

    [DataField]
    public string? CancelMessage { get; private set; }

    [DataField("rule", required: true)]
    public EntProtoId ErtRule;

    /// <summary>
    ///     Окно времени до спавна обр.
    /// </summary>
    [DataField("spawnWindow")]
    public TimedWindow TimeWindowToSpawn = new TimedWindow(TimeSpan.FromSeconds(600f), TimeSpan.FromSeconds(900f));

    /// <summary>
    ///     Окно времени кулдауна до возможности вызова следующего отряда.
    /// </summary>
    [DataField]
    public TimedWindow Cooldown = new TimedWindow(TimeSpan.FromSeconds(600f), TimeSpan.FromSeconds(900f));

    [DataField]
    public int Price = 1;

    [DataField]
    public bool DispatchPerTarget;

    /// <summary>
    ///     Особый Entity без которого не обойтись для спавна отряда.
    /// </summary>
    [DataField]
    public EntProtoId? Special = null;

    /// <summary>
    ///     Уровень угрозы станции при котором нельзя будет вызвать отряд.
    /// </summary>
    [DataField]
    public List<string>? CodeBlackList = null;

    [DataField]
    public List<EntitySpawnEntry> Spawns = new();

    [DataField]
    public LocId? StartAnnouncement = null;

    [DataField]
    public SoundSpecifier? StartAudio = null;

    [DataField]
    public SoundSpecifier? ApprovalAudio = null;

    [DataField]
    public Color? ApprovalColor = null;

    [DataField]
    public bool AnnounceOnApproval = true;
}

