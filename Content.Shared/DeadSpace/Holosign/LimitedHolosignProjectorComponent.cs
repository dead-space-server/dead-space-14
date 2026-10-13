// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Holosign;

[RegisterComponent]
public sealed partial class LimitedHolosignProjectorComponent : Component
{
    [DataField(required: true)]
    public EntProtoId SignProto;

    [DataField]
    public int MaxBarriers = 6;

    /// <summary>
    /// How long the projector has to wait before placing new barriers after one of its barriers was destroyed.
    /// Zero disables the recharge.
    /// </summary>
    [DataField]
    public TimeSpan RechargeTime;

    /// <summary>
    /// How long the "remove all barriers" action takes when invoked on the projector.
    /// </summary>
    [DataField]
    public TimeSpan RemoveAllDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Deadlines of the barrier slots that are currently recharging because their barrier was destroyed
    /// externally. Each slot recharges on its own timer: placement into these slots is blocked until the
    /// corresponding deadline passes. Slots freed by manually removing a barrier are not tracked here and
    /// can be used immediately.
    /// </summary>
    [ViewVariables]
    public List<TimeSpan> RechargingSlots = new();

    [ViewVariables]
    public List<EntityUid> Barriers = new();
}