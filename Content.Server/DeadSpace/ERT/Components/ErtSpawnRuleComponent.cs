// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.ERT;
using Content.Server.DeadSpace.SpawnERTShuttleCommand;
using Content.Shared.DeadSpace.ERT.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.ERTCall;

[RegisterComponent, Access(typeof(ErtResponseSystem))]
public sealed partial class ErtSpawnRuleComponent : Component
{
    public ProtoId<ErtTeamPrototype> Team;

    /// <summary>
    /// Цель вызова ERT отряда.
    /// </summary>
    public string? CallReason;

    /// <summary>
    /// Цель для pinpointer (для CriticalForce - игрок, которого нужно спасти).
    /// </summary>
    public EntityUid? PinpointerTarget;

    public EntityUid? ShuttleEntity;

    /// <summary>
    /// Suppresses station-facing announcements for an administrator's silent dispatch.
    /// </summary>
    public bool SuppressAnnouncements;

    [DataField(required: true)]
    public ProtoId<ERTShuttlePrototype> Shuttle;
}
